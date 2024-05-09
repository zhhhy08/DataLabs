namespace Microsoft.WindowsAzure.IdMappingService.Services
{
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.ActivityTracing;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.ActivityTracing.Contracts;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.Utils;
    using Microsoft.WindowsAzure.Governance.Notifications.ArnContracts.ResourceContracts;
    using Microsoft.WindowsAzure.IdMappingService.Services.Contracts;
    using Microsoft.WindowsAzure.IdMappingService.Services.Constants;
    using Newtonsoft.Json.Linq;
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using Microsoft.WindowsAzure.IdMappingService.Services.Telemetry;

    public class PropertyExtractionService
    {
        #region Tracing

        private static readonly ActivityMonitorFactory PropertyExtractionServiceExtractProperties = new("PropertyExtractionService.ExtractProperties");

        #endregion

        public static IList<Identifier> ExtractProperties(GenericResource resource, InternalIdSpecification spec, IActivity parentActivity)
        {
            GuardHelper.ArgumentNotNull(resource, nameof(resource));
            GuardHelper.ArgumentNotNull(spec, nameof(spec));

            using var monitor = PropertyExtractionServiceExtractProperties.ToMonitor(parentActivity);
            monitor.OnStart();

            var skippedOptionalIdentifierNames = new List<string>();
            try
            {
                var propertiesJObject = resource.Properties as JObject;
                if (propertiesJObject != null)
                {

                    var identifiers = new List<Identifier>();
                    foreach (InternalId idPath in spec.InternalIdPaths)
                    {
                        if (idPath.Path.StartsWith("properties."))
                        {
                            //strip out prefix
                            var propertiesPath = idPath.Path.Substring(11);

                            var foundIdentifier = propertiesJObject.TryGetValue(propertiesPath, StringComparison.OrdinalIgnoreCase, out var extractedIdentifier);
                            if (!foundIdentifier)
                            {
                                if (!idPath.IsOptional)
                                {
                                    if (!IsAPIVersionCorrect(resource.ApiVersion))
                                    {
                                        IdMappingMetricProvider.ReportWrongApiVersionMetric(spec.ResourceType, idPath.Path);
                                    }
                                    else
                                    {
                                        IdMappingMetricProvider.ReportInternalIdNotPresentMetric(spec.ResourceType,idPath.Path);
                                        monitor.Activity.Properties[$"InternalIdNotPresent-{idPath.Name}"] = idPath.Path;
                                    }
                                }
                                else
                                {
                                    skippedOptionalIdentifierNames.Add(idPath.Name);
                                }
                            }
                            else
                            {
                                // if ArmIdPath is included in spec for this identifier, it we must extract value from that path and include it in identifier
                                if (!string.IsNullOrEmpty(idPath.ArmIdPath))
                                {
                                    var armIdPath = idPath.ArmIdPath.Substring(11);
                                    var foundArmId = propertiesJObject.TryGetValue(armIdPath, StringComparison.OrdinalIgnoreCase, out var extractedArmId);
                                    if (!foundArmId)
                                    {
                                        if (idPath.FallbackToResourceArmId)
                                        {
                                            identifiers.Add(new Identifier { Name = idPath.Name, Value = (string)extractedIdentifier, OverrideResourceTypeIndex = idPath.OverrideResourceTypeIndex });
                                        }
                                        else if (!idPath.IsOptional)
                                        {
                                            IdMappingMetricProvider.ReportArmIdNotPresentMetric(spec.ResourceType, idPath.ArmIdPath);
                                            monitor.Activity.Properties[$"ArmIdNotPresent-{idPath.Name}"] = idPath.ArmIdPath;
                                        }
                                        else
                                        {
                                            skippedOptionalIdentifierNames.Add(idPath.Name);
                                        }

                                    }
                                    identifiers.Add(new Identifier { Name = idPath.Name, Value = (string)extractedIdentifier, ArmId = (string)extractedArmId, OverrideResourceTypeIndex = idPath.OverrideResourceTypeIndex });
                                }
                                else
                                {
                                    identifiers.Add(new Identifier { Name = idPath.Name, Value = (string)extractedIdentifier, OverrideResourceTypeIndex = idPath.OverrideResourceTypeIndex });
                                }
                            }
                        }
                        else //path should point to top level resource field, i.e. name
                        {
                            var identifierValue = GetTopLevelResourceFieldValue(resource, idPath.Path);
                            identifiers.Add(new Identifier { Name = idPath.Name, Value = identifierValue, OverrideResourceTypeIndex = idPath.OverrideResourceTypeIndex });
                        }
                    }

                    monitor.Activity.Properties["SkippedOptionalIdentifierNames"] = string.Join(",", skippedOptionalIdentifierNames);

                    if (identifiers.Count == 0)
                    {
                        throw new ArgumentException($"No identifiers found in properties for resource: {resource.Id}");
                    }

                    // Build compositeInternalId and add to extracted identifiers
                    var composteInternalId = GetCompositeInternalId(identifiers, spec);
                    if (!string.IsNullOrEmpty(composteInternalId))
                    {
                        identifiers.Add(new Identifier { Name = IdMappingConstants.CompositeInternalIdIdentifierName, Value = composteInternalId });
                        identifiers.Add(new Identifier { Name = IdMappingConstants.GlobalCompositeInternalIdIdentifierName, Value = composteInternalId });
                    }

                    monitor.OnCompleted();
                    return identifiers;
                }
                else
                {
                    IdMappingMetricProvider.ReportPayLoadNotPresentMetric(spec.ResourceType);
                    throw new ArgumentException($"Properties are not properly formatted for resource: {resource.Id}");
                }
            }
            catch (Exception ex )
            {
                monitor.OnError(ex);
                throw;
            }
        }

        //TODO Task 25014445: check and handler the wrong API version
        private static bool IsAPIVersionCorrect(string apiVersion)
        {
            return true;
        }

        private static string GetCompositeInternalId(List<Identifier> identifiers, InternalIdSpecification spec)
        {
            if (identifiers.Count > 1)
            {
                // if in this branch, we parsed two identiifers, delimiter should be defined, otherwise assume first identifier in spec is CompositeId
                if (spec.Delimiter.HasValue)
                {
                    return string.Join(spec.Delimiter.Value, identifiers.Select(i => i.Value));
                }
            }

            return identifiers.FirstOrDefault()?.Value;
        }

        private static string GetTopLevelResourceFieldValue(GenericResource resource, string path)
        {
            return path.ToLowerInvariant() switch
            {
                "id" => resource.Id,
                "name" => resource.Name,
                "type" => resource.Type,
                "location" => resource.Location,
                "kind" => resource.Kind,
                "managedby" => resource.ManagedBy,
                "displayname" => resource.DisplayName,
                "apiversion" => resource.ApiVersion,
                _ => throw new ArgumentException($"Invalid Specification: {path} does not point to valid location on resource ")
            };
        }
    }
}

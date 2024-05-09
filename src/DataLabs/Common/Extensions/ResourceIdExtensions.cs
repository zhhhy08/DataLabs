namespace Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.Extensions
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Runtime.CompilerServices;
    using System.Text;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.ActivityTracing;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.ActivityTracing.Contracts;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.Utils;

    public static class ResourceIdExtensions
    {
        #region Tracing

        private static readonly ActivityMonitorFactory SplitExtensionResourceIdFactory =
            new ActivityMonitorFactory("ResourceIdExtensions.SplitExtensionResourceId");

        private static readonly ActivityMonitorFactory ResourceIdExtensionsTryFixResourceIdFormat =
            new ActivityMonitorFactory("ResourceIdExtensions.TryFixResourceIdFormat");

        #endregion

        #region Constants

        public const string SubscriptionsSegment = "subscriptions";
        public const string ProvidersSegment = "providers";
        public const string ResourceGroupsSegment = "resourcegroups";
        public const string ManagementGroupsSegment = "managementgroups";
        public const string LocationSegment = "locations";
        public const string ManagementGroupsProvider = "Microsoft.Management";
        public const string ManagementGroupType = "Microsoft.Management/managementGroups";
        public const string ClassicAdminsResourceType = "Microsoft.Authorization/ClassicAdministrators";
        public const string RoleAssignmentType = "Microsoft.Authorization/roleAssignments";
        public const string NotSupportedRoleAssignmentType = "NotSupportedRoleAssignments";
        public const string TrackedResourcesType = "trackedResources";
        public const string RoleDefinitionType = "Microsoft.Authorization/roleDefinitions";

        // For MGs, ARM returns type "/providers/Microsoft.Management/managementGroups"
        // (e.g. "armclient get "https://management.azure.com/providers/Microsoft.Management/managementGroups/PolicyUIMG?api-version=2018-03-01-preview"")
        public const string ManagementGroupsListingType = "/providers/Microsoft.Management/managementGroups";
        public const string AuthorizationProvider = "Microsoft.Authorization";
        public const string MicrosoftResourcesProvider = "Microsoft.Resources";
        public const string RoleAssignmentsSegment = "roleassignments";
        public const string PrivateLinkAssociationType = "Microsoft.Authorization/privateLinkAssociations";
        public const string SubscriptionsType = "Microsoft.Resources/subscriptions";
        public const string ResourceGroupsType = "Microsoft.Resources/subscriptions/resourcegroups";
        public const string ResourceGroupsListingType = "Microsoft.Resources/resourcegroups";
        public const string LocationsType = "Microsoft.Resources/locations";
        public const char Separator = '/';
        public const int SubscriptionGuidLength = 36;
        // Once in a Lifetime construction/concatination is fine. (/providers/Microsoft.Management/managementgroups/)
        public static readonly string MGResourcePrefix = $"{Separator}{ProvidersSegment}{Separator}{ManagementGroupType}{Separator}";
        public static readonly HashSet<string> CustomProviders = new(StringComparer.OrdinalIgnoreCase) { "skuProviders", "featureProviders" };
        #endregion

        #region Fields

        private static readonly string ProvidersPrefix = Separator + ProvidersSegment;


        private static readonly char[] SeparatorArray = new char[1] { Separator };

        private static readonly IDictionary<string, string> ResourceTypeOverrides =
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                // We are overriding the Resource Group type when listing Proxy resources from ARM (Microsoft.Resources/resourceGroups)
                // To align with general resource type of Resource Groups (Microsoft.Resources/subscriptions/resourceGroups)
                { ResourceGroupsListingType, ResourceGroupsType },
                { ManagementGroupsListingType, ManagementGroupType }
            };

        #endregion

        #region Public methods

        public static bool IsMicrosoftResourcesProvider(string providerName)
        {
            return MicrosoftResourcesProvider.IsOrdinalMatch(providerName);
        }

        public static bool IsMicrosoftResourcesProviderSubscriptionType(string providerName, string typeName)
        {
            return IsMicrosoftResourcesProvider(providerName) && SubscriptionsSegment.IsOrdinalMatch(typeName);
        }

        public static bool IsMicrosoftResourcesProviderSubscriptionType(string fullType)
        {
            var (providerName, typeName) = SplitResourceNamespaceAndTypeFromFullType(fullType);
            return IsMicrosoftResourcesProviderSubscriptionType(providerName, typeName);
        }

        public static bool IsTrackedListingType(string providerName, string typeName)
        {
            return IsMicrosoftResourcesProvider(providerName) && TrackedResourcesType.IsOrdinalMatch(typeName);
        }

        public static string? GetResourceTypeWithOverrides(string? type, string fullyQualifiedResourceId)
        {
            if (type != null &&
                ResourceTypeOverrides.TryGetValue(type, out var overwrittenValue))
            {
                return overwrittenValue;
            }

            return !string.IsNullOrWhiteSpace(type)
                ? type
                : fullyQualifiedResourceId.GetResourceType();
        }

        // Check if the slashs of the resourceId are valid
        public static bool IsValidResourceIdFormat(this string fullyQualifiedResourceId)
        {
            GuardHelper.ArgumentNotNullOrEmpty(fullyQualifiedResourceId);

            return fullyQualifiedResourceId.StartsWith("/") &&
                !fullyQualifiedResourceId.EndsWith("/") &&
                !fullyQualifiedResourceId.Contains("//");
        }

        // Assume the only format issues are 1> no start /, 2> multiple /, 3> trailing or tailing space
        public static bool TryFixResourceIdFormat(this string originalResourceId, out string fixedResourceId, IActivity activity)
        {
            GuardHelper.ArgumentNotNullOrEmpty(originalResourceId);

            using var monitor = ResourceIdExtensionsTryFixResourceIdFormat.ToMonitor(activity);
            monitor.Activity["OriginalResourceId"] = originalResourceId;
            monitor.OnStart(false);

            try
            {
                var splits = originalResourceId.Split(new[] { Separator }, StringSplitOptions.RemoveEmptyEntries).Select(split => split);
                fixedResourceId = $"{Separator}{string.Join($"{Separator}", splits)}";

                monitor.Activity["FixedResourceId"] = fixedResourceId;

                var isFixed = fixedResourceId.IsValidResourceIdFormat();

                monitor.Activity["IsFixed"] = isFixed;

                monitor.OnCompleted();
                return isFixed;
            }
            catch (Exception e)
            {
                monitor.OnError(e);
                fixedResourceId = originalResourceId;
                return false;
            }
        }

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Design", "CA1031:DoNotCatchGeneralExceptionTypes")]
        public static bool IsValidResourceId(this string fullyQualifiedResourceId)
        {
            try
            {
                GuardHelper.ArgumentNotNullOrEmpty(fullyQualifiedResourceId);

                var idParts = fullyQualifiedResourceId.GetIdSegments().ToList();
                GuardHelper.IsArgumentGreaterThan(idParts.Count, 0);

                var isSubscriptionResourceId = idParts.Any(kv => kv.Key.IsOrdinalMatch(ProvidersSegment))
                       && idParts.First().Key.IsOrdinalMatch(SubscriptionsSegment)
                       && Guid.TryParse(idParts.First().Value, out _);
                if (isSubscriptionResourceId)
                {
                    return true;
                }

                // If the resourceId is the subscription itself  /Subscriptions/subId
                // it is still a valid resourceId but doesn't have provider or resource group
                var isSubscription = idParts.First().Key.IsOrdinalMatch(SubscriptionsSegment)
                        && Guid.TryParse(idParts.First().Value, out _) && idParts.Count == 1;
                if (isSubscription)
                {
                    return true;
                }

                var isResourceGroupResourceId = idParts.Count == 2
                                      && idParts.First().Key.IsOrdinalMatch(SubscriptionsSegment)
                                      && Guid.TryParse(idParts.First().Value, out _)
                                      && idParts[1].Key.IsOrdinalMatch(ResourceGroupsSegment);
                if (isResourceGroupResourceId)
                {
                    return true;
                }

                // Check whether the resource is a valid MG extension resource
                var isManagementGroupExtensionResourceId = idParts.Count == 4
                    ? idParts[0].Key.IsOrdinalMatch(ProvidersSegment)
                      && idParts[0].Value != null
                      && idParts[0].Value!.IsOrdinalMatch(ManagementGroupsProvider)
                      && idParts[1].Key.IsOrdinalMatch(ManagementGroupsSegment)
                      && idParts[2].Key.IsOrdinalMatch(ProvidersSegment)
                    : false;
                if (isManagementGroupExtensionResourceId)
                {
                    return true;
                }

                // MG resource, or Tenant level/built-in resources must start with providers.
                return idParts.Count >= 2 && idParts[0].Key.IsOrdinalMatch(ProvidersSegment);
            }
            catch (Exception)
            {
                return false;
            }
        }

        #region Resource Type 

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Design", "CA1031:DoNotCatchGeneralExceptionTypes")]
        private static string? GetResourceType(IList<KeyValuePair<string, string?>> idSegments, IList<IList<string>> extensionResourceBlocks)
        {
            try
            {
                if (idSegments.Count == 1 && idSegments[0].Key.Equals(SubscriptionsSegment, StringComparison.OrdinalIgnoreCase))
                {
                    return SubscriptionsType;
                }

                if (idSegments.Count == 2 && idSegments[0].Key.Equals(SubscriptionsSegment, StringComparison.OrdinalIgnoreCase)
                    && idSegments[1].Key.Equals(LocationSegment, StringComparison.OrdinalIgnoreCase))
                {
                    return LocationsType;
                }

                if (idSegments.Count == 2 && idSegments[1].Key.Equals(ResourceGroupsSegment, StringComparison.OrdinalIgnoreCase))
                {
                    return ResourceGroupsType;
                }

                // Hardcode Role Assignments type to only accept tenant, mg or subscription and below scopes
                // When we start syncing RA as a resource, this logic may need to be moved to FetchRoleAssignmentFromStorageJob in PartialSyncService
                // V3 schema ArnFilterNotificationJobHandler has explicit filtering for NotSupportedRoleAssignmentType
                if (idSegments.Count == 2 &&
                    idSegments[1].Key.Equals(RoleAssignmentsSegment, StringComparison.OrdinalIgnoreCase) &&
                    idSegments[0].Value != null &&
                    idSegments[0].Value!.Equals(AuthorizationProvider, StringComparison.OrdinalIgnoreCase))
                {
                    var firstResourceBlock = Separator + string.Join(Separator.ToString(), extensionResourceBlocks.First());
                    // subscription and below level role assignment
                    if (firstResourceBlock.StartsWith($"{Separator}{SubscriptionsSegment}", StringComparison.OrdinalIgnoreCase)
                        // tenant level role assignment (global)
                        || extensionResourceBlocks.Count == 1
                        // management group level role assignment
                        || (extensionResourceBlocks.Count == 2
                            && firstResourceBlock.StartsWith(ManagementGroupsListingType, StringComparison.OrdinalIgnoreCase)))
                    {
                        return RoleAssignmentType;
                    }
                    else
                    {
                        // for now we don't want to sync role assignments on tenant level resources 
                        // like /providers/Microsoft.PowerApps/environments/{name}/providers/Microsoft.Authorization/roleAssignments/{guid}
                        return NotSupportedRoleAssignmentType;
                    }
                }

                // Hardcode Authorization RP level notifications as elevateAccess so it can be enabled by proxy partner manifest.
                if (idSegments.Count == 1 && idSegments[0].Value != null && idSegments[0].Value!.Equals("Microsoft.Authorization", StringComparison.OrdinalIgnoreCase))
                {
                    return $"Microsoft.Authorization/elevateAccessRoleAssignment";
                }

                var type = new StringBuilder();
                for (var i = 0; i < idSegments.Count; i++)
                {
                    var part = idSegments[i];
                    if (part.Key.IsOrdinalMatch(ProvidersSegment))
                    {
                        type.Append(part.Value);
                    }
                    else if (type.Length > 0)
                    {
                        type.Append('/').Append(part.Key);
                    }
                }

                return type.ToString();
            }
            catch (Exception)
            {
                return null;
            }
        }

        public static string? GetResourceType(this string fullyQualifiedResourceId)
        {
            if (string.IsNullOrEmpty(fullyQualifiedResourceId))
            {
                return null;
            }

            try
            {
                var extensionResourceBlocks = fullyQualifiedResourceId.SplitExtensionResourceIdToTokens();
                var lastExtention = extensionResourceBlocks[extensionResourceBlocks.Count - 1];
                var idSegments = GetIdSegments(lastExtention);
                return GetResourceType(idSegments.ToList(), extensionResourceBlocks);
            }
            catch (Exception)
            {
                return null;
            }
        }

        public static (string? resourceType, string? resourceName) GetResourceTypeAndName(this string fullyQualifiedResourceId)
        {
            if (fullyQualifiedResourceId == null)
            {
                return default;
            }

            var extensionList = fullyQualifiedResourceId.SplitExtensionResourceIdToTokens();
            var lastExtension = extensionList[extensionList.Count - 1];
            var kvps = GetIdSegments(lastExtension).ToList();
            var resourceType = GetResourceType(kvps, extensionList);
            var resourceName = GetLastValueSegment(lastExtension);
            return (resourceType, resourceName);
        }

        public static (string providerName, string typeName) GetResourceNamespaceAndType(this string fullyQualifiedResourceId)
        {
            var resourceType = fullyQualifiedResourceId.GetResourceType();
            return SplitResourceNamespaceAndTypeFromFullType(resourceType!);
        }

        public static (string providerName, string typeName) SplitResourceNamespaceAndTypeFromFullType(string resourceType)
        {
            var resourceTypeSplitted = resourceType!.Split(new[] { Separator }, 2);
            return (resourceTypeSplitted[0], (resourceTypeSplitted.Length >= 2 ? resourceTypeSplitted[1] : string.Empty));
        }

        public static string? GetResourceNamespace(this string fullyQualifiedResourceId, bool isCustomProvider = false)
        {
            if (string.IsNullOrEmpty(fullyQualifiedResourceId))
            {
                return null;
            }

            try
            {
                var extensionResourceBlocks = fullyQualifiedResourceId.SplitExtensionResourceId();

                var rpNamespace = new StringBuilder();
                var idSegments = GetIdSegments(extensionResourceBlocks.Last()).ToList();
                if (idSegments.Count == 1 && idSegments[0].Key.Equals(SubscriptionsSegment, StringComparison.OrdinalIgnoreCase) ||
                    idSegments.Count == 2 && idSegments[1].Key.Equals(ResourceGroupsSegment, StringComparison.OrdinalIgnoreCase))
                {
                    return MicrosoftResourcesProvider;
                }

                foreach (var part in idSegments)
                {
                    if (isCustomProvider && CustomProviders.Contains(part.Key))
                    {
                        return part.Value;
                    }

                    if(part.Key.IsOrdinalMatch(ProvidersSegment))
                    {
                        rpNamespace.Append(part.Value);
                    }
                }

                return rpNamespace.ToString();
            }
            catch (Exception)
            {
                return null;
            }
        }

        public static bool IsRoleAssignmentResource(this string resourceId)
        {
            GuardHelper.ArgumentNotNullOrEmpty(resourceId);

            // Ends with instead of equals to handle resourceId scoped role assignments which would have type
            // Microsoft.RP/RpType/Microsoft.Authorization/RoleAssignment
            var resourceType = resourceId.GetResourceType();
            return !string.IsNullOrEmpty(resourceType)
                && resourceType.EndsWith(RoleAssignmentType, StringComparison.OrdinalIgnoreCase);
        }

        public static bool IsRoleDefinitionResource(this string resourceId)
        {
            GuardHelper.ArgumentNotNullOrEmpty(resourceId);

            var resourceType = resourceId.GetResourceType();
            return !string.IsNullOrEmpty(resourceType)
                && resourceType.EndsWith(RoleDefinitionType, StringComparison.OrdinalIgnoreCase);
        }

        public static bool IsPrivateLinkAssociationResource(this string resourceId)
        {
            GuardHelper.ArgumentNotNullOrEmpty(resourceId);

            var resourceType = resourceId.GetResourceType();
            return !string.IsNullOrEmpty(resourceType)
                && resourceType.IsOrdinalMatch(PrivateLinkAssociationType);
        }

        /// <summary>
        /// ARM resources are resources that only exist in ARM (like RG or SUB)
        /// </summary>
        public static bool IsArmResource(this string resourceId)
        {
            GuardHelper.ArgumentNotNullOrEmpty(resourceId);
            var resourceType = resourceId.GetResourceType();

            return !string.IsNullOrEmpty(resourceType) && resourceType.StartsWith(MicrosoftResourcesProvider, StringComparison.OrdinalIgnoreCase);
        }

        public static bool IsTenantLevelResource(this string resourceId)
        {
            return string.IsNullOrEmpty(resourceId.GetSubscriptionIdOrNull());
        }

        // Get resource location from resource id
        public static string? GetResourceLocation(this string fullyQualifiedResourceId)
        {
            GuardHelper.ArgumentNotNullOrEmpty(fullyQualifiedResourceId);

            return fullyQualifiedResourceId.GetValue(LocationSegment);
        }

        #endregion

        #region Subscription Id

        public static string GetSubscriptionId(this string fullyQualifiedResourceId)
        {
            // TODO use span in .Net Core
            var charIndex = 0;
            charIndex = LookForNextSlashChar(fullyQualifiedResourceId, false, charIndex);
            if (charIndex == fullyQualifiedResourceId.Length)
            {
                throw new ArgumentException($"ResourceId {fullyQualifiedResourceId} must be more than just slashes.");
            }
            charIndex = LookForNextSlashChar(fullyQualifiedResourceId, true, charIndex);
            if (charIndex == fullyQualifiedResourceId.Length)
            {
                throw new ArgumentException($"Resource id {fullyQualifiedResourceId} must have slash after subscriptions segment.");
            }
            charIndex = LookForNextSlashChar(fullyQualifiedResourceId, false, charIndex);
            if (charIndex == fullyQualifiedResourceId.Length)
            {
                throw new ArgumentException($"Resource id {fullyQualifiedResourceId} must have subscriptionId after subscriptions segment.");
            }
            var beginIndex = charIndex;
            charIndex = LookForNextSlashChar(fullyQualifiedResourceId, true, charIndex);
            var endIndex = charIndex;
            return fullyQualifiedResourceId.Substring(beginIndex, endIndex - beginIndex);
        }

        public static string? GetSubscriptionIdOrNullDeprecated(this string scope)
        {
            GuardHelper.ArgumentNotNullOrEmpty(scope);

            var beginningScopeSegments = scope.Split(SeparatorArray, 3, StringSplitOptions.RemoveEmptyEntries);
            if (beginningScopeSegments.Length >= 2 &&
                beginningScopeSegments[0].Equals(
                    SubscriptionsSegment, StringComparison.OrdinalIgnoreCase))
            {
                return beginningScopeSegments[1];
            }

            return null;
        }

        public static string? GetSubscriptionIdOrNull(this string scope)
        {
            GuardHelper.ArgumentNotNullOrEmpty(scope);

            try
            {
                /* Weird Supportability Notes:
                    1. Some notifications coming without "/" on the beginning. No other option, but you should support it based on the current Unit Test cases.
                    2. Some notifications coming with out hyphens in the subscription guid :) Such as 0b88dfdb55b34fb0b4745b6dcbe6b2ef. So you should support this as well :)
                    3. The ideal semantics of this helper should be to return either a valid GUID or return Null. 
                           a. But the way above deprecated helper is implmented doesn't do those checks thus causing the consumers to drop invalid subs later in the flow. Example BuildScopeId method in ScopeHelper.cs
                           b. This method for now implements the same not-so-great semantics just for parity. (Unless refactor of consumer codepaths is pursued)
                           c. If you pass input of [/subscriptions/invalid-subscription-id-to-be-dropped], you will get out out [invalid-subscription-id-to-be-dropped]
                 */

                // Lookup the respective segement directly based on Index pointers.
                var startIndex =
                    scope.StartsWith("/subscriptions/", StringComparison.OrdinalIgnoreCase)
                    ? 1
                    : (scope.StartsWith("subscriptions/", StringComparison.OrdinalIgnoreCase) ? 0 : -1);

                // Just return since it is not a Subscription level resource.
                if (startIndex == -1)
                {
                    return null;
                }

                // Adjusted start pointer to set the cursor to the character after the / in "subscriptions/".
                startIndex += 14;

                // Next Segment pointer start. 
                // You might wonder why to look up the next index when the Guid is fixed? Check point #2 in the above Notes. So you dont know the size :D
                var endIndex = scope.IndexOf(Separator, startIndex);

                if (endIndex == -1)
                {
                    // Move to the end where there is no forward segment / for subscription resources.
                    endIndex = scope.Length;
                }

                return scope.Substring(startIndex, endIndex - startIndex);
            }
            catch (Exception)
            {
                // Invalid/Meaning-lessresources such as shorter strings can rarely lead to invalid exceptions from the framework calls, that't fine.
                // Optimizing this according to the data patterns where 99.9% scenarios benefit.
            }

            return null;
        }

        public static string? GetNormalizedSubscriptionId(this string fullyQualifiedResourceId)
        {
            GuardHelper.ArgumentNotNullOrEmpty(fullyQualifiedResourceId);

            var subscriptionId = fullyQualifiedResourceId.GetSubscriptionId();
            return ArmNormalizationUtils.NormalizeGuid(subscriptionId);
        }

        #endregion

        #region Resource Group 

        public static string? GetResourceGroup(this string fullyQualifiedResourceId)
        {
            GuardHelper.ArgumentNotNullOrEmpty(fullyQualifiedResourceId);

            return fullyQualifiedResourceId.GetValue(ResourceGroupsSegment);
        }

        public static string? FastGetResourceGroup(this string fullyQualifiedResourceId)
        {
            GuardHelper.ArgumentNotNullOrEmpty(fullyQualifiedResourceId);

            return fullyQualifiedResourceId.FastGetValue(ResourceGroupsSegment);
        }

        public static string? GetNormalizedResourceGroup(this string fullyQualifiedResourceId)
        {
            GuardHelper.ArgumentNotNullOrEmpty(fullyQualifiedResourceId);

            var resourceGroup = fullyQualifiedResourceId.GetResourceGroup();
            return ArmNormalizationUtils.NormalizeResourceGroup(resourceGroup);
        }

        #endregion

        #region Resource Name 

        public static string? GetResourceName(this string fullyQualifiedResourceId)
        {
            var lastExtension = fullyQualifiedResourceId?.SplitExtensionResourceIdToTokens().LastOrDefault();
            if (lastExtension == null || lastExtension.Count == 0)
            {
                return null;
            }
            return GetLastValueSegment(lastExtension);
        }

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Design", "CA1031:DoNotCatchGeneralExceptionTypes")]
        public static string? GetResourceFullName(this string fullyQualifiedResourceId)
        {
            try
            {
                var extensionResourceBlocks = fullyQualifiedResourceId.SplitExtensionResourceIdToTokens();
                var lastExtensionResourceBlock = extensionResourceBlocks.LastOrDefault();
                if (lastExtensionResourceBlock == null)
                {
                    return null;
                }

                StringBuilder? fullName = null;
                foreach (var idPart in GetIdSegments(lastExtensionResourceBlock))
                {
                    if (idPart.Key.IsOrdinalMatch(ProvidersSegment))
                    {
                        fullName = new StringBuilder();
                    }
                    else
                    {
                        fullName?.Append(idPart.Value).Append("/");
                    }
                }

                return fullName?.ToString().TrimEnd(Separator);
            }
            catch (Exception)
            {
                return null;
            }
        }

        /// <summary>
        /// Extracts the paths of parent resource names, in the order of parent resource scopes, if any.
        /// The method should work for both nested and extension resources
        /// Examples of return values:
        /// .../providers/microsoft.compute/virtualmachinescalesets/vmss1 -> []
        /// .../providers/microsoft.compute/virtualmachinescalesets/vmss1/vm1 -> ["vmss1"]
        /// .../providers/microsoft.compute/virtualmachinescalesets/vmss1/vm1/extensions/ext1 -> ["vmss1", "vm1"]
        /// .../providers/microsoft.sql/servers/server1/providers/microsoft.advisor/recommendations/rec1 -> ["server1"]
        /// </summary>
        public static IList<string> GetParentNamePaths(this string fullyQualifiedResourceId)
        {
            try
            {
                List<string>? paths = null;
                foreach (var idPart in fullyQualifiedResourceId.GetIdSegments())
                {
                    if (idPart.Key.IsOrdinalMatch(ProvidersSegment))
                    {
                        paths = paths ?? new List<string>();
                    }
                    else if (idPart.Value != null)
                    {
                        paths?.Add(idPart.Value);
                    }
                }

                if (paths != null)
                {
                    return paths.Take(paths.Count - 1).ToList();
                }

                return Array.Empty<string>();
            }
            catch (Exception)
            {
                return Array.Empty<string>();
            }
        }

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Design", "CA1031:DoNotCatchGeneralExceptionTypes")]
        public static string? GetParentResourceName(this string fullyQualifiedResourceId)
        {
            try
            {
                var segments = fullyQualifiedResourceId.GetIdSegments().ToList();
                if (segments.Count == 0)
                {
                    return null;
                }

                var parentNamePart = segments.Count > 1 ? segments[segments.Count - 2] : segments[segments.Count - 1];
                return parentNamePart.Key.IsOrdinalMatch(ProvidersSegment)
                    ? null
                    : parentNamePart.Value;
            }
            catch (Exception)
            {
                return null;
            }
        }

        /// <summary>
        /// Extracts the provider and type -> name paths of the resource.
        /// The method should work for both nested and extension resources. For extension resource, the provider will be the extension provider instead of the root provider.
        /// Examples of return values:
        /// .../providers/microsoft.compute/virtualmachinescalesets/vmss1 -> ("microsoft.compute", ["virtualmachinescalesets"->"vmss1"])
        /// .../providers/microsoft.compute/virtualmachines/vm1/extensions/ext1 -> ("microsoft.compute", ["virtualmachinescalesets"->"vm1", "extensions"->"ext1"])
        /// .../providers/microsoft.sql/servers/server1/providers/microsoft.advisor/recommendations/rec1 -> ("microsoft.sql", ["servers"->"server1"]), ("microsoft.advisor", ["recommendations"->"rec1"])
        /// </summary>
        public static IList<(string provider, IList<(string type, string name)> typeNamePaths)> GetProviderTypeNamePaths(this string fullyQualifiedResourceId)
        {
            IList<(string, IList<(string, string)>)>? paths = null;
            List<(string, string)>? typeNamePaths = null;

            try
            {
                foreach (var segment in fullyQualifiedResourceId.GetIdSegments())
                {
                    if (segment.Key.IsOrdinalMatch(ProvidersSegment))
                    {
                        typeNamePaths = new List<(string, string)>();

                        if (paths == null)
                        {
                            paths = new List<(string, IList<(string, string)>)>();
                        }
                        paths.Add((segment.Value!, typeNamePaths));
                    }
                    else
                    {
                        if (!string.IsNullOrEmpty(segment.Value))
                        {
                            typeNamePaths?.Add((segment.Key, segment.Value));
                        }
                    }
                }

                return paths ?? Array.Empty<(string, IList<(string, string)>)>();
            }
            catch (Exception)
            {
                return Array.Empty<(string provider, IList<(string type, string name)> typeNamePaths)>();
            }
        }

        #endregion

        #region Management Group

        /// <summary>
        /// Determines whether is valid management group - /providers/Microsoft.Management/managementgroups/{mgId}.
        /// </summary>
        /// <param name="scope">The scope.</param>
        /// <returns>
        ///   <c>true</c> if [is valid management group] [the specified scope]; otherwise, <c>false</c>.
        /// </returns>
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Design", "CA1031:DoNotCatchGeneralExceptionTypes")]
        public static bool IsValidManagementGroupDeprecated(this string scope)
        {
            // Add providers to prefix to avoid string split as a majority of items checked as subscription scope.
            if (string.IsNullOrEmpty(scope) || !scope.StartsWith(ProvidersPrefix, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            try
            {
                // Split in general should be baned unless there is no other option. This creates way too many temp/short-lived objects.
                var scopeParts = scope.SplitAndRemoveEmpty(Separator);
                return scopeParts.Length == 4
                       && ProvidersSegment.IsOrdinalMatch(scopeParts[0])
                       && ManagementGroupsProvider.IsOrdinalMatch(scopeParts[1])
                       && ManagementGroupsSegment.IsOrdinalMatch(scopeParts[2]);
            }
            catch (Exception)
            {
                return false;
            }
        }

        /// <summary>
        /// Determines whether is valid management group - /providers/Microsoft.Management/managementgroups/{mgId}.
        /// </summary>
        /// <param name="scope">The scope.</param>
        /// <returns>
        ///   <c>true</c> if [is valid management group] [the specified scope]; otherwise, <c>false</c>.
        /// </returns>
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Design", "CA1031:DoNotCatchGeneralExceptionTypes")]
        public static bool IsValidManagementGroup(this string scope)
        {
            try
            {
                // Find the count of / == 4
                var scopePartCount = scope.CharacterOccurrenceCount(Separator);

                return scopePartCount == 4
                       && scope.StartsWith(MGResourcePrefix, StringComparison.OrdinalIgnoreCase) // MG prefix check
                       && !scope.AsSpan().Slice(scope.LastIndexOf(Separator) + 1).IsWhiteSpace(); // MG emtpy name check
            }
            catch (Exception)
            {
                return false;
            }
        }

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Design", "CA1031:DoNotCatchGeneralExceptionTypes")]
        public static bool IsRootManagementGroup(this string scope, string tenantId)
        {
            if (string.IsNullOrEmpty(scope) || !tenantId.SafeIsValidGuid())
            {
                return false;
            }

            try
            {
                // Check first if it is a valid MG resource or not
                if (scope.IsValidManagementGroup())
                {
                    var mgName = scope.AsSpan().Slice(scope.LastIndexOf(Separator) + 1);

                    // Match MG to the tenantId
                    if (mgName.Equals(tenantId.AsSpan(), StringComparison.OrdinalIgnoreCase))
                    {
                        return true;
                    }
                }

                return false;
            }
            catch (Exception)
            {
                return false;
            }
        }

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Design", "CA1031:DoNotCatchGeneralExceptionTypes")]
        public static string? GetManagementGroupName(this string scope)
        {
            GuardHelper.ArgumentNotNullOrEmpty(scope);

            try
            {
                return scope.GetValue(ManagementGroupsSegment);
            }
            catch (Exception)
            {
                return null;
            }
        }

        public static string? FastGetManagementGroupName(this string scope)
        {
            GuardHelper.ArgumentNotNullOrEmpty(scope);

            try
            {
                return scope.FastGetValue(ManagementGroupsSegment);
            }
            catch (Exception)
            {
                return null;
            }
        }

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Design", "CA1031:DoNotCatchGeneralExceptionTypes")]
        public static string? GetManagementGroupNameOrSlash(this string scope)
        {
            GuardHelper.ArgumentNotNullOrEmpty(scope);

            try
            {
                return scope.IsOrdinalMatch("/")
                    ? scope
                    : scope.GetValue(ManagementGroupsSegment);
            }
            catch (Exception)
            {
                return null;
            }
        }

        public static string? FastGetManagementGroupNameOrSlash(this string scope)
        {
            GuardHelper.ArgumentNotNullOrEmpty(scope);

            try
            {
                return scope.IsOrdinalMatch("/")
                    ? scope
                    : scope.FastGetValue(ManagementGroupsSegment);
            }
            catch (Exception)
            {
                return null;
            }
        }

        #endregion

        #region Scope

        /// <summary>
        /// Determines whether the scope represents the global scope ("/").
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool IsGlobalScope(this string scope)
        {
            return string.Equals(scope, "/", StringComparison.Ordinal);
        }

        /// <summary>
        /// creates all the sub scopes of a given scope
        /// for example this "/subscriptions/0b88dfdb-55b3-4fb0-b474-5b6dcbe6b2ef/resourceGroups/Default-Storage-EastUS/providers/Microsoft.ClassicStorage/storageAccounts/aainteusanalytics/nestedType1/nestedTypeName"
        /// would give us
        /// "/subscriptions/0b88dfdb-55b3-4fb0-b474-5b6dcbe6b2ef"
        /// "/subscriptions/0b88dfdb-55b3-4fb0-b474-5b6dcbe6b2ef/resourceGroups/Default-Storage-EastUS"
        /// "/subscriptions/0b88dfdb-55b3-4fb0-b474-5b6dcbe6b2ef/resourceGroups/Default-Storage-EastUS/providers/Microsoft.ClassicStorage/storageAccounts/aainteusanalytics"
        /// "/subscriptions/0b88dfdb-55b3-4fb0-b474-5b6dcbe6b2ef/resourceGroups/Default-Storage-EastUS/providers/Microsoft.ClassicStorage/storageAccounts/aainteusanalytics/nestedType1/nestedTypeName"
        /// </summary>
        /// <param name="fullyQualifiedResourceId">The fully qualified resource identifier.</param>
        public static IList<string> GetScopes(this string fullyQualifiedResourceId)
        {
            if (string.IsNullOrEmpty(fullyQualifiedResourceId))
            {
                return Array.Empty<string>();
            }

            var scopes = new List<string>();
            var idSegments = fullyQualifiedResourceId.GetIdSegments().ToList();
            string previousScope = string.Empty;
            for (int segmentIndex = 0; segmentIndex < idSegments.Count; segmentIndex++)
            {
                if (segmentIndex == 0 && idSegments[segmentIndex].Key.IsOrdinalMatch(SubscriptionsSegment))
                {
                    previousScope = GetFullyQualifiedSubscriptionId(idSegments[segmentIndex].Value!);
                    scopes.Add(previousScope);
                }
                else
                {
                    previousScope = string.Join("/", previousScope, idSegments[segmentIndex].Key, idSegments[segmentIndex].Value);
                    if (!idSegments[segmentIndex].Key.IsOrdinalMatch(ProvidersSegment))
                    {
                        scopes.Add(previousScope);
                    }
                }
            }

            if (!scopes.Any() && idSegments.Count == 1 && idSegments[0].Key.IsOrdinalMatch(ProvidersSegment))
            {
                scopes.Add(string.Join("/", string.Empty, idSegments[0].Key, idSegments[0].Value));
            }

            return scopes.Distinct().ToList();
        }

        public static IList<string> GetNonContainerScopes(this string fullyQualifiedResourceId)
        {
            var scopes = fullyQualifiedResourceId
                .GetScopes()
                .Where(scope =>
                {
                    var resourceType = scope.GetResourceType()!;
                    return !resourceType.IsOrdinalMatch(SubscriptionsType) &&
                        !resourceType.IsOrdinalMatch(ResourceGroupsType) &&
                        !resourceType.IsOrdinalMatch(ManagementGroupType);
                })
                .ToList();
            return scopes;
        }

        #endregion

        /// <summary>
        /// Replaces the value.
        /// </summary>
        /// <param name="fullyQualifiedResourceId">The fully qualified resource identifier.</param>
        /// <param name="key">The key.</param>
        /// <param name="replacedValue">The replaced value.</param>
        public static string? ToFullyQualifiedIdWithReplacedSegment(string fullyQualifiedResourceId, string key,
            string replacedValue)
        {
            GuardHelper.ArgumentNotNullOrEmpty(key);
            GuardHelper.ArgumentNotNullOrEmpty(replacedValue);

            if (string.IsNullOrEmpty(fullyQualifiedResourceId))
            {
                return null;
            }

            var modifiedResourceId = new StringBuilder("");
            foreach (var idSegment in fullyQualifiedResourceId.GetIdSegments())
            {
                modifiedResourceId.Append(idSegment.Key.IsOrdinalMatch(key)
                    ? $"/{key}/{replacedValue}"
                    : $"/{idSegment.Key}/{idSegment.Value}");
            }

            return modifiedResourceId.ToString();
        }

        public static string GetResourceType(string providerNamespace, string resourceTypeName)
        {
            return string.Join("/", providerNamespace.ToUpperInvariant(), resourceTypeName.ToUpperInvariant());
        }

        public static (string providerNamespace, string resourceTypeName) GetNamespaceTypeFromFullyQualifiedType(string fullyQualifiedType)
        {
            var split = fullyQualifiedType.Split(SeparatorArray, 2);
            return (split[0], split[1]);
        }

        /// <summary>
        /// Splits the extension resource identifier into tokenized List<string></string>
        /// For example:
        /// /subscriptions/77a79ff3-de8a-49a7-a804-b40e968033f6/resourceGroups/Default-SQL-WestUS/providers/Microsoft.Sql/servers/tnzn4h7oyb/providers/Microsoft.Advisor/recommendations/973d2fe1-7452-8449-3c5d-f8b41b4b54ea
        /// into 
        /// List<List<string>>
        /// 
        /// Each List<string> contains tokenized strings
        /// subscriptions 77a79ff3-de8a-49a7-a804-b40e968033f6 resourceGroups Default-SQL-WestUS providers Microsoft.Sql servers tnzn4h7oyb
        /// providers Microsoft.Advisor recommendations 973d2fe1-7452-8449-3c5d-f8b41b4b54ea
        /// </summary>
        /// <param name="fullyQualifiedResourceId">The fully qualified resource identifier.</param>
        internal static IList<IList<string>> SplitExtensionResourceIdToTokens(this string fullyQualifiedResourceId)
        {
            GuardHelper.ArgumentNotNull(fullyQualifiedResourceId);

            var split = fullyQualifiedResourceId.SplitAndRemoveEmpty(Separator); // We need to remove empty strings from first string "/subscription" to every List to be returned starts with non empty string token
            var result = new List<IList<string>> { new List<string>() };
            var providersCount = 0;

            for (var i = 0; i < split.Length; ++i)
            {
                if (split[i].IsOrdinalMatch(ProvidersSegment) && ++providersCount > 1)
                {
                    if (i % 2 == 0)
                    {
                        result.Add(new List<string> { split[i] });
                        continue;
                    }

                    // We already know that resources can have resource group name or name "providers",
                    // so we shouldn't send exception in that case.
                    if (i == 0 || split[i - 1].IsOrdinalMatch(ResourceGroupsSegment) &&
                        i != split.Length - 1)
                    {
                        // We don't expect to have resources with segment equals to "providers" in this position.
                        // But with enabling more proxy types, we can have it and we need to know and change our parsing logic.
                        // For example, some proxy resources can contain name with "/" in that.
                        // We already saw this in the last segment and it fixed in our code already, but maybe it also can be in middle name.
                        using var monitor = SplitExtensionResourceIdFactory.ToMonitor();
                        monitor.Activity.Properties["FullyQualifiedResourceId"] = fullyQualifiedResourceId;
                        monitor.OnError(new Exception("IdParsing: Word 'providers' is used in even position"));
                    }
                }

                result[result.Count - 1].Add(split[i]);
            }

            return result;
        }

        /// <summary>
        /// Gets the arm identifier parts.
        /// </summary>
        /// <param name="fullyQualifiedResourceId">The fully qualified resource identifier.</param>
        private static IEnumerable<KeyValuePair<string, string?>> GetIdSegments(IList<string> pieces)
        {
            for (var i = 0; i < pieces.Count - 1; i += 2)
            {
                // Special case when there are 3 pieces left
                //"id": "/subscriptions/5feb0c29-4f0d-4018-8e54-7b89df699c15/resourceGroups/SvcTelemetry/providers/Microsoft.ClassicCompute/virtualMachines/SvcTelemetry/metricdefinitions/Disk Read Bytes/sec"
                // In above example "Disk Read Bytes/sec" should not have been split
                if ((pieces.Count - i) <= 3)
                {
                    var lastPart = GetLastValueSegment(pieces);
                    yield return
                        new KeyValuePair<string, string?>(pieces[i], lastPart);
                }
                else
                {
                    yield return new KeyValuePair<string, string?>(pieces[i], pieces[i + 1]);
                }
            }

            // Special case when fullyQualifiedId is one chunk of string without any "/"
            if (pieces.Count == 1)
            {
                yield return new KeyValuePair<string, string?>(pieces[0], null);
            }
        }

        /// <summary>
        /// Gets the arm identifier parts.
        /// </summary>
        /// <param name="fullyQualifiedResourceId">The fully qualified resource identifier.</param>
        public static IEnumerable<KeyValuePair<string, string?>> GetIdSegments(this string fullyQualifiedResourceId)
        {
            GuardHelper.ArgumentNotNullOrEmpty(fullyQualifiedResourceId);
            var pieces = fullyQualifiedResourceId.SplitAndRemoveEmpty(Separator);
            return GetIdSegments(pieces);
        }

        private static string? GetLastValueSegment(IList<string> pieces)
        {
            if (pieces.Count < 2)
            {
                return null;
            }
            if ((pieces.Count % 2) == 0)
            {
                // even number
                return pieces[pieces.Count - 1];
            }
            else
            {
                // Special case when there are 3 pieces left
                //"id": "/subscriptions/5feb0c29-4f0d-4018-8e54-7b89df699c15/resourceGroups/SvcTelemetry/providers/Microsoft.ClassicCompute/virtualMachines/SvcTelemetry/metricdefinitions/Disk Read Bytes/sec"
                // In above example "Disk Read Bytes/sec" should not have been split
                return pieces[pieces.Count - 2] + '/' + pieces[pieces.Count - 1];
            }
        }

        public static bool StartsWithSubscriptionSegment(this string scope)
        {
            if (string.IsNullOrEmpty(scope))
            {
                return false;
            }

            return scope.StartsWith($"/{SubscriptionsSegment}", StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Verifies if the scope starts the with provider segment.
        /// </summary>
        /// <param name="scope">The scope.</param>
        public static bool StartsWithProviderSegment(this string scope)
        {
            if (string.IsNullOrEmpty(scope))
            {
                return false;
            }

            return scope.StartsWith($"/{ProvidersSegment}", StringComparison.OrdinalIgnoreCase);
        }

        public static string GetFullyQualifiedSubscriptionId(string subscriptionId)
        {
            return $"/subscriptions/{subscriptionId}";
        }

        /// <summary>
        /// Splits the extension resource identifier.
        /// For example:
        /// /subscriptions/77a79ff3-de8a-49a7-a804-b40e968033f6/resourceGroups/Default-SQL-WestUS/providers/Microsoft.Sql/servers/tnzn4h7oyb/providers/Microsoft.Advisor/recommendations/973d2fe1-7452-8449-3c5d-f8b41b4b54ea
        /// into 
        /// /subscriptions/77a79ff3-de8a-49a7-a804-b40e968033f6/resourceGroups/Default-SQL-WestUS/providers/Microsoft.Sql/servers/tnzn4h7oyb
        /// providers/Microsoft.Advisor/recommendations/973d2fe1-7452-8449-3c5d-f8b41b4b54ea
        /// </summary>
        /// <param name="fullyQualifiedResourceId">The fully qualified resource identifier.</param>
        public static IList<string> SplitExtensionResourceId(this string fullyQualifiedResourceId)
        {
            var extensionList = fullyQualifiedResourceId.SplitExtensionResourceIdToTokens();
            var results = new List<string>(extensionList.Count);

            for (var i = 0; i < extensionList.Count; i++)
            {
                if (i == 0)
                {
                    results.Add(Separator + string.Join(Separator, extensionList[i]));
                }
                else
                {
                    results.Add(string.Join(Separator, extensionList[i]));
                }
            }
            return results;
        }

        #endregion

        #region Private methods 

        private static bool ContainsResourceGroupSegment(string fullyQualifiedResourceId)
        {
            GuardHelper.ArgumentNotNullOrEmpty(fullyQualifiedResourceId);

            var splitted = fullyQualifiedResourceId.SplitAndRemoveEmpty('/');
            return splitted.Length >= 3 && splitted[2].IsOrdinalMatch(ResourceGroupsSegment);
        }

        /// <summary>
        /// Extract from fullyQualifiedResourceId Arm resource id, using Arm terminology
        /// Note: Arm terminilogy
        /// </summary>
        private static string GetResourceIdSegment(string fullyQualifiedResourceId)
        {
            GuardHelper.ArgumentNotNullOrEmpty(fullyQualifiedResourceId);

            if (ContainsResourceGroupSegment(fullyQualifiedResourceId))
            {
                return string.Join("/", fullyQualifiedResourceId.SplitAndRemoveEmpty('/').Skip(5));
            }

            return string.Join("/", fullyQualifiedResourceId.SplitAndRemoveEmpty('/').Skip(3));
        }

        /// <summary>
        /// Gets the arm value.
        /// </summary>
        /// <param name="fullyQualifiedResourceId">The fully qualified resource identifier.</param>
        /// <param name="key">The key.</param>
        public static string? GetValue(this string fullyQualifiedResourceId, string key)
        {
            if (string.IsNullOrEmpty(fullyQualifiedResourceId))
            {
                return null;
            }

            foreach (var idPart in fullyQualifiedResourceId.GetIdSegments())
            {
                if (idPart.Key.IsOrdinalMatch(key))
                {
                    return idPart.Value;
                }
            }

            return null;
        }

        // A fast algorithm to look up a resource Id segment without allocations
        // NOTE: This method does not work on resource type/name segment, since type does not have a key, and name can contain slashes
        public static string? FastGetValue(this string fullyQualifiedResourceId, string key)
        {
            if (string.IsNullOrEmpty(fullyQualifiedResourceId) || string.IsNullOrEmpty(key))
            {
                return null;
            }

            var retrieveNextSection = false;
            var keySection = true;
            var start = 0;
            var curr = 0;

            var nonEmpty = false;

            var ongoingKeyMatches = true;

            for (; curr < fullyQualifiedResourceId.Length; curr++)
            {
                if (fullyQualifiedResourceId[curr] == Separator)
                {

                    if (curr > start && nonEmpty)
                    {
                        if (retrieveNextSection)
                        {
                            return fullyQualifiedResourceId.Substring(start, curr - start);
                        }

                        // We found our matching key; the next nonempty section is value
                        // If we already exhaust all chars of a key and have not hit a separator yet, this is not a match
                        // e.g. "/subscriptions1/sub1" on key "subscriptions"
                        if (ongoingKeyMatches
                            && keySection
                            && curr - start == key.Length)
                        {
                            retrieveNextSection = true;
                        }

                        // Toggle between key and value sections
                        keySection = !keySection;
                    }

                    start = curr + 1;
                    nonEmpty = false;
                    ongoingKeyMatches = true;
                }
                else
                {
                    // NOTE: Eventually, we can also consider trimming each section, and allow look ups such as '/ subscriptions /sub1'
                    // Currently this is intentionally not supported so that old and new methods have exactly the same behaviors
                    if (!nonEmpty && fullyQualifiedResourceId[curr] != ' ')
                    {
                        nonEmpty = true;
                    }

                    if (keySection)
                    {
                        if (ongoingKeyMatches && curr - start < key.Length)
                        {
                            ongoingKeyMatches = CharacterEqualsOrdinalIgnoreCase(fullyQualifiedResourceId[curr], key[curr - start]);
                        }
                    }
                }
            }

            if (curr > start && nonEmpty && retrieveNextSection)
            {
                return fullyQualifiedResourceId.Substring(start, curr - start);
            }

            return null;
        }

        /// <summary>
        /// Continue searching a string for either the next / or the next non-slash
        /// </summary>
        /// <param name="str">The string to continue searching</param>
        /// <param name="equal">When true stop when a slash is found, otherwise stop when a nonslash is.</param>
        /// <param name="charIndex">The index to start from.</param>
        private static int LookForNextSlashChar(string str, bool equal, int charIndex)
        {
            for (; charIndex < str.Length; charIndex++)
            {
                if (str[charIndex] != '/' ^ equal)
                {
                    break;
                }
            }
            return charIndex;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static bool CharacterEqualsOrdinalIgnoreCase(char a, char b)
        {
            // Changing case on ASCII chars does not result in heap allocations
            return char.ToUpperInvariant(a) == char.ToUpperInvariant(b);
        }

        #endregion
    }
}
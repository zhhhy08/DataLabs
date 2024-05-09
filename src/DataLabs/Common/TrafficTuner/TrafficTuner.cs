namespace Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.TrafficTuner
{
    using System;
    using System.Collections.Generic;
    using System.Text.RegularExpressions;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Boost.Extensions;
    using Microsoft.Extensions.Logging;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.ActivityTracing;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.ConfigMap;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.ServiceMonitoring;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.ServiceMonitoring.Constants;
    using Newtonsoft.Json.Linq;

    /// <summary>
    /// Traffic tuner class.
    /// </summary>
    public class TrafficTuner : ITrafficTuner
    {
        private static readonly ActivityMonitorFactory TrafficTunerEvaluateTunerResult =
            new("TrafficTuner.EvaluateTunerResult");
        private static readonly ILogger<TrafficTuner> _logger = DataLabLoggerFactory.CreateLogger<TrafficTuner>();
        private TrafficTunerRule? _trafficTunerRule;
        private string _trafficTunerRuleString = string.Empty;

        public bool HasRule => _trafficTunerRule != null;

        /// <summary>
        /// Initializes a new instance of the <see cref="TrafficTuner"/> class.
        /// </summary>
        public TrafficTuner(string configKey)
        {
            var result = ConfigMapUtil.Configuration.GetValueWithCallBack(
                configKey,
                UpdateTrafficTunerRule,
                string.Empty,
                true);

            if (!string.IsNullOrWhiteSpace(result))
            {
                this._trafficTunerRule = CreateTrafficTunerRule(result);
            }
        }

        /// <inheritdoc/>
        public (TrafficTunerResult result, TrafficTunerNotAllowedReason reason) EvaluateTunerResult(in TrafficTunerRequest request)
        {
            using var monitor = TrafficTunerEvaluateTunerResult.ToMonitor();
            monitor.Activity[MonitoringConstants.TenantId] = request.TenantId;
            monitor.Activity[MonitoringConstants.SubscriptionId] = request.SubscriptionId;
            monitor.Activity[MonitoringConstants.ResourceType] = request.ResourceType;
            monitor.Activity[MonitoringConstants.MessageRetryCount] = request.MessageRetryCount;
            monitor.Activity[MonitoringConstants.ResourceLocation] = request.ResourceLocation;
            monitor.OnStart(logging: false);

            if (this._trafficTunerRule == null
                || this._trafficTunerRule.AllowAllTenants)
            {
                monitor.Activity[MonitoringConstants.AllowAllTenants] = true;
                monitor.OnCompleted(recordDurationMetric: false);

                return (TrafficTunerResult.Allowed, TrafficTunerNotAllowedReason.None);
            }

            if (this._trafficTunerRule.StopAllTenants)
            {
                monitor.Activity[MonitoringConstants.NotAllowedReason] =
                    TrafficTunerNotAllowedReason.StopAllTenants;
                monitor.OnCompleted(recordDurationMetric: false);

                return (TrafficTunerResult.NotAllowed, TrafficTunerNotAllowedReason.StopAllTenants);
            }

            // Compare MessageRetryCutOffCount only when it is > 0.
            if (this._trafficTunerRule.MessageRetryCutOffCount > 0
                    && request.MessageRetryCount > this._trafficTunerRule.MessageRetryCutOffCount)
            {
                monitor.Activity[MonitoringConstants.NotAllowedReason] =
                    TrafficTunerNotAllowedReason.MessageRetryCount;
                monitor.OnCompleted(recordDurationMetric: false);

                return (TrafficTunerResult.NotAllowed, TrafficTunerNotAllowedReason.MessageRetryCount);
            }

            var excludedResourceTypes = this._trafficTunerRule.ExcludedResourceTypes;
            if (excludedResourceTypes != null &&
                request.ResourceType != null &&
                excludedResourceTypes.Contains(request.ResourceType))
            {
                monitor.Activity[MonitoringConstants.NotAllowedReason] =
                    TrafficTunerNotAllowedReason.ResourceType;
                monitor.OnCompleted(recordDurationMetric: false);

                return (TrafficTunerResult.NotAllowed, TrafficTunerNotAllowedReason.ResourceType);
            }

            var excludedSubscriptions = this._trafficTunerRule.ExcludedSubscriptions;
            if (excludedSubscriptions != null &&
                request.TenantId != null &&
                excludedSubscriptions.TryGetValue(request.TenantId, out var excludedSubscriptionsSet))
            {
                if (request.SubscriptionId != null
                    && excludedSubscriptionsSet.Contains(request.SubscriptionId))
                {
                    monitor.Activity[MonitoringConstants.NotAllowedReason] = TrafficTunerNotAllowedReason.SubscriptionId;
                    monitor.OnCompleted(recordDurationMetric: false);

                    return (TrafficTunerResult.NotAllowed, TrafficTunerNotAllowedReason.SubscriptionId);
                }
            }

            var includedResourceIds = this._trafficTunerRule.IncludedResourceTypeWithMatchFunction;
            if (includedResourceIds != null && !string.IsNullOrEmpty(request.ResourceId) && !string.IsNullOrEmpty(request.ResourceType))
            {
                monitor.Activity[MonitoringConstants.ResourceId] = request.ResourceId;
                // If request resource type is not present, skip to next check.
                if (includedResourceIds.TryGetValue(request.ResourceType, out var resourceIdPatterns))
                {
                    foreach (var resourceIdPattern in resourceIdPatterns)
                    {
                        if (resourceIdPattern.Value(request.ResourceId))
                        {
                            monitor.Activity[MonitoringConstants.Allowed] = true;
                            monitor.OnCompleted(recordDurationMetric: false);
                            return (TrafficTunerResult.Allowed, TrafficTunerNotAllowedReason.None);
                        }
                    }

                    monitor.Activity[MonitoringConstants.NotAllowedReason] = TrafficTunerNotAllowedReason.ResourceId;
                    monitor.OnCompleted(recordDurationMetric: false);
                    return (TrafficTunerResult.NotAllowed, TrafficTunerNotAllowedReason.ResourceId);
                }

                // If input is a global resource, skip following subscription checks
                if (string.IsNullOrEmpty(request.SubscriptionId))
                {
                    monitor.Activity[MonitoringConstants.Allowed] = true;
                    monitor.OnCompleted(recordDurationMetric: false);
                    return (TrafficTunerResult.Allowed, TrafficTunerNotAllowedReason.None);
                }
            }

            // Check for tenant or subscription allowlisting.
            var includedSubscriptions = this._trafficTunerRule.IncludedSubscriptions;
            if (includedSubscriptions != null &&
                request.TenantId != null &&
                includedSubscriptions.TryGetValue(request.TenantId, out var includedSubscriptionsSet))
            {
                if (includedSubscriptionsSet.Count == 0 ||
                    (request.SubscriptionId != null &&
                        includedSubscriptionsSet.Contains(request.SubscriptionId)))
                {
                    monitor.Activity[MonitoringConstants.Allowed] = true;
                    monitor.OnCompleted(recordDurationMetric: false);

                    return (TrafficTunerResult.Allowed, TrafficTunerNotAllowedReason.None);
                }
            }

            // Region allowlisting
            var includedRegions = this._trafficTunerRule.IncludedRegions;
            if (includedRegions != null)
            {
                if (request.ResourceLocation == null ||
                    includedRegions.Contains(request.ResourceLocation))
                {
                    monitor.Activity[MonitoringConstants.Allowed] = true;
                    monitor.OnCompleted(recordDurationMetric: false);

                    return (TrafficTunerResult.Allowed, TrafficTunerNotAllowedReason.None);
                }
            }

            monitor.Activity[MonitoringConstants.NotAllowedReason] = TrafficTunerNotAllowedReason.Region;
            monitor.OnCompleted(recordDurationMetric: false);

            return (TrafficTunerResult.NotAllowed, TrafficTunerNotAllowedReason.Region);
        }

        /// <summary>
        /// Update the value of traffic tuner rule if it exists
        /// </summary>
        /// <param name="trafficTunerRuleString"></param>
        public void UpdateTrafficTunerRuleValue(string trafficTunerRuleString)
        {
            if (_trafficTunerRule != null)
            {
                UpdateTrafficTunerRule(trafficTunerRuleString);
                return;
            }
            _logger.LogWarning("No change made in UpdateTrafficTunerRuleValue as Rule does not exist");
        }

        /// <summary>
        /// Method to update traffic tuner rule.
        /// </summary>
        /// <param name="newTrafficTunerRuleString">Updated traffic tuner rule string.</param>
        /// <returns>Completed task.</returns>
        private Task UpdateTrafficTunerRule(string newTrafficTunerRuleString)
        {
            if (string.IsNullOrWhiteSpace(newTrafficTunerRuleString)
                || newTrafficTunerRuleString.EqualsInsensitively(_trafficTunerRuleString))
            {
                return Task.CompletedTask;
            }

            _logger.LogWarning($"New value {newTrafficTunerRuleString}. Old value {_trafficTunerRuleString}");
            var newTrafficTunerRule = CreateTrafficTunerRule(newTrafficTunerRuleString);

            var oldTrafficTunerRule = this._trafficTunerRule;

            if (Interlocked.CompareExchange(
                ref this._trafficTunerRule,
                newTrafficTunerRule, oldTrafficTunerRule) == oldTrafficTunerRule)
            {
                _trafficTunerRuleString = newTrafficTunerRuleString;
                _logger.LogWarning($"Traffic tuner rule updated.");
            }

            return Task.CompletedTask;
        }

        /// <summary>
        /// Method to create traffic tuner rule.
        /// </summary>
        /// <param name="tunerRule">Passed tuner rule string value.</param>
        /// <returns>Traffic tuner rule object.</returns>
        private TrafficTunerRule? CreateTrafficTunerRule(string tunerRule)
        {
            if (string.IsNullOrWhiteSpace(tunerRule))
            {
                return null;
            }

            var rule = new TrafficTunerRule();

            var options = StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries;
            var lines = tunerRule.Split(TrafficTunerConstants.Semicolon_Delimeter, options);

            foreach (var line in lines)
            {
                // Value is in Json format and below delimeter rules are not applicable.
                if (line.Contains(TrafficTunerConstants.IncludedResourceTypeWithMatchFunction, StringComparison.OrdinalIgnoreCase))
                {
                    // Extract the json string from the line.
                    var jsonString = Regex.Unescape(line[(line.IndexOf(TrafficTunerConstants.IncludedResourceTypeWithMatchFunction, StringComparison.OrdinalIgnoreCase) + TrafficTunerConstants.IncludedResourceTypeWithMatchFunction.Length + 2)..]);
                    rule.IncludedResourceTypeWithMatchFunction = new Dictionary<string, Dictionary<string, Func<string, bool>>>(StringComparer.OrdinalIgnoreCase);
                    var ruleArray = JArray.Parse(jsonString);

                    foreach (var item in ruleArray)
                    {
                        if (item is null || item["resourceType"] == null || item["matchValues"] == null || item["matchFunction"] == null)
                        {
                            throw new InvalidOperationException("Invalid pattern for included resource ids.");
                        }

                        var resourceType = item["resourceType"]!.ToString();
                        var matchFunction = item["matchFunction"]!.ToString();
                        var matchValues = JArray.Parse(item["matchValues"]!.ToString());
                        foreach (var matchValue in matchValues)
                        {
                            Func<string, bool> stringFunction = matchFunction.ToLower() switch
                            {
                                "startswith" => new Func<string, bool>(x => x.StartsWith(matchValue.ToString(), StringComparison.OrdinalIgnoreCase)),
                                "endswith" => new Func<string, bool>(x => x.EndsWith(matchValue.ToString(), StringComparison.OrdinalIgnoreCase)),
                                "contains" => new Func<string, bool>(x => x.Contains(matchValue.ToString(), StringComparison.OrdinalIgnoreCase)),
                                "equals" => new Func<string, bool>(x => x.Equals(matchValue.ToString(), StringComparison.OrdinalIgnoreCase)),
                                _ => throw new InvalidOperationException("Invalid pattern for included resource ids."),
                            };

                            if (rule.IncludedResourceTypeWithMatchFunction.ContainsKey(resourceType))
                            {
                                if (rule.IncludedResourceTypeWithMatchFunction[resourceType].ContainsKey(matchValue.ToString()))
                                {
                                    break;
                                }

                                rule.IncludedResourceTypeWithMatchFunction[resourceType].Add(matchValue.ToString(), stringFunction);
                            }
                            else
                            {
                                rule.IncludedResourceTypeWithMatchFunction.Add(resourceType, new Dictionary<string, Func<string, bool>> { { matchValue.ToString(), stringFunction } });
                            }
                        }
                    }
                }

                var splitLine = line.Split(TrafficTunerConstants.Colon_Delimeter, options);

                // Every line is a key value pair.
                if (splitLine.Length == 2)
                {
                    string key = splitLine[0].ToLowerInvariant();
                    string value = splitLine[1].ToLowerInvariant();

                    // We want initialization to fail is value is not a valid boolean.
                    // If key is not present in config. we want AllowAllTenants to set as false.
                    if (key.EqualsInsensitively(TrafficTunerConstants.AllowAllTenantsKey))
                    {
                        rule.AllowAllTenants = bool.Parse(value);
                    }

                    // We want initialization to fail is value is not a valid boolean.
                    // If key is not present in config. we want StopAllTenants to set as false.
                    if (key.EqualsInsensitively(TrafficTunerConstants.StopAllTenantsKey))
                    {
                        rule.StopAllTenants = bool.Parse(value);
                    }

                    if (key.EqualsInsensitively(TrafficTunerConstants.IncludedSubscriptionsKey))
                    {
                        rule.IncludedSubscriptions = PrepareTenantSubscriptionMapping(value, options);
                    }

                    if (key.EqualsInsensitively(TrafficTunerConstants.ExcludedSubscriptionsKey))
                    {
                        rule.ExcludedSubscriptions = PrepareTenantSubscriptionMapping(value, options);
                    }

                    if (key.EqualsInsensitively(TrafficTunerConstants.IncludedRegionsKey))
                    {
                        rule.IncludedRegions = new HashSet<string>(StringComparer.InvariantCultureIgnoreCase);
                        var regions = value.Split(TrafficTunerConstants.Comma_Delimiter, options);
                        foreach (var region in regions)
                        {
                            if (!string.IsNullOrWhiteSpace(region))
                            {
                                rule.IncludedRegions.Add(region.ToLowerInvariant());
                            }
                        }
                    }

                    if (key.EqualsInsensitively(TrafficTunerConstants.ExcludedResourceTypesKey))
                    {
                        rule.ExcludedResourceTypes = new HashSet<string>(StringComparer.InvariantCultureIgnoreCase);
                        var resourceTypes = value.Split(TrafficTunerConstants.Comma_Delimiter, options);
                        foreach (var resourceType in resourceTypes)
                        {
                            if (!string.IsNullOrWhiteSpace(resourceType))
                            {
                                rule.ExcludedResourceTypes.Add(resourceType.ToLowerInvariant());
                            }
                        }
                    }

                    // We want initialization to fail is value is not a valid int.
                    // If key is not present in config. we want MessageRetryCutOffCount to set as 0.
                    if (key.EqualsInsensitively(TrafficTunerConstants.MessageRetryCutoffCountKey))
                    {
                        rule.MessageRetryCutOffCount = int.Parse(value);
                    }
                }
            }

            // Check if someone has kept, AllowAllTenants = false, StopAllTenants = false,
            // they must define at least one included tenants to process.
            if (!rule.AllowAllTenants && !rule.StopAllTenants && rule.IncludedResourceTypeWithMatchFunction == null)
            {
                if ((rule.IncludedSubscriptions == null
                    || rule.IncludedSubscriptions.Count == 0)
                    && (rule.ExcludedSubscriptions == null
                    || rule.ExcludedSubscriptions.Count == 0)
                    && (rule.IncludedRegions == null
                    || rule.IncludedRegions.Count == 0))
                {
                    throw new InvalidOperationException(
                        "Included subscriptions, excluded subscriptions or included regions list is mandatory when " +
                        "allow and stop all tenants are set to false.");
                }
            }
            return rule;
        }

        /// <summary>
        /// Method to get the tenant to included subscriptions/ excluded subscriptions map
        /// </summary>
        /// <param name="map">Dictionary to keep tenant vs. included subscriptions or tenant vs. excluded subscriptions map.</param>
        /// <param name="value">Delimited value to create dictionary.</param>
        /// <param name="options">Split options.</param>
        private IDictionary<string, HashSet<string>> PrepareTenantSubscriptionMapping(
            string value,
            StringSplitOptions options)
        {
            var map = new Dictionary<string, HashSet<string>>(StringComparer.InvariantCultureIgnoreCase);

            var tenantSubscriptionsList = value.Split(TrafficTunerConstants.Or_Delimiter, options);

            foreach (var tenantSubscriptions in tenantSubscriptionsList)
            {
                var splitTenantSubscriptions = tenantSubscriptions.Split(TrafficTunerConstants.Equals_Delimiter, options);

                if (splitTenantSubscriptions.Length > 0)
                {
                    var tenant = splitTenantSubscriptions[0];
                    Guid.Parse(tenant);

                    if (splitTenantSubscriptions.Length == 1)
                    {
                        map.Add(tenant, new HashSet<string>());
                    }
                    else
                    {
                        var subscriptions = splitTenantSubscriptions[1];
                        var splitSubscriptions = subscriptions.Split(TrafficTunerConstants.Comma_Delimiter, options);
                        var set = new HashSet<string>(StringComparer.InvariantCultureIgnoreCase);
                        foreach (var subscription in splitSubscriptions)
                        {
                            Guid.Parse(subscription);
                            set.Add(subscription);
                        }

                        map.Add(tenant, set);
                    }
                }
            }

            return map;
        }
    }
}
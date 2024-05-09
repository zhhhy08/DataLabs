namespace Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.TrafficTuner
{
    using System;
    using System.Collections.Generic;

    /// <summary>
    /// Traffic tuner rule.
    /// </summary>
    public class TrafficTunerRule
    {
        /// <summary>
        /// Allow all tenants value.
        /// </summary>
        public bool AllowAllTenants { get; set; }

        /// <summary>
        /// Stop all tenants value..
        /// </summary>
        public bool StopAllTenants { get; set; }

        /// <summary>
        /// Included subscriptions set for a tenant dictionary.
        /// </summary>
        public IDictionary<string, HashSet<string>>? IncludedSubscriptions { get; set; }

        /// <summary>
        /// Included regions set for which traffic is allowed.
        /// </summary>
        public HashSet<string>? IncludedRegions { get; set; }

        /// <summary>
        /// Excluded subscriptions set for a tenant dictionary.
        /// </summary>
        public IDictionary<string, HashSet<string>>? ExcludedSubscriptions { get; set; }

        /// <summary>
        /// Excluded resource types set.
        /// </summary>
        public HashSet<string>? ExcludedResourceTypes { get; set; }

        /// <summary>
        /// message retry cutoff count value.
        /// </summary>
        public int MessageRetryCutOffCount { get; set; }

        /// <summary>
        /// Included resource ids dictionary with string operation functions.
        /// </summary>
        public IDictionary<string, Dictionary<string, Func<string, bool>>>? IncludedResourceTypeWithMatchFunction { get; set; }
    }
}

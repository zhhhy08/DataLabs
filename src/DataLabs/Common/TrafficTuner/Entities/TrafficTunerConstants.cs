namespace Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.TrafficTuner
{
    /// <summary>
    /// Traffic tuner contants.
    /// </summary>
    public static class TrafficTunerConstants
    {
        /// <summary>
        /// Allow all tenants key.
        /// </summary>
        public const string AllowAllTenantsKey = "allowalltenants";

        /// <summary>
        /// Stop all tenants key.
        /// </summary>
        public const string StopAllTenantsKey = "stopalltenants";

        /// <summary>
        /// Included subscriptions key.
        /// </summary>
        public const string IncludedSubscriptionsKey = "includedsubscriptions";

        /// <summary>
        /// Included regions key.
        /// </summary>
        public const string IncludedRegionsKey = "includedregions";

        /// <summary>
        /// Excluded subscriptions key.
        /// </summary>
        public const string ExcludedSubscriptionsKey = "excludedsubscriptions";

        /// <summary>
        /// Excluded resource types key.
        /// </summary>
        public const string ExcludedResourceTypesKey = "excludedresourcetypes";

        /// <summary>
        /// Message Retry Cutoff CountKey key.
        /// </summary>
        public const string MessageRetryCutoffCountKey = "messageretrycutoffcount";

        /// <summary>
        /// Included resource ids key.
        /// </summary>
        public const string IncludedResourceTypeWithMatchFunction = "includedResourceTypeWithMatchFunction";

        /// <summary>
        /// Semicolon delimiter.
        /// </summary>
        public const char Semicolon_Delimeter = ';';

        /// <summary>
        /// Colon delimiter.
        /// </summary>
        public const char Colon_Delimeter = ':';

        /// <summary>
        /// Or delimiter.
        /// </summary>
        public const char Or_Delimiter = '|';

        /// <summary>
        /// Commma delimiter.
        /// </summary>
        public const char Comma_Delimiter = ',';

        /// <summary>
        /// Equals delimiter
        /// </summary>
        public const char Equals_Delimiter = '=';

        /// <summary>
        /// Pound delimiter.
        /// </summary>
        public const char Pound_Delimiter = '#';
    }
}

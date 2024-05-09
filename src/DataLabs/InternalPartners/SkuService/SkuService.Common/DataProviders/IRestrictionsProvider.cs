namespace SkuService.Common.DataProviders
{
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.ActivityTracing.Contracts;
    using Microsoft.WindowsAzure.ResourceStack.Common.Collections;
    using SkuService.Common.Models.V1;

    public interface IRestrictionsProvider
    {
        /// <summary>
        /// Gets the SKU capacity restrictions by SKU name.
        /// </summary>
        /// <param name="resourceProvider">Resource provider.</param>
        /// <param name="registrationDate">Registration date.</param>
        /// <param name="subscriptionInternalProperties">Subscription internal properties.</param>
        /// <param name="subscriptionMappings">Subscription mappings.</param>
        /// <param name="activity">Activity</param>
        /// <param name="skipCacheRead">Skip cache read.</param>
        /// <param name="cancellationToken">Cancellation Token.</param>
        /// <returns></returns>
        Task<InsensitiveDictionary<SkuLocationInfo[]>> GetSkuCapacityRestrictionsAsync(string resourceProvider, string registrationDate, SubscriptionInternalPropertiesModel subscriptionInternalProperties, SubscriptionMappingsModel subscriptionMappings, IActivity activity, bool skipCacheRead, CancellationToken cancellationToken);
    }
}
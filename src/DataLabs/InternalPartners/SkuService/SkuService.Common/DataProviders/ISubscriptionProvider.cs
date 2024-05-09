namespace SkuService.Common.DataProviders
{
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.ActivityTracing.Contracts;
    using SkuService.Common.Models.V1;
    using System.Collections.Generic;
    using System.Threading.Tasks;

    public interface ISubscriptionProvider
    {
        /// <summary>
        /// Gets the subscription feature registration properties.
        /// </summary>
        /// <param name="subscriptionId">Subscription id.</param>
        /// <param name="resourceProvider">Resource provider.</param>
        /// <param name="activity">Activity monitor.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>Subscription feature registration properties.</returns>
        Task<IList<SubscriptionFeatureRegistrationPropertiesModel>> GetSubscriptionFeatureRegistrationPropertiesAsync(string subscriptionId, string resourceProvider, IActivity activity, CancellationToken cancellationToken);

        /// <summary>
        /// Gets the subscription mappings.
        /// </summary>
        /// <param name="subscriptionId">Subscription id.</param>
        /// <param name="activity">Activity monitor.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>Subscription mappings.</returns>
        Task<SubscriptionMappingsModel> GetSubscriptionMappingsAsync(string subscriptionId, IActivity activity, CancellationToken cancellationToken);

        /// <summary>
        /// Gets the subscription internal properties.
        /// </summary>
        /// <param name="subscriptionId">Subscription id.</param>
        /// <param name="activity">Activity monitor.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>Subscription internal properties.</returns>
        Task<SubscriptionInternalPropertiesModel> GetSubscriptionInternalPropertiesAsync(string subscriptionId, IActivity activity, CancellationToken cancellationToken);

        /// <summary>
        /// Gets the subscription registrations properties.
        /// </summary>
        /// <param name="subscriptionId">Subscription id.</param>
        /// <param name="resourceProvider">Resource provider.</param>
        /// <param name="activity">Activity monitor.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>Subscription Registrations</returns>
        Task<SubscriptionRegistrationModel> GetSubscriptionRegistrationAsync(string subscriptionId, string resourceProvider, IActivity activity, CancellationToken cancellationToken);

        /// <summary>
        /// Gets the list of subscription ids.
        /// </summary>
        /// <param name="key">key.</param>
        /// <param name="start">start index.</param>
        /// <param name="end">end index.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>List of subscription ids.</returns>
        Task<string[]> GetSubscriptionsByRangeAsync(string key, int start, int end, CancellationToken cancellationToken);
    }
}

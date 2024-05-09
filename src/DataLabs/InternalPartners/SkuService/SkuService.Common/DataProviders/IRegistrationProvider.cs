namespace SkuService.Common.DataProviders
{
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.ActivityTracing.Contracts;
    using SkuService.Common.Models.V1;
    using System.Collections.Generic;
    using System.Threading.Tasks;

    public interface IRegistrationProvider
    {
        /// <summary>
        /// Returns the global sku settings for a given resource provider.
        /// </summary>
        /// <param name="resourceProvider">The resource provider.</param>
        /// <param name="subscriptionFeatureRegistrationProperties">The subscription feature registration properties</param>
        /// <param name="changedSkus">List of changed skus.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>List of Sku settings.</returns>
        public Task<IEnumerable<GlobalSku>> GetGlobalSkuAsync(string resourceProvider, IEnumerable<SubscriptionFeatureRegistrationPropertiesModel> subscriptionFeatureRegistrationProperties, GlobalSku? changedSkus, CancellationToken cancellationToken);

        /// <summary>
        /// Returns the list of registrations for a given resource provider and feature set.
        /// </summary>
        /// <param name="resourceProvider"></param>
        /// <param name="subscriptionFeatureRegistrationProperties"></param>
        /// <param name="activity">Activity.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns></returns>
        public Task<ResourceTypeRegistration[]> FindRegistrationsForFeatureSetAsync(string resourceProvider, IEnumerable<SubscriptionFeatureRegistrationPropertiesModel> subscriptionFeatureRegistrationProperties, IActivity activity, CancellationToken cancellationToken);

        /// <summary>
        /// Gets the list of resource providers.
        /// </summary>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>RP list.</returns>
        public Task<string?[]> GetResourceProvidersAsync(CancellationToken cancellationToken);
    }
}

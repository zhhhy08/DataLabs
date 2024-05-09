namespace SkuService.Common.Builders
{
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.ActivityTracing.Contracts;
    using SkuService.Common.Models.V1;

    public interface IDataBuilder<T>
    {
        /// <summary>
        /// Builds the records.
        /// </summary>
        /// <param name="resourceProvider"></param>
        /// <param name="subscriptionId"></param>
        /// <param name="parentActivity"></param>
        /// <param name="changedDatasets"></param>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        IAsyncEnumerable<T> BuildAsync(string resourceProvider, string subscriptionId, IActivity parentActivity, ChangedDatasets changedDatasets, CancellationToken cancellationToken = default);
    }
}
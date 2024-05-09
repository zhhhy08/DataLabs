namespace SkuService.Main.Pipelines;
using Microsoft.WindowsAzure.Governance.DataLabs.Common.Partner.DataLabsInterface;

public interface IDataPipeline<TIn,TOut>
{
    /// <summary>
    /// Gets partial sync notifications for a given input.
    /// </summary>
    /// <param name="resource"></param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    public IAsyncEnumerable<TOut> GetResourcesForSingleSubscriptionAsync(TIn resource, CancellationToken cancellationToken);

    public IAsyncEnumerable<TOut> GetSubJobsAsync(TIn request, CancellationToken cancellationToken);

    public IAsyncEnumerable<TOut> GetSubJobsForFullSyncAsync(TIn request, CancellationToken cancellationToken);

    public IAsyncEnumerable<TOut> GetResourcesForSubjobsAsync(DataLabsARNV3Request request, CancellationToken cancellationToken);

}
namespace Microsoft.WindowsAzure.Governance.DataLabs.IOService.InputDataProvider
{
    using System.Threading;
    using System.Threading.Tasks;

    internal interface IEventhubInputProvider : IInputProvider
    {
        public Task<bool> DeleteCheckpointsAsync(string consumerGroupName, CancellationToken cancellationToken);

    }
}

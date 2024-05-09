namespace Microsoft.WindowsAzure.Governance.DataLabs.IOService.InputDataProvider
{
    using System.Threading;
    using System.Threading.Tasks;

    internal interface IInputProvider
    {
        public abstract string Name { get; }
        public Task StartAsync(CancellationToken cancellationToken);
        public Task StopAsync(CancellationToken cancellationToken);
    }
}

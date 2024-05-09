namespace Microsoft.WindowsAzure.Governance.DataLabs.Tests.IOService.Services
{
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using Microsoft.WindowsAzure.Governance.DataLabs.IOService.Services;
    using System.Threading.Tasks;

    internal static class TestInputOutputServiceExtensions
    {
        public static async Task ConcurrencyCheck(this TestInputOutputService testInputOutputService, long partitionId)
        {
            await Task.Delay(100).ConfigureAwait(false);
            Assert.AreEqual(0, SolutionInputOutputService.GlobalConcurrencyManager.NumRunning);
            Assert.AreEqual(0, SolutionInputOutputService.RawInputChannelConcurrencyManager.GetCurrentNumRunning());
            Assert.AreEqual(0, SolutionInputOutputService.InputChannelConcurrencyManager.GetCurrentNumRunning());
            Assert.AreEqual(0, SolutionInputOutputService.InputCacheChannelConcurrencyManager.GetCurrentNumRunning());
            Assert.AreEqual(0, SolutionInputOutputService.SourceOfTruthChannelConcurrencyManager.GetCurrentNumRunning());
            Assert.AreEqual(0, SolutionInputOutputService.OutputCacheChannelConcurrencyManager.GetCurrentNumRunning());

            int count = testInputOutputService.taskInfoQueuePerPartition.Length;
            for (int i = 0; i < count; i++)
            {
                if (i == partitionId)
                {
                    Assert.AreEqual(1, testInputOutputService.taskInfoQueuePerPartition[i].TaskInfoQueueLength);
                    testInputOutputService.taskInfoQueuePerPartition[i].UpdateCompletedTasks();
                    Assert.AreEqual(0, testInputOutputService.taskInfoQueuePerPartition[i].TaskInfoQueueLength);
                }
                else
                {
                    Assert.AreEqual(0, testInputOutputService.taskInfoQueuePerPartition[i].TaskInfoQueueLength);
                }
            }
        }
    }
}

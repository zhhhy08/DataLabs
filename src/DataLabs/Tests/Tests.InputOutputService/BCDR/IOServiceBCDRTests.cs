namespace Microsoft.WindowsAzure.Governance.DataLabs.Tests.IOService.BCDR
{
    using Microsoft.Extensions.Configuration;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.ActivityTracing;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.ArnPublishClient.Interfaces;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.ConfigMap;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.Constants;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.Contracts;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.PartnerBlobClient;
    using Microsoft.WindowsAzure.Governance.DataLabs.IOService.Constants;
    using Microsoft.WindowsAzure.Governance.DataLabs.IOService.EventHub;
    using Microsoft.WindowsAzure.Governance.DataLabs.IOService.Services;
    using Microsoft.WindowsAzure.Governance.DataLabs.Tests.Common.Core.Common;
    using Microsoft.WindowsAzure.Governance.DataLabs.Tests.Common.Core.Constants;
    using Microsoft.WindowsAzure.Governance.DataLabs.Tests.IOService.Services;
    using Microsoft.WindowsAzure.Governance.Notifications.ArnContracts;
    using Microsoft.WindowsAzure.Governance.Notifications.ArnContracts.ResourceContracts;
    using Microsoft.WindowsAzure.Governance.NotificationsClient.Clients.Contracts;
    using Microsoft.WindowsAzure.Governance.NotificationsClient.Notifications.Contracts;
    using Microsoft.WindowsAzure.Governance.NotificationsClient.Notifications.Contracts.Data;
    using Microsoft.WindowsAzure.Governance.DataLabs.IOService.InputDataProvider;
    using Moq;
    using System.Threading.Tasks;
    using Microsoft.WindowsAzure.Governance.DataLabs.Tests.Common;
    using static Microsoft.WindowsAzure.Governance.DataLabs.Tests.IOService.Services.TestInputOutputService;

    [TestClass]
    public class IOServiceBCDRTests
    {
        #region Fields

        #endregion

        [TestInitialize]
        public void TestInitializeAsync()
        {
            ConfigMapUtil.Reset();
            var configBuilder = new ConfigurationBuilder().AddInMemoryCollection();
            ConfigMapUtil.Initialize(configBuilder, false);
            ConfigMapUtil.Configuration[InputOutputConstants.AllowedOutputTypes] = ResourcesConstants.AllowedSampleOutputResourceType;
            ConfigMapUtil.Configuration[SolutionConstants.PartnerSingleResponseResourcesRouting] = ResourcesConstants.PartnerSingleResponseResourcesRouting;
            ConfigMapUtil.Configuration[InputOutputConstants.PartnerChannelConcurrency] = ResourcesConstants.PartnerChannelConcurrency;
            ConfigMapUtil.Configuration[SolutionConstants.PrimaryRegionName] = "p-eus";
            ConfigMapUtil.Configuration[SolutionConstants.BackupRegionName] = "b-eus";
            ConfigMapUtil.Configuration[InputOutputConstants.PublishOutputToArn] = "false";
            ConfigMapUtil.Configuration[InputOutputConstants.EnableBlobPayloadRouting] = "true";
            ConfigMapUtil.Configuration[InputOutputConstants.DelaySecsForEHLoadBalancing] = "1";
        }

        [TestCleanup]
        public void TestCleanup()
        {
            ConfigMapUtil.Reset();
            ActivityMonitorFactory.UseTaskAwareActivityMonitor = false;
        }

        #region Node restart scenarios

        [DataRow(true, "", "")] // normal deployment
        [DataRow(true, "true", "")] // after BCDR end value 
        [DataRow(true, "true/00:00:10", "")] // after BCDR end value
        [DataRow(true, "", "false")] // after BCDR end value
        [DataRow(true, "", "false/00:00:10")] // after BCDR end value
        [DataRow(false, "", "")] // normal deployment
        [DataRow(false, "true", "")] // after BCDR end value 
        [DataRow(false, "true/00:00:10", "")] // after BCDR end value
        [DataRow(false, "", "false")] // after BCDR end value
        [DataRow(false, "", "false/00:00:10")] // after BCDR end value
        [TestMethod]
        public async Task InitializeInputProvidersAsyncOnDefaultStartup(bool cacheEnabledPartner, string inputChannelActiveString, string backupInputChannelString)
        {
            
            ConfigMapUtil.Configuration[InputOutputConstants.StartBackupInputProvidersAtStartup] = cacheEnabledPartner.ToString();
            ConfigMapUtil.Configuration[InputOutputConstants.InputChannelActive] = inputChannelActiveString;
            ConfigMapUtil.Configuration[InputOutputConstants.BackupInputChannelActive] = backupInputChannelString;

            var testInputOutputService = new TestInputOutputService(numInputProviders: 2, numBackupInputProviders: 2);
            await testInputOutputService.InitializeAndStartAsync().ConfigureAwait(false);

            var inputEventhubs = testInputOutputService.GetInputProviders();
            var backupEventhubs = testInputOutputService.GetBackupInputProviders();

            CheckAllAsyncMethodCalledOnce(inputEventhubs, MethodName.StartAsync);
            if (cacheEnabledPartner)
            {            
                CheckAllAsyncMethodCalledOnce(backupEventhubs, MethodName.StartAsync);
            } else
            {
                CheckAllAsyncMethodNotCalled(backupEventhubs, MethodName.StartAsync);
            }
        }


        [DataRow(true, "false", "")] // cache, BCDR start value , default  value
        [DataRow(true, "false/00:00:10", "")] // cache, BCDR start value, default value
        [DataRow(true, "false", "false")] // cache, BCDR start value, BCDR end value 
        [DataRow(true, "false/00:00:10", "false/00:00:10")] // cache, BCDR start value, BCDR end value
        [DataRow(false, "false", "")] // no cache, BCDR start value , default  value
        [DataRow(false, "false/00:00:10", "")] // no cache, BCDR start value, default value
        [DataRow(false, "false", "false")] // no cache, BCDR start value, BCDR end value 
        [DataRow(false, "false/00:00:10", "false/00:00:10")] // no cache, BCDR start value, BCDR end value
        [TestMethod]
        public async Task InitializeInputProvidersAsyncOnBCDRStartupForInputChannel(bool cacheEnabledPartner, string inputChannelActiveString, string backupInputChannelString)
        {

            ConfigMapUtil.Configuration[InputOutputConstants.StartBackupInputProvidersAtStartup] = cacheEnabledPartner.ToString();
            ConfigMapUtil.Configuration[InputOutputConstants.InputChannelActive] = inputChannelActiveString;
            ConfigMapUtil.Configuration[InputOutputConstants.BackupInputChannelActive] = backupInputChannelString;

            var testInputOutputService = new TestInputOutputService(numInputProviders: 2, numBackupInputProviders: 2);
            await testInputOutputService.InitializeAndStartAsync().ConfigureAwait(false);

            var inputEventhubs = testInputOutputService.GetInputProviders();
            var backupEventhubs = testInputOutputService.GetBackupInputProviders();

            CheckAllAsyncMethodNotCalled(inputEventhubs, MethodName.StartAsync);
            CheckAllAsyncMethodNotCalled(backupEventhubs, MethodName.StartAsync);
        }

        [DataRow(true, "", "true")] // cache, default value, BCDR start value
        [DataRow(true, "", "true/00:00:10")] // cache, default value, BCDR start value
        [DataRow(true, "true", "true")] // cache, BCDR end value, BCDR start value
        [DataRow(true, "true/00:00:10", "true/00:00:10")] // cache, BCDR end value, BCDR start value
        [DataRow(false, "", "true")] // cache, default value, BCDR start value
        [DataRow(false, "", "true/00:00:10")] // cache, default value, BCDR start value
        [DataRow(false, "true", "true")] // cache, BCDR end value, BCDR start value
        [DataRow(false, "true/00:00:10", "true/00:00:10")] // cache, BCDR end value, BCDR start value
        [TestMethod]
        public async Task InitializeInputProvidersAsyncOnBCDRStartupForBackupInputChannel(bool cacheEnabledPartner, string inputChannelActiveString, string backupInputChannelString)
        {
            ConfigMapUtil.Configuration[InputOutputConstants.StartBackupInputProvidersAtStartup] = cacheEnabledPartner.ToString();
            ConfigMapUtil.Configuration[InputOutputConstants.InputChannelActive] = inputChannelActiveString;
            ConfigMapUtil.Configuration[InputOutputConstants.BackupInputChannelActive] = backupInputChannelString;

            var testInputOutputService = new TestInputOutputService(numInputProviders: 2, numBackupInputProviders: 2);
            await testInputOutputService.InitializeAndStartAsync().ConfigureAwait(false);

            var inputEventhubs = testInputOutputService.GetInputProviders();
            var backupEventhubs = testInputOutputService.GetBackupInputProviders();

            CheckAllAsyncMethodCalledOnce(inputEventhubs, MethodName.StartAsync);
            CheckAllAsyncMethodCalledOnce(backupEventhubs, MethodName.StartAsync);
            // TODO test after refactor: backup eventhubs start with the secondary consumer group.
            // Requires EventHubInputProvider.AddBackupEventHubInputProviders(TimeSpan? initialOffsetFromCurrent = null, string consumerGroupOverride = null) to be overridden.
        }

        #endregion

        #region Input Channel scenarios - Disaster Region operations

        // Input channel is always on as its reading from the primary eventhub in a non BCDR scenario
        [DataRow(true, "false", "")] // cache, BCDR start value , default  value
        [DataRow(true, "false/00:00:10", "")] // cache, BCDR start value, default value
        [DataRow(true, "false", "false")] // cache, BCDR start value, BCDR end value 
        [DataRow(true, "false/00:00:10", "false/00:00:10")] // cache, BCDR start value, BCDR end value
        [DataRow(false, "false", "")] // no cache, BCDR start value , default  value
        [DataRow(false, "false/00:00:10", "")] // no cache, BCDR start value, default value
        [DataRow(false, "false", "false")] // no cache, BCDR start value, BCDR end value 
        [DataRow(false, "false/00:00:10", "false/00:00:10")] // no cache, BCDR start value, BCDR end value
        [TestMethod]
        public async Task StopInputChannel(bool cacheEnabledPartner, string inputChannelActiveString, string backupInputChannelString)
        {
            ConfigMapUtil.Configuration[InputOutputConstants.StartBackupInputProvidersAtStartup] = cacheEnabledPartner.ToString();

            var testInputOutputService = new TestInputOutputService(numInputProviders: 2, numBackupInputProviders: 2);
            await testInputOutputService.InitializeAndStartAsync().ConfigureAwait(false);

            //Configs for BCDR applied after startup
            ConfigMapUtil.Configuration[InputOutputConstants.InputChannelActive] = inputChannelActiveString;
            ConfigMapUtil.Configuration[InputOutputConstants.BackupInputChannelActive] = backupInputChannelString;

            PrivateFunctionAccessHelper.RunStaticAsyncMethod(typeof(SolutionInputOutputService), "EvaluateInputChannelConfigAndPerformActions", [inputChannelActiveString]); 
            var inputEventhubs = testInputOutputService.GetInputProviders();
            var backupEventhubs = testInputOutputService.GetBackupInputProviders();

            // sleep for delete checkpoint code in background to finish
            Thread.Sleep(30000); // needs 30 secs as per local test runs to finish bg thread

            if (cacheEnabledPartner)
            {
                CheckAllAsyncMethodCalledOnce(inputEventhubs, MethodName.StartAsync);
                CheckAllAsyncMethodCalledOnce(inputEventhubs, MethodName.StopAsync);
                CheckAllAsyncMethodCalledOnce(inputEventhubs, MethodName.DeleteCheckpointAsync, InputOutputConstants.DefaultConsumerGroupName);

                CheckAllAsyncMethodCalledOnce(backupEventhubs, MethodName.StartAsync);
                CheckAllAsyncMethodCalledOnce(backupEventhubs, MethodName.StopAsync);
                CheckAllAsyncMethodCalledOnce(backupEventhubs, MethodName.DeleteCheckpointAsync, InputOutputConstants.DefaultConsumerGroupName);
            }
            else
            {
                CheckAllAsyncMethodCalledOnce(inputEventhubs, MethodName.StartAsync);
                CheckAllAsyncMethodCalledOnce(inputEventhubs, MethodName.StopAsync);
                CheckAllAsyncMethodCalledOnce(inputEventhubs, MethodName.DeleteCheckpointAsync, InputOutputConstants.DefaultConsumerGroupName);

                CheckAllAsyncMethodNotCalled(backupEventhubs, MethodName.StartAsync);
                CheckAllAsyncMethodNotCalled(backupEventhubs, MethodName.StopAsync);
                CheckAllAsyncMethodNotCalled(backupEventhubs, MethodName.DeleteCheckpointAsync);
            }

        }

        // Input channel is always on as its reading from the primary eventhub in a non BCDR scenario
        [Ignore]
        [TestMethod]
        public  Task StartInputChannel(bool cacheEnabledPartner, string inputChannelActiveString, string backupInputChannelString)
        {
            // TODO test after refactor
            // Requires EventHubInputProvider.AddBackupEventHubInputProviders to be overridden
            // Requires EventHubInputProvider.AddEventHubInputProviders to be overridden
            return Task.CompletedTask;
        }



        #endregion

        #region Backup Input Channel scenarios - Paired Region operations

        [Ignore]
        [TestMethod]
        public Task StartBackupInputChannel()
        {
            // TODO test after refactor
            // Requires EventHubInputProvider.AddBackupEventHubInputProviders to be overridden
            return Task.CompletedTask;
        }

        [DataRow(false, "", "false")] // no cache , default  value, BCDR end value
        [DataRow(false, "", "false/00:00:10")] // no cache, default value,  BCDR end value
        [DataRow(false, "true", "false")] // no cache, ,BCDR end value,  BCDR end value
        [DataRow(false, "true/00:00:10", "false/00:00:10")] // no cache, BCDR end value,  BCDR end value
        [TestMethod]
        public async Task StopBackupInputChannel(bool cacheEnabledPartner, string inputChannelActiveString, string backupInputChannelString)
        {
            ConfigMapUtil.Configuration[InputOutputConstants.StartBackupInputProvidersAtStartup] = cacheEnabledPartner.ToString();
            ConfigMapUtil.Configuration[InputOutputConstants.InputChannelActive] = inputChannelActiveString;
            ConfigMapUtil.Configuration[InputOutputConstants.BackupInputChannelActive] = "true/00:00:10"; // startup with BCDR already happening

            var testInputOutputService = new TestInputOutputService(numInputProviders: 2, numBackupInputProviders: 2);
            await testInputOutputService.InitializeAndStartAsync().ConfigureAwait(false);

            //apply change in config after startup
            ConfigMapUtil.Configuration[InputOutputConstants.BackupInputChannelActive] = backupInputChannelString;


            PrivateFunctionAccessHelper.RunStaticAsyncMethod(typeof(SolutionInputOutputService), "EvaluateBackupInputChannelConfigAndPerformActions", [backupInputChannelString]);

            var inputEventhubs = testInputOutputService.GetInputProviders();
            var backupEventhubs = testInputOutputService.GetBackupInputProviders();

            // sleep for delete checkpoint code in background to finish
            Thread.Sleep(20000);

            if (cacheEnabledPartner)
            {
                // TODO test after refactor for cache partners
                // Requires EventHubInputProvider.AddBackupEventHubInputProviders to be overridden
            } else
            {
                CheckAllAsyncMethodCalledOnce(inputEventhubs, MethodName.StartAsync);
                CheckAllAsyncMethodNotCalled(inputEventhubs, MethodName.StopAsync);

                CheckAllAsyncMethodCalledOnce(backupEventhubs, MethodName.StartAsync);
                CheckAllAsyncMethodCalledOnce(backupEventhubs, MethodName.StopAsync);
                CheckAllAsyncMethodCalledOnce(backupEventhubs, MethodName.DeleteCheckpointAsync, InputOutputConstants.SecondaryConsumerGroupName);
            }

        }

        #endregion

        #region Helper methods

        public void CheckAllAsyncMethodCalledOnce(List<TestEventHubInputProvider> providers, MethodName methodName, string consumerGroupName = null)
        {

            foreach (var item in providers)
            {
                int comparisonVar = 0;
                string cgVar = null;
                switch (methodName)
                {
                    case MethodName.StartAsync:
                        comparisonVar = item.numStartAsyncCalled;
                        break;
                    case MethodName.StopAsync:
                        comparisonVar = item.numStopAsyncCalled;
                        break;
                    case MethodName.DeleteCheckpointAsync:
                        comparisonVar = item.numDeleteCheckpointAsyncCalled;
                        cgVar = item.consumerGroupDeleted;
                        break;
                    default: throw new NotImplementedException();
                }
                Assert.AreEqual(1, comparisonVar);
                if(cgVar != null)
                {
                    Assert.AreEqual(consumerGroupName, cgVar);
                }
            }
        }

        public void CheckAllAsyncMethodNotCalled(List<TestEventHubInputProvider> providers, MethodName methodName)
        {
            foreach (var item in providers)
            {
                int comparisonVar = 0;
                switch (methodName)
                {
                    case MethodName.StartAsync:
                        comparisonVar = item.numStartAsyncCalled;
                        break;
                    case MethodName.StopAsync:
                        comparisonVar = item.numStopAsyncCalled;
                        break;
                    case MethodName.DeleteCheckpointAsync:
                        comparisonVar = item.numDeleteCheckpointAsyncCalled;
                        break;
                    default: throw new NotImplementedException();
                }
                Assert.AreEqual(0, comparisonVar);
            }
        }

        public enum MethodName
        {
            StartAsync,
            StopAsync,
            DeleteCheckpointAsync
        }

        #endregion

    }
}
namespace Microsoft.WindowsAzure.Governance.DataLabs.Tests.Common.Core.TaskChannel
{
    using Microsoft.Extensions.Configuration;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.ConcurrencyManager;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.ConfigMap;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.ServiceMonitoring;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.TaskChannel;
    using Moq;
    using System.Diagnostics;
    using System.Reflection;

    [TestClass]
    public class TaskChannelTests
    {
        [TestInitialize]
        public void TestInitialize()
        {
            var configBuilder = new ConfigurationBuilder().AddInMemoryCollection();
            ConfigMapUtil.Initialize(configBuilder, false);
            Tracer.CreateDataLabsTracerProvider(TestTaskChannel.TestActivitySource.Name);
        }

        [TestCleanup]
        public void TestCleanup()
        {
            ConfigMapUtil.Reset();
        }

        [TestMethod]
        public async Task TestWithoutConcurrency()
        {
            var testChannel = new TestTaskChannel("testChannel");
            var testTaskFactory = new TestSubTaskFactory(false, false);
            testChannel.AddSubTaskFactory(testTaskFactory);

            var abstractChannel = (AbstractConcurrentTaskChannelManager<TestEventTaskContext>)testChannel;
            var configurableConcurrencyManager = PrivateFunctionAccessHelper.GetPrivateField(
                               typeof(AbstractConcurrentTaskChannelManager<TestEventTaskContext>), "_perTypeConfigurableConcurrencyManager", abstractChannel);
            Assert.IsNotNull(configurableConcurrencyManager);

            var perTypeConcurrencyManager = PrivateFunctionAccessHelper.GetPrivateField(
                   typeof(AbstractConcurrentTaskChannelManager<TestEventTaskContext>), "_perTypeConcurrencyManager", abstractChannel);
            Assert.IsNull(perTypeConcurrencyManager);

            var externalConcurrencyManager = PrivateFunctionAccessHelper.GetPrivateField(
                   typeof(AbstractConcurrentTaskChannelManager<TestEventTaskContext>), "_externalConcurrencyManager", abstractChannel);
            Assert.IsNull(externalConcurrencyManager);

            var testTaskContext = new TestEventTaskContext(default, true, 0);
            var testScenario = "TestScenario";
            testTaskContext.Scenario = testScenario;

            Assert.AreEqual(null, OpenTelemetryActivityWrapper.Current);

            await testChannel.ExecuteEventTaskContextAsync(testTaskContext).ConfigureAwait(false);

            Assert.AreEqual(testChannel.ChannelName, testTaskContext.CurrentTaskChannel.ChannelName);
            Assert.AreEqual(1, testChannel.BeforeProcessCalled);
            Assert.AreEqual(1, testChannel.TaskNotMovedCalled);
            Assert.AreEqual(0, testChannel.ProcessErrorCalled);
            Assert.AreEqual(0, testChannel.testErrorTaskChannel.BeforeProcessCalled);
            Assert.AreEqual(0, testChannel.testErrorTaskChannel.TaskNotMovedCalled);
            Assert.AreEqual(0, testChannel.testErrorTaskChannel.ProcessErrorCalled);

            Assert.AreEqual(testScenario, testChannel.BeforeProcessIActivity?.Scenario);
            //Assert.AreEqual(testScenario, testChannel.TaskNotMovedIActivity?.Scenario);
            Assert.AreEqual(null, testChannel.ProcessErrorIActivity?.Scenario);

            var beforeActivityWrapper = testChannel.BeforeCurrentActivityWrapper;
            Assert.IsNotNull(beforeActivityWrapper);
            Assert.AreEqual(testTaskContext.EventTaskActivity, beforeActivityWrapper);
            Assert.AreEqual(beforeActivityWrapper, testChannel.TaskNotMovedCurrentActivityWrapper);
            Assert.AreEqual(null, testChannel.ErrorCurrentActivityWrapper);

            Assert.AreEqual(null, OpenTelemetryActivityWrapper.Current);
            Assert.AreEqual(testTaskContext.EventTaskActivity, testTaskFactory._testTask.CurrentActivityWrapper);
            Assert.AreEqual(testTaskContext.EventTaskActivity.Context, testTaskFactory._testTask.CurrentActivity.Context);
            Assert.AreEqual(testTaskContext.EventTaskActivity, testTaskFactory._testTask.AfterActivityWrapper);
            Assert.AreEqual(testTaskContext.EventTaskActivity.Context, testTaskFactory._testTask.AfterActivity.Context);
            Assert.AreEqual(testTaskContext.EventTaskActivity, testTaskFactory._testTask.DoTaskWork1CurrentActivityWrapper);
            Assert.AreEqual(testTaskContext.EventTaskActivity.Context, testTaskFactory._testTask.DoTaskWork1CurrentActivity.Context);
            Assert.AreEqual(testTaskContext.EventTaskActivity, testTaskFactory._testTask.DoTaskWork1AfterActivityWrapper);
            Assert.AreEqual(testTaskContext.EventTaskActivity.Context, testTaskFactory._testTask.DoTaskWork1AfterActivity.Context);
            Assert.AreEqual(testTaskContext.EventTaskActivity, testTaskFactory._testTask.DoTaskWork2CurrentActivityWrapper);
            Assert.AreEqual(testTaskContext.EventTaskActivity.Context, testTaskFactory._testTask.DoTaskWork2CurrentActivity.Context);
            Assert.AreEqual(1, testTaskFactory._testTask.TaskCalled);
            Assert.AreEqual(true, testTaskFactory._testTask.DoTaskWork1Called);
            Assert.AreEqual(true, testTaskFactory._testTask.DoTaskWork2Called);
            Assert.AreEqual(0, testTaskFactory._testTask.ValueTaskCalled);
        }

        [TestMethod]
        public async Task TestValueTaskWithoutConcurrency()
        {
            var testChannel = new TestTaskChannel("testChannel");
            var testTaskFactory = new TestSubTaskFactory(true, false);
            testChannel.AddSubTaskFactory(testTaskFactory);

            var abstractChannel = (AbstractConcurrentTaskChannelManager<TestEventTaskContext>)testChannel;
            var configurableConcurrencyManager = PrivateFunctionAccessHelper.GetPrivateField(
                               typeof(AbstractConcurrentTaskChannelManager<TestEventTaskContext>), "_perTypeConfigurableConcurrencyManager", abstractChannel);
            Assert.IsNotNull(configurableConcurrencyManager);

            var perTypeConcurrencyManager = PrivateFunctionAccessHelper.GetPrivateField(
                   typeof(AbstractConcurrentTaskChannelManager<TestEventTaskContext>), "_perTypeConcurrencyManager", abstractChannel);
            Assert.IsNull(perTypeConcurrencyManager);

            var externalConcurrencyManager = PrivateFunctionAccessHelper.GetPrivateField(
                   typeof(AbstractConcurrentTaskChannelManager<TestEventTaskContext>), "_externalConcurrencyManager", abstractChannel);
            Assert.IsNull(externalConcurrencyManager);

            var testTaskContext = new TestEventTaskContext(default, true, 0);
            var testScenario = "TestScenario";
            testTaskContext.Scenario = testScenario;
            
            await testChannel.ExecuteEventTaskContextAsync(testTaskContext).ConfigureAwait(false);

            Assert.AreEqual(testChannel.ChannelName, testTaskContext.CurrentTaskChannel.ChannelName);
            Assert.AreEqual(1, testChannel.BeforeProcessCalled);
            Assert.AreEqual(1, testChannel.TaskNotMovedCalled);
            Assert.AreEqual(0, testChannel.ProcessErrorCalled);
            Assert.AreEqual(0, testChannel.testErrorTaskChannel.BeforeProcessCalled);
            Assert.AreEqual(0, testChannel.testErrorTaskChannel.TaskNotMovedCalled);
            Assert.AreEqual(0, testChannel.testErrorTaskChannel.ProcessErrorCalled);

            Assert.AreEqual(testScenario, testChannel.BeforeProcessIActivity?.Scenario);
            //Assert.AreEqual(testScenario, testChannel.TaskNotMovedIActivity?.Scenario);
            Assert.AreEqual(null, testChannel.ProcessErrorIActivity?.Scenario);

            var beforeActivityWrapper = testChannel.BeforeCurrentActivityWrapper;
            Assert.IsNotNull(beforeActivityWrapper);
            Assert.AreEqual(testTaskContext.EventTaskActivity, beforeActivityWrapper);
            Assert.AreEqual(beforeActivityWrapper, testChannel.TaskNotMovedCurrentActivityWrapper);
            Assert.AreEqual(null, testChannel.ErrorCurrentActivityWrapper);

            Assert.AreEqual(null, OpenTelemetryActivityWrapper.Current);
            Assert.AreEqual(testTaskContext.EventTaskActivity, testTaskFactory._testTask.CurrentActivityWrapper);
            Assert.AreEqual(testTaskContext.EventTaskActivity.Context, testTaskFactory._testTask.CurrentActivity.Context);
            Assert.AreEqual(0, testTaskFactory._testTask.TaskCalled);
            Assert.AreEqual(1, testTaskFactory._testTask.ValueTaskCalled);
        }

        [TestMethod]
        public async Task ErrorTestWithoutConcurrency()
        {
            var testChannel = new TestTaskChannel("testChannel");
            var testTaskFactory = new TestSubTaskFactory(false, true);
            testChannel.AddSubTaskFactory(testTaskFactory);

            var abstractChannel = (AbstractConcurrentTaskChannelManager<TestEventTaskContext>)testChannel;
            var configurableConcurrencyManager = PrivateFunctionAccessHelper.GetPrivateField(
                               typeof(AbstractConcurrentTaskChannelManager<TestEventTaskContext>), "_perTypeConfigurableConcurrencyManager", abstractChannel);
            Assert.IsNotNull(configurableConcurrencyManager);

            var perTypeConcurrencyManager = PrivateFunctionAccessHelper.GetPrivateField(
                   typeof(AbstractConcurrentTaskChannelManager<TestEventTaskContext>), "_perTypeConcurrencyManager", abstractChannel);
            Assert.IsNull(perTypeConcurrencyManager);

            var externalConcurrencyManager = PrivateFunctionAccessHelper.GetPrivateField(
                   typeof(AbstractConcurrentTaskChannelManager<TestEventTaskContext>), "_externalConcurrencyManager", abstractChannel);
            Assert.IsNull(externalConcurrencyManager);

            var parentContext = Tracer.CreateNewActivityContext();
            var testTaskContext = new TestEventTaskContext(parentContext, true, 0);
            var testScenario = "TestScenario";
            testTaskContext.Scenario = testScenario;

            await testChannel.ExecuteEventTaskContextAsync(testTaskContext).ConfigureAwait(false);

            Assert.AreEqual(testChannel.testErrorTaskChannel.ChannelName, testTaskContext.CurrentTaskChannel.ChannelName);
            Assert.AreEqual(1, testChannel.BeforeProcessCalled);
            Assert.AreEqual(0, testChannel.TaskNotMovedCalled);
            Assert.AreEqual(1, testChannel.ProcessErrorCalled);

            Assert.AreEqual(1, testChannel.testErrorTaskChannel.BeforeProcessCalled);
            Assert.AreEqual(1, testChannel.testErrorTaskChannel.TaskNotMovedCalled);
            Assert.AreEqual(0, testChannel.testErrorTaskChannel.ProcessErrorCalled);

            Assert.AreEqual(testScenario, testChannel.BeforeProcessIActivity?.Scenario);
            Assert.AreEqual(null, testChannel.TaskNotMovedIActivity?.Scenario);
            Assert.AreEqual(testScenario, testChannel.ProcessErrorIActivity?.Scenario);

            Assert.AreEqual(testScenario, testChannel.testErrorTaskChannel.BeforeProcessIActivity?.Scenario);
            Assert.AreEqual(testScenario, testChannel.testErrorTaskChannel.TaskNotMovedIActivity?.Scenario);
            Assert.AreEqual(null, testChannel.testErrorTaskChannel.ProcessErrorIActivity?.Scenario);

            var beforeActivityWrapper = testChannel.BeforeCurrentActivityWrapper;
            Assert.IsNotNull(beforeActivityWrapper);
            Assert.AreEqual(testTaskContext.EventTaskActivity, beforeActivityWrapper);
            Assert.AreEqual(null, testChannel.TaskNotMovedCurrentActivityWrapper);
            Assert.AreEqual(beforeActivityWrapper, testChannel.ErrorCurrentActivityWrapper);

            Assert.AreEqual(beforeActivityWrapper, testChannel.testErrorTaskChannel.BeforeCurrentActivityWrapper);
            Assert.AreEqual(beforeActivityWrapper, testChannel.testErrorTaskChannel.TaskNotMovedCurrentActivityWrapper);
            Assert.AreEqual(null, testChannel.testErrorTaskChannel.ErrorCurrentActivityWrapper);

            Assert.AreEqual(null, OpenTelemetryActivityWrapper.Current);
            Assert.AreEqual(testTaskContext.EventTaskActivity, testTaskFactory._testTask.CurrentActivityWrapper);
            Assert.AreEqual(testTaskContext.EventTaskActivity.Context, testTaskFactory._testTask.CurrentActivity.Context);
            Assert.AreNotEqual(testTaskContext.EventTaskActivity.Context.TraceId, parentContext.TraceId);

            var activityInstance = (Activity)PrivateFunctionAccessHelper.GetPrivateField(
                   typeof(OpenTelemetryActivityWrapper), "_activity", testTaskContext.EventTaskActivity);
            Assert.AreEqual(activityInstance.ParentSpanId, parentContext.SpanId);

            Assert.AreEqual(1, testTaskFactory._testTask.TaskCalled);
            Assert.AreEqual(0, testTaskFactory._testTask.ValueTaskCalled);
        }

        [TestMethod]
        public async Task TestWithConcurrency()
        {
            var testConfig = "testConfig";
            var configurableConcurrencyManager = new ConfigurableConcurrencyManager(testConfig, 100);

            var testChannel = new TestTaskChannel("testChannel");
            configurableConcurrencyManager.RegisterObject(testChannel.SetExternalConcurrencyManager);

            var testTaskFactory = new TestSubTaskFactory(false, false);
            testChannel.AddSubTaskFactory(testTaskFactory);

            var abstractChannel = (AbstractConcurrentTaskChannelManager<TestEventTaskContext>)testChannel;

            var perTypeConcurrencyManager = PrivateFunctionAccessHelper.GetPrivateField(
                   typeof(AbstractConcurrentTaskChannelManager<TestEventTaskContext>), "_perTypeConcurrencyManager", abstractChannel);
            Assert.IsNull(perTypeConcurrencyManager);

            var externalConcurrencyManager = PrivateFunctionAccessHelper.GetPrivateField(
                   typeof(AbstractConcurrentTaskChannelManager<TestEventTaskContext>), "_externalConcurrencyManager", abstractChannel);
            Assert.IsNotNull(externalConcurrencyManager);

            var parentContext = Tracer.CreateNewActivityContext();
            var testTaskContext = new TestEventTaskContext(parentContext, true, 0);
            var testScenario = "TestScenario";
            testTaskContext.Scenario = testScenario;

            await testTaskContext.StartEventTaskAsync(testChannel, true, null).ConfigureAwait(false);

            Assert.AreEqual(testChannel.ChannelName, testTaskContext.CurrentTaskChannel.ChannelName);
            Assert.AreEqual(1, testChannel.BeforeProcessCalled);
            Assert.AreEqual(1, testChannel.TaskNotMovedCalled);
            Assert.AreEqual(0, testChannel.ProcessErrorCalled);
            Assert.AreEqual(0, testChannel.testErrorTaskChannel.BeforeProcessCalled);
            Assert.AreEqual(0, testChannel.testErrorTaskChannel.TaskNotMovedCalled);
            Assert.AreEqual(0, testChannel.testErrorTaskChannel.ProcessErrorCalled);

            Assert.AreEqual(testScenario, testChannel.BeforeProcessIActivity?.Scenario);
            Assert.AreEqual(testScenario, testChannel.TaskNotMovedIActivity?.Scenario);
            Assert.AreEqual(null, testChannel.ProcessErrorIActivity?.Scenario);

            Assert.AreEqual(null, testChannel.testErrorTaskChannel.BeforeProcessIActivity?.Scenario);
            Assert.AreEqual(null, testChannel.testErrorTaskChannel.TaskNotMovedIActivity?.Scenario);
            Assert.AreEqual(null, testChannel.testErrorTaskChannel.ProcessErrorIActivity?.Scenario);

            var beforeActivityWrapper = testChannel.BeforeCurrentActivityWrapper;
            Assert.IsNotNull(beforeActivityWrapper);
            Assert.AreEqual(testTaskContext.EventTaskActivity, beforeActivityWrapper);
            Assert.AreEqual(beforeActivityWrapper, testChannel.TaskNotMovedCurrentActivityWrapper);
            Assert.AreEqual(null, testChannel.ErrorCurrentActivityWrapper);

            Assert.AreEqual(null, testChannel.testErrorTaskChannel.BeforeCurrentActivityWrapper);
            Assert.AreEqual(null, testChannel.testErrorTaskChannel.TaskNotMovedCurrentActivityWrapper);
            Assert.AreEqual(null, testChannel.testErrorTaskChannel.ErrorCurrentActivityWrapper);

            Assert.AreEqual(null, OpenTelemetryActivityWrapper.Current);
            Assert.AreEqual(testTaskContext.EventTaskActivity, testTaskFactory._testTask.CurrentActivityWrapper);
            Assert.AreEqual(testTaskContext.EventTaskActivity.Context, testTaskFactory._testTask.CurrentActivity.Context);
            Assert.AreNotEqual(testTaskContext.EventTaskActivity.Context.TraceId, parentContext.TraceId); // new trace id

            var activityInstance = (Activity)PrivateFunctionAccessHelper.GetPrivateField(
                   typeof(OpenTelemetryActivityWrapper), "_activity", testTaskContext.EventTaskActivity);
            Assert.AreEqual(activityInstance.ParentSpanId, parentContext.SpanId);

            Assert.AreEqual(1, testTaskFactory._testTask.TaskCalled);
            Assert.AreEqual(0, testTaskFactory._testTask.ValueTaskCalled);

        }

        [TestMethod]
        public async Task TestTaskTimeOutWithConcurrency()
        {
            var testConfig = "testConfig";
            var configurableConcurrencyManager = new ConfigurableConcurrencyManager(testConfig, 100);

            var testChannel = new TestTaskChannel("testChannel");
            configurableConcurrencyManager.RegisterObject(testChannel.SetExternalConcurrencyManager);

            var testTaskFactory = new TestSubTaskFactory(false, false);
            testChannel.AddSubTaskFactory(testTaskFactory);

            var abstractChannel = (AbstractConcurrentTaskChannelManager<TestEventTaskContext>)testChannel;

            var perTypeConcurrencyManager = PrivateFunctionAccessHelper.GetPrivateField(
                   typeof(AbstractConcurrentTaskChannelManager<TestEventTaskContext>), "_perTypeConcurrencyManager", abstractChannel);
            Assert.IsNull(perTypeConcurrencyManager);

            var externalConcurrencyManager = PrivateFunctionAccessHelper.GetPrivateField(
                   typeof(AbstractConcurrentTaskChannelManager<TestEventTaskContext>), "_externalConcurrencyManager", abstractChannel);
            Assert.IsNotNull(externalConcurrencyManager);

            var parentContext = Tracer.CreateNewActivityContext();
            var testTaskContext = new TestEventTaskContext(parentContext, true, 0);
            var testScenario = "TestScenario";
            testTaskContext.Scenario = testScenario;

            // Cancel CancellationTokenSource to simulate timeout
            testTaskContext.TaskTimeOutCancellationTokenSource.Cancel();

            await testTaskContext.StartEventTaskAsync(testChannel, true, null).ConfigureAwait(false);

            // Task should be moved to Retry
            Assert.AreEqual(1, testTaskContext.MovingToRetryCalledCount);
            Assert.AreEqual(0, testTaskContext.MovingToPoisonCalledCount);

            Assert.AreEqual(testTaskContext.RetryChannel, testTaskContext.CurrentTaskChannel);
            Assert.AreEqual(0, testChannel.BeforeProcessCalled);
            Assert.AreEqual(0, testChannel.TaskNotMovedCalled);
            Assert.AreEqual(0, testChannel.ProcessErrorCalled);
            Assert.AreEqual(0, testChannel.testErrorTaskChannel.BeforeProcessCalled);
            Assert.AreEqual(0, testChannel.testErrorTaskChannel.TaskNotMovedCalled);
            Assert.AreEqual(0, testChannel.testErrorTaskChannel.ProcessErrorCalled);

            Assert.AreEqual(1, testTaskContext.RetryChannel.BeforeProcessCount);
            Assert.AreEqual(1, testTaskContext.RetryChannel.ProcessNotMovedCount);
            Assert.AreEqual(0, testTaskContext.RetryChannel.ProcessErrorCount);

            Assert.IsNull(testChannel.BeforeProcessIActivity?.Scenario);
            Assert.IsNull(testChannel.TaskNotMovedIActivity?.Scenario);
            Assert.IsNull(testChannel.ProcessErrorIActivity?.Scenario);

            Assert.IsNull(testChannel.testErrorTaskChannel.BeforeProcessIActivity?.Scenario);
            Assert.IsNull(testChannel.testErrorTaskChannel.TaskNotMovedIActivity?.Scenario);
            Assert.IsNull(testChannel.testErrorTaskChannel.ProcessErrorIActivity?.Scenario);

            var beforeActivityWrapper = testChannel.BeforeCurrentActivityWrapper;
            Assert.IsNull(beforeActivityWrapper);
            Assert.IsNull(testChannel.ErrorCurrentActivityWrapper);

            Assert.IsNull(testChannel.testErrorTaskChannel.BeforeCurrentActivityWrapper);
            Assert.IsNull(testChannel.testErrorTaskChannel.TaskNotMovedCurrentActivityWrapper);
            Assert.IsNull(testChannel.testErrorTaskChannel.ErrorCurrentActivityWrapper);

            Assert.IsNull(OpenTelemetryActivityWrapper.Current);
            Assert.IsNull(testTaskFactory._testTask.CurrentActivityWrapper);
            Assert.IsNull(testTaskFactory._testTask.CurrentActivity);
            Assert.AreNotEqual(testTaskContext.EventTaskActivity.Context.TraceId, parentContext.TraceId); // new trace id

            var activityInstance = (Activity)PrivateFunctionAccessHelper.GetPrivateField(
                   typeof(OpenTelemetryActivityWrapper), "_activity", testTaskContext.EventTaskActivity);
            Assert.AreEqual(activityInstance.ParentSpanId, parentContext.SpanId);

            Assert.AreEqual(0, testTaskFactory._testTask.TaskCalled);
            Assert.AreEqual(0, testTaskFactory._testTask.ValueTaskCalled);
        }

        [TestMethod]
        public async Task TestChannelConcurrencyWaitTimeoutWithConcurrency()
        {
            var testConfig = "testConfig";
            var configurableConcurrencyManager = new ConfigurableConcurrencyManager(testConfig, 100);

            // Replace IConcurrencyManager with a mock object
            var mockConcurrencyManager = new Mock<IConcurrencyManager>();
            mockConcurrencyManager.Setup(
                x => x.AcquireResourceAsync(
                    It.IsAny<int>(),
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(false);

            // Set _concurrencyManager with MockConcurrencyManager
            FieldInfo fieldInfo = typeof(ConfigurableConcurrencyManager).GetField("_concurrencyManager", BindingFlags.Instance | BindingFlags.NonPublic);
            fieldInfo.SetValue(configurableConcurrencyManager, mockConcurrencyManager.Object);

            var testChannel = new TestTaskChannel("testChannel");
            configurableConcurrencyManager.RegisterObject(testChannel.SetExternalConcurrencyManager);

            var testTaskFactory = new TestSubTaskFactory(false, false);
            testChannel.AddSubTaskFactory(testTaskFactory);

            var abstractChannel = (AbstractConcurrentTaskChannelManager<TestEventTaskContext>)testChannel;

            var perTypeConcurrencyManager = PrivateFunctionAccessHelper.GetPrivateField(
                   typeof(AbstractConcurrentTaskChannelManager<TestEventTaskContext>), "_perTypeConcurrencyManager", abstractChannel);
            Assert.IsNull(perTypeConcurrencyManager);

            var externalConcurrencyManager = PrivateFunctionAccessHelper.GetPrivateField(
                   typeof(AbstractConcurrentTaskChannelManager<TestEventTaskContext>), "_externalConcurrencyManager", abstractChannel);
            Assert.IsNotNull(externalConcurrencyManager);

            var parentContext = Tracer.CreateNewActivityContext();
            var testTaskContext = new TestEventTaskContext(parentContext, true, 0);
            var testScenario = "TestScenario";
            testTaskContext.Scenario = testScenario;

            await testTaskContext.StartEventTaskAsync(testChannel, true, null).ConfigureAwait(false);

            // Task should be moved to Retry
            Assert.AreEqual(1, testTaskContext.MovingToRetryCalledCount);
            Assert.AreEqual(0, testTaskContext.MovingToPoisonCalledCount);

            Assert.AreEqual(testTaskContext.RetryChannel, testTaskContext.CurrentTaskChannel);
            Assert.AreEqual(0, testChannel.BeforeProcessCalled);
            Assert.AreEqual(0, testChannel.TaskNotMovedCalled);
            Assert.AreEqual(0, testChannel.ProcessErrorCalled);
            Assert.AreEqual(0, testChannel.testErrorTaskChannel.BeforeProcessCalled);
            Assert.AreEqual(0, testChannel.testErrorTaskChannel.TaskNotMovedCalled);
            Assert.AreEqual(0, testChannel.testErrorTaskChannel.ProcessErrorCalled);

            Assert.AreEqual(1, testTaskContext.RetryChannel.BeforeProcessCount);
            Assert.AreEqual(1, testTaskContext.RetryChannel.ProcessNotMovedCount);
            Assert.AreEqual(0, testTaskContext.RetryChannel.ProcessErrorCount);

            Assert.IsNull(testChannel.BeforeProcessIActivity?.Scenario);
            Assert.IsNull(testChannel.TaskNotMovedIActivity?.Scenario);
            Assert.IsNull(testChannel.ProcessErrorIActivity?.Scenario);

            Assert.IsNull(testChannel.testErrorTaskChannel.BeforeProcessIActivity?.Scenario);
            Assert.IsNull(testChannel.testErrorTaskChannel.TaskNotMovedIActivity?.Scenario);
            Assert.IsNull(testChannel.testErrorTaskChannel.ProcessErrorIActivity?.Scenario);

            var beforeActivityWrapper = testChannel.BeforeCurrentActivityWrapper;
            Assert.IsNull(beforeActivityWrapper);
            Assert.IsNull(testChannel.ErrorCurrentActivityWrapper);

            Assert.IsNull(testChannel.testErrorTaskChannel.BeforeCurrentActivityWrapper);
            Assert.IsNull(testChannel.testErrorTaskChannel.TaskNotMovedCurrentActivityWrapper);
            Assert.IsNull(testChannel.testErrorTaskChannel.ErrorCurrentActivityWrapper);

            Assert.IsNull(OpenTelemetryActivityWrapper.Current);
            Assert.IsNull(testTaskFactory._testTask.CurrentActivityWrapper);
            Assert.IsNull(testTaskFactory._testTask.CurrentActivity);
            Assert.AreNotEqual(testTaskContext.EventTaskActivity.Context.TraceId, parentContext.TraceId); // new trace id

            var activityInstance = (Activity)PrivateFunctionAccessHelper.GetPrivateField(
                   typeof(OpenTelemetryActivityWrapper), "_activity", testTaskContext.EventTaskActivity);
            Assert.AreEqual(activityInstance.ParentSpanId, parentContext.SpanId);

            Assert.AreEqual(0, testTaskFactory._testTask.TaskCalled);
            Assert.AreEqual(0, testTaskFactory._testTask.ValueTaskCalled);
        }

        [TestMethod]
        public async Task ValueTestWithConcurrency()
        {
            var testConfig = "testConfig";
            var configurableConcurrencyManager = new ConfigurableConcurrencyManager(testConfig, 100);

            var testChannel = new TestTaskChannel("testChannel");
            configurableConcurrencyManager.RegisterObject(testChannel.SetExternalConcurrencyManager);

            var testTaskFactory = new TestSubTaskFactory(true, false);
            testChannel.AddSubTaskFactory(testTaskFactory);

            var abstractChannel = (AbstractConcurrentTaskChannelManager<TestEventTaskContext>)testChannel;

            var perTypeConcurrencyManager = PrivateFunctionAccessHelper.GetPrivateField(
                   typeof(AbstractConcurrentTaskChannelManager<TestEventTaskContext>), "_perTypeConcurrencyManager", abstractChannel);
            Assert.IsNull(perTypeConcurrencyManager);

            var externalConcurrencyManager = PrivateFunctionAccessHelper.GetPrivateField(
                   typeof(AbstractConcurrentTaskChannelManager<TestEventTaskContext>), "_externalConcurrencyManager", abstractChannel);
            Assert.IsNotNull(externalConcurrencyManager);

            var parentContext = Tracer.CreateNewActivityContext();
            var testTaskContext = new TestEventTaskContext(parentContext, false, 0);
            var testScenario = "TestScenario";
            testTaskContext.Scenario = testScenario;

            await testTaskContext.StartEventTaskAsync(testChannel, true, null).ConfigureAwait(false);

            Assert.AreEqual(testChannel.ChannelName, testTaskContext.CurrentTaskChannel.ChannelName);
            Assert.AreEqual(1, testChannel.BeforeProcessCalled);
            Assert.AreEqual(1, testChannel.TaskNotMovedCalled);
            Assert.AreEqual(0, testChannel.ProcessErrorCalled);
            Assert.AreEqual(0, testChannel.testErrorTaskChannel.BeforeProcessCalled);
            Assert.AreEqual(0, testChannel.testErrorTaskChannel.TaskNotMovedCalled);
            Assert.AreEqual(0, testChannel.testErrorTaskChannel.ProcessErrorCalled);

            Assert.AreEqual(testScenario, testChannel.BeforeProcessIActivity?.Scenario);
            Assert.AreEqual(testScenario, testChannel.TaskNotMovedIActivity?.Scenario);
            Assert.AreEqual(null, testChannel.ProcessErrorIActivity?.Scenario);

            Assert.AreEqual(null, testChannel.testErrorTaskChannel.BeforeProcessIActivity?.Scenario);
            Assert.AreEqual(null, testChannel.testErrorTaskChannel.TaskNotMovedIActivity?.Scenario);
            Assert.AreEqual(null, testChannel.testErrorTaskChannel.ProcessErrorIActivity?.Scenario);

            var beforeActivityWrapper = testChannel.BeforeCurrentActivityWrapper;
            Assert.IsNotNull(beforeActivityWrapper);
            Assert.AreEqual(testTaskContext.EventTaskActivity, beforeActivityWrapper);
            Assert.AreEqual(beforeActivityWrapper, testChannel.TaskNotMovedCurrentActivityWrapper);
            Assert.AreEqual(null, testChannel.ErrorCurrentActivityWrapper);

            Assert.AreEqual(null, testChannel.testErrorTaskChannel.BeforeCurrentActivityWrapper);
            Assert.AreEqual(null, testChannel.testErrorTaskChannel.TaskNotMovedCurrentActivityWrapper);
            Assert.AreEqual(null, testChannel.testErrorTaskChannel.ErrorCurrentActivityWrapper);

            Assert.AreEqual(null, OpenTelemetryActivityWrapper.Current);
            Assert.AreEqual(testTaskContext.EventTaskActivity, testTaskFactory._testTask.CurrentActivityWrapper);
            Assert.AreEqual(testTaskContext.EventTaskActivity.Context.TraceId, parentContext.TraceId); // same trace id
            Assert.AreEqual(testTaskContext.EventTaskActivity.Context, testTaskFactory._testTask.CurrentActivity.Context);

            var activityInstance = (Activity)PrivateFunctionAccessHelper.GetPrivateField(
                typeof(OpenTelemetryActivityWrapper), "_activity", testTaskContext.EventTaskActivity);
            Assert.AreEqual(activityInstance.ParentSpanId, parentContext.SpanId);

            Assert.AreEqual(0, testTaskFactory._testTask.TaskCalled);
            Assert.AreEqual(1, testTaskFactory._testTask.ValueTaskCalled);
        }

        [TestMethod]
        public async Task ErrorTestWithConcurrency()
        {
            var testConfig = "testConfig";
            var configurableConcurrencyManager = new ConfigurableConcurrencyManager(testConfig, 100);

            var testChannel = new TestTaskChannel("testChannel");
            configurableConcurrencyManager.RegisterObject(testChannel.SetExternalConcurrencyManager);

            var testTaskFactory = new TestSubTaskFactory(true, true);
            testChannel.AddSubTaskFactory(testTaskFactory);

            var abstractChannel = (AbstractConcurrentTaskChannelManager<TestEventTaskContext>)testChannel;

            var perTypeConcurrencyManager = PrivateFunctionAccessHelper.GetPrivateField(
                   typeof(AbstractConcurrentTaskChannelManager<TestEventTaskContext>), "_perTypeConcurrencyManager", abstractChannel);
            Assert.IsNull(perTypeConcurrencyManager);

            var externalConcurrencyManager = PrivateFunctionAccessHelper.GetPrivateField(
                   typeof(AbstractConcurrentTaskChannelManager<TestEventTaskContext>), "_externalConcurrencyManager", abstractChannel);
            Assert.IsNotNull(externalConcurrencyManager);

            var parentContext = Tracer.CreateNewActivityContext();
            var testTaskContext = new TestEventTaskContext(default, true, 0);
            var testScenario = "TestScenario";
            testTaskContext.Scenario = testScenario;

            await testTaskContext.StartEventTaskAsync(testChannel, true, null).ConfigureAwait(false);

            Assert.AreEqual(testChannel.testErrorTaskChannel.ChannelName, testTaskContext.CurrentTaskChannel.ChannelName);
            Assert.AreEqual(1, testChannel.BeforeProcessCalled);
            Assert.AreEqual(0, testChannel.TaskNotMovedCalled);
            Assert.AreEqual(1, testChannel.ProcessErrorCalled);

            Assert.AreEqual(1, testChannel.testErrorTaskChannel.BeforeProcessCalled);
            Assert.AreEqual(1, testChannel.testErrorTaskChannel.TaskNotMovedCalled);
            Assert.AreEqual(0, testChannel.testErrorTaskChannel.ProcessErrorCalled);

            Assert.AreEqual(testScenario, testChannel.BeforeProcessIActivity?.Scenario);
            Assert.AreEqual(null, testChannel.TaskNotMovedIActivity?.Scenario);
            Assert.AreEqual(testScenario, testChannel.ProcessErrorIActivity?.Scenario);

            Assert.AreEqual(testScenario, testChannel.testErrorTaskChannel.BeforeProcessIActivity?.Scenario);
            Assert.AreEqual(testScenario, testChannel.testErrorTaskChannel.TaskNotMovedIActivity?.Scenario);
            Assert.AreEqual(null, testChannel.testErrorTaskChannel.ProcessErrorIActivity?.Scenario);

            var beforeActivityWrapper = testChannel.BeforeCurrentActivityWrapper;
            Assert.IsNotNull(beforeActivityWrapper);
            Assert.AreEqual(testTaskContext.EventTaskActivity, beforeActivityWrapper);
            Assert.AreEqual(null, testChannel.TaskNotMovedCurrentActivityWrapper);
            Assert.AreEqual(beforeActivityWrapper, testChannel.ErrorCurrentActivityWrapper);

            Assert.AreEqual(beforeActivityWrapper, testChannel.testErrorTaskChannel.BeforeCurrentActivityWrapper);
            Assert.AreEqual(beforeActivityWrapper, testChannel.testErrorTaskChannel.TaskNotMovedCurrentActivityWrapper);
            Assert.AreEqual(null, testChannel.testErrorTaskChannel.ErrorCurrentActivityWrapper);

            Assert.AreEqual(null, OpenTelemetryActivityWrapper.Current);
            Assert.AreEqual(testTaskContext.EventTaskActivity, testTaskFactory._testTask.CurrentActivityWrapper);
            Assert.AreEqual(testTaskContext.EventTaskActivity.Context, testTaskFactory._testTask.CurrentActivity.Context);
            Assert.AreNotEqual(testTaskContext.EventTaskActivity.Context.TraceId, parentContext.TraceId);
            Assert.AreNotEqual(testTaskContext.EventTaskActivity.Context.SpanId, parentContext.SpanId);
            Assert.AreEqual(0, testTaskFactory._testTask.TaskCalled);
            Assert.AreEqual(1, testTaskFactory._testTask.ValueTaskCalled);
        }

        [TestMethod]
        public async Task TestRecursiveAddToSameChannel()
        {
            var testConfig = "testConfig";
            var configurableConcurrencyManager = new ConfigurableConcurrencyManager(testConfig, 100);

            var testChannel = new TestTaskChannel("testChannel");
            configurableConcurrencyManager.RegisterObject(testChannel.SetExternalConcurrencyManager);

            var testTaskFactory = new TestSubTaskFactory(false, false, testChannel);
            testChannel.AddSubTaskFactory(testTaskFactory);

            var abstractChannel = (AbstractConcurrentTaskChannelManager<TestEventTaskContext>)testChannel;

            var perTypeConcurrencyManager = PrivateFunctionAccessHelper.GetPrivateField(
                   typeof(AbstractConcurrentTaskChannelManager<TestEventTaskContext>), "_perTypeConcurrencyManager", abstractChannel);
            Assert.IsNull(perTypeConcurrencyManager);

            var externalConcurrencyManager = PrivateFunctionAccessHelper.GetPrivateField(
                   typeof(AbstractConcurrentTaskChannelManager<TestEventTaskContext>), "_externalConcurrencyManager", abstractChannel);
            Assert.IsNotNull(externalConcurrencyManager);

            var testTaskContext = new TestEventTaskContext(default, true, 0);
            var testScenario = "TestScenario";
            testTaskContext.Scenario = testScenario;

            await testTaskContext.StartEventTaskAsync(testChannel, true, null).ConfigureAwait(false);

            Assert.AreEqual(testChannel.testErrorTaskChannel.ChannelName, testTaskContext.CurrentTaskChannel.ChannelName);
            Assert.AreEqual(1, testChannel.BeforeProcessCalled);
            Assert.AreEqual(0, testChannel.TaskNotMovedCalled);
            Assert.AreEqual(1, testChannel.ProcessErrorCalled);

            Assert.AreEqual(1, testChannel.testErrorTaskChannel.BeforeProcessCalled);
            Assert.AreEqual(1, testChannel.testErrorTaskChannel.TaskNotMovedCalled);
            Assert.AreEqual(0, testChannel.testErrorTaskChannel.ProcessErrorCalled);

            Assert.AreEqual(testScenario, testChannel.BeforeProcessIActivity?.Scenario);
            Assert.AreEqual(null, testChannel.TaskNotMovedIActivity?.Scenario);
            Assert.AreEqual(testScenario, testChannel.ProcessErrorIActivity?.Scenario);

            Assert.AreEqual(testScenario, testChannel.testErrorTaskChannel.BeforeProcessIActivity?.Scenario);
            Assert.AreEqual(testScenario, testChannel.testErrorTaskChannel.TaskNotMovedIActivity?.Scenario);
            Assert.AreEqual(null, testChannel.testErrorTaskChannel.ProcessErrorIActivity?.Scenario);

            var beforeActivityWrapper = testChannel.BeforeCurrentActivityWrapper;
            Assert.IsNotNull(beforeActivityWrapper);
            Assert.AreEqual(testTaskContext.EventTaskActivity, beforeActivityWrapper);
            Assert.AreEqual(null, testChannel.TaskNotMovedCurrentActivityWrapper);
            Assert.AreEqual(beforeActivityWrapper, testChannel.ErrorCurrentActivityWrapper);

            Assert.AreEqual(beforeActivityWrapper, testChannel.testErrorTaskChannel.BeforeCurrentActivityWrapper);
            Assert.AreEqual(beforeActivityWrapper, testChannel.testErrorTaskChannel.TaskNotMovedCurrentActivityWrapper);
            Assert.AreEqual(null, testChannel.testErrorTaskChannel.ErrorCurrentActivityWrapper);

            Assert.AreEqual(testScenario, testTaskFactory._testTask.DoTaskWork1IActivity.Scenario);
            Assert.AreEqual(testScenario, testTaskFactory._testTask.DoTaskWork2IActivity.Scenario);

            Assert.AreEqual(testChannel.ChannelName, testChannel.BeforeProcessIActivity.Component);
            Assert.AreEqual(testChannel.ChannelName, testChannel.ProcessErrorIActivity.Component);
            Assert.AreEqual(testChannel.ChannelName, testTaskFactory._testTask.DoTaskWork1IActivity.Component);
            Assert.AreEqual(testChannel.ChannelName, testTaskFactory._testTask.DoTaskWork2IActivity.Component);

            Assert.AreEqual(1, testTaskFactory._testTask.TaskCalled);
            Assert.AreEqual(true, testTaskFactory._testTask.DoTaskWork1Called);
            Assert.AreEqual(true, testTaskFactory._testTask.DoTaskWork2Called);
            Assert.AreEqual(true, testTaskFactory._testTask.DoTaskWork2MovedToOtherChannel);
            Assert.AreEqual(0, testTaskFactory._testTask.ValueTaskCalled);
            Assert.IsTrue(testChannel.ProcessErrorException.ToString().Contains("Adding to the same Channel"));
        }

        [TestMethod]
        public async Task TestMoveToNextChannelWithoutConcurrency()
        {
            var testChannel = new TestTaskChannel("testChannel");
            var testChannel2 = new TestTaskChannel("TestChannel2");
            var testTaskFactory = new TestSubTaskFactory(false, false, testChannel2);
            testChannel.AddSubTaskFactory(testTaskFactory);

            var parentContext = Tracer.CreateNewActivityContext();
            var testTaskContext = new TestEventTaskContext(parentContext, true, 0);
            var testScenario = "TestScenario";
            testTaskContext.Scenario = testScenario;

            await testChannel.ExecuteEventTaskContextAsync(testTaskContext).ConfigureAwait(false);

            Assert.AreEqual(testChannel.ChannelName, testTaskContext.PrevTaskChannel.ChannelName);
            Assert.AreEqual(testChannel2.ChannelName, testTaskContext.CurrentTaskChannel.ChannelName);
            Assert.AreEqual(null, testTaskContext.NextTaskChannel);

            Assert.AreEqual(1, testChannel.BeforeProcessCalled);
            Assert.AreEqual(0, testChannel.TaskNotMovedCalled);
            Assert.AreEqual(0, testChannel.ProcessErrorCalled);
            Assert.AreEqual(testScenario, testChannel.BeforeProcessIActivity?.Scenario);
            Assert.AreEqual(null, testChannel.TaskNotMovedIActivity?.Scenario);
            Assert.AreEqual(null, testChannel.ProcessErrorIActivity?.Scenario);

            Assert.IsNotNull(testTaskFactory.channel2);
            Assert.AreEqual(1, testChannel2.BeforeProcessCalled);
            Assert.AreEqual(1, testChannel2.TaskNotMovedCalled);
            Assert.AreEqual(0, testChannel2.ProcessErrorCalled);
            Assert.AreEqual(testScenario, testChannel2.BeforeProcessIActivity?.Scenario);
            Assert.AreEqual(testScenario, testChannel2.TaskNotMovedIActivity?.Scenario);
            Assert.AreEqual(null, testChannel2.ProcessErrorIActivity?.Scenario);

            var beforeActivityWrapper = testChannel.BeforeCurrentActivityWrapper;
            Assert.IsNotNull(beforeActivityWrapper);
            Assert.AreEqual(testTaskContext.EventTaskActivity, beforeActivityWrapper);
            Assert.AreEqual(null, testChannel.TaskNotMovedCurrentActivityWrapper);
            Assert.AreEqual(null, testChannel.ErrorCurrentActivityWrapper);

            Assert.IsNotNull(testChannel2.BeforeCurrentActivityWrapper);
            Assert.AreEqual(testTaskContext.EventTaskActivity, testChannel2.BeforeCurrentActivityWrapper);
            Assert.AreEqual(testTaskContext.EventTaskActivity, testChannel2.TaskNotMovedCurrentActivityWrapper);
            Assert.AreEqual(null, testChannel2.ErrorCurrentActivityWrapper);

            Assert.AreEqual(null, OpenTelemetryActivityWrapper.Current);
            Assert.AreEqual(testTaskContext.EventTaskActivity, testTaskFactory._testTask.CurrentActivityWrapper);
            Assert.AreEqual(testTaskContext.EventTaskActivity.Context, testTaskFactory._testTask.CurrentActivity.Context);
            Assert.AreNotEqual(testTaskContext.EventTaskActivity.Context.TraceId, parentContext.TraceId);

            var activityInstance = (Activity)PrivateFunctionAccessHelper.GetPrivateField(
                   typeof(OpenTelemetryActivityWrapper), "_activity", testTaskContext.EventTaskActivity);
            Assert.AreEqual(activityInstance.ParentSpanId, parentContext.SpanId);

            Assert.AreEqual(1, testTaskFactory._testTask.TaskCalled);
            Assert.AreEqual(0, testTaskFactory._testTask.ValueTaskCalled);
        }

        [TestMethod]
        public async Task TestNotMoveToNextChannelWithoutConcurrency()
        {
            var testChannel2 = new TestTaskChannel("TestChannel2");
            var testChannel = new TestTaskChannel("TestChannel1", testChannel2);

            var parentContext = Tracer.CreateNewActivityContext();
            var testTaskContext = new TestEventTaskContext(parentContext, true, 0);
            var testScenario = "TestScenario";
            testTaskContext.Scenario = testScenario;

            await testChannel.ExecuteEventTaskContextAsync(testTaskContext).ConfigureAwait(false);

            Assert.AreEqual(testChannel.ChannelName, testTaskContext.PrevTaskChannel.ChannelName);
            Assert.AreEqual(testChannel2.ChannelName, testTaskContext.CurrentTaskChannel.ChannelName);
            Assert.AreEqual(null, testTaskContext.NextTaskChannel);

            Assert.AreEqual(1, testChannel.BeforeProcessCalled);
            Assert.AreEqual(1, testChannel.TaskNotMovedCalled);
            Assert.AreEqual(0, testChannel.ProcessErrorCalled);
            Assert.AreEqual(testScenario, testChannel.BeforeProcessIActivity?.Scenario);
            //Assert.AreEqual(testScenario, testChannel.TaskNotMovedIActivity?.Scenario);
            Assert.AreEqual(null, testChannel.ProcessErrorIActivity?.Scenario);

            Assert.AreEqual(1, testChannel2.BeforeProcessCalled);
            Assert.AreEqual(1, testChannel2.TaskNotMovedCalled);
            Assert.AreEqual(0, testChannel2.ProcessErrorCalled);
            Assert.AreEqual(testScenario, testChannel2.BeforeProcessIActivity?.Scenario);
            Assert.AreEqual(testScenario, testChannel2.TaskNotMovedIActivity?.Scenario);
            Assert.AreEqual(null, testChannel2.ProcessErrorIActivity?.Scenario);

            var beforeActivityWrapper = testChannel.BeforeCurrentActivityWrapper;
            Assert.IsNotNull(beforeActivityWrapper);
            Assert.AreEqual(testTaskContext.EventTaskActivity, beforeActivityWrapper);
            Assert.AreEqual(testTaskContext.EventTaskActivity, testChannel.TaskNotMovedCurrentActivityWrapper);
            Assert.AreEqual(null, testChannel.ErrorCurrentActivityWrapper);

            Assert.IsNotNull(testChannel2.BeforeCurrentActivityWrapper);
            Assert.AreEqual(testTaskContext.EventTaskActivity, testChannel2.BeforeCurrentActivityWrapper);
            Assert.AreEqual(testTaskContext.EventTaskActivity, testChannel2.TaskNotMovedCurrentActivityWrapper);
            Assert.AreEqual(null, testChannel2.ErrorCurrentActivityWrapper);

            Assert.AreNotEqual(testTaskContext.EventTaskActivity.Context.TraceId, parentContext.TraceId);

            var activityInstance = (Activity)PrivateFunctionAccessHelper.GetPrivateField(
                   typeof(OpenTelemetryActivityWrapper), "_activity", testTaskContext.EventTaskActivity);
            Assert.AreEqual(activityInstance.ParentSpanId, parentContext.SpanId);
        }

        [TestMethod]
        public void TestEventTaskContextTimeOut()
        {
            var testTaskContext = new TestEventTaskContext(default, true, 10);
            Thread.Sleep(100);
            Assert.AreEqual(false, testTaskContext.Disposed);
            Assert.AreEqual(true, testTaskContext.TimeOutExpired);
        }

        [TestMethod]
        public void TestEventTaskContextTimeOutDispose()
        {
            var testTaskContext = new TestEventTaskContext(default, true, 10);
            testTaskContext.Dispose();
            Thread.Sleep(100);
            Assert.AreEqual(true, testTaskContext.Disposed);
            Assert.AreEqual(false, testTaskContext.TimeOutExpired);
        }

        [TestMethod]
        public async Task TestBufferedTaskChannelWithSubTaskFactory()
        {
            var testChannel = new TestBufferedTaskChannel("TestBufferedChannel");
            var testTaskFactory = new TestSubTaskFactory(false, false);
            testChannel.AddSubTaskFactory(testTaskFactory);

            var testTaskContext = new TestEventTaskContext(default, true, 0);
            var testScenario = "TestScenario";
            testTaskContext.Scenario = testScenario;

            await testTaskContext.StartEventTaskAsync(testChannel, true, null).ConfigureAwait(false);
            
            Assert.AreEqual(testChannel.ChannelName, testTaskContext.CurrentTaskChannel.ChannelName);
            Assert.AreEqual(1, testChannel.BeforeProcessCalled);
            Assert.AreEqual(1, testChannel.TaskNotMovedCalled);
            Assert.AreEqual(0, testChannel.ProcessErrorCalled);
            Assert.AreEqual(0, testChannel.testErrorTaskChannel.BeforeProcessCalled);
            Assert.AreEqual(0, testChannel.testErrorTaskChannel.TaskNotMovedCalled);
            Assert.AreEqual(0, testChannel.testErrorTaskChannel.ProcessErrorCalled);

            Assert.AreEqual(testScenario, testChannel.BeforeProcessIActivity?.Scenario);
            Assert.AreEqual(testScenario, testChannel.TaskNotMovedIActivity?.Scenario);
            Assert.AreEqual(null, testChannel.ProcessErrorIActivity?.Scenario);

            Assert.AreEqual(testTaskContext.EventTaskActivity, testChannel.BeforeCurrentActivityWrapper);
            Assert.AreEqual(testTaskContext.EventTaskActivity, testChannel.TaskNotMovedCurrentActivityWrapper);
            Assert.AreEqual(null, testChannel.ErrorCurrentActivityWrapper);

            Assert.AreEqual(null, OpenTelemetryActivityWrapper.Current);
            Assert.AreEqual(testTaskContext.EventTaskActivity, testTaskFactory._testTask.CurrentActivityWrapper);
            Assert.AreEqual(testTaskContext.EventTaskActivity.Context, testTaskFactory._testTask.CurrentActivity.Context);
            Assert.AreEqual(testTaskContext.EventTaskActivity, testTaskFactory._testTask.AfterActivityWrapper);
            Assert.AreEqual(testTaskContext.EventTaskActivity.Context, testTaskFactory._testTask.AfterActivity.Context);
            Assert.AreEqual(testTaskContext.EventTaskActivity, testTaskFactory._testTask.DoTaskWork1CurrentActivityWrapper);
            Assert.AreEqual(testTaskContext.EventTaskActivity.Context, testTaskFactory._testTask.DoTaskWork1CurrentActivity.Context);
            Assert.AreEqual(testTaskContext.EventTaskActivity, testTaskFactory._testTask.DoTaskWork1AfterActivityWrapper);
            Assert.AreEqual(testTaskContext.EventTaskActivity.Context, testTaskFactory._testTask.DoTaskWork1AfterActivity.Context);
            Assert.AreEqual(testTaskContext.EventTaskActivity, testTaskFactory._testTask.DoTaskWork2CurrentActivityWrapper);
            Assert.AreEqual(testTaskContext.EventTaskActivity.Context, testTaskFactory._testTask.DoTaskWork2CurrentActivity.Context);
            Assert.AreEqual(1, testTaskFactory._testTask.TaskCalled);
            Assert.AreEqual(true, testTaskFactory._testTask.DoTaskWork1Called);
            Assert.AreEqual(true, testTaskFactory._testTask.DoTaskWork2Called);
            Assert.AreEqual(0, testTaskFactory._testTask.ValueTaskCalled);
        }

        [TestMethod]
        public async Task TestBufferedTaskChannelWithBufferedTaskProcessorFactory()
        {
            var testChannel = new TestBufferedTaskChannel("TestBufferedChannel");
            var bufferedTaskProcessorFactory = new TestBufferedTaskProcessorFactory();
            testChannel.SetBufferedTaskProcessorFactory(bufferedTaskProcessorFactory);
            
            var testTaskContext = new TestEventTaskContext(default, true, 0);
            var testScenario = "TestScenario";
            testTaskContext.Scenario = testScenario;

            await testTaskContext.StartEventTaskAsync(testChannel, true, null).ConfigureAwait(false);

            Assert.AreEqual(testChannel.ChannelName, testTaskContext.CurrentTaskChannel.ChannelName);
            Assert.AreEqual(1, bufferedTaskProcessorFactory.NumTask);
            Assert.AreEqual(1, testChannel.BeforeProcessCalled);
            Assert.AreEqual(1, testChannel.TaskNotMovedCalled);
            Assert.AreEqual(0, testChannel.ProcessErrorCalled);
            Assert.AreEqual(0, testChannel.testErrorTaskChannel.BeforeProcessCalled);
            Assert.AreEqual(0, testChannel.testErrorTaskChannel.TaskNotMovedCalled);
            Assert.AreEqual(0, testChannel.testErrorTaskChannel.ProcessErrorCalled);

            Assert.AreEqual(testScenario, testChannel.BeforeProcessIActivity?.Scenario);
            Assert.AreEqual(testScenario, testChannel.TaskNotMovedIActivity?.Scenario);
            Assert.AreEqual(null, testChannel.ProcessErrorIActivity?.Scenario);

            Assert.AreEqual(testTaskContext.EventTaskActivity, testChannel.BeforeCurrentActivityWrapper);
            Assert.AreEqual(testTaskContext.EventTaskActivity, testChannel.TaskNotMovedCurrentActivityWrapper);
            Assert.AreEqual(null, testChannel.ErrorCurrentActivityWrapper);
            Assert.AreEqual(null, OpenTelemetryActivityWrapper.Current);
        }
    }
}


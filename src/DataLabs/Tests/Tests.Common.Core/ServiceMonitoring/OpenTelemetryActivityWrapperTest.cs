namespace Microsoft.WindowsAzure.Governance.DataLabs.Tests.Common.Core.ServiceMonitoring
{
    using Microsoft.Extensions.Configuration;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.ConfigMap;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.ServiceMonitoring;
    using System.Diagnostics;

    [TestClass]
    public class OpenTelemetryActivityWrapperTest
    {
        public static readonly ActivitySource TestActivitySource = new("TestActivitySource");

        private OpenTelemetryActivityWrapper testBeforeActivityWrapper;
        private OpenTelemetryActivityWrapper testAfterActivityWrapper;
        private OpenTelemetryActivityWrapper testAnotherBeforeActivityWrapper;
        private OpenTelemetryActivityWrapper testAnotherAfterActivityWrapper;

        private Activity testBeforeActivity;
        private Activity testAfterActivity;
        private Activity testAnotherBeforeActivity;
        private Activity testAnotherAfterActivity;

        [TestInitialize]
        public void TestInitialize()
        {
            var config = new ConfigurationBuilder().AddInMemoryCollection();
            ConfigMapUtil.Initialize(config, false);
            Tracer.CreateDataLabsTracerProvider("TestActivitySource");
        }

        [TestCleanup]
        public void TestCleanup()
        {
            ConfigMapUtil.Reset();
        }

        [TestMethod]
        public async Task TestExplicitCurrentActivityAsync()
        {

            var activityWrapper1 = new OpenTelemetryActivityWrapper(TestActivitySource, "test1", ActivityKind.Internal, default, true, default);
            var activity1 = GetActivity(activityWrapper1);

            Assert.AreEqual(null, OpenTelemetryActivityWrapper.Current);
            Assert.AreEqual(activity1, Activity.Current);

            await Method1Async(activityWrapper1, true).ConfigureAwait(false);

            Assert.AreEqual(activityWrapper1, testBeforeActivityWrapper);
            Assert.AreEqual(activity1, testBeforeActivity);
            Assert.AreEqual(activityWrapper1, testAfterActivityWrapper);
            Assert.AreEqual(activity1, testAfterActivity);

            Assert.AreEqual(activityWrapper1, testAnotherBeforeActivityWrapper);
            Assert.AreEqual(activity1, testAnotherBeforeActivity);
            Assert.AreEqual(null, testAnotherAfterActivityWrapper);
            Assert.AreEqual(null, testAnotherAfterActivity);
        }

        [TestMethod]
        public async Task TestImplicitCurrentActivityAsync()
        {

            var activityWrapper1 = new OpenTelemetryActivityWrapper(TestActivitySource, "test1", ActivityKind.Internal, default, true, default);
            var activity1 = GetActivity(activityWrapper1);

            Assert.AreEqual(null, OpenTelemetryActivityWrapper.Current);
            Assert.AreEqual(activity1, Activity.Current);

            await Method1Async(activityWrapper1, false).ConfigureAwait(false);

            Assert.AreEqual(activityWrapper1, testBeforeActivityWrapper);
            Assert.AreEqual(activity1, testBeforeActivity);
            Assert.AreEqual(activityWrapper1, testAfterActivityWrapper);
            Assert.AreEqual(activity1, testAfterActivity);

            Assert.AreEqual(activityWrapper1, testAnotherBeforeActivityWrapper);
            Assert.AreEqual(activity1, testAnotherBeforeActivity);
            Assert.AreEqual(null, testAnotherAfterActivityWrapper);
            Assert.AreEqual(null, testAnotherAfterActivity);
        }

        private Activity GetActivity(OpenTelemetryActivityWrapper activityWrapper)
        {
            return (Activity)PrivateFunctionAccessHelper.GetPrivateField(
                               typeof(OpenTelemetryActivityWrapper),
                               "_activity", activityWrapper);
        }

        private async Task Method1Async(OpenTelemetryActivityWrapper activityWrapper, bool explicitSet)
        {
            OpenTelemetryActivityWrapper.Current = activityWrapper;
            testBeforeActivityWrapper = OpenTelemetryActivityWrapper.Current;
            testBeforeActivity = Activity.Current;

            if (explicitSet)
            {
                await Method2AsyncWithSet(activityWrapper).ConfigureAwait(false);
            }else
            {
                await Method2AsyncWithoutSet(activityWrapper).ConfigureAwait(false);
            }

            testAfterActivityWrapper = OpenTelemetryActivityWrapper.Current;
            testAfterActivity = Activity.Current;
        }

        private async Task Method2AsyncWithSet(OpenTelemetryActivityWrapper activityWrapper)
        {
            OpenTelemetryActivityWrapper.Current = activityWrapper;
            testAnotherBeforeActivityWrapper = OpenTelemetryActivityWrapper.Current;
            testAnotherBeforeActivity = Activity.Current;

            await Task.Delay(10).ConfigureAwait(false);

            OpenTelemetryActivityWrapper.Current = null;
            testAnotherAfterActivityWrapper = OpenTelemetryActivityWrapper.Current;
            testAnotherAfterActivity = Activity.Current;
        }

        private async Task Method2AsyncWithoutSet(OpenTelemetryActivityWrapper activityWrapper)
        {
            testAnotherBeforeActivityWrapper = OpenTelemetryActivityWrapper.Current;
            testAnotherBeforeActivity = Activity.Current;

            await Task.Delay(10).ConfigureAwait(false);

            OpenTelemetryActivityWrapper.Current = null;
            testAnotherAfterActivityWrapper = OpenTelemetryActivityWrapper.Current;
            testAnotherAfterActivity = Activity.Current;
        }
    }
}

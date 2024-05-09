namespace Microsoft.WindowsAzure.Governance.DataLabs.Tests.Common.Core.Utils
{
    using Microsoft.Extensions.Configuration;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.ConfigMap;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.Utils;
    using System.Timers;

    [TestClass]
    public class ConfigurableTimerTests
    {
        private volatile int _counter1 = 0;
        private volatile int _counter2 = 0;

        [TestMethod]
        public void TestConfigurableSyncTimer1()
        {
            var configKey = "TestTimerKey";
            ConfigMapUtil.Reset();
            var appSettingsStub = new Dictionary<string, string>
            {
                { configKey, "00:00:01" }
            };
            var config = new ConfigurationBuilder()
                .AddInMemoryCollection(appSettingsStub);

            ConfigMapUtil.Initialize(config, false);
            var configurableTimer = new ConfigurableTimer(configKey, TimeSpan.FromMilliseconds(100));
            configurableTimer.Elapsed += TestTimerHandler;

            configurableTimer.Start();
            try
            {
                Assert.AreEqual(1 * 1000, (long)configurableTimer.Interval);

                Thread.Sleep(2 * 1000);
                Assert.AreEqual(10, _counter1);

                configurableTimer.UpdateInterval(TimeSpan.FromMilliseconds(200));
                Assert.AreEqual(200, (long)configurableTimer.Interval);

                _counter1 = 0;
                Thread.Sleep(1 * 1000);
                Assert.AreEqual(10, _counter1);
            }
            finally
            {
                configurableTimer.Stop();
            }
        }

        [TestMethod]
        public void TestConfigurableSyncTimer2()
        {
            var configKey = "TestTimerKey";
            ConfigMapUtil.Reset();
            var appSettingsStub = new Dictionary<string, string>
            {
                { configKey, "00:00:01" }
            };
            var config = new ConfigurationBuilder()
                .AddInMemoryCollection(appSettingsStub);

            ConfigMapUtil.Initialize(config, false);
            var configurableTimer = new ConfigurableTimer(configKey, TimeSpan.FromMilliseconds(100));
            configurableTimer.AddTimeEventHandlerSafely(TestTimerHandler);

            configurableTimer.Start();
            try
            {
                Assert.AreEqual(1 * 1000, (long)configurableTimer.Interval);

                Thread.Sleep(2 * 1000);
                Assert.AreEqual(10, _counter1);

                configurableTimer.UpdateInterval(TimeSpan.FromMilliseconds(200));
                Assert.AreEqual(200, (long)configurableTimer.Interval);

                _counter1 = 0;
                Thread.Sleep(1 * 1000);
                Assert.AreEqual(10, _counter1);
            }
            finally
            {
                configurableTimer.Stop();
            }
        }

        [TestMethod]
        public void TestConfigurableAsyncTimer1()
        {
            var configKey = "TestTimerKey";
            ConfigMapUtil.Reset();
            var appSettingsStub = new Dictionary<string, string>
            {
                { configKey, "00:00:01" }
            };
            var config = new ConfigurationBuilder()
                .AddInMemoryCollection(appSettingsStub);

            ConfigMapUtil.Initialize(config, false);
            var configurableTimer = new ConfigurableTimer(configKey, TimeSpan.FromMilliseconds(100));
            configurableTimer.Elapsed += async (sender, e) => await TestTimerHandlerAsync(sender, e);

            configurableTimer.Start();
            try
            {
                Assert.AreEqual(1 * 1000, (long)configurableTimer.Interval);

                Thread.Sleep(2 * 1000);
                Assert.AreEqual(20, _counter2);

                configurableTimer.UpdateInterval(TimeSpan.FromMilliseconds(200));
                Assert.AreEqual(200, (long)configurableTimer.Interval);

                _counter2 = 0;
                Thread.Sleep(1 * 1000);
                Assert.AreEqual(20, _counter2);
            }
            finally
            {
                configurableTimer.Stop();
            }
        }

        [TestMethod]
        public void TestConfigurableAsyncTimer2()
        {
            var configKey = "TestTimerKey";
            ConfigMapUtil.Reset();
            var appSettingsStub = new Dictionary<string, string>
            {
                { configKey, "00:00:01" }
            };
            var config = new ConfigurationBuilder()
                .AddInMemoryCollection(appSettingsStub);

            ConfigMapUtil.Initialize(config, false);
            var configurableTimer = new ConfigurableTimer(configKey, TimeSpan.FromMilliseconds(100));
            configurableTimer.AddTimeEventHandlerAsyncSafely(TestTimerHandlerAsync);

            configurableTimer.Start();
            try
            {
                Assert.AreEqual(1 * 1000, (long)configurableTimer.Interval);

                Thread.Sleep(2 * 1000);
                Assert.AreEqual(20, _counter2);

                configurableTimer.UpdateInterval(TimeSpan.FromMilliseconds(200));
                Assert.AreEqual(200, (long)configurableTimer.Interval);

                _counter2 = 0;
                Thread.Sleep(1 * 1000);
                Assert.AreEqual(20, _counter2);
            }
            finally
            {
                configurableTimer.Stop();
            }
        }

        [TestMethod]
        public void TestConfigurableTimerInvalidUpdate()
        {
            var configKey = "TestTimerKey";
            ConfigMapUtil.Reset();
            var appSettingsStub = new Dictionary<string, string>
            {
                { configKey, "00:00:01" }
            };
            var config = new ConfigurationBuilder()
                .AddInMemoryCollection(appSettingsStub);

            ConfigMapUtil.Initialize(config, false);
            var configurableTimer = new ConfigurableTimer(configKey, TimeSpan.FromMilliseconds(100));
            configurableTimer.Elapsed += TestTimerHandler;

            configurableTimer.Start();
            try
            {
                Assert.AreEqual(1 * 1000, (long)configurableTimer.Interval);

                Thread.Sleep(2 * 1000);
                Assert.AreEqual(10, _counter1);

                configurableTimer.UpdateInterval(TimeSpan.FromMilliseconds(0));
                Assert.AreEqual(1 * 1000, (long)configurableTimer.Interval);
            }
            finally
            {
                configurableTimer.Stop();
            }
        }

        private void TestTimerHandler(object? sender, ElapsedEventArgs e)
        {
            _counter1 = 10;
        }

        private async Task TestTimerHandlerAsync(object? sender, ElapsedEventArgs e)
        {
            await Task.Delay(1).ConfigureAwait(false);
            _counter2 = 20;
        }
    }
}

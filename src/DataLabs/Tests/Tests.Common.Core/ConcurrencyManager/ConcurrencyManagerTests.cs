namespace Microsoft.WindowsAzure.Governance.DataLabs.Tests.Common.Core.ConcurrencyManager
{
    using Microsoft.Extensions.Configuration;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.ConfigMap;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.ConcurrencyManager;


    [TestClass]
    public class TaskChannelTests
    {
        private static bool _isSetConcurrencyManager = false;
        private static IConcurrencyManager _concurrencyManager;

        [TestInitialize]
        public void TestInitialize()
        {
            var configBuilder = new ConfigurationBuilder().AddInMemoryCollection();
            ConfigMapUtil.Initialize(configBuilder, false);
        }

        [TestCleanup]
        public void TestCleanup()
        {
            ConfigMapUtil.Reset();
        }

        [TestMethod]
        public async Task SetConcurrencyCancellationTokenTest()
        {
            var concurrencyManager = new ConcurrencyManager("test", 100);
            Assert.AreEqual(100, concurrencyManager.MaxConcurrency);
            Assert.AreEqual(100, concurrencyManager.NumAvailables);
            Assert.AreEqual(0, concurrencyManager.NumRunning);

            await concurrencyManager.AcquireResourceAsync(CancellationToken.None).ConfigureAwait(false);
            Assert.AreEqual(100, concurrencyManager.MaxConcurrency);
            Assert.AreEqual(99, concurrencyManager.NumAvailables);
            Assert.AreEqual(1, concurrencyManager.NumRunning);

            concurrencyManager.ReleaseResource();
            Assert.AreEqual(100, concurrencyManager.MaxConcurrency);
            Assert.AreEqual(100, concurrencyManager.NumAvailables);
            Assert.AreEqual(0, concurrencyManager.NumRunning);

            var cancellationTokenSource = new CancellationTokenSource();
            var cancellationToken = cancellationTokenSource.Token;
            cancellationTokenSource.Cancel();

            Exception ex = null;
            bool hasAcquire = false;
            try
            {
                await concurrencyManager.AcquireResourceAsync(cancellationToken).ConfigureAwait(false);
                hasAcquire = true;
            }
            catch (Exception e)
            {
                ex = e;
            }finally
            {
                if (hasAcquire)
                {
                    concurrencyManager.ReleaseResource();
                }
            }

            Assert.IsNotNull(ex);
            Assert.IsNotNull(ex as OperationCanceledException);
            Assert.AreEqual(100, concurrencyManager.MaxConcurrency);
            Assert.AreEqual(100, concurrencyManager.NumAvailables);
            Assert.AreEqual(0, concurrencyManager.NumRunning);

            // Dispose
            concurrencyManager.Dispose();
        }

        [TestMethod]
        public void SetNewMaxConcurrencyTest()
        {
            var concurrencyManager = new ConcurrencyManager("test", 100);
            Assert.AreEqual(100, concurrencyManager.MaxConcurrency);
            Assert.AreEqual(100, concurrencyManager.NumAvailables);
            Assert.AreEqual(0, concurrencyManager.NumRunning);

            concurrencyManager.AcquireResourceAsync(CancellationToken.None);
            Assert.AreEqual(100, concurrencyManager.MaxConcurrency);
            Assert.AreEqual(99, concurrencyManager.NumAvailables);
            Assert.AreEqual(1, concurrencyManager.NumRunning);

            concurrencyManager.ReleaseResource();
            Assert.AreEqual(100, concurrencyManager.MaxConcurrency);
            Assert.AreEqual(100, concurrencyManager.NumAvailables);
            Assert.AreEqual(0, concurrencyManager.NumRunning);

            concurrencyManager.SetNewMaxConcurrencyAsync(200).GetAwaiter().GetResult();
            Assert.AreEqual(200, concurrencyManager.MaxConcurrency);
            Assert.AreEqual(200, concurrencyManager.NumAvailables);
            Assert.AreEqual(0, concurrencyManager.NumRunning);

            concurrencyManager.AcquireResourceAsync(CancellationToken.None);
            Assert.AreEqual(200, concurrencyManager.MaxConcurrency);
            Assert.AreEqual(199, concurrencyManager.NumAvailables);
            Assert.AreEqual(1, concurrencyManager.NumRunning);

            concurrencyManager.SetNewMaxConcurrencyAsync(50).GetAwaiter().GetResult();
            Assert.AreEqual(50, concurrencyManager.MaxConcurrency);
            Assert.AreEqual(49, concurrencyManager.NumAvailables);
            Assert.AreEqual(1, concurrencyManager.NumRunning);

            // Dispose
            concurrencyManager.Dispose();
        }

        [TestMethod]
        public void ConfigurableConcurrencyManagerTest1()
        {
            // 100 -> NO_CONCURRENCY

            var testConfig = "testConfig";
            var configurableConcurrencyManager = new ConfigurableConcurrencyManager(testConfig, 100);

            _isSetConcurrencyManager = false;
            _concurrencyManager = null;
            configurableConcurrencyManager.RegisterObject(SetConcurrencyManager);
            Assert.AreEqual(true, _isSetConcurrencyManager);
            Assert.IsNotNull(_concurrencyManager);

            Assert.AreEqual(100, _concurrencyManager.MaxConcurrency);
            Assert.AreEqual(100, _concurrencyManager.NumAvailables);
            Assert.AreEqual(0, _concurrencyManager.NumRunning);

            _concurrencyManager.AcquireResourceAsync(CancellationToken.None);
            Assert.AreEqual(100, _concurrencyManager.MaxConcurrency);
            Assert.AreEqual(99, _concurrencyManager.NumAvailables);
            Assert.AreEqual(1, _concurrencyManager.NumRunning);

            _concurrencyManager.ReleaseResource();
            Assert.AreEqual(100, _concurrencyManager.MaxConcurrency);
            Assert.AreEqual(100, _concurrencyManager.NumAvailables);
            Assert.AreEqual(0, _concurrencyManager.NumRunning);

            var oldConcurrencyManager = _concurrencyManager;

            // Update the value
            ConfigMapUtil.Configuration[testConfig] = "0";

            _isSetConcurrencyManager = false;
            _concurrencyManager = null;

            ConfigMapUtil.Configuration.CheckChangeAndCallBack(CancellationToken.None);
            Thread.Sleep(50);

            Assert.AreEqual(true, _isSetConcurrencyManager);
            Assert.IsNull(_concurrencyManager); // 0 -> NO_CONCURRENCY

            // OldConcurrencyManager should be disposed
            Assert.ThrowsException<ObjectDisposedException>(() => oldConcurrencyManager.ReleaseResource());

            // Dispose
            configurableConcurrencyManager.Dispose();
        }

        [TestMethod]
        public void ConfigurableConcurrencyManagerTest2()
        {
            // with NO_CONCURRENCY -> 100
            var testConfig = "testConfig";
            var configurableConcurrencyManager = new ConfigurableConcurrencyManager(testConfig, 0);

            _isSetConcurrencyManager = false;
            _concurrencyManager = null;
            configurableConcurrencyManager.RegisterObject(SetConcurrencyManager);
            Assert.AreEqual(true, _isSetConcurrencyManager);
            Assert.IsNull(_concurrencyManager);

            // Update the value
            ConfigMapUtil.Configuration[testConfig] = "100";

            _isSetConcurrencyManager = false;
            _concurrencyManager = null;

            ConfigMapUtil.Configuration.CheckChangeAndCallBack(CancellationToken.None);
            Thread.Sleep(50);

            Assert.AreEqual(true, _isSetConcurrencyManager);
            Assert.IsNotNull(_concurrencyManager);

            Assert.AreEqual(100, _concurrencyManager.MaxConcurrency);
            Assert.AreEqual(100, _concurrencyManager.NumAvailables);
            Assert.AreEqual(0, _concurrencyManager.NumRunning);

            _concurrencyManager.AcquireResourceAsync(CancellationToken.None);
            Assert.AreEqual(100, _concurrencyManager.MaxConcurrency);
            Assert.AreEqual(99, _concurrencyManager.NumAvailables);
            Assert.AreEqual(1, _concurrencyManager.NumRunning);

            _concurrencyManager.ReleaseResource();
            Assert.AreEqual(100, _concurrencyManager.MaxConcurrency);
            Assert.AreEqual(100, _concurrencyManager.NumAvailables);
            Assert.AreEqual(0, _concurrencyManager.NumRunning);

            // Dispose
            configurableConcurrencyManager.Dispose();
        }

        [TestMethod]
        public void ConfigurableConcurrencyManagerTest3()
        {
            // with 100 -> 200
            var testConfig = "testConfig";
            var configurableConcurrencyManager = new ConfigurableConcurrencyManager(testConfig, 100);

            _isSetConcurrencyManager = false;
            _concurrencyManager = null;
            configurableConcurrencyManager.RegisterObject(SetConcurrencyManager);
            Assert.AreEqual(true, _isSetConcurrencyManager);
            Assert.IsNotNull(_concurrencyManager);

            Assert.AreEqual(100, _concurrencyManager.MaxConcurrency);
            Assert.AreEqual(100, _concurrencyManager.NumAvailables);
            Assert.AreEqual(0, _concurrencyManager.NumRunning);

            _concurrencyManager.AcquireResourceAsync(CancellationToken.None);
            Assert.AreEqual(100, _concurrencyManager.MaxConcurrency);
            Assert.AreEqual(99, _concurrencyManager.NumAvailables);
            Assert.AreEqual(1, _concurrencyManager.NumRunning);

            _concurrencyManager.ReleaseResource();
            Assert.AreEqual(100, _concurrencyManager.MaxConcurrency);
            Assert.AreEqual(100, _concurrencyManager.NumAvailables);
            Assert.AreEqual(0, _concurrencyManager.NumRunning);

            var oldConcurrencyManager = _concurrencyManager;

            // Update the value
            ConfigMapUtil.Configuration[testConfig] = "200";

            _isSetConcurrencyManager = false;

            ConfigMapUtil.Configuration.CheckChangeAndCallBack(CancellationToken.None);
            Thread.Sleep(50);

            // Callback should not be called
            Assert.AreEqual(false, _isSetConcurrencyManager);

            Assert.AreEqual(200, _concurrencyManager.MaxConcurrency);
            Assert.AreEqual(200, _concurrencyManager.NumAvailables);
            Assert.AreEqual(0, _concurrencyManager.NumRunning);

            _concurrencyManager.AcquireResourceAsync(CancellationToken.None);
            Assert.AreEqual(200, _concurrencyManager.MaxConcurrency);
            Assert.AreEqual(199, _concurrencyManager.NumAvailables);
            Assert.AreEqual(1, _concurrencyManager.NumRunning);

            _concurrencyManager.ReleaseResource();
            Assert.AreEqual(200, _concurrencyManager.MaxConcurrency);
            Assert.AreEqual(200, _concurrencyManager.NumAvailables);
            Assert.AreEqual(0, _concurrencyManager.NumRunning);

            // Dispose
            configurableConcurrencyManager.Dispose();
        }



        private static void SetConcurrencyManager(IConcurrencyManager? channelConcurrencyManager)
        {
            _isSetConcurrencyManager = true;
            _concurrencyManager = channelConcurrencyManager;
        }
    }
}


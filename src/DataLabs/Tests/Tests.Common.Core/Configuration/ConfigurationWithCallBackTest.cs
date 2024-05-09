namespace Microsoft.WindowsAzure.Governance.DataLabs.Tests.Common.Core.Configuration
{
    using Microsoft.Extensions.Configuration;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.Configuration;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.Extensions;
    using System.Collections.Generic;
    using System.Runtime.CompilerServices;
    using static Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.Configuration.ConfigurationWithCallBack;

    [TestClass]
    public class ConfigurationWithCallBackTest
    {
        private static object newValue = null;

        [TestMethod]
        public async Task TestWithStaticMethod()
        {
            var configBuilder = new ConfigurationBuilder().AddInMemoryCollection();
            var config = configBuilder.Build();
            var configurationWithCallBack = new ConfigurationWithCallBack(config);
            configurationWithCallBack.GetValueWithCallBack<string>("testKey1", StaticTestCallBack, String.Empty);

            var persistentWrappers = (IList<CallBackWrapper>)PrivateFunctionAccessHelper.GetPrivateField(
                              typeof(ConfigurationWithCallBack),
                              "_persistentWrappers", configurationWithCallBack);

            var target2WrapperMappings = (ConditionalWeakTable<object, IList<CallBackWrapper>>)PrivateFunctionAccessHelper.GetPrivateField(
                              typeof(ConfigurationWithCallBack),
                              "_targetToWrapperMappings", configurationWithCallBack);

            Assert.AreEqual(1, persistentWrappers.Count);
            Assert.AreEqual(0, target2WrapperMappings.Count());

            newValue = null;
            foreach (var persistWrapper in persistentWrappers)
            {
                Assert.AreEqual("testKey1", persistWrapper.Key);

                await persistWrapper.ReloadInternalAsync("NEWVALUE1").ConfigureAwait(false);
                Assert.AreEqual("NEWVALUE1", newValue);
            }
        }

        [TestMethod]
        public async Task TestWithInstanceMethod()
        {
            var managedClass = new TestManagedClass();
            var configBuilder = new ConfigurationBuilder().AddInMemoryCollection();
            var config = configBuilder.Build();
            var configurationWithCallBack = new ConfigurationWithCallBack(config);
            configurationWithCallBack.GetValueWithCallBack<string>("testKey2", managedClass.InstanceTestCallBack, String.Empty);

            var persistentWrappers = (IList<CallBackWrapper>)PrivateFunctionAccessHelper.GetPrivateField(
                              typeof(ConfigurationWithCallBack),
                              "_persistentWrappers", configurationWithCallBack);

            var target2WrapperMappings = (ConditionalWeakTable<object, IList<CallBackWrapper>>)PrivateFunctionAccessHelper.GetPrivateField(
                              typeof(ConfigurationWithCallBack),
                              "_targetToWrapperMappings", configurationWithCallBack);

            Assert.AreEqual(0, persistentWrappers.Count);
            Assert.AreEqual(1, target2WrapperMappings.Count());

            foreach (var targetWrappers in target2WrapperMappings)
            {
                var wrapperList = targetWrappers.Value;
                Assert.AreEqual(1, wrapperList.Count);
                Assert.AreEqual("testKey2", wrapperList[0].Key);

                await wrapperList[0].ReloadInternalAsync("NEWVALUE2").ConfigureAwait(false);
                Assert.AreEqual("NEWVALUE2", managedClass.newValue);
            }
        }

        private static Task StaticTestCallBack(string value)
        {
            newValue = value;
            return Task.CompletedTask;
        }
    }

    public class TestManagedClass
    {
        public string newValue;
        public Task InstanceTestCallBack(string value)
        {
            newValue = value;
            return Task.CompletedTask;
        }
    }
}
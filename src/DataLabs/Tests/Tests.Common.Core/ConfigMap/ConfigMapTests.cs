namespace Microsoft.WindowsAzure.Governance.DataLabs.Tests.Common.Core.ConfigMap
{
    using Microsoft.Extensions.Configuration;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.ConfigMap;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.Extensions;

    [TestClass]
    public class ConfigMapTests
    {
        private const string TestStringKey = "testKey";
        private const string TestStringValue = "testValue";

        private const string TestIntKey = "testIntKey";
        private const int TestIntValue = 100;

        private const string TestListKey = "testListKey";
        private const string TestListValue = "value1;Value1";

        private static bool _isFirstCallBackCalled = false;
        private static string _firstCallBackValue = string.Empty;
        private static bool _isSecondCallBackCalled = false;
        private static string _secondCallBackValue = string.Empty;

        [TestInitialize]
        public void TestInitialize()
        {
            var configBuilder = new ConfigurationBuilder().AddInMemoryCollection();
            ConfigMapUtil.Initialize(configBuilder, false);

            ConfigMapUtil.Configuration[TestStringKey] = TestStringValue;
            ConfigMapUtil.Configuration[TestIntKey] = TestIntValue.ToString();

            ConfigMapUtil.Configuration[TestListKey] = "value1;Value1";
        }

        [TestCleanup]
        public void TestCleanup()
        {
            ConfigMapUtil.Reset();
        }

        [TestMethod]
        public void GetStringValueTest()
        {
            var result = ConfigMapUtil.Configuration.GetValue<string>(TestStringKey);
            Assert.AreEqual(TestStringValue, result);
        }

        [TestMethod]
        public void GetIntValueTest()
        {
            var result = ConfigMapUtil.Configuration.GetValue<int>(TestIntKey);
            Assert.AreEqual(TestIntValue, result);
        }

        [TestMethod]
        public void ConvertToListTest()
        {
            var result = ConfigMapUtil.Configuration.GetValue<string>(TestListKey).ConvertToList();
            var expected = TestListValue.Split(';').ToList();
            Assert.AreEqual(expected.ToString(), result.ToString());
        }


        [TestMethod]
        public void ConvertToSetTest()
        {
            // case sensitive
            var value = ConfigMapUtil.Configuration.GetValue<string>(TestListKey);
            var result = value.ConvertToSet(caseSensitive: true);
            Assert.AreEqual(2, result.Count);

            // case insensitive
            value = ConfigMapUtil.Configuration.GetValue<string>(TestListKey);
            result = value.ConvertToSet(caseSensitive: false);
            Assert.AreEqual(1, result.Count);
        }

        [TestMethod]
        public void SingleCallBackTest()
        {
            _isFirstCallBackCalled = false;
            _firstCallBackValue = String.Empty;

            var initValue = "initValue";
            var testKey = "newKey";
            var newValue = "newValue";

            var value = ConfigMapUtil.Configuration.GetValueWithCallBack<string>(testKey, UpdateCallback1, initValue, false);
            Assert.AreEqual(initValue, value);

            Assert.IsTrue(ConfigMapUtil.Configuration.HasRegisteredCallBack(testKey));

            // Update the value
            ConfigMapUtil.Configuration[testKey] = newValue;

            ConfigMapUtil.Configuration.CheckChangeAndCallBack(CancellationToken.None);

            Thread.Sleep(50);

            Assert.AreEqual(newValue, _firstCallBackValue);
            Assert.AreEqual(true, _isFirstCallBackCalled);
        }

        [TestMethod]
        public void MultiCallBackNotAllowTest()
        {
            _isFirstCallBackCalled = false;
            _firstCallBackValue = String.Empty;

            var initValue = "initValue";
            var testKey = "newKey";
            var value = ConfigMapUtil.Configuration.GetValueWithCallBack<string>(testKey, UpdateCallback1, initValue, true);
            Assert.ThrowsException<ArgumentException>(() => ConfigMapUtil.Configuration.GetValueWithCallBack<string>(testKey, UpdateCallback2, initValue, false));
        }

        [TestMethod]
        public void MultiCallBackAllowTest()
        {
            _isFirstCallBackCalled = false;
            _firstCallBackValue = String.Empty;

            var initValue = "initValueMulti";
            var testKey = "newKeyMulti";
            var newValue = "newValueMulti";


            var value = ConfigMapUtil.Configuration.GetValueWithCallBack<string>(testKey, UpdateCallback1, initValue, true);

            // via IConfiguration Extension

            var value2 = ((IConfiguration)ConfigMapUtil.Configuration).GetValueWithCallBack<string>(testKey, UpdateCallback2, initValue, true);

            Assert.AreEqual(initValue, value);
            Assert.AreEqual(initValue, value2);

            // Update the value
            ConfigMapUtil.Configuration[testKey] = newValue;

            ConfigMapUtil.Configuration.CheckChangeAndCallBack(CancellationToken.None);

            Thread.Sleep(100);

            Assert.AreEqual(newValue, _firstCallBackValue);
            Assert.AreEqual(true, _isFirstCallBackCalled);

            Assert.AreEqual(newValue, _secondCallBackValue);
            Assert.AreEqual(true, _isSecondCallBackCalled);
        }

        private static Task UpdateCallback1(string newValue)
        {
            _isFirstCallBackCalled = true;
            _firstCallBackValue = newValue;
            return Task.CompletedTask;
        }

        private static Task UpdateCallback2(string newValue)
        {
            _isSecondCallBackCalled = true;
            _secondCallBackValue = newValue;
            return Task.CompletedTask;
        }
    }
}


namespace Microsoft.WindowsAzure.Governance.DataLabs.Tests.Common.Core.Utils
{
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.Utils;
    using Newtonsoft.Json;
    using System.Globalization;

    [TestClass]
    public class SerializationHelperTests
    {
        [TestMethod]
        public void MinValueDateTimeTest()
        {
            var minValueDateTime = new DateTimeTestClass()
            {
                dateTime = DateTime.MinValue
            };

            var serialized = SerializationHelper.SerializeToString(minValueDateTime);
            Assert.AreEqual("{\"dateTime\":\"0001-01-01T00:00:00Z\"}", serialized);

            var dateTimeClass = SerializationHelper.Deserialize<DateTimeTestClass>(serialized);
            Assert.AreEqual(DateTime.MinValue, dateTimeClass.dateTime);
        }

        [TestMethod]
        public void DefaultDateTimeTest()
        {
            var defaultDateTime = new DateTimeTestClass()
            {
                dateTime = default
            };

            var serialized = SerializationHelper.SerializeToString(defaultDateTime);
            Assert.AreEqual("{\"dateTime\":\"0001-01-01T00:00:00Z\"}", serialized);

            var dateTimeClass = SerializationHelper.Deserialize<DateTimeTestClass>(serialized);
            Assert.AreEqual(DateTime.MinValue, dateTimeClass.dateTime);
        }

        [TestMethod]
        public void DefaultDateTimeTestWithOtherFormat()
        {
            var defaultDateTimeWithoutZ = "{\"dateTime\":\"0001-01-01 00:00:00\"}";
            var dateTimeClass = SerializationHelper.Deserialize<DateTimeTestClass>(defaultDateTimeWithoutZ);
            Assert.AreEqual(new DateTime(), dateTimeClass.dateTime);

            // Serialize again
            var serialized = SerializationHelper.SerializeToString(dateTimeClass);
            Assert.AreEqual("{\"dateTime\":\"0001-01-01T00:00:00Z\"}", serialized);
        }

        [TestMethod]
        public void DateTimeTest()
        {
            var dateTimeStr = "2013-11-09T11:22:33Z";
            var expectedDateTime = DateTime.Parse(dateTimeStr, CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.RoundtripKind);

            var dateTime = new DateTimeTestClass() 
            { 
                dateTime = expectedDateTime 
            };
            
            var serialized = SerializationHelper.SerializeToString(dateTime);
            Assert.AreEqual("{\"dateTime\":\"" + dateTimeStr + "\"}", serialized);

            var dateTimeClass = SerializationHelper.Deserialize<DateTimeTestClass>(serialized);
            Assert.AreEqual(expectedDateTime, dateTimeClass.dateTime);
        }

        private class DateTimeTestClass
        {
            [JsonProperty]
            public DateTime dateTime;
        }

    }
}

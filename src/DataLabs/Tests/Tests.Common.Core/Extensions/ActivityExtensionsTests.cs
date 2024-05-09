namespace Microsoft.WindowsAzure.Governance.DataLabs.Tests.Common.Core.Extensions
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.ActivityTracing;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.ActivityTracing.Contracts;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.Extensions;

    /// <summary>
    /// Activity extensions tests
    /// </summary>
    [TestClass]
    public class ActivityExtensionsTests
    {
        /*
         * 
         * Notice!!!!!!
         * 
         * 
         * If you see unit test fail in this unit test (not local unit test but in PR build), 
         *   it is mostly because some of new added codes don't call disposal (using) of ActivityMonitor. 
         * In local unit test, it doesn't seem to reuse same async context internally but PR build seems to use same async context for several unit tests for performance.
         * 
         * Please check all new added codes to make sure that activityMonitor is calling disposal through using keyword
         *   
         */

        /// <summary>
        /// Tests the small collection.
        /// </summary>
        [TestMethod]
        public void TestSmallCollection()
        {
            Assert.AreEqual(null, IActivityMonitor.CurrentActivity, IActivityMonitor.CurrentActivity?.ToString());

            var activity = new BasicActivity("test");
            activity.LogCollectionAndCount("list", new List<string> { "test1", "test2" });

            Assert.AreEqual("test1|test2", activity.Properties["list"]);
            Assert.AreEqual(2, activity.Properties["listCount"]);

            Assert.AreEqual(null, IActivityMonitor.CurrentActivity, IActivityMonitor.CurrentActivity?.ToString());
        }

        /// <summary>
        /// Tests the large collection.
        /// </summary>
        [TestMethod]
        public void TestLargeCollection()
        {
            Assert.AreEqual(null, IActivityMonitor.CurrentActivity, IActivityMonitor.CurrentActivity?.ToString());

            var activity = new BasicActivity("test");
            var collection = new List<string> { "test1", "test2", "test3", "test4", "test5" };
            activity.LogCollectionAndCount("list", collection, 15);

            Assert.AreEqual("test1|test2|tes...", activity.Properties["list"]);
            Assert.AreEqual(5, activity.Properties["listCount"]);

            Assert.AreEqual(null, IActivityMonitor.CurrentActivity, IActivityMonitor.CurrentActivity?.ToString());
        }

        /// <summary>
        /// Tests the empty collection.
        /// </summary>
        [TestMethod]
        public void TestEmptyCollection()
        {
            Assert.AreEqual(null, IActivityMonitor.CurrentActivity, IActivityMonitor.CurrentActivity?.ToString());

            var activity = new BasicActivity("test");
            activity.LogCollectionAndCount("list", new List<string>());

            Assert.AreEqual(string.Empty, activity.Properties["list"]);
            Assert.AreEqual(0, activity.Properties["listCount"]);

            Assert.AreEqual(null, IActivityMonitor.CurrentActivity, IActivityMonitor.CurrentActivity?.ToString());
        }

        /// <summary>
        /// Tests the null collection.
        /// </summary>
        [TestMethod]
        public void TestNullCollection()
        {
            Assert.AreEqual(null, IActivityMonitor.CurrentActivity, IActivityMonitor.CurrentActivity?.ToString());

            var activity = new BasicActivity("test");
            activity.LogCollectionAndCount("list", (List<string>)null);

            Assert.IsNull(activity.Properties["list"]);
            Assert.AreEqual(0, activity.Properties["listCount"]);

            Assert.AreEqual(null, IActivityMonitor.CurrentActivity, IActivityMonitor.CurrentActivity?.ToString());
        }

        /// <summary>
        /// Tests the null activity.
        /// </summary>
        [TestMethod]
        public void TestNullActivity()
        {
            Assert.AreEqual(null, IActivityMonitor.CurrentActivity, IActivityMonitor.CurrentActivity?.ToString());

            BasicActivity activity = null;
            // ReSharper disable once ExpressionIsAlwaysNull
            AssertUtils.AssertThrows<ArgumentNullException>(() => activity.LogCollectionAndCount("list", new List<string>()));

            Assert.AreEqual(null, IActivityMonitor.CurrentActivity, IActivityMonitor.CurrentActivity?.ToString());
        }

        /// <summary>
        /// Tests the null property.
        /// </summary>
        [TestMethod]
        public void TestNullProperty()
        {
            Assert.AreEqual(null, IActivityMonitor.CurrentActivity, IActivityMonitor.CurrentActivity?.ToString());

            var activity = new BasicActivity("test");
            AssertUtils.AssertThrows<ArgumentNullException>(() => activity.LogCollectionAndCount(null, new List<string>()));

            Assert.AreEqual(null, IActivityMonitor.CurrentActivity, IActivityMonitor.CurrentActivity?.ToString());
        }

        /// <summary>
        /// Tests the zero length.
        /// </summary>
        [TestMethod]
        public void TestZeroLength()
        {
            Assert.AreEqual(null, IActivityMonitor.CurrentActivity, IActivityMonitor.CurrentActivity?.ToString());

            var activity = new BasicActivity("test");
            AssertUtils.AssertThrows<ArgumentOutOfRangeException>(() => activity.LogCollectionAndCount("list", new List<string>(), 0));

            Assert.AreEqual(null, IActivityMonitor.CurrentActivity, IActivityMonitor.CurrentActivity?.ToString());
        }

        /// <summary>
        /// Tests the negative length.
        /// </summary>
        [TestMethod]
        public void TestNegativeLength()
        {
            Assert.AreEqual(null, IActivityMonitor.CurrentActivity, IActivityMonitor.CurrentActivity?.ToString());

            var activity = new BasicActivity("test");
            AssertUtils.AssertThrows<ArgumentOutOfRangeException>(() => activity.LogCollectionAndCount("list", new List<string>(), -5));

            Assert.AreEqual(null, IActivityMonitor.CurrentActivity, IActivityMonitor.CurrentActivity?.ToString());
        }

        /// <summary>
        /// Tests the negative length.
        /// </summary>
        [TestMethod]
        public void TestRepeatedItems()
        {
            Assert.AreEqual(null, IActivityMonitor.CurrentActivity, IActivityMonitor.CurrentActivity?.ToString());

            var activity = new BasicActivity("test");
            activity.LogCollectionAndCount("list", new[] { "a", "b", "b" }, 3);

            Assert.AreEqual("a|b...", activity.Properties["list"]);
            Assert.AreEqual(3, activity.Properties["listCount"]);

            Assert.AreEqual(null, IActivityMonitor.CurrentActivity, IActivityMonitor.CurrentActivity?.ToString());
        }

        [TestMethod]
        public void TestIgnoringSingleValuePropertyCount()
        {
            Assert.AreEqual(null, IActivityMonitor.CurrentActivity, IActivityMonitor.CurrentActivity?.ToString());

            var activity = new BasicActivity("test");
            activity.LogCollectionAndCount("list", new[] { "a" }, ignoreCountForSingleValueProperty: true);

            Assert.AreEqual("a", activity.Properties["list"]);
            Assert.IsFalse(activity.Properties.ContainsKey("listCount"));

            Assert.AreEqual(null, IActivityMonitor.CurrentActivity, IActivityMonitor.CurrentActivity?.ToString());
        }

        [TestMethod]
        public void TestNotIgnoringMultiValuePropertiesCount()
        {
            Assert.AreEqual(null, IActivityMonitor.CurrentActivity, IActivityMonitor.CurrentActivity?.ToString());

            var activity = new BasicActivity("test");
            activity.LogCollectionAndCount("list", new[] { "a", "b" }, ignoreCountForSingleValueProperty: true);

            Assert.AreEqual("a|b", activity.Properties["list"]);
            Assert.AreEqual(2, activity.Properties["listCount"]);

            Assert.AreEqual(null, IActivityMonitor.CurrentActivity, IActivityMonitor.CurrentActivity?.ToString());
        }

        [TestMethod]
        public void TestCustomDelimiter()
        {
            Assert.AreEqual(null, IActivityMonitor.CurrentActivity, IActivityMonitor.CurrentActivity?.ToString());

            var activity = new BasicActivity("test");
            activity.LogCollectionAndCount("list", new[] { "a", "b", "c" }, delimiter: ',');

            Assert.AreEqual("a,b,c", activity.Properties["list"]);
            Assert.AreEqual(3, activity.Properties["listCount"]);

            Assert.AreEqual(null, IActivityMonitor.CurrentActivity, IActivityMonitor.CurrentActivity?.ToString());
        }

        [TestMethod]
        public void TestListHasOnlyNulls()
        {
            Assert.AreEqual(null, IActivityMonitor.CurrentActivity, IActivityMonitor.CurrentActivity?.ToString());

            var activity = new BasicActivity("test");
            activity.LogCollectionAndCount("list", new string[2] { null, null }, delimiter: ',');

            Assert.AreEqual("\"null\",\"null\"", activity.Properties["list"]);
            Assert.AreEqual(2, activity.Properties["listCount"]);

            Assert.AreEqual(null, IActivityMonitor.CurrentActivity, IActivityMonitor.CurrentActivity?.ToString());
        }

        [TestMethod]
        public void TestNonInheritableProperties()
        {
            Assert.AreEqual(null, IActivityMonitor.CurrentActivity, IActivityMonitor.CurrentActivity?.ToString());

            var topActivity = IActivityMonitor.CurrentActivity;
            
            var activityMonitor = new ActivityMonitorFactory("test");
            using var parentMonitor = activityMonitor.ToMonitor();

            var currentActivityAfterParent = IActivityMonitor.CurrentActivity;
            Assert.AreNotSame(currentActivityAfterParent, topActivity);

            parentMonitor.Activity["p1"] = "p1Value";
            parentMonitor.Activity["p2", false] = "p1Value";
            parentMonitor.OnStart();

            Assert.IsTrue(parentMonitor.Activity.GetProperties(true).Any(x => x.Key == "p1"));
            Assert.IsTrue(parentMonitor.Activity.GetProperties(false).Any(x => x.Key == "p2"));
            Assert.IsFalse(parentMonitor.Activity.GetProperties(true).Any(x => x.Key == "p2"));
            Assert.IsTrue(parentMonitor.Activity.TopLevelActivity == parentMonitor.Activity);

            using var childMonitor1 = activityMonitor.ToMonitor(parentMonitor.Activity, inheritProperties: true);

            var currentActivityAfterChild1 = IActivityMonitor.CurrentActivity;
            Assert.AreNotSame(currentActivityAfterChild1, currentActivityAfterParent);
            Assert.AreNotSame(currentActivityAfterChild1, topActivity);

            childMonitor1.Activity["c11"] = "p1Value";
            childMonitor1.Activity["c12", false] = "p2Value";
            childMonitor1.OnStart();

            Assert.IsTrue(childMonitor1.Activity.GetProperties(false).Any(x => x.Key == "p1"));
            Assert.IsFalse(childMonitor1.Activity.GetProperties(false).Any(x => x.Key == "p2"));
            Assert.IsTrue(childMonitor1.Activity.GetProperties(true).Any(x => x.Key == "c11"));
            Assert.IsTrue(childMonitor1.Activity.GetProperties(false).Any(x => x.Key == "c12"));
            Assert.IsFalse(childMonitor1.Activity.GetProperties(true).Any(x => x.Key == "c12"));
            Assert.IsTrue(childMonitor1.Activity.TopLevelActivity == parentMonitor.Activity);

            using var childMonitor2 = activityMonitor.ToMonitor(parentMonitor.Activity); // default is inheritProperties: false

            var currentActivityAfterChild2 = IActivityMonitor.CurrentActivity;
            Assert.AreNotSame(currentActivityAfterChild2, currentActivityAfterChild1);
            Assert.AreNotSame(currentActivityAfterChild2, currentActivityAfterParent);
            Assert.AreNotSame(currentActivityAfterChild2, topActivity);

            childMonitor2.Activity["c21"] = "p1Value";
            childMonitor2.Activity["c22", false] = "p2Value";
            childMonitor2.OnStart();

            Assert.IsFalse(childMonitor2.Activity.GetProperties(false).Any(x => x.Key == "p1"));
            Assert.IsFalse(childMonitor2.Activity.GetProperties(false).Any(x => x.Key == "p2"));
            Assert.IsTrue(childMonitor2.Activity.GetProperties(true).Any(x => x.Key == "c21"));
            Assert.IsTrue(childMonitor2.Activity.GetProperties(false).Any(x => x.Key == "c22"));
            Assert.IsFalse(childMonitor2.Activity.GetProperties(true).Any(x => x.Key == "c22"));
            Assert.IsTrue(childMonitor2.Activity.TopLevelActivity == parentMonitor.Activity);

            Assert.IsTrue(childMonitor2.Activity.Elapsed < parentMonitor.Activity.Elapsed);

            // .net will call Dispose in reverse order() of order in which we use "using"
            // For unit test, let's call Dispose here
            childMonitor2.Dispose();
            Assert.AreSame(currentActivityAfterChild1, IActivityMonitor.CurrentActivity);

            childMonitor1.Dispose();
            Assert.AreSame(currentActivityAfterParent, IActivityMonitor.CurrentActivity);

            parentMonitor.Dispose();
            Assert.AreSame(topActivity, IActivityMonitor.CurrentActivity);

            Assert.AreEqual(null, IActivityMonitor.CurrentActivity, IActivityMonitor.CurrentActivity?.ToString());
        }

        [DataRow(new[] { "test1", "test2" }, 7, "test1|t...")]
        [DataRow(new[] { "test1", "test2" }, 20, "test1|test2")]
        [DataRow(new string[0], 20, "")]
        [DataRow(null, 20, null)]
        [TestMethod]
        public void TestLogCollectionAndCount(IEnumerable<string>? strings, int length, string? output)
        {
            Assert.AreEqual(null, IActivityMonitor.CurrentActivity, IActivityMonitor.CurrentActivity?.ToString());

            var propertyName = "log";
            var activity = new BasicActivity("test");
            activity.LogCollectionAndCount(propertyName, strings, length);

            Assert.AreEqual(output, activity.Properties[propertyName]);
            Assert.AreEqual(strings?.Count() ?? 0, activity.Properties[propertyName + ActivityExtensions.CountSuffix]);

            Assert.AreEqual(null, IActivityMonitor.CurrentActivity, IActivityMonitor.CurrentActivity?.ToString());
        }
    }
}
namespace Microsoft.WindowsAzure.Governance.DataLabs.Tests.Common.Core.ActivityTracing
{
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.ActivityTracing;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.ActivityTracing.Contracts;

    [TestClass]
    public class ActivityTracingTests
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
         *   
         */

        private static string DUMMY_ACTIVITY = "dummyActivity";
        private static string DUMMY_COMPONENT = "dummyComponent";
        private static string DUMMY_SCENARIO = "dummyScenario";

        private static TimeSpan ZERO_TIME = new TimeSpan(0, 0, 0);

        private readonly ActivityMonitorFactory activityMonitorFactory = new(DUMMY_ACTIVITY);

        #region BasicActivityTests

        [TestMethod]
        public void TestBasicActivity()
        {
            Assert.AreEqual(null, IActivityMonitor.CurrentActivity, IActivityMonitor.CurrentActivity?.ToString());

            IActivity activity = new BasicActivity(DUMMY_ACTIVITY);

            // Check activity fields
            Assert.AreEqual(DUMMY_ACTIVITY, activity.ActivityName);
            Assert.AreEqual(null, activity.Component);
            Assert.AreEqual(null, activity.Scenario);

            // Checks ParentActivity is Null
            Assert.AreEqual(BasicActivity.Null, activity.ParentActivity);
            Assert.AreEqual(Guid.Empty, activity.ParentActivity.ActivityId);
        }

        [DataTestMethod]
        [DataRow(true, DisplayName = "ActivityStartAndCompleteTest_RecordDuration_True")]
        [DataRow(false, DisplayName = "ActivityStartAndCompleteTest_RecordDuration_False")]
        public void ActivityStartAndCompleteTest(bool recordDurationMetric)
        {
            Assert.AreEqual(null, IActivityMonitor.CurrentActivity, IActivityMonitor.CurrentActivity?.ToString());

            var currentActivity = IActivityMonitor.CurrentActivity;

            using IActivityMonitor activityMonitor = activityMonitorFactory.ToMonitor();
            activityMonitor.OnStart();
            
            Thread.Sleep(1000);

            activityMonitor.OnCompleted(recordDurationMetric: recordDurationMetric);

            Assert.AreNotEqual(activityMonitor.Activity.Elapsed, ZERO_TIME);
            Assert.AreSame(currentActivity, IActivityMonitor.CurrentActivity);

            Assert.AreEqual(null, IActivityMonitor.CurrentActivity, IActivityMonitor.CurrentActivity?.ToString());
        }

        [TestMethod]
        public void ActivityScenarioComponentTest()
        {
            Assert.AreEqual(null, IActivityMonitor.CurrentActivity, IActivityMonitor.CurrentActivity?.ToString());

            var currentActivity = IActivityMonitor.CurrentActivity;

            using IActivityMonitor activityMonitor = activityMonitorFactory.ToMonitor(scenario: DUMMY_SCENARIO, component: DUMMY_COMPONENT);
            activityMonitor.OnCompleted();

            Assert.AreEqual(activityMonitor.Activity.Component, DUMMY_COMPONENT);
            Assert.AreEqual(activityMonitor.Activity.Scenario, DUMMY_SCENARIO);
            Assert.AreSame(currentActivity, IActivityMonitor.CurrentActivity);

            Assert.AreEqual(null, IActivityMonitor.CurrentActivity, IActivityMonitor.CurrentActivity?.ToString());
        }

        #endregion

        #region CurrentActivity

        [TestMethod]
        public void CurrentActivityAndToMonitorTest()
        {
            Assert.AreEqual(null, IActivityMonitor.CurrentActivity, IActivityMonitor.CurrentActivity?.ToString());

            var currentActivity = IActivityMonitor.CurrentActivity;

            using (IActivityMonitor activityMonitor = activityMonitorFactory.ToMonitor())
            {
                Assert.AreEqual(activityMonitor.Activity, IActivityMonitor.CurrentActivity);
            }

            Assert.AreSame(currentActivity, IActivityMonitor.CurrentActivity);

            Assert.AreEqual(null, IActivityMonitor.CurrentActivity, IActivityMonitor.CurrentActivity?.ToString());
        }


        [TestMethod]
        public void ActivityCompletedWithoutStartTest()
        {
            Assert.AreEqual(null, IActivityMonitor.CurrentActivity, IActivityMonitor.CurrentActivity?.ToString());

            var currentActivity = IActivityMonitor.CurrentActivity;
            using IActivityMonitor activityMonitor = activityMonitorFactory.ToMonitor();

            activityMonitor.OnCompleted();
            Assert.AreEqual(IActivityMonitor.CurrentActivity, null);
            Assert.AreEqual(activityMonitor.Activity.Elapsed, ZERO_TIME);
            Assert.AreSame(currentActivity, IActivityMonitor.CurrentActivity);

            Assert.AreEqual(null, IActivityMonitor.CurrentActivity, IActivityMonitor.CurrentActivity?.ToString());
        }

        [TestMethod]
        public void PassingActivityToChildMethodTest()
        {
            Assert.AreEqual(null, IActivityMonitor.CurrentActivity, IActivityMonitor.CurrentActivity?.ToString());

            var currentActivity = IActivityMonitor.CurrentActivity;
            
            using IActivityMonitor activityMonitor = activityMonitorFactory.ToMonitor();
            Assert.AreEqual(IActivityMonitor.CurrentActivity, activityMonitor.Activity);

            activityMonitor.OnStart();
            activityMonitor.Activity["parentTestKey"] = "parentTestValue";
            Assert.AreEqual(activityMonitor.Activity.Properties["parentTestKey"], "parentTestValue");

            DummyChildMethod();

            Assert.AreEqual(activityMonitor.Activity.Properties["testKey"], "testValue");
            Assert.AreEqual(activityMonitor.Activity.Properties["parentTestKey"], "parentTestValue");
            
            activityMonitor.OnCompleted();
            Assert.AreEqual(IActivityMonitor.CurrentActivity, null);
            Assert.AreSame(currentActivity, IActivityMonitor.CurrentActivity);

            Assert.AreEqual(null, IActivityMonitor.CurrentActivity, IActivityMonitor.CurrentActivity?.ToString());
        }

        private void DummyChildMethod()
        {
            IActivity parentActivity = IActivityMonitor.CurrentActivity;
            
            Assert.AreEqual(parentActivity.Properties["parentTestKey"], "parentTestValue");

            parentActivity?.SetProperty("testKey", "testValue");
        }

        [TestMethod]
        public void ActivityUpdatePropertiesTest()
        {
            Assert.AreEqual(null, IActivityMonitor.CurrentActivity, IActivityMonitor.CurrentActivity?.ToString());

            var currentActivity = IActivityMonitor.CurrentActivity;
            using IActivityMonitor activityMonitor = activityMonitorFactory.ToMonitor();
            Assert.AreEqual(IActivityMonitor.CurrentActivity, activityMonitor.Activity);

            activityMonitor.OnStart();
            activityMonitor.Activity["testKey"] = "testValue";

            DummyChildUpdateCurrentKey();

            Assert.AreEqual(activityMonitor.Activity.Properties["testKey"], "testValueUpdated");
            activityMonitor.OnCompleted();
            Assert.AreSame(currentActivity, IActivityMonitor.CurrentActivity);

            Assert.AreEqual(null, IActivityMonitor.CurrentActivity, IActivityMonitor.CurrentActivity?.ToString());
        }

        private void DummyChildUpdateCurrentKey()
        {
            IActivityMonitor.CurrentActivity?.SetProperty("testKey", "testValueUpdated");
        }

        #endregion

        #region Inheritance

        [TestMethod]
        public void ActivityInheritedTest()
        {
            Assert.AreEqual(null, IActivityMonitor.CurrentActivity, IActivityMonitor.CurrentActivity?.ToString());

            var correlationId = Guid.NewGuid().ToString();
            var resourceId = "/subscriptions/abc/resourcegroups/def/providers/Microsoft.Compute/virtualMachines/test/providers/Microsoft.AzureBusinessContinuity/unifiedProtectedItems/testupi";

            var currentActivity = IActivityMonitor.CurrentActivity;
            using IActivityMonitor activityMonitor = activityMonitorFactory.ToMonitor(scenario: DUMMY_SCENARIO, component: DUMMY_COMPONENT, correlationId: correlationId, inputResourceId: resourceId);

            Assert.AreEqual(correlationId, activityMonitor.Activity.CorrelationId);
            Assert.AreEqual(resourceId, activityMonitor.Activity.InputResourceId);

            activityMonitor.OnStart();

            DummyChildMonitorInherit();

            activityMonitor.OnCompleted();

            Assert.AreNotEqual(activityMonitor.Activity.Elapsed, ZERO_TIME);
            Assert.AreSame(currentActivity, IActivityMonitor.CurrentActivity);

            Assert.AreEqual(null, IActivityMonitor.CurrentActivity, IActivityMonitor.CurrentActivity?.ToString());
        }

        [TestMethod]
        public void ActivityInheritedTestWithPredefinedProperty()
        {
            Assert.AreEqual(null, IActivityMonitor.CurrentActivity, IActivityMonitor.CurrentActivity?.ToString());

            var correlationId = Guid.NewGuid().ToString();
            var resourceId = "/subscriptions/abc/resourcegroups/def/providers/Microsoft.Compute/virtualMachines/test/providers/Microsoft.AzureBusinessContinuity/unifiedProtectedItems/testupi";

            var currentActivity = IActivityMonitor.CurrentActivity;
            using IActivityMonitor activityMonitor = activityMonitorFactory.ToMonitor(scenario: DUMMY_SCENARIO, component: DUMMY_COMPONENT);

            Assert.AreEqual(null, activityMonitor.Activity.CorrelationId);
            Assert.AreEqual(null, activityMonitor.Activity.InputResourceId);

            activityMonitor.OnStart();

            activityMonitor.Activity.CorrelationId = correlationId;
            activityMonitor.Activity.InputResourceId = resourceId;

            Assert.AreEqual(correlationId, activityMonitor.Activity.CorrelationId);
            Assert.AreEqual(resourceId, activityMonitor.Activity.InputResourceId);

            DummyChildMonitorInherit();

            activityMonitor.OnCompleted();

            Assert.AreNotEqual(activityMonitor.Activity.Elapsed, ZERO_TIME);
            Assert.AreSame(currentActivity, IActivityMonitor.CurrentActivity);

            Assert.AreEqual(null, IActivityMonitor.CurrentActivity, IActivityMonitor.CurrentActivity?.ToString());
        }

        private void DummyChildMonitorInherit()
        {
            var currentActivity = IActivityMonitor.CurrentActivity;
            using IActivityMonitor activityMonitor = activityMonitorFactory.ToMonitor(inheritProperties: true);
            IActivity activity = activityMonitor.Activity;

            Assert.AreEqual(activity.ParentActivity.Component, activity.Component);
            Assert.AreEqual(activity.ParentActivity.Scenario, activity.Scenario);

            Assert.AreEqual(activity.ParentActivity.CorrelationId, activity.CorrelationId);
            Assert.AreEqual(activity.ParentActivity.InputResourceId, activity.InputResourceId);

            Assert.AreEqual(ZERO_TIME, activity.Elapsed);
            
            activityMonitor.OnCompleted();
            Assert.AreSame(currentActivity, IActivityMonitor.CurrentActivity);
        }

        [TestMethod]
        public void ActivityNoInheritedTest()
        {
            Assert.AreEqual(null, IActivityMonitor.CurrentActivity, IActivityMonitor.CurrentActivity?.ToString());

            var currentActivity = IActivityMonitor.CurrentActivity;
            using IActivityMonitor activityMonitor = activityMonitorFactory.ToMonitor(scenario: DUMMY_SCENARIO, component: DUMMY_COMPONENT);

            activityMonitor.Activity.Properties["parentType"] = "parentValue";
            activityMonitor.OnStart();
            Assert.AreEqual("parentValue", activityMonitor.Activity.Properties["parentType"]);

            DummyChildMonitorNoInherit();

            activityMonitor.OnCompleted();

            Assert.AreNotEqual(activityMonitor.Activity.Elapsed, ZERO_TIME);
            Assert.AreSame(currentActivity, IActivityMonitor.CurrentActivity);

            Assert.AreEqual(null, IActivityMonitor.CurrentActivity, IActivityMonitor.CurrentActivity?.ToString());
        }

        [TestMethod]
        public void ActivityCurrentCreationSequenceTest()
        {
            Assert.AreEqual(null, IActivityMonitor.CurrentActivity, IActivityMonitor.CurrentActivity?.ToString());

            using IActivityMonitor topActivityMonitor = activityMonitorFactory.ToMonitor(scenario: DUMMY_SCENARIO, component: DUMMY_COMPONENT);

            var creationTime1 = topActivityMonitor.Activity.CreationDateTime;
            Assert.IsTrue(creationTime1 != default);
            Assert.IsTrue(topActivityMonitor.Activity.StartDateTime == default);

            topActivityMonitor.OnStart();

            var startTime1 = topActivityMonitor.Activity.StartDateTime;
            Assert.IsTrue(startTime1 != default);

            topActivityMonitor.OnCompleted();


            Assert.IsTrue(topActivityMonitor.Activity.CreationDateTime != default);
            Assert.IsTrue(topActivityMonitor.Activity.StartDateTime != default);

            Assert.AreEqual(null, IActivityMonitor.CurrentActivity, IActivityMonitor.CurrentActivity?.ToString());
        }

        [DataTestMethod]
        [DataRow(true, DisplayName = "ActivityMultiCompletedTest_RecordDuration_True")]
        [DataRow(false, DisplayName = "ActivityMultiCompletedTest_RecordDuration_False")]
        public void ActivityMultiCompletedTest(bool recordDurationMetric)
        {
            Assert.AreEqual(null, IActivityMonitor.CurrentActivity, IActivityMonitor.CurrentActivity?.ToString());

            var currentActivity = IActivityMonitor.CurrentActivity;
            using IActivityMonitor activityMonitor = activityMonitorFactory.ToMonitor(scenario: DUMMY_SCENARIO, component: DUMMY_COMPONENT);
            var parentActivity = activityMonitor.Activity;
            Assert.AreEqual(IActivityMonitor.CurrentActivity, parentActivity);

            activityMonitor.OnStart();
            activityMonitor.Activity["testKey"] = "testValue";

            // Create child activity
            var childCurrentActivity = IActivityMonitor.CurrentActivity;
            using IActivityMonitor childActivityMonitor = activityMonitorFactory.ToMonitor();
            var childActivity = childActivityMonitor.Activity;

            Assert.AreNotEqual(IActivityMonitor.CurrentActivity, parentActivity);
            Assert.AreEqual(IActivityMonitor.CurrentActivity, childActivity);
            Assert.AreEqual(DUMMY_SCENARIO, childActivity.Scenario);
            Assert.AreEqual(DUMMY_COMPONENT, childActivity.Component);

            childActivityMonitor.OnCompleted(recordDurationMetric: recordDurationMetric);

            // It should pop the child activity and set the current activity to the parent activity
            Assert.AreNotEqual(IActivityMonitor.CurrentActivity, childActivity);
            Assert.AreEqual(IActivityMonitor.CurrentActivity, parentActivity);
            Assert.AreSame(childCurrentActivity, IActivityMonitor.CurrentActivity);

            childActivityMonitor.OnCompleted(recordDurationMetric: recordDurationMetric);
            
            // Calling onCompleted one more doesn't change currentActivity
            Assert.AreNotEqual(IActivityMonitor.CurrentActivity, childActivity);
            Assert.AreEqual(IActivityMonitor.CurrentActivity, parentActivity);
            Assert.AreSame(childCurrentActivity, IActivityMonitor.CurrentActivity);

            activityMonitor.OnCompleted();
            Assert.AreSame(currentActivity, IActivityMonitor.CurrentActivity);

            Assert.AreEqual(null, IActivityMonitor.CurrentActivity, IActivityMonitor.CurrentActivity?.ToString());
        }

        private void DummyChildMonitorNoInherit()
        {
            var currentActivity = IActivityMonitor.CurrentActivity;
            using IActivityMonitor activityMonitor = activityMonitorFactory.ToMonitor(inheritProperties: false);
            Assert.IsFalse(activityMonitor.Activity.Properties.TryGetValue("parentType", out var value));

            IActivity activity = activityMonitor.Activity;

            Assert.AreEqual(activity.Component, activity.ParentActivity.Component);
            Assert.AreEqual(activity.Scenario, activity.ParentActivity.Scenario);

            activityMonitor.OnCompleted();
            Assert.AreSame(currentActivity, IActivityMonitor.CurrentActivity);
        }

        #endregion
    }
}

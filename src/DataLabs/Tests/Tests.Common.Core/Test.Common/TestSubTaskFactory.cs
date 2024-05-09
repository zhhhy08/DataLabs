namespace Microsoft.WindowsAzure.Governance.DataLabs.Tests.Common
{
    using System.Diagnostics;
    using System.Threading.Tasks;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.ActivityTracing;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.ActivityTracing.Contracts;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.Contracts;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.ServiceMonitoring;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.TaskChannel;

    internal class TestSubTaskFactory : ISubTaskFactory<TestEventTaskContext>
    {
        internal static ActivityMonitorFactory TestSubTaskFactoryDoWork1 = new ActivityMonitorFactory("TestSubTaskFactory.DoWork1");

        public string SubTaskName => "TestSubTaskFactory";
        public bool CanContinueToNextTaskOnException => false;

        public readonly TestSubTask _testTask; // singleton
        public ITaskChannelManager<TestEventTaskContext> channel2;

        public TestSubTaskFactory(bool useValueTask, bool throwException, AbstractConcurrentTaskChannelManager<TestEventTaskContext> channel2 = null)
        {
            _testTask = new TestSubTask(useValueTask, throwException, channel2);
            this.channel2 = channel2;
        }

        public ISubTask<TestEventTaskContext> CreateSubTask(AbstractEventTaskContext<TestEventTaskContext> eventTaskContext)
        {
            return _testTask;
        }

        public void Dispose()
        {
            throw new NotImplementedException();
        }

        internal class TestSubTask : ISubTask<TestEventTaskContext>
        {
            public bool UseValueTask { get; }

            internal readonly bool _throwException;
            internal bool DoTaskWork1Called = false;
            internal bool DoTaskWork2Called = false;
            internal bool DoTaskWork2MovedToOtherChannel = false;

            internal OpenTelemetryActivityWrapper CurrentActivityWrapper;
            internal Activity CurrentActivity;
            internal OpenTelemetryActivityWrapper AfterActivityWrapper;
            internal Activity AfterActivity;
            internal OpenTelemetryActivityWrapper DoTaskWork1CurrentActivityWrapper;
            internal Activity DoTaskWork1CurrentActivity;
            internal OpenTelemetryActivityWrapper DoTaskWork1AfterActivityWrapper;
            internal Activity DoTaskWork1AfterActivity;
            internal OpenTelemetryActivityWrapper DoTaskWork2CurrentActivityWrapper;
            internal Activity DoTaskWork2CurrentActivity;

            internal IActivity DoTaskWork1IActivity = null;
            internal IActivity DoTaskWork2IActivity = null;
            
            internal int TaskCalled = 0;
            internal int ValueTaskCalled = 0;

            internal ITaskChannelManager<TestEventTaskContext> channel2;

            public TestSubTask(bool valueTask, bool throwException, ITaskChannelManager<TestEventTaskContext> channel2)
            {
                UseValueTask = valueTask;
                _throwException = throwException;
                this.channel2 = channel2;
            }

            public Task ProcessEventTaskContextAsync(AbstractEventTaskContext<TestEventTaskContext> eventTaskContext)
            {
                CurrentActivityWrapper = OpenTelemetryActivityWrapper.Current;
                CurrentActivity = Activity.Current;

                TaskCalled = 1;

                if (_throwException)
                {
                    throw new Exception("Test");
                }

                DoTaskWork1(eventTaskContext);

                AfterActivityWrapper = OpenTelemetryActivityWrapper.Current;
                AfterActivity = Activity.Current;
                return Task.CompletedTask;
            }

            public ValueTask ProcessEventTaskContextValueAsync(AbstractEventTaskContext<TestEventTaskContext> eventTaskContext)
            {
                CurrentActivityWrapper = OpenTelemetryActivityWrapper.Current;
                CurrentActivity = Activity.Current;

                ValueTaskCalled = 1;

                if (_throwException)
                {
                    throw new Exception("Test");
                }

                return ValueTask.CompletedTask;
            }

            private void DoTaskWork1(AbstractEventTaskContext<TestEventTaskContext> eventTaskContext)
            {
                DoTaskWork1IActivity = IActivityMonitor.CurrentActivity;
                DoTaskWork1CurrentActivityWrapper = OpenTelemetryActivityWrapper.Current;
                DoTaskWork1CurrentActivity = Activity.Current;
                DoTaskWork1Called = true;

                using var methodMonitor = TestSubTaskFactoryDoWork1.ToMonitor();

                DoTaskWork2(eventTaskContext);

                methodMonitor.OnCompleted();

                DoTaskWork1AfterActivityWrapper = OpenTelemetryActivityWrapper.Current;
                DoTaskWork1AfterActivity = Activity.Current;
            }

            private void DoTaskWork2(AbstractEventTaskContext<TestEventTaskContext> eventTaskContext)
            {
                DoTaskWork2IActivity = IActivityMonitor.CurrentActivity;

                DoTaskWork2CurrentActivityWrapper = OpenTelemetryActivityWrapper.Current;
                DoTaskWork2CurrentActivity = Activity.Current;
                DoTaskWork2Called = true;

                if (channel2 != null)
                {
                    DoTaskWork2MovedToOtherChannel = true;
                    eventTaskContext.SetNextChannel(channel2);
                }
            }
        }
    }
}
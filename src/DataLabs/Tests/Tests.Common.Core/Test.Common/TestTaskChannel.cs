namespace Microsoft.WindowsAzure.Governance.DataLabs.Tests.Common
{
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Threading.Tasks;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.ActivityTracing.Contracts;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.Contracts;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.ServiceMonitoring;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.TaskChannel;

    internal class TestTaskChannel : AbstractConcurrentTaskChannelManager<TestEventTaskContext>
    {
        internal static ActivitySource TestActivitySource = new("TestActivitySource");

        internal int TaskNotMovedCalled = 0;
        internal int BeforeProcessCalled = 0;
        internal int ProcessErrorCalled = 0;
        internal Exception ProcessErrorException = null;

        internal IActivity TaskNotMovedIActivity = null;
        internal IActivity BeforeProcessIActivity = null;
        internal IActivity ProcessErrorIActivity = null;

        internal OpenTelemetryActivityWrapper BeforeCurrentActivityWrapper;
        internal OpenTelemetryActivityWrapper TaskNotMovedCurrentActivityWrapper;
        internal OpenTelemetryActivityWrapper ErrorCurrentActivityWrapper;
        internal TestErrorTaskChannel testErrorTaskChannel;
        internal AbstractConcurrentTaskChannelManager<TestEventTaskContext> nextChannelForNotMoving;
        
        public TestTaskChannel(string channelName) : base(channelName, "TestEventTaskContext")
        {
            testErrorTaskChannel = new TestErrorTaskChannel(channelName + ".TestErrorTaskChannel");
        }

        public TestTaskChannel(string channelName, AbstractConcurrentTaskChannelManager<TestEventTaskContext> nextChannelForNotMoving = null) : base(channelName, "TestEventTaskContext")
        {
            this.nextChannelForNotMoving = nextChannelForNotMoving;
            testErrorTaskChannel = new TestErrorTaskChannel(channelName + ".TestErrorTaskChannel");
        }

        protected override ValueTask ProcessNotMovedTaskAsync(AbstractEventTaskContext<TestEventTaskContext> eventTaskContext)
        {
            TaskNotMovedIActivity = IActivityMonitor.CurrentActivity;
            TaskNotMovedCurrentActivityWrapper = OpenTelemetryActivityWrapper.Current;
            TaskNotMovedCalled = 1;

            if (nextChannelForNotMoving != null)
            {
                eventTaskContext.SetNextChannel(nextChannelForNotMoving);
            }else
            {
                eventTaskContext.Dispose();
            }
            return ValueTask.CompletedTask;
        }

        protected override ValueTask BeforeProcessAsync(AbstractEventTaskContext<TestEventTaskContext> eventTaskContext)
        {
            BeforeProcessIActivity = IActivityMonitor.CurrentActivity;
            BeforeCurrentActivityWrapper = OpenTelemetryActivityWrapper.Current;
            BeforeProcessCalled = 1;
            return ValueTask.CompletedTask;
        }

        protected override void Dispose(bool disposing)
        {
        }

        protected override ValueTask ProcessErrorAsync(AbstractEventTaskContext<TestEventTaskContext> eventTaskContext, Exception ex)
        {
            ProcessErrorIActivity = IActivityMonitor.CurrentActivity;
            ErrorCurrentActivityWrapper = OpenTelemetryActivityWrapper.Current;

            ProcessErrorException = ex;
            ProcessErrorCalled = 1;

            eventTaskContext.SetNextChannel(testErrorTaskChannel);
            return ValueTask.CompletedTask;
        }
    }

    public class TestNextChannel : AbstractConcurrentTaskChannelManager<TestEventTaskContext>
    {
        public int BeforeProcessCount = 0;
        public int ProcessErrorCount = 0;
        public int ProcessNotMovedCount = 0;

        public TestNextChannel(string channelName) : base(channelName, "TestEventTaskContext")
        {
        }

        protected override ValueTask BeforeProcessAsync(AbstractEventTaskContext<TestEventTaskContext> eventTaskContext)
        {
            BeforeProcessCount++;
            return ValueTask.CompletedTask;
        }

        protected override void Dispose(bool disposing)
        {
        }

        protected override ValueTask ProcessErrorAsync(AbstractEventTaskContext<TestEventTaskContext> eventTaskContext, Exception ex)
        {
            ProcessErrorCount++;
            return ValueTask.CompletedTask;
        }

        protected override ValueTask ProcessNotMovedTaskAsync(AbstractEventTaskContext<TestEventTaskContext> eventTaskContext)
        {
            ProcessNotMovedCount++;
            eventTaskContext.Dispose();
            return ValueTask.CompletedTask;
        }
    }

    internal class TestErrorTaskChannel : AbstractConcurrentTaskChannelManager<TestEventTaskContext>
    {
        internal int TaskNotMovedCalled = 0;
        internal int BeforeProcessCalled = 0;
        internal int ProcessErrorCalled = 0;
        internal Exception ProcessErrorException = null;

        internal IActivity TaskNotMovedIActivity = null;
        internal IActivity BeforeProcessIActivity = null;
        internal IActivity ProcessErrorIActivity = null;

        internal OpenTelemetryActivityWrapper BeforeCurrentActivityWrapper;
        internal OpenTelemetryActivityWrapper TaskNotMovedCurrentActivityWrapper;
        internal OpenTelemetryActivityWrapper ErrorCurrentActivityWrapper;

        public TestErrorTaskChannel(string channelName) : base(channelName, "TestEventTaskContext")
        {
        }

        protected override ValueTask ProcessNotMovedTaskAsync(AbstractEventTaskContext<TestEventTaskContext> eventTaskContext)
        {
            TaskNotMovedIActivity = IActivityMonitor.CurrentActivity;
            TaskNotMovedCurrentActivityWrapper = OpenTelemetryActivityWrapper.Current;
            TaskNotMovedCalled = 1;
            eventTaskContext.Dispose();
            return ValueTask.CompletedTask;
        }

        protected override ValueTask BeforeProcessAsync(AbstractEventTaskContext<TestEventTaskContext> eventTaskContext)
        {
            BeforeProcessIActivity = IActivityMonitor.CurrentActivity;
            BeforeCurrentActivityWrapper = OpenTelemetryActivityWrapper.Current;
            BeforeProcessCalled = 1;
            return ValueTask.CompletedTask;
        }

        protected override void Dispose(bool disposing)
        {
        }

        protected override ValueTask ProcessErrorAsync(AbstractEventTaskContext<TestEventTaskContext> eventTaskContext, Exception ex)
        {
            ProcessErrorIActivity = IActivityMonitor.CurrentActivity;
            ErrorCurrentActivityWrapper = OpenTelemetryActivityWrapper.Current;

            ProcessErrorException = ex;
            ProcessErrorCalled = 1;
            return ValueTask.CompletedTask;
        }
    }

    internal class TestBufferedTaskProcessorFactory : IBufferedTaskProcessorFactory<TestEventTaskContext>
    {
        internal int NumTask;

        public IBufferedTaskProcessor<TestEventTaskContext> CreateBufferedTaskProcessor()
        {
            return new TestBufferedTaskProcessor(this);
        }

        public void Dispose()
        {
        }

        public class TestBufferedTaskProcessor : IBufferedTaskProcessor<TestEventTaskContext>
        {
            private TestBufferedTaskProcessorFactory _testBufferedTaskProcessorFactory;

            public TestBufferedTaskProcessor(TestBufferedTaskProcessorFactory testBufferedTaskProcessorFactory)
            {
                _testBufferedTaskProcessorFactory = testBufferedTaskProcessorFactory;
            }

            public Task ProcessBufferedTasksAsync(IReadOnlyList<AbstractEventTaskContext<TestEventTaskContext>> eventTaskContexts)
            {
                Interlocked.Add(ref _testBufferedTaskProcessorFactory.NumTask, eventTaskContexts.Count);
                return Task.CompletedTask;
            }
        }
    }

    internal class TestBufferedTaskChannel : AbstractPartitionedBufferedTaskChannelManager<TestEventTaskContext>
    {
        internal static ActivitySource TestActivitySource = new("TestActivitySource");

        internal int TaskNotMovedCalled = 0;
        internal int BeforeProcessCalled = 0;
        internal int ProcessErrorCalled = 0;
        internal Exception ProcessErrorException = null;

        internal IActivity TaskNotMovedIActivity = null;
        internal IActivity BeforeProcessIActivity = null;
        internal IActivity ProcessErrorIActivity = null;

        internal OpenTelemetryActivityWrapper BeforeCurrentActivityWrapper;
        internal OpenTelemetryActivityWrapper TaskNotMovedCurrentActivityWrapper;
        internal OpenTelemetryActivityWrapper ErrorCurrentActivityWrapper;
        internal TestErrorTaskChannel testErrorTaskChannel;

        public TestBufferedTaskChannel(string channelName, int numReaders = 5) : base(channelName, "TestEventTaskContext", intNumQueue: numReaders)
        {
            testErrorTaskChannel = new(channelName + ".TestErrorTaskChannel");
        }

        protected override ValueTask ProcessNotMovedTaskAsync(AbstractEventTaskContext<TestEventTaskContext> eventTaskContext)
        {
            TaskNotMovedIActivity = IActivityMonitor.CurrentActivity;
            TaskNotMovedCurrentActivityWrapper = OpenTelemetryActivityWrapper.Current;
            TaskNotMovedCalled++;
            eventTaskContext.Dispose();
            return ValueTask.CompletedTask;
        }

        protected override ValueTask BeforeProcessAsync(AbstractEventTaskContext<TestEventTaskContext> eventTaskContext)
        {
            BeforeProcessIActivity = IActivityMonitor.CurrentActivity;
            BeforeCurrentActivityWrapper = OpenTelemetryActivityWrapper.Current;
            BeforeProcessCalled++;
            return ValueTask.CompletedTask;
        }

        protected override void Dispose(bool disposing)
        {
        }

        protected override ValueTask ProcessErrorAsync(AbstractEventTaskContext<TestEventTaskContext> eventTaskContext, Exception ex)
        {
            ProcessErrorIActivity = IActivityMonitor.CurrentActivity;
            ErrorCurrentActivityWrapper = OpenTelemetryActivityWrapper.Current;

            ProcessErrorException = ex;
            ProcessErrorCalled++;

            eventTaskContext.SetNextChannel(testErrorTaskChannel);
            return ValueTask.CompletedTask;
        }
    }

}

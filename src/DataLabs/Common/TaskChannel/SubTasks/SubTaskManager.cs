namespace Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.TaskChannel
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.Contracts;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.ServiceMonitoring;

    public class SubTaskManager<T> : IDisposable
    {
        // instead use ProducerCallback model
        public int NumTaskFactory => _taskFactories.Count;
        private static readonly TagList _emptyTagList = new();

        private readonly List<ISubTaskFactory<T>> _taskFactories;
        private readonly List<string> _startedNames;
        private readonly List<string> _completedNames;
        private readonly List<string> _failedNames;
        private int _disposed;

        public SubTaskManager() {
            _taskFactories = new List<ISubTaskFactory<T>>(4);
            _startedNames = new List<string>(4);
            _completedNames = new List<string>(4);
            _failedNames = new List<string>(4);
        }

        public void AddSubTaskFactory(ISubTaskFactory<T> taskFactory)
        {
            _taskFactories.Add(taskFactory);
            _startedNames.Add(taskFactory.SubTaskName + ".Started");
            _completedNames.Add(taskFactory.SubTaskName + ".Completed");
            _failedNames.Add(taskFactory.SubTaskName + ".Failed");
        }

        public async Task ProcessEventTaskContextAsync(AbstractEventTaskContext<T> eventTaskContext)
        {
            OpenTelemetryActivityWrapper.Current = eventTaskContext.EventTaskActivity;

            for (int i = 0; i < _taskFactories.Count; i++)
            {
                var taskFactory = _taskFactories[i];

                try
                {
                    if (eventTaskContext.NextTaskChannel != null)
                    {
                        // TaskContext is already moved to other channel
                        return;
                    }

                    var subTask = taskFactory.CreateSubTask(eventTaskContext);
                    if (subTask == null)
                    {
                        continue;
                    }

                    eventTaskContext.EventTaskActivity.AddEvent(_startedNames[i], _emptyTagList);

                    if (subTask.UseValueTask)
                    {
                        await subTask.ProcessEventTaskContextValueAsync(eventTaskContext).ConfigureAwait(false);
                    }
                    else
                    {
                        await subTask.ProcessEventTaskContextAsync(eventTaskContext).ConfigureAwait(false);
                    }

                    eventTaskContext.EventTaskActivity.AddEvent(_completedNames[i]);
                    
                    if (eventTaskContext.NextTaskChannel != null)
                    {
                        // TaskContext is already moved to other channel
                        return;
                    }
                }
                catch (Exception ex)
                {
                    if (!taskFactory.CanContinueToNextTaskOnException)
                    {
                        eventTaskContext.EventTaskActivity.RecordException(_failedNames[i], ex);
                        throw;
                    }else
                    {
                        eventTaskContext.EventTaskActivity.RecordContinuableException(_failedNames[i], ex);
                    }
                }
            }
        }

        public void Dispose()
        {
            if (_disposed > 1 || Interlocked.CompareExchange(ref _disposed, 1, 0) != 0)
            {
                // Already disposed
                return;
            }

            for (int i = 0; i < _taskFactories.Count; i++)
            {
                _taskFactories[i].Dispose();
            }
        }
    }
}
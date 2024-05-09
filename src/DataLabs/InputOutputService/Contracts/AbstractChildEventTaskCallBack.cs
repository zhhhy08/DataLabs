namespace Microsoft.WindowsAzure.Governance.DataLabs.IOService.Contracts
{
    using System;
    using System.Diagnostics;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.Constants;
    using Microsoft.WindowsAzure.Governance.DataLabs.IOService.Constants;

    public abstract class AbstractChildEventTaskCallBack<TInput> : IEventTaskCallBack where TInput : IInputMessage
    {
        protected readonly IOEventTaskContext<TInput> _parentEventTaskContext;

        public int TotalChild => _totalChilds;
        public int TotalChildSuccess => _taskSuccess;
        public int TotalChildMovedToRetry => _movedToRetry;
        public int TotalChildMovedToPoison => _movedToPoison;
        public int TotalChildDropped => _dropped;
        public int TotalChildCancelled => _cancelled;
        public int TotalChildTimeout => _timeout;
        public int TotalChildTaskError => _taskError;
        public bool HasDropPoisonedETagConflict => _hasDropPoisonedETagConflict;

        private int _refCount;
        private int _totalChilds;
        private int _taskSuccess;
        private int _movedToRetry;
        private int _movedToPoison;
        private int _dropped;
        private int _cancelled;
        private int _timeout;
        private int _taskError;
        private bool _hasDropPoisonedETagConflict;

        private long _maxPartnerTotalSpentTime;

        protected string _cancelReason;
        protected string _cancelReasonDetail;

        public CancellationToken AllChildTaskCancellationToken { get; }
        private CancellationTokenSource _childTaskCancellationTokenSource;
        private volatile bool _isAllChildTaskCancelled;

        public bool IsTaskCancelled => _isAllChildTaskCancelled;
        public bool HasParentTask => true;
        public string TaskCancelledReason
        {
            get
            {
                if (!IsTaskCancelled)
                {
                    return null;
                }

                if (_cancelReason != null)
                {
                    return _cancelReason;
                }

                // there is no explict cancelled Reason
                // Check ParentTask
                if (_parentEventTaskContext.HasTaskTimeoutExpired)
                {
                    return SolutionConstants.ParentTaskTimeoutExpired;
                }
                else if (_parentEventTaskContext.IsAlreadyTaskCancelled())
                {
                    return SolutionConstants.ParentTaskCancelled;
                }
                else if (_parentEventTaskContext.TaskCancellationToken.IsCancellationRequested)
                {
                    return SolutionConstants.ParentTaskCancellationTokenSet;
                }

                return null;
            }
        }

        public long PartnerTotalSpentTime
        {
            get
            {
                return _maxPartnerTotalSpentTime;
            }
            set
            {
                lock (this)
                {
                    if (value > _maxPartnerTotalSpentTime)
                    {
                        _maxPartnerTotalSpentTime = value;
                    }
                }
            }
        }

        public AbstractChildEventTaskCallBack(IOEventTaskContext<TInput> parentEventTaskContext)
        {
            _parentEventTaskContext = parentEventTaskContext;
            _childTaskCancellationTokenSource = CancellationTokenSource.CreateLinkedTokenSource(parentEventTaskContext.TaskCancellationToken);
            AllChildTaskCancellationToken = _childTaskCancellationTokenSource.Token;

            parentEventTaskContext.SetChildEventTaskCallBack(this);
        }

        public void StartAddChildEvent()
        {
            _refCount = 1;
            
            // Decrease mainTask's concurrency to avoid deadLock
            _parentEventTaskContext.DecreaseGlobalConcurrency();
        }

        public void IncreaseChildEventCount()
        {
            Interlocked.Increment(ref _totalChilds);
            Interlocked.Increment(ref _refCount);
        }

        public void FinishAddChildEvent()
        {
            _parentEventTaskContext.StartWaitingChildTasksAction = DecreaseRefCount;
            _parentEventTaskContext.NumChildTasks = _totalChilds;
        }

        public void CancelAllChildTasks(string reason, string reasonDetail)
        {
            lock(this)
            {
                if (_cancelReason == null)
                {
                    _cancelReason = reason ?? "CancelAllChildTasks is called";
                    _cancelReasonDetail = reasonDetail;
                }
                if (!_isAllChildTaskCancelled)
                {
                    _isAllChildTaskCancelled = true;
                    if (_childTaskCancellationTokenSource != null)
                    {
                        _childTaskCancellationTokenSource.Cancel();
                        _childTaskCancellationTokenSource.Dispose();
                        _childTaskCancellationTokenSource = null;
                    }
                    
                }
            }
        }

        public virtual void TaskStarted(IIOEventTaskContext eventTaskContext, ref TagList tagList)
        {
        }

        public virtual void TaskCancelCalled(IIOEventTaskContext eventTaskContext)
        {
            Interlocked.Increment(ref _cancelled);
        }

        public virtual void TaskTimeoutCalled(IIOEventTaskContext eventTaskContext)
        {
            Interlocked.Increment(ref _timeout);
        }

        public virtual void TaskErrorCalled(IIOEventTaskContext eventTaskContext, Exception ex)
        {
            Interlocked.Increment(ref _taskError);
        }

        public virtual void TaskSuccess(IIOEventTaskContext eventTaskContext)
        {
            Interlocked.Increment(ref _taskSuccess);
        }

        public virtual void TaskMovedToRetry(IIOEventTaskContext eventTaskContext)
        {
            Interlocked.Increment(ref _movedToRetry);
        }

        public virtual void TaskMovedToPoison(IIOEventTaskContext eventTaskContext)
        {
            if (IOEventTaskFlagHelper.HasSourceOfTruthConflict(eventTaskContext.IOEventTaskFlags))
            {
                _hasDropPoisonedETagConflict = true;
            }
            Interlocked.Increment(ref _movedToPoison);
        }

        public virtual void TaskDropped(IIOEventTaskContext eventTaskContext)
        {
            if (IOEventTaskFlagHelper.HasSourceOfTruthConflict(eventTaskContext.IOEventTaskFlags))
            {
                _hasDropPoisonedETagConflict = true;
            }
            Interlocked.Increment(ref _dropped);
        }

        protected abstract void SetParentTaskToNextChannel();

        private void DecreaseRefCount()
        {
            if (Interlocked.Decrement(ref _refCount) == 0)
            {
                var parentTaskActivity = _parentEventTaskContext.EventTaskActivity;
                parentTaskActivity.SetTag(InputOutputConstants.TotalChild, TotalChild);
                parentTaskActivity.SetTag(InputOutputConstants.TotalChildSuccess, TotalChildSuccess);
                parentTaskActivity.SetTag(InputOutputConstants.TotalChildMovedToRetry, TotalChildMovedToRetry);
                parentTaskActivity.SetTag(InputOutputConstants.TotalChildMovedToPoison, TotalChildMovedToPoison);
                parentTaskActivity.SetTag(InputOutputConstants.TotalChildDropped, TotalChildDropped);
                parentTaskActivity.SetTag(InputOutputConstants.TotalChildCancelled, TotalChildCancelled);
                parentTaskActivity.SetTag(InputOutputConstants.TotalChildTaskError, TotalChildTaskError);
                parentTaskActivity.SetTag(InputOutputConstants.TotalChildTimeout, TotalChildTimeout);
                parentTaskActivity.SetTag(InputOutputConstants.AllChildTaskCancalled, _isAllChildTaskCancelled);

                if (_cancelReason != null)
                {
                    parentTaskActivity.SetTag(InputOutputConstants.AllChildTasksCancelReason, _cancelReason);
                    parentTaskActivity.SetTag(InputOutputConstants.AllChildTasksCancelReasonDetail, _cancelReasonDetail);

                    TagList tagList = default;
                    tagList.Add(InputOutputConstants.AllChildTasksCancelReason, _cancelReason);
                    tagList.Add(InputOutputConstants.AllChildTasksCancelReasonDetail, _cancelReasonDetail);
                    parentTaskActivity.AddEvent(SolutionConstants.EventName_AllChildTaskCancelled, tagList);
                }

                SetParentTaskToNextChannel();

                _ = Task.Run(() => _parentEventTaskContext.StartNextChannelAsync());

                lock (this)
                {
                    _childTaskCancellationTokenSource?.Dispose();
                    _childTaskCancellationTokenSource = null;
                }
                
            }
        }

        public void FinalCleanup()
        {
            DecreaseRefCount();
        }
    }
}

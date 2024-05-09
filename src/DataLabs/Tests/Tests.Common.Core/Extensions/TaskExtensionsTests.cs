namespace Microsoft.WindowsAzure.Governance.DataLabs.Tests.Common.Core.Extensions
{
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.Extensions;
    using System;
    using System.Threading;
    using System.Threading.Tasks;

    [TestClass]
    public class TaskExtensionsTests
    {
        private static readonly TimeSpan SomeTimeout = TimeSpan.FromMinutes(1);

        private static readonly object OperationResult = new object();

        #region WithTimeout(Task, TimeSpan)

        [TestMethod]
        public async Task TestNonGenericTaskCompletes()
        {
            Task task = CompletingOperationAsync();
            Task taskWithTimeout = task.WithTimeout(SomeTimeout);

            await taskWithTimeout.IgnoreContext();
        }

        [TestMethod]
        public async Task TestNonGenericTaskThrows()
        {
            Task task = FailingOperationAsync();
            Task taskWithTimeout = task.WithTimeout(SomeTimeout);

            await WhenAllWithoutThrowing(task, taskWithTimeout).IgnoreContext();

            Assert.AreEqual(TaskStatus.Faulted, task.Status);
            Assert.AreEqual(TaskStatus.Faulted, taskWithTimeout.Status);
            Assert.AreEqual(
                task.Exception?.InnerExceptions[0].GetType(),
                taskWithTimeout.Exception?.InnerExceptions[0].GetType());
        }

        [TestMethod]
        public async Task TestNonGenericTaskCancelled()
        {
            var cts = new CancellationTokenSource();
            cts.Cancel();
            Task task = CancelledOperationAsync(cts.Token);
            Task taskWithTimeout = task.WithTimeout(SomeTimeout);

            await WhenAllWithoutThrowing(task, taskWithTimeout).IgnoreContext();

            Assert.AreEqual(TaskStatus.Canceled, task.Status);
            Assert.AreEqual(TaskStatus.Canceled, taskWithTimeout.Status);
        }

        [TestMethod]
        public async Task TestNonGenericTaskTimedOut()
        {
            Task task = BlockedOperationAsync();
            Task taskWithTimeout = task.WithTimeout(TimeSpan.Zero);

            await AssertUtils.AssertThrowsAsync<TaskTimeoutException>(taskWithTimeout)
                .IgnoreContext();

            Assert.IsFalse(task.IsCompleted);
            Assert.AreEqual(TaskStatus.Faulted, taskWithTimeout.Status);
        }

        #endregion

        #region WithTimeout<T>(Task<T>, TimeSpan)

        [TestMethod]
        public async Task TestGenericTaskCompletes()
        {
            Task<object> task = CompletingOperationAsync();
            Task<object> taskWithTimeout = task.WithTimeout(SomeTimeout);

            var result = await taskWithTimeout.IgnoreContext();

            Assert.IsTrue(ReferenceEquals(OperationResult, result));
        }

        [TestMethod]
        public async Task TestGenericTaskThrows()
        {
            Task<object> task = FailingOperationAsync();
            Task<object> taskWithTimeout = task.WithTimeout(SomeTimeout);

            await WhenAllWithoutThrowing(task, taskWithTimeout).IgnoreContext();

            Assert.AreEqual(TaskStatus.Faulted, task.Status);
            Assert.AreEqual(TaskStatus.Faulted, taskWithTimeout.Status);
            Assert.AreEqual(
                task.Exception?.InnerExceptions[0].GetType(),
                taskWithTimeout.Exception?.InnerExceptions[0].GetType());
        }

        [TestMethod]
        public async Task TestGenericTaskCancelled()
        {
            var cts = new CancellationTokenSource();
            cts.Cancel();
            Task<object> task = CancelledOperationAsync(cts.Token);
            Task<object> taskWithTimeout = task.WithTimeout(SomeTimeout);

            await WhenAllWithoutThrowing(task, taskWithTimeout).IgnoreContext();

            Assert.AreEqual(TaskStatus.Canceled, task.Status);
            Assert.AreEqual(TaskStatus.Canceled, taskWithTimeout.Status);
        }

        [TestMethod]
        public async Task TestGenericTaskTimedOut()
        {
            Task<object> task = BlockedOperationAsync();
            Task<object> taskWithTimeout = task.WithTimeout(TimeSpan.Zero);

            await AssertUtils.AssertThrowsAsync<TaskTimeoutException>(taskWithTimeout)
                .IgnoreContext();

            Assert.IsFalse(task.IsCompleted);
            Assert.AreEqual(TaskStatus.Faulted, taskWithTimeout.Status);
        }

        #endregion

        #region WithTimeout(Task, TimeSpan, CancellationTokenSource)

        [TestMethod]
        public async Task TestCancellableNonGenericTaskCompletes()
        {
            var cts = new CancellationTokenSource();
            Task task = CompletingOperationAsync();
            Task taskWithTimeout = task.WithTimeout(SomeTimeout, cts);

            await taskWithTimeout.IgnoreContext();

            Assert.IsFalse(cts.IsCancellationRequested);
        }

        [TestMethod]
        public async Task TestCancellableNonGenericTaskThrows()
        {
            var cts = new CancellationTokenSource();
            Task task = FailingOperationAsync();
            Task taskWithTimeout = task.WithTimeout(SomeTimeout, cts);

            await WhenAllWithoutThrowing(task, taskWithTimeout).IgnoreContext();

            Assert.IsFalse(cts.IsCancellationRequested);
            Assert.AreEqual(TaskStatus.Faulted, task.Status);
            Assert.AreEqual(TaskStatus.Faulted, taskWithTimeout.Status);
            Assert.AreEqual(
                task.Exception?.InnerExceptions[0].GetType(),
                taskWithTimeout.Exception?.InnerExceptions[0].GetType());
        }

        [TestMethod]
        public async Task TestCancellableNonGenericTaskCancelled()
        {
            var cts = new CancellationTokenSource();
            cts.Cancel();
            Task task = CancelledOperationAsync(cts.Token);
            Task taskWithTimeout = task.WithTimeout(SomeTimeout, cts);

            await WhenAllWithoutThrowing(task, taskWithTimeout).IgnoreContext();

            Assert.AreEqual(TaskStatus.Canceled, task.Status);
            Assert.AreEqual(TaskStatus.Canceled, taskWithTimeout.Status);
        }

        [TestMethod]
        public async Task TestCancellableNonGenericTaskTimedOut()
        {
            var cts = new CancellationTokenSource();
            Task task = BlockedOperationAsync(cts.Token);
            Task taskWithTimeout = task.WithTimeout(TimeSpan.Zero, cts);

            await AssertUtils.AssertThrowsAsync<TaskTimeoutException>(taskWithTimeout)
                .IgnoreContext();

            await Task.Delay(1);

            Assert.AreEqual(TaskStatus.Canceled, task.Status);
            Assert.AreEqual(TaskStatus.Faulted, taskWithTimeout.Status);
        }

        #endregion

        #region WithTimeout<T>(Task<T>, TimeSpan, CancellationTokenSource)

        [TestMethod]
        public async Task TestCancellableGenericTaskCompletes()
        {
            var cts = new CancellationTokenSource();
            Task<object> task = CompletingOperationAsync();
            Task<object> taskWithTimeout = task.WithTimeout(SomeTimeout, cts);

            var result = await taskWithTimeout.IgnoreContext();

            Assert.IsFalse(cts.IsCancellationRequested);
            Assert.IsTrue(ReferenceEquals(OperationResult, result));
        }

        [TestMethod]
        public async Task TestCancellableGenericTaskThrows()
        {
            var cts = new CancellationTokenSource();
            Task<object> task = FailingOperationAsync();
            Task<object> taskWithTimeout = task.WithTimeout(SomeTimeout, cts);

            await WhenAllWithoutThrowing(task, taskWithTimeout).IgnoreContext();

            Assert.IsFalse(cts.IsCancellationRequested);
            Assert.AreEqual(TaskStatus.Faulted, task.Status);
            Assert.AreEqual(TaskStatus.Faulted, taskWithTimeout.Status);
            Assert.AreEqual(
                task.Exception?.InnerExceptions[0].GetType(),
                taskWithTimeout.Exception?.InnerExceptions[0].GetType());
        }

        [TestMethod]
        public async Task TestCancellableGenericTaskCancelled()
        {
            var cts = new CancellationTokenSource();
            cts.Cancel();
            Task<object> task = CancelledOperationAsync(cts.Token);
            Task<object> taskWithTimeout = task.WithTimeout(SomeTimeout, cts);

            await WhenAllWithoutThrowing(task, taskWithTimeout).IgnoreContext();

            Assert.AreEqual(TaskStatus.Canceled, task.Status);
            Assert.AreEqual(TaskStatus.Canceled, taskWithTimeout.Status);
        }

        [TestMethod]
        public async Task TestCancellableGenericTaskTimedOut()
        {
            var cts = new CancellationTokenSource();
            Task<object> task = BlockedOperationAsync(cts.Token);
            Task<object> taskWithTimeout = task.WithTimeout(TimeSpan.Zero, cts);

            await AssertUtils.AssertThrowsAsync<TaskTimeoutException>(taskWithTimeout)
                .IgnoreContext();

            await Task.Delay(1);

            Assert.AreEqual(TaskStatus.Canceled, task.Status);
            Assert.AreEqual(TaskStatus.Faulted, taskWithTimeout.Status);
            Assert.AreEqual(true, cts.IsCancellationRequested);
        }

        #endregion

        #region Helper methods

        private static Task<object> CompletingOperationAsync()
        {
            return Task.FromResult<object>(OperationResult);
        }

        private static Task<object> FailingOperationAsync()
        {
            var tcs = new TaskCompletionSource<object>();
            tcs.SetException(new InvalidOperationException());
            return tcs.Task;
        }

        private static async Task<object> CancelledOperationAsync(CancellationToken cancellationToken)
        {
            await Task.Yield();
            cancellationToken.ThrowIfCancellationRequested();
            return string.Empty;
        }

        private static async Task<object> BlockedOperationAsync(
            CancellationToken? cancellationToken = null)
        {
            await Task.Delay(-1, cancellationToken ?? CancellationToken.None).IgnoreContext();
            return string.Empty;
        }

        private static async Task WhenAllWithoutThrowing(params Task[] tasks)
        {
            try
            {
                await Task.WhenAll(tasks).IgnoreContext();
            }
            catch (Exception)
            {
                // Ignore
            }
        }

        #endregion
    }
}

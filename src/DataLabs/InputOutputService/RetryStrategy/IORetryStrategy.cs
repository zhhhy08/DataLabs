namespace Microsoft.WindowsAzure.Governance.DataLabs.IOService.InputOutputService.RetryStrategy
{
    using System;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.RetryPolicy;

    public class IORetryStrategy : RetryStrategyBase
    {
        public IORetryStrategy() : base()
        {
        }

        public override bool CanRetry(Exception exception)
        {
            // TODO
            // in the future, we could add exception specfic retry filtering. 
            // That is, some specific exception, return false
            return true;
        }

        public override IRetryStrategy GetRetryStrategy(string resourceType)
        {
            return GetDefaultRetryStrategy();
        }
    }
}
namespace Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.Utils
{
    using System;

    public static class SolutionConfigMapKeyValueUtils
    {
        // Some keys could be shared between multiple configMaps
        // Let's create utility here so that multiple container components can use same parsing function
        public static (TimeSpan nonRetryFlowTimeout, TimeSpan retryFlowTimeout) ParseConfigTimeOut(string timeoutStr)
        {
            // timeoutString is like 
            //defaultArmClientTimeOutInSec: "30/60"

            var options = StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries;
            var split = timeoutStr.Split('/', options);
            if (split.Length != 2)
            {
                throw new ArgumentException($"Invalid timeout value: {timeoutStr}. It must be X/Y format");
            }

            if (!int.TryParse(split[0], out var nonRetryFlowTimeout))
            {
                throw new ArgumentException($"Invalid timeout value: {timeoutStr}. It must be X/Y format");
            }

            if (!int.TryParse(split[1], out var retryFlowTimeout))
            {
                throw new ArgumentException($"Invalid timeout value: {timeoutStr}. It must be X/Y format");
            }

            return (TimeSpan.FromSeconds(nonRetryFlowTimeout), TimeSpan.FromSeconds(retryFlowTimeout));
        }
    }
}

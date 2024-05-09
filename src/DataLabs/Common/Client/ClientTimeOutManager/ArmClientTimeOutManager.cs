namespace Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.Client.ClientTimeOutManager
{
    using System;
    using Microsoft.Extensions.Configuration;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.Constants;

    public class ArmClientTimeOutManager
    {
        private static volatile ArmClientTimeOutManager? _instance;
        private static readonly object SyncRoot = new object();

        #region Singleton Impl

        public static ArmClientTimeOutManager Create(IConfiguration configuration)
        {
            if (_instance == null)
            {
                lock (SyncRoot)
                {
                    if (_instance == null)
                    {
                        _instance = new ArmClientTimeOutManager(configuration);
                    }
                }
            }
            return _instance;
        }

        // For testing purpose
        public static void Reset()
        {
            _instance = null;
        }

        #endregion

        private readonly ClientTimeOutManager _resourceTimeOutManager;
        private readonly ClientTimeOutManager _genericApiTimeOutManager;

        private ArmClientTimeOutManager(IConfiguration configuration)
        {
            _resourceTimeOutManager = new ClientTimeOutManager(SolutionConstants.DefaultArmClientGetResourceTimeOutInSec, SolutionConstants.ArmClientResourceTimeOutMappings, configuration);
            _genericApiTimeOutManager = new ClientTimeOutManager(SolutionConstants.DefaultArmClientGenericApiTimeOutInSec, SolutionConstants.ArmClientGenericApiTimeOutMappings, configuration);
        }

        public TimeSpan GetResourceTypeTimeOut(string resourceType, int retryFlowCount)
        {
            var timeOutMap = _resourceTimeOutManager.TimeOutMap;
            if (timeOutMap?.Count > 0)
            {
                if (timeOutMap.TryGetValue(resourceType, out var timeOutInfo))
                {
                    return retryFlowCount > 0 ? timeOutInfo.RetryFlowTimeOut : timeOutInfo.NonRetryFlowTimeOut;
                }
            }

            // There is no matched specific timeout.
            // Let's use default timeout
            return retryFlowCount > 0 ? _resourceTimeOutManager.DefaultRetryFlowTimeOut : _resourceTimeOutManager.DefaultNonRetryFlowTimeOut;
        }

        public TimeSpan GetGenericApiTimeOut(string urlPath, int retryFlowCount)
        {
            var timeOutMap = _genericApiTimeOutManager.TimeOutMap;
            if (timeOutMap?.Count > 0)
            {
                if (timeOutMap.TryGetValue(urlPath, out var timeOutInfo))
                {
                    return retryFlowCount > 0 ? timeOutInfo.RetryFlowTimeOut : timeOutInfo.NonRetryFlowTimeOut;
                }
            }

            // There is no matched specific timeout.
            // Let's use default timeout
            return retryFlowCount > 0 ? _genericApiTimeOutManager.DefaultRetryFlowTimeOut : _genericApiTimeOutManager.DefaultNonRetryFlowTimeOut;
            
        }
    }
}

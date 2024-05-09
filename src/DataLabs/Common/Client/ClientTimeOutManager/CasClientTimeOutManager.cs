namespace Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.Client.ClientTimeOutManager
{
    using System;
    using Microsoft.Extensions.Configuration;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.Constants;

    public class CasClientTimeOutManager
    {
        private static volatile CasClientTimeOutManager? _instance;
        private static readonly object SyncRoot = new object();

        #region Singleton Impl

        public static CasClientTimeOutManager Create(IConfiguration configuration)
        {
            if (_instance == null)
            {
                lock (SyncRoot)
                {
                    if (_instance == null)
                    {
                        _instance = new CasClientTimeOutManager(configuration);
                    }
                }
            }
            return _instance;
        }

        #endregion

        private readonly ClientTimeOutManager _callTimeOutManager;

        private CasClientTimeOutManager(IConfiguration configuration)
        {
            _callTimeOutManager = new ClientTimeOutManager(SolutionConstants.DefaultCasClientTimeOutInSec, SolutionConstants.CasClientTimeOutMappings, configuration);
        }

        public TimeSpan GetCasCallTimeOut(string callMethod, int retryFlowCount)
        {
            var timeOutMap = _callTimeOutManager.TimeOutMap;
            if (timeOutMap?.Count > 0)
            {
                if (timeOutMap.TryGetValue(callMethod, out var timeOutInfo))
                {
                    return retryFlowCount > 0 ? timeOutInfo.RetryFlowTimeOut : timeOutInfo.NonRetryFlowTimeOut;
                }
            }

            // There is no matched specific timeout.
            // Let's use default timeout
            return retryFlowCount > 0 ? _callTimeOutManager.DefaultRetryFlowTimeOut : _callTimeOutManager.DefaultNonRetryFlowTimeOut;
        }
    }
}

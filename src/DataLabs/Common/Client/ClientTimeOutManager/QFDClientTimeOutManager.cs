namespace Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.Client.ClientTimeOutManager
{
    using System;
    using Microsoft.Extensions.Configuration;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.Constants;

    public class QFDClientTimeOutManager
    {
        private static volatile QFDClientTimeOutManager? _instance;
        private static readonly object SyncRoot = new object();

        #region Singleton Impl

        public static QFDClientTimeOutManager Create(IConfiguration configuration)
        {
           
            if (_instance == null)
            {
                lock (SyncRoot)
                {
                    if (_instance == null)
                    {
                        _instance = new QFDClientTimeOutManager(configuration);
                    }
                }
            }
            return _instance;
        }

        #endregion

        private readonly ClientTimeOutManager _callTimeOutManager;

        private QFDClientTimeOutManager(IConfiguration configuration)
        {
            _callTimeOutManager = new ClientTimeOutManager(SolutionConstants.DefaultQFDClientTimeOutInSec, SolutionConstants.QfdClientTimeOutMappings, configuration);
        }

        public TimeSpan GetQFDCallTimeOut(string callMethod, int retryFlowCount)
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

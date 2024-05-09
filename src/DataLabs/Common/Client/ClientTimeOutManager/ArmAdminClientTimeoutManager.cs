namespace Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.Client.ClientTimeOutManager
{
    using System;
    using Microsoft.Extensions.Configuration;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.Constants;

    public class ArmAdminClientTimeOutManager
    {
        private static volatile ArmAdminClientTimeOutManager? _instance;
        private static readonly object SyncRoot = new object();

        // TODO, consider to do more clean up / refactoring to use Generic Type for several client time out managers
        // Because XClientTimeOutManager has similar implementation

        #region Singleton Impl

        public static ArmAdminClientTimeOutManager Create(IConfiguration configuration)
        {
            if (_instance == null)
            {
                lock (SyncRoot)
                {
                    if (_instance == null)
                    {
                        _instance = new ArmAdminClientTimeOutManager(configuration);
                    }
                }
            }
            return _instance;
        }

        #endregion

        private readonly ClientTimeOutManager _callTimeOutManager;

        private ArmAdminClientTimeOutManager(IConfiguration configuration)
        {
            _callTimeOutManager = new ClientTimeOutManager(SolutionConstants.DefaultArmAdminClientTimeOutInSec, SolutionConstants.ArmAdminClientTimeOutMappings, configuration);
        }

        public TimeSpan GetAdminCallTimeOut(string callMethod, int retryFlowCount)
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

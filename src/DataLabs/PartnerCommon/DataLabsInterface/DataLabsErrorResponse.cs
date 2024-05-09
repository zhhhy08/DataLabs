namespace Microsoft.WindowsAzure.Governance.DataLabs.Common.Partner.DataLabsInterface
{
    public class DataLabsErrorResponse
    {
        public DataLabsErrorType ErrorType;
        public int RetryDelayInMilliseconds;
        public string? ErrorCode;
        public string? ErrorDescription;
        public string FailedComponent;

        public DataLabsErrorResponse(
            DataLabsErrorType errorType, 
            int retryDelayInMilliseconds,
            string? errorCode,
            string? errorDescription,
            string failedComponent)
        {
            ErrorType = errorType;
            RetryDelayInMilliseconds = retryDelayInMilliseconds;
            ErrorCode = errorCode;
            ErrorDescription = errorDescription;
            FailedComponent = failedComponent;
        }
    }

    public enum DataLabsErrorType
    {
        DROP = 0,
        RETRY = 1,
        POISON = 2
    }
}

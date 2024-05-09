namespace Microsoft.WindowsAzure.Governance.DataLabs.Common.Partner.DataLabsInterface
{
    public class DataLabsProxyErrorResponse
    {
        public DataLabsErrorType ErrorType;
        public int RetryDelayInMilliseconds;
        public int HttpStatusCode; // could be 0 if error doesn't come from httpResponse code
        public string? ErrorDescription;
        public string FailedComponent;

        public DataLabsProxyErrorResponse(
            DataLabsErrorType errorType, 
            int retryDelayInMilliseconds,
            int httpStatusCode,
            string? errorDescription,
            string failedComponent)
        {
            ErrorType = errorType;
            RetryDelayInMilliseconds = retryDelayInMilliseconds;
            HttpStatusCode = httpStatusCode;
            ErrorDescription = errorDescription;
            FailedComponent = failedComponent;
        }
    }
}

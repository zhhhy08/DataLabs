namespace Microsoft.WindowsAzure.IdMappingService.Utilities
{
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Partner.DataLabsInterface;
    using System;

    public static class IdMappingUtils
    {
        public static DataLabsARNV3Response BuildIdMappingErrorResponse(DataLabsARNV3Request request, Exception exception)
        {
            DataLabsErrorResponse? errorResponse;

            if (exception is TimeoutException)
            {
                //TODO TASK 26957322: determine retry policy for idmapping service
                errorResponse = new DataLabsErrorResponse(DataLabsErrorType.RETRY, request.RetryCount * 1000, "Unknown", exception.Message, "NotSpecified");
            }
            else if (exception is ArgumentException)
            {
                //Ideally this case should have DataLabsErrorType.Poison, but since poision queue does not drain currently this is temporarily set to drop
                errorResponse = new DataLabsErrorResponse(DataLabsErrorType.DROP, request.RetryCount * 1000, "Unknown", exception.Message, "NotSpecified");
            }
            else
            {
                errorResponse = new DataLabsErrorResponse(DataLabsErrorType.DROP, -1, "Unknown", exception.Message, "NotSpecified");
            }

            return new DataLabsARNV3Response(
                DateTimeOffset.UtcNow,
                request.CorrelationId,
                null,
                errorResponse,
                null);
        }
    }
}

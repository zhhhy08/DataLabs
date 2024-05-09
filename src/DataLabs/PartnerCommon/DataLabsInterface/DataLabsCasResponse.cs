namespace Microsoft.WindowsAzure.Governance.DataLabs.Common.Partner.DataLabsInterface
{
    using System;
    using System.Collections.Generic;

    public class DataLabsCasResponse
    {
        public readonly DateTimeOffset ResponseTime;
        public string? CorrelationId;
        public DataLabsCasSuccessResponse? SuccessCasResponse;
        public DataLabsProxyErrorResponse? ErrorResponse;
        public IDictionary<string, string>? Attributes;
        public readonly DataLabsDataSource DataSource;

        public DataLabsCasResponse(
            DateTimeOffset responseTime, 
            string? correlationId, 
            DataLabsCasSuccessResponse? successResponse,
            DataLabsProxyErrorResponse? errorResponse, 
            IDictionary<string, string>? attributes,
            DataLabsDataSource dataSource)
        {
            ResponseTime = responseTime;
            CorrelationId = correlationId;
            SuccessCasResponse = successResponse;
            ErrorResponse = errorResponse;
            Attributes = attributes;
            DataSource = dataSource;
        }
    }
}

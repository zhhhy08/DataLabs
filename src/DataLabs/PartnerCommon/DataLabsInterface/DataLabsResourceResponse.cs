namespace Microsoft.WindowsAzure.Governance.DataLabs.Common.Partner.DataLabsInterface
{
    using System;
    using System.Collections.Generic;

    public class DataLabsResourceResponse
    {
        public readonly DateTimeOffset ResponseTime;
        public readonly string? CorrelationId;
        public readonly DataLabsARNV3SuccessResponse? SuccessARNV3Response;
        public readonly DataLabsARMSuccessResponse? SuccessARMResponse;
        public readonly DataLabsProxyErrorResponse? ErrorResponse;
        public readonly IDictionary<string, string>? Attributes;
        public readonly DataLabsDataSource DataSource;
        
        public DataLabsResourceResponse(
            DateTimeOffset responseTime,
            string? correlationId, 
            DataLabsARNV3SuccessResponse? successARNV3Response, 
            DataLabsARMSuccessResponse? successARMResponse,
            DataLabsProxyErrorResponse? errorResponse,
            IDictionary<string, string>? attributes,
            DataLabsDataSource dataSource = DataLabsDataSource.NONE)
        {
            ResponseTime = responseTime;
            CorrelationId = correlationId;
            SuccessARNV3Response = successARNV3Response;
            SuccessARMResponse = successARMResponse;
            ErrorResponse = errorResponse;
            Attributes = attributes;
            DataSource = dataSource;
        }
    }
}

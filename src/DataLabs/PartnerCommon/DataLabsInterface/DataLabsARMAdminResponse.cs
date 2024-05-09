namespace Microsoft.WindowsAzure.Governance.DataLabs.Common.Partner.DataLabsInterface
{
    using System;
    using System.Collections.Generic;

    public class DataLabsARMAdminResponse
    {
        public readonly DateTimeOffset ResponseTime;
        public readonly string? CorrelationId;
        public readonly DataLabsARMAdminSuccessResponse? SuccessAdminResponse;
        public readonly DataLabsProxyErrorResponse? ErrorResponse;
        public readonly IDictionary<string, string>? Attributes;
        public readonly DataLabsDataSource DataSource;

        public DataLabsARMAdminResponse(
            DateTimeOffset responseTime, 
            string? correlationId, 
            DataLabsARMAdminSuccessResponse? successResponse,
            DataLabsProxyErrorResponse? errorResponse, 
            IDictionary<string, string>? attributes,
            DataLabsDataSource dataSource)
        {
            ResponseTime = responseTime;
            CorrelationId = correlationId;
            SuccessAdminResponse = successResponse;
            ErrorResponse = errorResponse;
            Attributes = attributes;
            DataSource = dataSource;
        }
    }
}

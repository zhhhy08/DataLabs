namespace Microsoft.WindowsAzure.Governance.DataLabs.Common.Partner.DataLabsInterface
{
    using System;
    using System.Collections.Generic;

    public sealed class DataLabsARMGenericResponse
    {
        public readonly DateTimeOffset ResponseTime;
        public readonly string? CorrelationId;
        public readonly DataLabsARMGenericSuccessResponse? SuccessResponse;
        public readonly DataLabsProxyErrorResponse? ErrorResponse;
        public readonly IDictionary<string, string>? Attributes;
        public readonly DataLabsDataSource DataSource;

        public DataLabsARMGenericResponse(
            DateTimeOffset responseTime, 
            string? correlationId,
            DataLabsARMGenericSuccessResponse? successResponse,
            DataLabsProxyErrorResponse? errorResponse, 
            IDictionary<string, string>? attributes,
            DataLabsDataSource dataSource)
        {
            ResponseTime = responseTime;
            CorrelationId = correlationId;
            SuccessResponse = successResponse;
            ErrorResponse = errorResponse;
            Attributes = attributes;
            DataSource = dataSource;
        }
    }
}

namespace Microsoft.WindowsAzure.Governance.DataLabs.Common.Partner.DataLabsInterface
{
    using System;
    using System.Collections.Generic;

    public class DataLabsResourceCollectionResponse
    {
        public readonly DateTimeOffset ResponseTime;
        public readonly string? CorrelationId;
        public readonly DataLabsResourceCollectionSuccessResponse? SuccessResponse;
        public readonly DataLabsProxyErrorResponse? ErrorResponse;
        public readonly IDictionary<string, string>? Attributes;
        public readonly DataLabsDataSource DataSource;

        public DataLabsResourceCollectionResponse(
            DateTimeOffset responseTime,
            string? correlationId,
            DataLabsResourceCollectionSuccessResponse? successResponse,
            DataLabsProxyErrorResponse? errorResponse,
            IDictionary<string, string>? attributes,
            DataLabsDataSource dataSource = DataLabsDataSource.NONE
            )
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

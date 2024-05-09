namespace Microsoft.WindowsAzure.Governance.DataLabs.Common.Partner.DataLabsInterface
{
    using System;
    using System.Collections.Generic;

    public class DataLabsIdMappingResponse
    {
        public readonly DateTimeOffset ResponseTime;
        public readonly string? CorrelationId;
        public readonly List<IdMapping>? SuccessResponse;
        public readonly DataLabsProxyErrorResponse? ErrorResponse;
        public readonly IDictionary<string, string>? Attributes;
        public readonly DataLabsDataSource DataSource;

        public DataLabsIdMappingResponse(
            DateTimeOffset responseTime, 
            string? correlationId,
            List<IdMapping>? successResponse, 
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

    public class IdMapping
    {
        public string AliasResourceId;
        public IList<string>? ArmIds;
        public string StatusCode;
        public string? ErrorMessage;

        public IdMapping(string aliasResourceId, IList<string>? armIds, string statusCode, string? errorMessage)
        {
            this.AliasResourceId = aliasResourceId;
            this.ArmIds = armIds;
            this.StatusCode = statusCode;
            this.ErrorMessage = errorMessage;
        }
    }
}

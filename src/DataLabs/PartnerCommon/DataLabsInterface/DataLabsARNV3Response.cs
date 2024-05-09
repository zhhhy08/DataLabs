namespace Microsoft.WindowsAzure.Governance.DataLabs.Common.Partner.DataLabsInterface
{
    using System;
    using System.Collections.Generic;

    public class DataLabsARNV3Response
    {
        public const string AttributeKey_INTERNAL = "INTERNAL";
        public const string AttributeKey_GROUPID = "GROUPID";
        public const string AttributeKey_SUBJOB = "SUBJOB";

        /*
         * AttributeKey Explanation
         * 
         * 1. INTERNAL: true 
         *       It indicates that it will be uploaded to sourceOfTrue but not be published to Output
         *    default value: false. Every response will be published to Output
         *
         * 2. GROUPID: <number>
         *    This attribute is only possible in streaming responses. 
         *    When GROUPID exists in Attributes Dictionary, responses with same group Id will be grouped. 
         *    Then upload/publish order should be same as responses' order in the same group
         *    That is, for example that in streaming responses, when both response1 and response2 have the same GROUPID, 
         *    response2 will be uploaded ONLY if response1 succeeds to be uploaded
         *    if response1 fails to be uploaded, response2 will not be uploaded. 
         *
         *  Currently GROUPID should be changed once one Group is done. We don't support intermingled group order.
         *   Supported scenario
         *    response1(group1), response2(group1), response3(group2), response4(group3), response5(group3)
         *    group1: response1 -> response2
         *    group2: response3
         *    group3: response4 -> response5
         *     
         *  Not Supported Scenario (intermingled GROUPIDs)
         *    response1(group1) -> response2(group2) -> response3(group1) -> response4(group3) -> response5(group3)
         *    In above example. group2 appears inbetween group1's responses (response1 and response3)
         *    Because we don't know the end of each group until streaming is finished, we don't support this scenario for now.
         */

        public readonly DateTimeOffset ResponseTime;
        public string? CorrelationId;
        public DataLabsARNV3SuccessResponse? SuccessResponse;
        public DataLabsErrorResponse? ErrorResponse;
        public IDictionary<string, string>? Attributes;

        public DataLabsARNV3Response(
            DateTimeOffset responseTime,
            string? correlationId, 
            DataLabsARNV3SuccessResponse? successResponse, 
            DataLabsErrorResponse? errorResponse, 
            IDictionary<string, string>? attributes)
        {
            ResponseTime = responseTime == default ? DateTimeOffset.UtcNow : responseTime;
            CorrelationId = correlationId;
            SuccessResponse = successResponse;
            ErrorResponse = errorResponse;
            Attributes = attributes;
        }
    }
}
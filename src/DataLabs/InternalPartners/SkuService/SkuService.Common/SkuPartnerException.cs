namespace SkuService.Common
{
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Partner.DataLabsInterface;
    using System;

    public class SkuPartnerException: Exception
    {
        public DataLabsProxyErrorResponse ErrorResponse { get; }
        public SkuPartnerException(DataLabsProxyErrorResponse errorResponse): base("SkuPartnerException")
        {
            this.ErrorResponse = errorResponse;
        }
    }
}

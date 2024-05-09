namespace Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.CAS
{
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.Utils;
    using System;

    public class SkuInfo
    {
        public double Size { get; }

        public string OfferFamily { get; }

        public SkuInfo(double size, string offerFamily)
        {
            if (size < 0.0)
            {
                throw new ArgumentException("size cannot be negative");
            }

            GuardHelper.ArgumentNotNullOrEmpty(offerFamily, nameof(offerFamily));
            Size = size;
            OfferFamily = offerFamily;
        }
    }
}

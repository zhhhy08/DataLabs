namespace Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.CAS
{
    using Newtonsoft.Json;
    using System.Collections.Generic;

    public class RestrictionsInfo
    {
        [JsonRequired]
        public readonly HashSet<OfferTermType> OfferTerms = new HashSet<OfferTermType>();

        [JsonRequired]
        public string? Location { get; set; }

        [JsonRequired]
        public string[]? PhysicalAvailabilityZones { get; set; }

        [JsonIgnore]
        public bool IsSpot => HasOfferTerm(OfferTermType.Spot);

        [JsonIgnore]
        public bool IsOndemand => HasOfferTerm(OfferTermType.Ondemand);

        [JsonIgnore]
        public bool IsCapacityReservation => HasOfferTerm(OfferTermType.CapacityReservation);

        private bool HasOfferTerm(OfferTermType offerTerm)
        {
            return OfferTerms.Contains(offerTerm);
        }
    }
}
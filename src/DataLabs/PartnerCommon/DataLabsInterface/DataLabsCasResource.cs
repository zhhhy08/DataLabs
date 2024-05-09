using System.Collections.Generic;

namespace Microsoft.WindowsAzure.Governance.DataLabs.Common.Partner.DataLabsInterface
{
    public class DataLabsCasResource
    {
        public string? responseCode { get; set; }
        public Dictionary<string, IEnumerable<Restrictions>>? restrictionsByOfferFamily { get; set; }
        public Dictionary<string, string>? restrictedSkuNamesToOfferFamily { get; set; }
    }

    public class Restrictions
    {
        public IEnumerable<string>? offerTerms { get; set; }
        public string? location { get; set; }
        public IEnumerable<string>? physicalAvailabilityZones { get; set; }
    }
}

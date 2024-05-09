
namespace Microsoft.WindowsAzure.Governance.DataLabs.Common.Partner.DataLabsInterface
{
    using System.Collections.Generic;

    public class CasRequestBody
    {
        public required string SubscriptionId { get; set; }
        public required string Provider { get; set; }
        public required string OfferCategory { get; set; }
        public required string ClientAppId { get; set; }
        public required string SubscriptionRegistrationDate { get; set; }
        public string? EntitlementStartDate { get; set; }
        public IEnumerable<SubscriptionLocationsAndZones>? SubscriptionLocationsAndZones { get; set; }
        public BillingProperties? BillingProperties { get; set; }
        public InternalSubscriptionPolicies? InternalSubscriptionPolicies { get; set; }
    }

    public class SubscriptionLocationsAndZones
    {
        public string? Location { get; set; }
        public IReadOnlyList<Zones>? Zones { get; set; }
    }

    public class Zones
    {
        public string? LogicalZone { get; set; }
        public string? PhysicalZone { get; set; }
    }

    public class BillingProperties
    {
        public string? ChannelType { get; set; }
        public string? PaymentType { get; set; }
        public string? WorkloadType { get; set; }
        public string? BillingType { get; set; }
        public string? Tier { get; set; }
        public BillingAccount? BillingAccount { get; set; }
    }

    public class BillingAccount
    {
        public string? Id { get; set; }
    }

    public class InternalSubscriptionPolicies
    {
        public string? SubscriptionCostCategory { get; set; }
        public string? SubscriptionPcCode { get; set; }
        public string? SubscriptionEnvironment { get; set; }
    }
}

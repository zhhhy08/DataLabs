namespace SkuService.Common.Models
{
    public class Enums
    {
        public enum SkuScaleType
        {
            /// <summary>
            /// No scaling is allowed.
            /// </summary>
            None = 0,

            /// <summary>
            /// Manual scaling is allowed.
            /// </summary>
            Manual = 1,

            /// <summary>
            /// Manual and automatic scaling are allowed.
            /// </summary>
            Automatic = 2,
        }

        public enum LocationType
        {
            /// <summary>
            /// Azure region
            /// </summary>
            Region,

            /// <summary>
            /// Edge Zone extended location
            /// </summary>
            EdgeZone
        }

        public enum SkuRestrictionType
        {
            /// <summary>
            /// The SKU restriction type is unknown.
            /// </summary>
            NotSpecified,

            /// <summary>
            /// The SKU is restricted by location.
            /// </summary>
            Location,

            /// <summary>
            /// The SKU is restricted by zone.
            /// </summary>
            Zone,
        }

        public enum SkuRestrictionReasonCode
        {
            /// <summary>
            /// The SKU restriction is unknown.
            /// </summary>
            NotSpecified,

            /// <summary>
            /// The SKU is restricted because of quota id.
            /// </summary>
            QuotaId,

            /// <summary>
            /// The SKU is restricted because of capacity.
            /// </summary>
            /// <remarks>Not named as capacity to prevent leak of capacity implementation detail.</remarks>
            NotAvailableForSubscription,
        }

        public enum SubscriptionOfferType
        {
            /// <summary>
            /// The offer type is not specified.
            /// </summary>
            NotSpecified,



            /// <summary>
            /// Trial offer type.
            /// </summary>
            Trial,



            /// <summary>
            /// Buy offer type.
            /// </summary>
            Buy,
        }

        public enum FeaturesPolicy
        {
            //
            // Summary:
            //     Subscription only needs any of the required features for access, equivalent to
            //     not setting a FeaturesRule
            Any,
            //
            // Summary:
            //     Subscription needs all of the required features for access.
            All
        }

        public enum EndpointType
        {
            //
            // Summary:
            //     The endpoint type is not specified.
            NotSpecified,
            //
            // Summary:
            //     The endpoint type is production.
            Production,
            //
            // Summary:
            //     The endpoint type is test in production.
            TestInProduction,
            //
            // Summary:
            //     The endpoint type is canary.
            Canary
        }

        public enum OfferTermType
        {
            Ondemand,
            Shared,
            AnyReservationBooking,
            Spot,
            DedicatedHost,
            CapacityReservation
        }
    }
}

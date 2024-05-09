namespace Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.Utils
{
    using System;
    using System.Linq;

    public static class CapacityStorageUtility
    {
        private const string OfferCategoryPrefix = "Azure_MS-AZR-";

        private const string MooncakeOfferCategoryPrefix = "Azure_MS-MC-AZR-";

        public static string NormalizeResourceFamily(string resourceFamily)
        {
            if (resourceFamily != null)
            {
                resourceFamily = resourceFamily.Split(new char[1] { '.' }).Last();
            }

            GuardHelper.ArgumentNotNull(resourceFamily, nameof(resourceFamily));

            return NormalizeLetterOrDigitToUpperInvariant(resourceFamily);
        }

        public static string NormalizeCustomerSegment(string customerSegment)
        {
            return NormalizeLetterOrDigitToUpperInvariant(customerSegment);
        }

        public static string NormalizeCostCategory(string costCategory)
        {
            return NormalizeLetterOrDigitToUpperInvariant(costCategory);
        }

        public static string NormalizeOfferUnit(string offerUnit)
        {
            return NormalizeLetterOrDigitToUpperInvariant(offerUnit);
        }

        public static string NormalizeOfferFamily(string offerFamily)
        {
            return NormalizeLetterOrDigitToUpperInvariant(offerFamily);
        }

        public static string NormalizeOfferTerm(string offerTerm)
        {
            return NormalizeLetterOrDigitToUpperInvariant(offerTerm);
        }

        public static string NormalizeOfferCategory(string offerCategory)
        {
            return NormalizeToUpperRemoveWhiteSpace(offerCategory);
        }

        public static string NormalizeQuotaId(string quotaId)
        {
            return NormalizeToUpperRemoveWhiteSpace(quotaId);
        }

        public static string? ExtractNormalizedOfferCategory(string offerCategoryString)
        {
            if (string.IsNullOrWhiteSpace(offerCategoryString))
            {
                return null;
            }

            string[] array = offerCategoryString.Split(new char[1] { ';' });
            foreach (string text in array)
            {
                if (text.StartsWith("Azure_MS-AZR-", StringComparison.InvariantCultureIgnoreCase))
                {
                    return NormalizeOfferCategory(text);
                }

                if (text.StartsWith("Azure_MS-MC-AZR-", StringComparison.InvariantCultureIgnoreCase))
                {
                    return NormalizeOfferCategory(text);
                }
            }

            return null;
        }

        public static string NormalizeOperatingSystemType(string operatingSystemType)
        {
            return NormalizeLetterOrDigitToUpperInvariant(operatingSystemType);
        }

        public static string NormalizeReservationItemId(string reservationItemId)
        {
            return NormalizeLetterOrDigitToUpperInvariant(reservationItemId);
        }

        public static string? NormalizeAvailabilityZone(string zone)
        {
            return zone?.Trim();
        }

        public static string NormalizeStringToUpper(string abnormalString)
        {
            return NormalizeLetterOrDigitToUpperInvariant(abnormalString);
        }

        private static string NormalizeToUpperRemoveWhiteSpace(string value)
        {
            GuardHelper.ArgumentNotNull(value, nameof(value));

            return new string(value.Where((char c) => !char.IsWhiteSpace(c)).ToArray()).ToUpperInvariant();
        }


        private static string NormalizeLetterOrDigitToUpperInvariant(string value)
        {
            GuardHelper.ArgumentNotNull(value, nameof(value));

            return new string(value.Where((char c) => char.IsLetterOrDigit(c)).ToArray()).ToUpperInvariant();
        }
    }
}

namespace Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.Extensions
{
    using System;
    using System.Diagnostics.CodeAnalysis;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.Utils;

    /// <summary>
    /// StringExtensions
    /// </summary>
    public static class StringExtensions
    {
        /// <summary>
        /// Determines whether [contains ignore case] [the specified string].
        /// </summary>
        /// <param name="str">The string.</param>
        /// <param name="substring">The substring.</param>
        /// <returns>
        ///   <c>true</c> if [contains ignore case] [the specified string]; otherwise, <c>false</c>.
        /// </returns>
        public static bool ContainsIgnoreCase(this string str, string substring)
        {
            return str?.IndexOf(substring, StringComparison.OrdinalIgnoreCase) >= 0;
        }

        /// <summary>
        /// Determines whether [is valid unique identifier].
        /// </summary>
        /// <param name="str">The string.</param>
        /// <returns>
        ///   <c>true</c> if [is valid unique identifier] [the specified string]; otherwise, <c>false</c>.
        /// </returns>
        public static bool SafeIsValidGuid(this string str)
        {
            return Guid.TryParse(str, out _);
        }

        /// <summary>
        /// Determines whether [is ordinal match] [the specified second].
        /// </summary>
        /// <param name="first">The first.</param>
        /// <param name="second">The second.</param>
        /// <returns>
        ///   <c>true</c> if [is ordinal match] [the specified second]; otherwise, <c>false</c>.
        /// </returns>        
        public static bool IsOrdinalMatch(this string first, string second)
        {
            return string.Equals(first, second, StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Looks up given character in the input string and returns the occurrence count
        /// </summary>
        /// <param name="input">Input string</param>
        /// <param name="lookupChar">Char to be looked up</param>
        /// <returns>Occurrence count of the lookup character in the input string</returns>
        public static int CharacterOccurrenceCount(this string input, char lookupChar)
        {
            if (string.IsNullOrEmpty(input))
            {
                return 0;
            }

            var count = 0;
            foreach (var ch in input)
            {
                if (ch == lookupChar)
                {
                    count++;
                }
            }

            return count;
        }

        /// <summary>
        /// Splits the str and remove empty segments.
        /// </summary>
        /// <param name="str">The string.</param>
        /// <param name="separator">The separator.</param>
        public static string[] SplitAndRemoveEmpty(this string str, params char[] separator)
        {
            GuardHelper.ArgumentNotNull(str);
            GuardHelper.ArgumentNotNullOrEmpty(separator);

            return str.Split(separator, StringSplitOptions.RemoveEmptyEntries);
        }

        public static string FastSplitAndReturnFirst(this string str, char separator)
        {
            GuardHelper.ArgumentNotNull(str);

            var idx = str.IndexOf(separator);
            if (idx < 0)
            {
                return str;
            }

            return str.Substring(0, idx);
        }

        /// <summary>
        /// Shortens the string to the first n characters and adds ellipsis if shortened.
        /// </summary>
        /// <param name="str">The string.</param>
        /// <param name="numberOfCharacters">Desired max length.</param>
        /// <returns>A shortened string</returns>
        /// <remarks>
        /// TODO: 
        /// Get the framework convergence and then rid of these String concatinations by the programmatic string create pattern 
        /// Where you will create the buffer of [numberOfCharacters + 3] and fill the buffer with the characters per logic.
        /// This helper you need the most for the largest offender strings. So this has to be optimized well.
        /// </remarks>

        [return: NotNullIfNotNull(nameof(str))]
        public static string? TruncateWithEllipsis(this string str, int numberOfCharacters)
        {
            GuardHelper.IsArgumentPositive(numberOfCharacters);

            if (str == null || str.Length <= numberOfCharacters)
            {
                return str;
            }

            return str.Substring(0, numberOfCharacters) + "...";
        }
    }
}

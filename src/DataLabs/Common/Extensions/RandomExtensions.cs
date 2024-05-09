namespace Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.Extensions
{
    using System;

    /// <summary>
    /// Random extensions
    /// </summary>
    public static class RandomExtensions
    {
        /// <summary>
        /// Generates a random double in [minValue, maxValue).
        /// </summary>
        /// <param name="random">The random.</param>
        /// <param name="minValue">The minimum value.</param>
        /// <param name="maxValue">The maximum value.</param>
        public static double NextDouble(this Random random, double minValue, double maxValue)
        {
            return minValue + (random.NextDouble() * (maxValue - minValue));
        }
    }
}

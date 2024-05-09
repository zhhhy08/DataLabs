// <copyright file="MetricExtensions.cs" company="Microsoft Corporation">
//   Copyright (c) Microsoft Corporation. All rights reserved.
// </copyright>

namespace Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.ServiceMonitoring
{
    using System.Diagnostics;
    using System.Diagnostics.Metrics;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.Constants;

    public static class MetricExtensions
    {
        /// <summary>
        /// Increment success counter.
        /// </summary>
        /// <param name="counter">Counter.</param>
        /// <param name="dimensions">Dimensions.</param>
        public static void IncrementSuccessCounter(
            this Counter<long> counter, ref TagList dimensions)
        {
            dimensions.Add(SolutionConstants.IsFailed, false);
            counter.Add(1, dimensions);
        }

        /// <summary>
        /// Increment failure counter.
        /// </summary>
        /// <param name="counter">Counter.</param>
        /// <param name="dimensions">Dimensions.</param>
        public static void IncrementFailureCounter(
            this Counter<long> counter, ref TagList dimensions)
        {
            dimensions.Add(SolutionConstants.IsFailed, true);
            counter.Add(1, dimensions);
        }
    }
}
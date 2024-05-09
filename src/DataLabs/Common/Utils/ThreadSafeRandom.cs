namespace Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.Utils
{
    using System;
    using System.Collections.Generic;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.Extensions;

    /// <summary>
    /// Thread safe random.
    /// Taken as is from Stephen Toub's blog http://blogs.msdn.com/b/pfxteam/archive/2009/02/19/9434171.aspx
    /// </summary>
    public static class ThreadSafeRandom
    {
        #region Members

        [ThreadStatic]
        private static Random? _local;

        private static readonly Random Global = new Random();

        #endregion

        public static int Next()
        {
            return GetInstance().Next();
        }

        public static int Next(int maxValue)
        {
            return GetInstance().Next(maxValue);
        }

        public static int Next(int minValue, int maxValue)
        {
            return GetInstance().Next(minValue, maxValue);
        }

        public static double NextDouble()
        {
            return GetInstance().NextDouble();
        }

        public static double NextDouble(double minValue, double maxValue)
        {
            return GetInstance().NextDouble(minValue, maxValue);
        }

        public static T Choose<T>(IList<T> items)
        {
            var index = Next(0, items.Count);
            return items[index];
        }

        private static Random GetInstance()
        {
            var inst = _local;
            if (inst == null)
            {
                int seed;
                lock (Global)
                {
                    seed = Global.Next();
                }
                _local = inst = new Random(seed);
            }

            return inst;
        }
    }
}

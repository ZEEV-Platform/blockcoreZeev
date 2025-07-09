using System;
using System.Collections.Generic;
using System.Linq;

namespace Blockcore.Utilities
{
    /// <summary>
    /// Extension methods for IEnumerable interface.
    /// </summary>
    public static class LinqExtensions
    {
        /// <summary>
        /// Calculates a median value of a collection of long integers.
        /// </summary>
        /// <param name="source">Collection of numbers to count median of.</param>
        /// <returns>Median value, or 0 if the collection is empty.</returns>
        public static long Median(this IEnumerable<long> source)
        {
            int count = source.Count();
            if (count == 0)
                return 0;

            int midpoint = count / 2;
            IOrderedEnumerable<long> ordered = source.OrderBy(n => n);
            if ((count % 2) == 0)
                return (long)Math.Round(ordered.Skip(midpoint - 1).Take(2).Average());
            else
                return ordered.ElementAt(midpoint);
        }

        /// <summary>
        /// Calculates a median value of a collection of integers.
        /// </summary>
        /// <param name="source">Collection of numbers to count median of.</param>
        /// <returns>Median value, or 0 if the collection is empty.</returns>
        public static int Median(this IEnumerable<int> source)
        {
            int count = source.Count();
            if (count == 0)
                return 0;

            int midpoint = count / 2;
            IOrderedEnumerable<int> ordered = source.OrderBy(n => n);
            if ((count % 2) == 0)
                return (int)Math.Round(ordered.Skip(midpoint - 1).Take(2).Average());
            else
                return ordered.ElementAt(midpoint);
        }

        /// <summary>
        /// Calculates a median value of a collection of doubles.
        /// </summary>
        /// <param name="source">Collection of numbers to count median of.</param>
        /// <returns>Median value, or 0 if the collection is empty.</returns>
        public static double Median(this IEnumerable<double> source)
        {
            int count = source.Count();
            if (count == 0)
                return 0;

            int midpoint = count / 2;
            IOrderedEnumerable<double> ordered = source.OrderBy(n => n);
            if ((count % 2) == 0)
                return ordered.Skip(midpoint - 1).Take(2).Average();
            else
                return ordered.ElementAt(midpoint);
        }

        /// <summary>
        /// Calculates the median of a sequence of <see cref="decimal"/> values.
        /// </summary>
        /// <param name="source">A sequence of <see cref="decimal"/> values to calculate the median of.</param>
        /// <returns>The median of the sequence, or null if the source sequence is empty.</returns>
        /// <exception cref="ArgumentNullException">Thrown if the source is null.</exception>
        public static decimal Median(this IEnumerable<decimal> source)
        {
            int count = source.Count();
            if (count == 0)
                return 0;

            // Order the elements and convert to a list for indexed access
            var sortedList = source.OrderBy(n => n).ToList();
            int countList = sortedList.Count;
            int mid = countList / 2;

            if (countList % 2 == 0) // Even number of elements
            {
                // Median is the average of the two middle elements
                return (sortedList[mid - 1] + sortedList[mid]) / 2m; // Use 'm' for decimal literal
            }
            else // Odd number of elements
            {
                // Median is the middle element
                return sortedList[mid];
            }
        }
    }
}

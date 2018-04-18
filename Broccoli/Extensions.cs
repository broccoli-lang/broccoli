// STICK ALL YOUR EXTENSION METHODS IN HERE

using System.Linq;
using System.Collections.Generic;


namespace Broccoli {
    /// <summary>
    /// Generic extension methods.
    /// </summary>
    public static class Extensions {
        // Used for creating foreach-with-index loops, `foreach(var (value, index) in collection.WithIndex())`
        /// <summary>
        /// Returns a tuple of the value and index for each element in a collection.
        /// </summary>
        /// <param name="enumerable">The current collection.</param>
        /// <typeparam name="T">The type of the collection values.</typeparam>
        /// <returns>The collection values paired with their indices.</returns>
        public static IEnumerable<(T value, int index)>
            WithIndex<T>(this IEnumerable<T> enumerable) => enumerable.Select((v, i) => (v, i));

        // Because, as it turns out, python's `in` keyword is super neat.
        /// <summary>
        /// Imitates Python's `in` keyword and returns whether the current object is one of the given values.
        /// </summary>
        /// <param name="obj">The current object.</param>
        /// <param name="values">The values to check against.</param>
        /// <typeparam name="T">The type of the checking values.</typeparam>
        /// <returns>Whether the current object is one of the values.</returns>
        public static bool In<T>(this T obj, params T[] values) => values.Contains(obj);

        /// <summary>
        /// Imitates Python's `in` keyword and returns whether the current object is in the given collection.
        /// </summary>
        /// <param name="obj">The current object.</param>
        /// <param name="values">The collection to check against.</param>
        /// <typeparam name="T">The type of the collection values.</typeparam>
        /// <returns>Whether the current object is in the collection.</returns>
        public static bool In<T>(this T obj, IEnumerable<T> values) => values.Contains(obj);
    }
}

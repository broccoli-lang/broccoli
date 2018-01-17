// STICK ALL YOUR EXTENSION METHODS IN HERE

using System.Linq;
using System.Collections.Generic;


namespace Broccoli
{
    public static class Extensions {
        // Used for creating foreach-with-index loops, `foreach(var (value, index) in collection.WithIndex())`
        public static IEnumerable<(T value, int index)> WithIndex<T>(this IEnumerable<T> enumerable) {
            return enumerable.Select((v, i) => (v, i));
        }

        // Because, as it turns out, python's `in` keyword is super neat.
        public static bool In<T>(this T obj, params T[] values) {
            return values.Contains(obj);
        }
        public static bool In<T>(this T obj, IEnumerable<T> values) {
            return values.Contains(obj);
        }
    }
}
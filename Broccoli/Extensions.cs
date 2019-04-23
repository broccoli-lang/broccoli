// STICK ALL YOUR EXTENSION METHODS IN HERE

using System;
using System.Linq;
using System.Collections.Generic;
using System.Reflection;


namespace Broccoli {
    /// <summary>
    /// Generic extension methods.
    /// </summary>
    internal static class Extensions {
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

    /// <summary>
    /// Useful variants of type methods.
    /// </summary>
    internal static class TypeExtensions {
        /// <summary>
        /// Gets property of object.
        /// </summary>
        /// <param name="type">Type to get property from.</param>
        /// <param name="name">Name of property.</param>
        /// <returns>Property.</returns>
        /// <exception cref="Exception">Throws when property is not found.</exception>
        public static PropertyInfo TryGetProperty(this Type type, string name) => type.GetProperty(name) ?? throw new Exception($"Type '{type.FullName}' has no field '{name}'");

        /// <summary>
        /// Gets field of object.
        /// </summary>
        /// <param name="type">Type to get field from.</param>
        /// <param name="name">Name of field.</param>
        /// <returns>Field.</returns>
        /// <exception cref="Exception">Throws when field is not found.</exception>
        public static FieldInfo TryGetField(this Type type, string name)  => type.GetField(name) ?? throw new Exception($"Type '{type.FullName}' has no field '{name}'");
    }

    /// <summary>
    /// Dictionary extension methods.
    /// </summary>
    internal static class DictionaryExtensions {
        /// <summary>
        /// Adds all the keys + values of a dictionary to the current dictionary.
        /// </summary>
        /// <param name="self">The current dictionary.</param>
        /// <param name="other">The other dictionary whose values to add.</param>
        /// <param name="overwrite">Whether or not to overwrite the value when inserting duplicate keys.</param>
        /// <typeparam name="K">The key type.</typeparam>
        /// <typeparam name="V">The value type.</typeparam>
        /// <returns>The current dictionary.</returns>
        public static Dictionary<K, V> Extend<K, V>(this Dictionary<K, V> self, Dictionary<K, V> other, bool overwrite = false) {
            foreach (var (key, value) in other)
                if (overwrite || !self.ContainsKey(key))
                    self[key] = value;
            return self;
        }

        public static Dictionary<K, V> Alias<K, V>(this Dictionary<K, V> self, K key, params K[] newKeys) {
            var value = self[key];
            foreach (var newKey in newKeys)
                self[newKey] = value;
            return self;
        }
    }
}

using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;

namespace EMA.MaterialDesignInXAMLExtender
{
    /// <summary>
    /// Provides extension methods for the <see cref="Type"/> class.
    /// </summary>
    public static class CollectionExtensions
    {
        /// <summary>
        /// Gets the generic underlying type of a collection.
        /// </summary>
        /// <param name="enumerable">A collection to be processed.</param>
        /// <returns>The first degree type of the collection.</returns>
        public static Type GetGenericType(this IEnumerable enumerable)
        {
            if (enumerable != null)
                foreach (var interfacetype in enumerable.GetType().GetInterfaces())
                    if (interfacetype.IsGenericType
                        && interfacetype.GetGenericTypeDefinition() == typeof(IEnumerable<>)
                        && interfacetype.GetGenericArguments().Length > 0)
                        return interfacetype.GetGenericArguments()[0];
            return null;
        }

        /// <summary>
        /// Indicates if the underlying generic type of a collection implements <see cref="INotifyPropertyChanged"/>.
        /// </summary>
        /// <param name="enumerable">A collection to be processed.</param>
        /// <returns>True if items implements <see cref="INotifyPropertyChanged"/>, false otherwise
        /// or if no items or underlying generic type.</returns>
        public static bool AreItemsINotifyPropertyChanged(this IEnumerable enumerable)
        {
            var type = enumerable?.GetGenericType();
            if (type != null)
                return type.GetInterfaces().Any(x => x == typeof(INotifyPropertyChanged));
            return false;
        }
    }
}
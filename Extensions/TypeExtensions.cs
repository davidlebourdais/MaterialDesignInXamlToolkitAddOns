using System;
using System.Reflection;

namespace EMA.MaterialDesignInXAMLExtender.Utils
{
    /// <summary>
    /// Performs additional operation on <see cref="Type"/> objects.
    /// </summary>
    public static class TypeExtensions
    {
        private static readonly MethodInfo GetDefaultGenericAccess = typeof(TypeExtensions).GetRuntimeMethod(nameof(GetDefaultGeneric), new Type[] { }); // stores access to GetDefaultGeneric.

        /// <summary>
        /// Gets default value of a passed type using Reflection.
        /// </summary>
        /// <param name="type">The type from which to get default value.</param>
        /// <returns>The default value matching the type.</returns>
        public static object GetDefault(this Type type) => GetDefaultGenericAccess.MakeGenericMethod(type).Invoke(null, null);

        /// <summary>
        /// Gets default value of a passed generic type.
        /// </summary>
        /// <typeparam name="T">The generic type to get default value from.</typeparam>
        /// <returns>The default value of the generic type.</returns>
        public static T GetDefaultGeneric<T>() => default;
    }
}

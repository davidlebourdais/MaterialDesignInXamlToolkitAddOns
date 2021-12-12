using System;
using System.ComponentModel;
using System.Linq;
using System.Windows;

namespace MaterialDesignThemes.Wpf.AddOns.Extensions
{
    /// <summary>
    /// Extensions for <see cref="FrameworkElement"/> objects.
    /// </summary>
    public static class DependencyObjectExtensions
    {
        /// <summary>
        /// Gets all dependency properties of a <see cref="DependencyObject"/>.
        /// </summary>
        /// <param name="obj">Source object.</param>
        /// <returns>An array of all dependency properties for the object.</returns>
        public static DependencyProperty[] GetDependencyProperties(this DependencyObject obj)
        {
            return (from PropertyDescriptor pd in TypeDescriptor.GetProperties(obj, new Attribute[] {new PropertyFilterAttribute(PropertyFilterOptions.All)})
                    select DependencyPropertyDescriptor.FromProperty(pd)
                    into dpd
                    where dpd != null
                    select dpd.DependencyProperty).ToArray();
        }

        /// <summary>
        /// Gets all writeable dependency properties of a <see cref="DependencyObject"/>.
        /// </summary>
        /// <param name="obj">Source object.</param>
        /// <returns>An array of all non-readonly dependency properties for the object.</returns>
        public static DependencyProperty[] GetNonReadOnlyDependencyProperties(this DependencyObject obj)
            => GetDependencyProperties(obj).Where(x => x.ReadOnly == false).ToArray();
    }
}

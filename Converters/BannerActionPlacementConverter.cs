using System;
using System.Globalization;
using System.Windows.Controls;
using System.Windows.Data;
using MaterialDesignThemes.Wpf;

namespace EMA.MaterialDesignInXAMLExtender.Converters
{
    /// <summary>
    /// Output suitable dockpanel positionning for the action buttons.
    /// </summary>
    public class BannerActionPlacementConverter : IMultiValueConverter
    {
        /// <summary>
        /// Converts a set of input values into a <see cref="Dock"/> placement.
        /// </summary>
        /// <param name="values">Different input values, see code for meaning.</param>
        /// <param name="targetType">Unused.</param>
        /// <param name="parameter">Unused.</param>
        /// <param name="culture">Unused.</param>
        /// <returns>A suitable <see cref="Dock"/> placement for the action buttons.</returns>
        public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
        {
            var mode = (SnackbarActionButtonPlacementMode)values[0];
            var inlineMaxHeight = (double)values[1];
            var actualHeight = (double)values[2];
            var all_fits_inline = false;
            if (values.Length > 2)
            {
                var containerWidth = (double)values[3];
                var contentWidth = (double)values[4];
                var buttonsWidth = (double)values[5];
                all_fits_inline = containerWidth - contentWidth - buttonsWidth > 0;
            }

            if (mode == SnackbarActionButtonPlacementMode.Auto)
                return (actualHeight <= inlineMaxHeight || all_fits_inline) ? Dock.Right : Dock.Bottom;
            else if (mode == SnackbarActionButtonPlacementMode.SeparateLine)
                return Dock.Bottom;
            return Dock.Right;
        }

        /// <summary>
        /// Unimplemented convert back method.
        /// </summary>
        /// <param name="value">Unused.</param>
        /// <param name="targetTypes">Unused.</param>
        /// <param name="parameter">Unused.</param>
        /// <param name="culture">Unused.</param>
        /// <returns>Nothing.</returns>
        public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture) => throw new InvalidOperationException();
    }
}
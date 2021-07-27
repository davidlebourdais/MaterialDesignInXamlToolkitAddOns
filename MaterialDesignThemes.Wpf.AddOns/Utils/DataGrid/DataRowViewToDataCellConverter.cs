using System;
using System.Data;
using System.Globalization;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Markup;

namespace MaterialDesignThemes.Wpf.AddOns.Utils.DataGrid
{
    /// <summary>
    /// Extracts a DataContext object from a given <see cref="DataGridCell"/>.
    /// </summary>
    public class DataGridCellToDataContextConverter : MarkupExtension, IValueConverter
    {
        /// <summary>
        /// Extracts a DataContext from a given <see cref="DataGridCell"/> item.
        /// </summary>
        /// <param name="value">A <see cref="DataGridCell"/> item to process.</param>
        /// <param name="targetType">Unused.</param>
        /// <param name="parameter">Unused.</param>
        /// <param name="culture">Unused.</param>
        /// <returns>The data value contained in the DataGrid cell.</returns>
        /// <exception cref="IndexOutOfRangeException">Thrown if cell structure is not well defined.</exception>
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (!(value is DataGridCell cell) 
                || !(cell.Content is ContentPresenter cp)
                || !(cp.Content is DataRowView drv))
                return null;
            
            var index = cell.Column.DisplayIndex;
            if (index < 0 || drv.Row.ItemArray.Length <= index)
                throw new IndexOutOfRangeException("Invalid index found in " + nameof(DataGridCellToDataContextConverter));

            return drv.Row.ItemArray[index];
        }

        /// <summary>
        /// Unsupported conversion method.
        /// </summary>
        /// <param name="value">Unused.</param>
        /// <param name="targetType">Unused.</param>
        /// <param name="parameter">Unused.</param>
        /// <param name="culture">Unused.</param>
        /// <returns>Nothing.</returns>
        /// <exception cref="NotSupportedException">Thrown if this method is called.</exception>
        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotSupportedException();
        }

        /// <summary>
        /// Returns an object that is provided as the value of the target property for this markup extension
        /// </summary>
        /// <param name="serviceProvider">A service provider helper that can provide services for the markup extension.</param>
        /// <returns>The object value to set on the property where the extension is applied.</returns>
        public override object ProvideValue(IServiceProvider serviceProvider)
        {
            return this;
        }
    }
}

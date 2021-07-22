using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using EMA.ExtendedWPFVisualTreeHelper;

namespace MaterialDesignThemes.Wpf.AddOns.Extensions
{
    /// <summary>
    /// Provides extension methods for the <see cref="DataGrid"/> class.
    /// </summary>
    public static class DataGridExtensions
    {
        /// <summary>
        /// Returns the <see cref="DataGridCell"/> from a <see cref="DataGrid"/>
        /// </summary>
        /// <param name="dataGrid">The DataGrid on which to find the cell.</param>
        /// <param name="rowIndex">The raw row index where to find the cell.</param>
        /// <param name="columnIndex">The raw column index where to find the cell.</param>
        /// <returns>A corresponding <see cref="DataGridCell"/>, or null if not found at coordinates.</returns>
        /// <remarks>Adapted from https://stackoverflow.com/questions/1755799/beginedit-of-a-specific-cell-from-code-behind </remarks>
        public static DataGridCell GetDataGridCell(this DataGrid dataGrid, int rowIndex, int columnIndex)
        {
            DataGridCell cell;

            var rowContainer = dataGrid.GetDataGridRow(rowIndex);
            if (rowContainer == null)
                return null; // will be null if nothing found.
            
            var presenter = rowContainer.FindChild<DataGridCellsPresenter>();

            if ((cell = (DataGridCell)presenter.ItemContainerGenerator.ContainerFromIndex(columnIndex)) != null)
                return cell;
            
            // May be virtualized, bring into view and try again:
            dataGrid.ScrollIntoView(rowContainer, dataGrid.Columns[columnIndex]);
            cell = (DataGridCell)presenter.ItemContainerGenerator.ContainerFromIndex(columnIndex);

            return cell;  // will be null if nothing found.
        }

        /// <summary>
        /// Gets a <see cref="DataGridRow"/> from a <see cref="DataGrid"/>.
        /// </summary>
        /// <param name="dataGrid">The DataGrid on which to find the row.</param>
        /// <param name="index">The index where to find the row.</param>
        /// <returns>A corresponding <see cref="DataGridRow"/> or null if not found.</returns>
        /// <remarks>Adapted from https://stackoverflow.com/questions/1755799/beginedit-of-a-specific-cell-from-code-behind</remarks>
        private static DataGridRow GetDataGridRow(this DataGrid dataGrid, int index)
        {
            var row = (DataGridRow)dataGrid.ItemContainerGenerator.ContainerFromIndex(index);
            if (row != null)
                return row;
            
            // May be virtualized, bring into view and try again:
            dataGrid.ScrollIntoView(dataGrid.Items[index]);
            row = (DataGridRow)dataGrid.ItemContainerGenerator.ContainerFromIndex(index);
            return row;
        }
    }
}
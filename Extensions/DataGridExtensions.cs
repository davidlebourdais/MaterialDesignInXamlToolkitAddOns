using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using EMA.ExtendedWPFVisualTreeHelper;

namespace EMA.MaterialDesignInXAMLExtender
{
    /// <summary>
    /// Provides extension methods for the <see cref="DataGrid"/> class.
    /// </summary>
    public static class DataGridExtensions
    {
        /// <summary>
        /// Returns the <see cref="DataGridCell"/> from a <see cref="DataGrid"/>
        /// </summary>
        /// <param name="datagrid">The datagrid on which to find the cell.</param>
        /// <param name="row_index">The raw row index where to find the cell.</param>
        /// <param name="column_index">The raw column index where to find the cell.</param>
        /// <returns>A corresponding <see cref="DataGridCell"/>, or null if not found at coordinates.</returns>
        /// <remarks>Adapted from https://stackoverflow.com/questions/1755799/beginedit-of-a-specific-cell-from-code-behind </remarks>
        public static DataGridCell GetDataGridCell(this DataGrid datagrid, int row_index, int column_index)
        {
            var cell = (DataGridCell)null;

            var rowContainer = datagrid.GetDataGridRow(row_index);
            if (rowContainer != null)
            {
                var presenter = rowContainer.FindChild<DataGridCellsPresenter>();

                if ((cell = (DataGridCell)presenter.ItemContainerGenerator.ContainerFromIndex(column_index)) == null)
                {
                    // May be virtualized, bring into view and try again:
                    datagrid.ScrollIntoView(rowContainer, datagrid.Columns[column_index]);
                    cell = (DataGridCell)presenter.ItemContainerGenerator.ContainerFromIndex(column_index);
                }
            }

            return cell;  // will be null if nothing found.
        }

        /// <summary>
        /// Gets a <see cref="DataGridRow"/> from a <see cref="DataGrid"/>.
        /// </summary>
        /// <param name="datagrid">The datagrid on which to find the row.</param>
        /// <param name="index">The index where to find the row.</param>
        /// <returns>A corresponding <see cref="DataGridRow"/> or null if not found.</returns>
        /// <remarks>Adapted from https://stackoverflow.com/questions/1755799/beginedit-of-a-specific-cell-from-code-behind </remarks>
        public static DataGridRow GetDataGridRow(this DataGrid datagrid, int index)
        {
            var row = (DataGridRow)datagrid.ItemContainerGenerator.ContainerFromIndex(index);
            if (row == null)
            {
                // May be virtualized, bring into view and try again:
                datagrid.ScrollIntoView(datagrid.Items[index]);
                row = (DataGridRow)datagrid.ItemContainerGenerator.ContainerFromIndex(index);
            }
            return row;
        }
    }
}
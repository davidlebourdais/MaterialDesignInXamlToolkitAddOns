using System;
using System.Data;
using System.Windows.Controls;

namespace MaterialDesignThemes.Wpf.AddOns
{
    /// <summary>
    /// Used to enhance <see cref="DataGridTemplateColumn"/> by offering additional
    /// properties like column name and changing some methods like OnCopyingCellClipboardContent.
    /// </summary>
    /// <remarks>Used with <see cref="PagedDataGrid"/>.</remarks>
    public class PagedDataGridTemplateColumn : DataGridTemplateColumn
    {
        /// <summary>
        /// Gets a value indicating if the column
        /// if used to display unique IDs. 
        /// </summary>
        private bool IsIDsColumn { get; }

        /// <summary>
        /// Gets a value indicating if the column
        /// if used to display line selection boxes.
        /// </summary>
        private bool IsSelectColumn { get; }

        /// <summary>
        /// Initiates a new instance of <see cref="PagedDataGridTemplateColumn"/>.
        /// </summary>
        /// <param name="asIdColumn">Set this if this column is used to show IDs.</param>
        /// <param name="asSelectColumn">Set this if this column is used to show line selection boxes.</param>
        public PagedDataGridTemplateColumn(bool asIdColumn = false, bool asSelectColumn = false)
        {
            IsIDsColumn = asIdColumn;
            IsSelectColumn = asSelectColumn;
            if (IsIDsColumn && IsSelectColumn) throw new Exception("Column cannot be ID and selection at the same time");
        }

        /// <summary>
        /// Overrides the default <see cref="DataGridTemplateColumn"/> behavior
        /// of this method to retrieve value from any "ToString()" computations.
        /// </summary>
        /// <param name="item">A <see cref="DataRowView"/> object passed by the framework.</param>
        /// <returns>A string representation of the given row object.</returns>
        public override object OnCopyingCellClipboardContent(object item)
        {
            if (item is DataRowView dataRowView 
                && DisplayIndex >= 0 
                && dataRowView.Row.ItemArray.Length > DisplayIndex)
                return dataRowView.Row.ItemArray[DisplayIndex].ToString();
            else return base.OnCopyingCellClipboardContent(item);
        }
    }
}
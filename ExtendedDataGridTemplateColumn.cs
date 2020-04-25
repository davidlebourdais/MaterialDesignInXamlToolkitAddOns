using System;
using System.Data;
using System.Windows;
using System.Windows.Controls;

namespace EMA.MaterialDesignInXAMLExtender
{
    /// <summary>
    /// Used to enhance <see cref="DataGridTemplateColumn"/> by oferring additional
    /// properties like column name and changing some methods like OnCopyingCellClipboardContent.
    /// </summary>
    /// <remarks>Used with <see cref="ExtendedDataGrid"/>.</remarks>
    public class ExtendedDataGridTemplateColumn : DataGridTemplateColumn
    {
        /// <summary>
        /// Gets the name associated to the column.
        /// </summary>
        public string ColumnName { get; }

        /// <summary>
        /// Gets a value indicating if the column
        /// if used to display unique IDs. 
        /// </summary>
        public bool IsIDsColumn { get; }

        /// <summary>
        /// Gets a value indicating if the column
        /// if used to display line selection boxes.
        /// </summary>
        public bool IsSelectColumn { get; }

        /// <summary>
        /// Initiates a new instance of <see cref="ExtendedDataGridTemplateColumn"/>.
        /// </summary>
        /// <param name="column_name">The name of the column.</param>
        /// <param name="as_id_column">Set this if this column is used to show IDs.</param>
        /// <param name="as_select_column">Set this if this column is used to show line selection boxes.</param>
        public ExtendedDataGridTemplateColumn(string column_name, bool as_id_column = false, bool as_select_column = false)
        {
            ColumnName = column_name;
            IsIDsColumn = as_id_column;
            IsSelectColumn = as_select_column;
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
            if (item is DataRowView datarowview 
                && DisplayIndex >= 0 
                && datarowview.Row.ItemArray.Length > DisplayIndex)
                return datarowview.Row.ItemArray[DisplayIndex].ToString();
            else return base.OnCopyingCellClipboardContent(item);
        }
 
        //protected override System.Windows.FrameworkElement GenerateElement(DataGridCell cell, object dataItem)
        //{
        //    // The DataGridTemplateColumn uses ContentPresenter with your DataTemplate.
        //    ContentPresenter cp = (ContentPresenter)base.GenerateElement(cell, dataItem);
        //    // Reset the Binding to the specific column. The default binding is to the DataRowView.
        //    BindingOperations.SetBinding(cp, ContentPresenter.ContentProperty, new Binding(this.Columname));
        //    //BindingOperations.SetBinding(cp, ContentPresenter.DataContextProperty, new Binding(row[Columname]));
        //    return cp;
        //}
    }
}
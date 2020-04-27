﻿using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Data;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using EMA.MaterialDesignInXAMLExtender.Utils;

namespace EMA.MaterialDesignInXAMLExtender
{
    /// <summary>
    /// This control extends conventional datagrid so it displays features
    /// that helps it match the material design specifications.
    /// </summary>
    public class ExtendedDataGrid : DataGrid
    {
        #region Private attributes and properties
        private bool items_source_inner_change;               // disables reentrancy when ItemsSource changes
        private bool can_add_rows_inner_change;               // disables reentrancy when CanUserAddRows changes
        private readonly DataTablePagingManager PagedTable;   // paging manager that will be used to split raw source into pages when required
        private IList<bool> CheckMarksValuesBackup;           // stores a backup of selectors value for control initialization.
        private bool can_user_add_row_cache;                  // stores a backup of the CanUserAddRows property
        private bool source_is_readonly;                      // stores a value indicating if source is readonly

        /// <summary>
        /// Determines if paging system should be used instead of raw source.
        /// </summary>
        private bool UsesPagingInternal => UsesPaging || ShowsCheckMarks || ShowsIDs || ForceBackgroundLoading || PagedTable.AreSourceItemsDynamic;
        #endregion

        #region Constructors
        /// <summary>
        /// Creates a new instance of <see cref="ExtendedDataGrid"/>.
        /// </summary>
        public ExtendedDataGrid()
        {
            // Build paging manager to be used:
            PagedTable = new DataTablePagingManager();
            PagedTable.PageChanged += PagedTable_PageChanged;
            PagedTable.CheckMarkedRowsChanged += PagedTable_CheckMarkedRowsChanged;
            PagedTable.PageLoading += (s, e) => IsPageLoading = true;
            PagedTable.PageLoaded += (s, e) => IsPageLoading = false;

            // Build inner commands:
            GoToNextPageCommand = new SimpleCommand(() => PagedTable.GoNext(), () => PagedTable.HasNext());
            GoToPreviousPageCommand = new SimpleCommand(() => PagedTable.GoPrevious(), () => PagedTable.HasPrevious());
            GoToFirstPageCommand = new SimpleCommand(() => PagedTable.GoFirst(), () => !PagedTable.CurrentPageIsFirstPage);
            GoToLastPageCommand = new SimpleCommand(() => PagedTable.GoLast(), () => !PagedTable.CurrentPageIsLastPage);

            Loaded += ExtendedDataGrid_Loaded;
        }

        /// <summary>
        /// Occurs when the control is loaded and bindings are resolved.
        /// </summary>
        /// <param name="sender">Unused.</param>
        /// <param name="e">unused.</param>
        private void ExtendedDataGrid_Loaded(object sender, RoutedEventArgs e)
        {
            Loaded -= ExtendedDataGrid_Loaded;

            // Reevaluate selected values if set
            // to other than default and control is loaded
            // (i.e. source bindings are resolved):
            PagedTable.SetRowCheckMarksValues(CheckMarksValuesBackup);
            CheckMarksValuesBackup = null;
        }

        /// <summary>
        /// Static constructor for <see cref="ExtendedDataGrid"/> type.
        /// Override some base dependency properties.
        /// </summary>
        static ExtendedDataGrid()
        {
            DefaultStyleKeyProperty.OverrideMetadata(typeof(ExtendedDataGrid), new FrameworkPropertyMetadata(typeof(ExtendedDataGrid)));
            ItemsControl.ItemsSourceProperty.OverrideMetadata(typeof(ExtendedDataGrid), new FrameworkPropertyMetadata(null, ItemsSourcePropertyChanged));
            DataGrid.CanUserAddRowsProperty.OverrideMetadata(typeof(ExtendedDataGrid), new FrameworkPropertyMetadata(false, CanUserAddRowsPropertyChanged));
        }
        #endregion

        #region Core properties and methods
        /// <summary>
        /// Called whenever the <see cref="ItemsControl.ItemsSource"/> property changes.
        /// </summary>
        /// <param name="sender">The object whose property changed.</param>
        /// <param name="args">Information about the property change.</param>
        public static void ItemsSourcePropertyChanged(DependencyObject sender, DependencyPropertyChangedEventArgs args)
        {
            if (sender is ExtendedDataGrid casted)
            {
                var newSource = args.NewValue as IEnumerable<object>;
                if (args.NewValue == null || newSource != null)  // 'pure' null value accepted.
                {
                    if (!casted.items_source_inner_change)
                    {
                        casted.PagedTable.SetSource(newSource);
                        casted.ProcessCanUserAddRowsForNewSource(newSource);
                    }
                }
            }
        }

        /// <summary>
        /// Occurs whenever paged table page changes.
        /// Allows to internally update every related properties.
        /// </summary>
        /// <param name="sender">Unused.</param>
        /// <param name="e">Unused.</param>
        /// <remarks>Core method, syncs with page manager.</remarks>
        private void PagedTable_PageChanged(object sender, EventArgs e)
        {
            // Update properties:
            (GoToNextPageCommand as SimpleCommand).RaiseCanExecuteChanged();
            (GoToPreviousPageCommand as SimpleCommand).RaiseCanExecuteChanged();

            CurrentPageIndex = (uint)PagedTable.CurrentPageIndex;
            CheckMarksColumnIndex = PagedTable.CheckMarksColumnPosition;
            IDsColumnIndex = PagedTable.IndexesColumnPosition;
            DisplayedIDs = new ReadOnlyCollection<int>(PagedTable.Indexes);

            ProcessCanUserAddRows();
            UpdateCurrentVisibleItemRangeText();

            // Update datagrid source items if required:
            if (UsesPagingInternal)
            {
                items_source_inner_change = true;
                CancelEdit();
                Columns.Clear();
                this.SetCurrentValue(ItemsSourceProperty, PagedTable.CurrentPage.DefaultView);
                items_source_inner_change = false;
            }
        }

        /// <summary>
        /// Called whenever the checkmark selection list changes.
        /// </summary>
        /// <param name="sender">Unused.</param>
        /// <param name="e">Unused.</param>
        private void PagedTable_CheckMarkedRowsChanged(object sender, EventArgs e)
        {
            // Update checkmarks:
            CheckMarkedItems = PagedTable.CheckMarkedRows;
            SelectAllRowsIsTristated = PagedTable.AllRowsCheckMarkState == null;
            SelectAllRowsState = PagedTable.AllRowsCheckMarkState;
            if (CheckMarksValuesBackup == null)  // update only if there is no backup ready to be injected when control loads.
                CheckMarksValues = PagedTable.RowCheckMarks.Select(x => x.IsChecked).ToList();
        }

        /// <summary>
        /// Called at datagrid column generation.
        /// </summary>
        /// <param name="e">Event info.</param>
        protected override void OnAutoGeneratingColumn(DataGridAutoGeneratingColumnEventArgs e)
        {
            var index = Columns.Count;
            var is_select = ShowsCheckMarks && index == CheckMarksColumnIndex;
            var is_id = ShowsIDs && index == IDsColumnIndex;

            // Cancel only when necessary:
            if (is_select || is_id
                || ColumnHeaderStyle != null || ColumnHeaderTemplate != null || ColumnHeaderTemplateSelector != null
               || CellStyle != null || CellTemplate != null || CellTemplateSelector != null
               || CellEditingTemplate != null || CellEditingTemplateSelector != null)
            {
                e.Column = new ExtendedDataGridTemplateColumn(e.PropertyName, is_id, is_select)
                {
                    Header = e.Column.Header,
                    SortMemberPath = e.PropertyName,
                    HeaderStyle =
                        is_select ? CheckMarksColumnHeaderStyle :
                        is_id ? IDsColumnHeaderStyle :
                        ColumnHeaderStyle,
                    HeaderTemplate =
                        is_select ? CheckMarksColumnHeaderTemplate :
                        is_id ? IDsColumnHeaderTemplate :
                        ColumnHeaderTemplate,
                    HeaderTemplateSelector =
                        is_select ? CheckMarksColumnHeaderTemplateSelector :
                        is_id ? IDsColumnHeaderTemplateSelector :
                        ColumnHeaderTemplateSelector,
                    CellStyle =
                        is_select ? CheckMarkCellStyle :
                        is_id ? IDCellStyle :
                        CellStyle,
                    CellTemplate =
                        is_select ? CheckMarkCellTemplate :
                        is_id ? IDCellTemplate :
                        CellTemplate,
                    CellTemplateSelector =
                        is_select ? CheckMarkCellTemplateSelector :
                        is_id ? IDCellTemplateSelector :
                        CellTemplateSelector,
                    CellEditingTemplate =
                        is_select ? CheckMarkCellEditingTemplate :
                        CellEditingTemplate,
                    CellEditingTemplateSelector =
                        is_select ? CheckMarkCellEditingTemplateSelector :
                        CellEditingTemplateSelector,
                    IsReadOnly = is_id,
                    CanUserSort = CanUserSortColumns,
                    CanUserReorder = CanUserReorderIDsAndCheckMarksColumns || (!is_id && !is_select && CanUserReorderColumns)
                };
            }
            else base.OnAutoGeneratingColumn(e);
        }
        #endregion

        #region CanUserAddRows property override
        /// <summary>
        /// Called whenever the <see cref="DataGrid.CanUserAddRows"/> property changes.
        /// </summary>
        /// <param name="sender">The object whose property changed.</param>
        /// <param name="args">Information about the property change.</param>
        public static void CanUserAddRowsPropertyChanged(DependencyObject sender, DependencyPropertyChangedEventArgs args)
        {
            if (sender is ExtendedDataGrid casted)
                if (!casted.can_add_rows_inner_change)  // store backup when user change value.
                    casted.can_user_add_row_cache = args.NewValue as bool? == true;
        }

        /// <summary>
        /// Determines the <see cref="DataGrid.CanUserAddRows"/> property depending
        /// on the source type (if writteable or not).
        /// </summary>
        /// <param name="newSource">A reference to the newly set source.</param>
        private void ProcessCanUserAddRowsForNewSource(IEnumerable<object> newSource = null)
        {
            // If has a new source in writable mode:
            if (newSource != null && newSource is IList list && !list.IsReadOnly)
                source_is_readonly = false;
            // If has new source that is not writteable:
            else
                source_is_readonly = true;

            ProcessCanUserAddRows();
        }

        /// <summary>
        /// Determines the <see cref="DataGrid.CanUserAddRows"/> property depending
        /// on various internal factor related to paging and source typ.
        /// </summary>
        private void ProcessCanUserAddRows()
        {
            // Can add items only if at last page:
            if (!CanUserAddRows && can_user_add_row_cache && PagedTable.CurrentPageIsLastPage && !source_is_readonly)
                CanUserAddRows = true;
            else if (CanUserAddRows && (!PagedTable.CurrentPageIsLastPage || source_is_readonly))
            {
                can_add_rows_inner_change = true;
                CanUserAddRows = false;
                can_add_rows_inner_change = false;
            }
        }
        #endregion

        #region Commands related to page navigation
        /// <summary>
        /// Gets the command to reach next page.
        /// </summary>
        public ICommand GoToNextPageCommand { get; }

        /// <summary>
        /// Gets the command to reach previous page.
        /// </summary>
        public ICommand GoToPreviousPageCommand { get; }

        /// <summary>
        /// Gets the command to reach first page.
        /// </summary>
        public ICommand GoToFirstPageCommand { get; }

        /// <summary>
        /// Gets the command to reach last page.
        /// </summary>
        public ICommand GoToLastPageCommand { get; }
        #endregion

        #region General properties
        /// <summary>
        /// Gets or sets the corner radius to be applied to the control.
        /// </summary>
        public CornerRadius CornerRadius
        {
            get => (CornerRadius)GetValue(CornerRadiusProperty);
            set => SetCurrentValue(CornerRadiusProperty, value);
        }
        /// <summary>
        /// Registers <see cref="CornerRadius"/> as a dependency property.
        /// </summary>
        public static readonly DependencyProperty CornerRadiusProperty
            = DependencyProperty.Register(nameof(CornerRadius), typeof(CornerRadius), typeof(ExtendedDataGrid), new FrameworkPropertyMetadata(default(CornerRadius)));

        /// <summary>
        /// Gets or sets a value indicating if column headers must be shown.
        /// </summary>
        /// <remarks>Will partly (on column header visibility) override the <see cref="DataGrid.HeadersVisibility"/> property.</remarks>
        public bool ShowsHeaders
        {
            get => (bool)GetValue(ShowsHeadersProperty);
            set => SetCurrentValue(ShowsHeadersProperty, value);
        }
        /// <summary>
        /// Registers <see cref="ShowsHeadersProperty"/> as a dependency property.
        /// </summary>
        public static readonly DependencyProperty ShowsHeadersProperty
            = DependencyProperty.Register(nameof(ShowsHeaders), typeof(bool), typeof(ExtendedDataGrid), new FrameworkPropertyMetadata(true, ShowsHeadersChanged));

        /// <summary>
        /// Called whenever the <see cref="ShowsHeaders"/> property changes.
        /// </summary>
        /// <param name="sender">The object whose property changed.</param>
        /// <param name="args">Information about the property change.</param>
        public static void ShowsHeadersChanged(DependencyObject sender, DependencyPropertyChangedEventArgs args)
        {
            if (sender is ExtendedDataGrid casted && args.NewValue is bool new_value)
            {
                if (new_value)
                {
                    if (casted.HeadersVisibility == DataGridHeadersVisibility.Row)
                        casted.HeadersVisibility = DataGridHeadersVisibility.All;
                    else if (casted.HeadersVisibility == DataGridHeadersVisibility.None)
                        casted.HeadersVisibility = DataGridHeadersVisibility.Column;
                }
                else
                {
                    if (casted.HeadersVisibility == DataGridHeadersVisibility.Column)
                        casted.HeadersVisibility = DataGridHeadersVisibility.None;
                    else if (casted.HeadersVisibility == DataGridHeadersVisibility.All)
                        casted.HeadersVisibility = DataGridHeadersVisibility.Row;
                }
            }
        }     

        /// <summary>
        /// Gets or sets a value indicating if paging is activated.
        /// </summary>
        public bool UsesPaging
        {
            get => (bool)GetValue(UsesPagingProperty);
            set => SetCurrentValue(UsesPagingProperty, value);
        }
        /// <summary>
        /// Registers <see cref="UsesPagingProperty"/> as a dependency property.
        /// </summary>
        public static readonly DependencyProperty UsesPagingProperty
            = DependencyProperty.Register(nameof(UsesPaging), typeof(bool), typeof(ExtendedDataGrid), new FrameworkPropertyMetadata(true, UsesPagingChanged));

        /// <summary>
        /// Called whenever the <see cref="UsesPaging"/> property changes.
        /// </summary>
        /// <param name="sender">The object whose property changed.</param>
        /// <param name="args">Information about the property change.</param>
        public static void UsesPagingChanged(DependencyObject sender, DependencyPropertyChangedEventArgs args)
        {
            if (sender is ExtendedDataGrid casted && args.NewValue is bool new_value)
            {
                casted.CancelEdit();

                if (!new_value)
                {
                    casted.PagedTable.PagingEnabled = false;
                    casted.PagingOptionsVisibility = Visibility.Collapsed;
               
                    casted.InvalidateProperty(ItemsSourceProperty);  // will force property to reevaluate and tie to source without going though pages.
                }
                else
                {
                    casted.PagedTable.PagingEnabled = true;
                    casted.PagingOptionsVisibility = Visibility.Visible;
                    casted.InvalidateProperty(ItemsSourceProperty);
                }
            }
        }

        /// <summary>
        /// Gets the visibility of the paging options.
        /// </summary>
        public Visibility PagingOptionsVisibility
        {
            get => (Visibility)GetValue(PagingOptionsVisibilityProperty);
            protected set => SetValue(PagingOptionsVisibilityPropertyKey, value);
        }
        private static readonly DependencyPropertyKey PagingOptionsVisibilityPropertyKey =
            DependencyProperty.RegisterReadOnly(nameof(PagingOptionsVisibility), typeof(Visibility), typeof(ExtendedDataGrid), new PropertyMetadata(Visibility.Visible));
        /// <summary>
        /// Registers <see cref="PagingOptionsVisibility"/> as a readonly dependency property.
        /// </summary>
        public static readonly DependencyProperty PagingOptionsVisibilityProperty = PagingOptionsVisibilityPropertyKey.DependencyProperty;

        /// <summary>
        /// Gets or sets the template to be used for paging options control, including navigation arrows.
        /// </summary>
        public DataTemplate PagingOptionsTemplate
        {
            get => (DataTemplate)GetValue(PagingOptionsTemplateProperty);
            set => SetCurrentValue(PagingOptionsTemplateProperty, value);
        }
        /// <summary>
        /// Registers <see cref="PagingOptionsTemplate"/> as a dependency property.
        /// </summary>
        public static readonly DependencyProperty PagingOptionsTemplateProperty
            = DependencyProperty.Register(nameof(PagingOptionsTemplate), typeof(DataTemplate), typeof(ExtendedDataGrid), new FrameworkPropertyMetadata(null));

        /// <summary>
        /// Gets or sets a value indicating if controls allowing to go to first or last page should be shown.
        /// </summary>
        /// <remarks>No special behavior on this property, just an helper for templating.</remarks>
        public bool ShowsGoToFirstAndLastPageControls
        {
            get => (bool)GetValue(ShowsGoToFirstAndLastPageControlsProperty);
            set => SetCurrentValue(ShowsGoToFirstAndLastPageControlsProperty, value);
        }
        /// <summary>
        /// Registers <see cref="ShowsGoToFirstAndLastPageControls"/> as a dependency property.
        /// </summary>
        public static readonly DependencyProperty ShowsGoToFirstAndLastPageControlsProperty
            = DependencyProperty.Register(nameof(ShowsGoToFirstAndLastPageControls), typeof(bool), typeof(ExtendedDataGrid), new FrameworkPropertyMetadata(default(bool)));

        /// <summary>
        /// Gets or sets from how many rows found in the source background loading must be used, 
        /// and how many rows should be loaded per background thread round.
        /// </summary>
        public int BackgoundLoadSizeInRows
        {
            get => (int)GetValue(BackgoundLoadSizeInRowsProperty);
            set => SetCurrentValue(BackgoundLoadSizeInRowsProperty, value);
        }
        /// <summary>
        /// Registers <see cref="BackgoundLoadSizeInRows"/> as a dependency property.
        /// </summary>
        public static readonly DependencyProperty BackgoundLoadSizeInRowsProperty
            = DependencyProperty.Register(nameof(BackgoundLoadSizeInRows), typeof(int), typeof(ExtendedDataGrid), new FrameworkPropertyMetadata(DataTablePagingManager.DefaultItemLoadSize, CacheSizeInRowsChanged));

        /// <summary>
        /// Called whenever the <see cref="BackgoundLoadSizeInRows"/> property changes.
        /// </summary>
        /// <param name="sender">The object whose property changed.</param>
        /// <param name="args">Information about the property change.</param>
        public static void CacheSizeInRowsChanged(DependencyObject sender, DependencyPropertyChangedEventArgs args)
        {
            if (sender is ExtendedDataGrid casted && args.NewValue is int new_value)
                casted.PagedTable.SetLoadSize(new_value);
        }

        /// <summary>
        /// Gets or sets a value that indicates if background loading shall be used even when 
        /// not using paging, selection or ID features.
        /// </summary>
        public bool ForceBackgroundLoading
        {
            get => (bool)GetValue(ForceBackgroundLoadingProperty);
            set => SetCurrentValue(ForceBackgroundLoadingProperty, value);
        }
        /// <summary>
        /// Registers <see cref="ForceBackgroundLoading"/> as a dependency property.
        /// </summary>
        public static readonly DependencyProperty ForceBackgroundLoadingProperty
            = DependencyProperty.Register(nameof(ForceBackgroundLoading), typeof(bool), typeof(ExtendedDataGrid), new FrameworkPropertyMetadata(default(bool), ForceBackgroundLoadingChanged));

        /// <summary>
        /// Called whenever the <see cref="ForceBackgroundLoading"/> property changes.
        /// </summary>
        /// <param name="sender">The object whose property changed.</param>
        /// <param name="args">Information about the property change.</param>
        public static void ForceBackgroundLoadingChanged(DependencyObject sender, DependencyPropertyChangedEventArgs args)
        {
            if (sender is ExtendedDataGrid casted && args.NewValue is bool new_value)
            {
                // In case setting this property enabled paging 
                // then force page to update:
                if (new_value && !casted.UsesPaging && !casted.ShowsCheckMarks && !casted.ShowsIDs && !casted.PagedTable.AreSourceItemsDynamic)
                {
                    casted.CancelEdit();
                    casted.PagedTable.UpdateCurrentPage();
                }
                // In case unsetting this property disabled paging,
                // then force source update to original list:
                else if (!new_value && !casted.UsesPagingInternal)
                {
                    casted.CancelEdit();
                    casted.InvalidateProperty(ItemsSourceProperty);
                }
            }
        }

        /// <summary>
        /// Gets a value indicating if page is loading in the background.
        /// </summary>
        public bool IsPageLoading
        {
            get => (bool)GetValue(IsPageLoadingProperty);
            protected set => SetValue(IsPageLoadingPropertyKey, value);
        }
        private static readonly DependencyPropertyKey IsPageLoadingPropertyKey =
            DependencyProperty.RegisterReadOnly(nameof(IsPageLoading), typeof(bool), typeof(ExtendedDataGrid), new PropertyMetadata(default(bool)));
        /// <summary>
        /// Registers <see cref="IsPageLoading"/> as a readonly dependency property.
        /// </summary>
        public static readonly DependencyProperty IsPageLoadingProperty = IsPageLoadingPropertyKey.DependencyProperty;

        /// <summary>
        /// Gets or sets the text to be displayed when items are loading in the background.
        /// </summary>
        public string PageLoadingText
        {
            get => (string)GetValue(PageLoadingTextProperty);
            set => SetCurrentValue(PageLoadingTextProperty, value);
        }
        /// <summary>
        /// Registers <see cref="PageLoadingText"/> as a dependency property.
        /// </summary>
        public static readonly DependencyProperty PageLoadingTextProperty
            = DependencyProperty.Register(nameof(PageLoadingText), typeof(string), typeof(ExtendedDataGrid), new FrameworkPropertyMetadata("Background loading in progress"));
        #endregion

        #region Rows per page
        /// <summary>
        /// Gets or sets a value indicating if the rows per page property can be edited.
        /// </summary>
        public bool CanChangeRowsPerPage
        {
            get => (bool)GetValue(CanChangeRowsPerPageProperty);
            set => SetCurrentValue(CanChangeRowsPerPageProperty, value);
        }
        /// <summary>
        /// Registers <see cref="CanChangeRowsPerPage"/> as a dependency property.
        /// </summary>
        public static readonly DependencyProperty CanChangeRowsPerPageProperty
            = DependencyProperty.Register(nameof(CanChangeRowsPerPage), typeof(bool), typeof(ExtendedDataGrid), new FrameworkPropertyMetadata(true, CanChangeRowsPerPageChanged));

        /// <summary>
        /// Called whenever the <see cref="CanChangeRowsPerPage"/> property changes.
        /// </summary>
        /// <param name="sender">The object whose property changed.</param>
        /// <param name="args">Information about the property change.</param>
        public static void CanChangeRowsPerPageChanged(DependencyObject sender, DependencyPropertyChangedEventArgs args)
        {
            if (sender is ExtendedDataGrid casted && args.NewValue is bool?)
            {
                if (args.NewValue as bool? == true)
                    casted.RowsPerPageSelectionVisibility = Visibility.Visible;
                else casted.RowsPerPageSelectionVisibility = Visibility.Collapsed;
            }
        }

        /// <summary>
        /// Gets the rows per page selection option visibility.
        /// </summary>
        public Visibility RowsPerPageSelectionVisibility
        {
            get => (Visibility)GetValue(RowsPerPageSelectionVisibilityProperty);
            protected set => SetValue(RowsPerPageSelectionVisibilityPropertyKey, value);
        }
        private static readonly DependencyPropertyKey RowsPerPageSelectionVisibilityPropertyKey =
            DependencyProperty.RegisterReadOnly(nameof(RowsPerPageSelectionVisibility), typeof(Visibility), typeof(ExtendedDataGrid), new PropertyMetadata(Visibility.Visible));
        /// <summary>
        /// Registers <see cref="RowsPerPageSelectionVisibility"/> as a readonly dependency property.
        /// </summary>
        public static readonly DependencyProperty RowsPerPageSelectionVisibilityProperty = RowsPerPageSelectionVisibilityPropertyKey.DependencyProperty;

        /// <summary>
        /// Gets or sets the template to be used for rows per page selection.
        /// </summary>
        public DataTemplate RowsPerPageSelectionTemplate
        {
            get => (DataTemplate)GetValue(RowsPerPageSelectionTemplateProperty);
            set => SetCurrentValue(RowsPerPageSelectionTemplateProperty, value);
        }
        /// <summary>
        /// Registers <see cref="RowsPerPageSelectionTemplate"/> as a dependency property.
        /// </summary>
        public static readonly DependencyProperty RowsPerPageSelectionTemplateProperty
            = DependencyProperty.Register(nameof(RowsPerPageSelectionTemplate), typeof(DataTemplate), typeof(ExtendedDataGrid), new FrameworkPropertyMetadata(null));

        /// <summary>
        /// Gets or sets a value indicating how many rows shall are displayed per page.
        /// Set to 0 or negative value and paging won't be functional.
        /// </summary>
        public int RowsPerPage
        {
            get => (int)GetValue(RowsPerPageProperty);
            set => SetCurrentValue(RowsPerPageProperty, value);
        }
        /// <summary>
        /// Registers <see cref="RowsPerPage"/> as a dependency property.
        /// </summary>
        public static readonly DependencyProperty RowsPerPageProperty
            = DependencyProperty.Register(nameof(RowsPerPage), typeof(int), typeof(ExtendedDataGrid), new FrameworkPropertyMetadata(0, RowsPerPageChanged));

        /// <summary>
        /// Called whenever the <see cref="RowsPerPage"/> property changes.
        /// </summary>
        /// <param name="sender">The object whose property changed.</param>
        /// <param name="args">Information about the property change.</param>
        public static void RowsPerPageChanged(DependencyObject sender, DependencyPropertyChangedEventArgs args)
        {
            if (sender is ExtendedDataGrid casted && args.NewValue is int new_value)
            {
                casted.CancelEdit();
                casted.PagedTable.RecordsPerPage = new_value;
            }        
        }
        #endregion

        #region Current page
        /// <summary>
        /// Gets the current page number of a paged datagrid.
        /// </summary>
        public uint CurrentPageIndex
        {
            get => (uint)GetValue(CurrentPageIndexProperty);
            protected set => SetValue(CurrentPageIndexPropertyKey, value);
        }
        private static readonly DependencyPropertyKey CurrentPageIndexPropertyKey =
            DependencyProperty.RegisterReadOnly(nameof(CurrentPageIndex), typeof(uint), typeof(ExtendedDataGrid), new PropertyMetadata(default(uint)));
        /// <summary>
        /// Registers <see cref="CurrentPageIndex"/> as a readonly dependency property.
        /// </summary>
        public static readonly DependencyProperty CurrentPageIndexProperty = CurrentPageIndexPropertyKey.DependencyProperty;

        /// <summary>
        /// Gets the row selection option visibility.
        /// </summary>
        public string CurrentVisibleItemRangeText
        {
            get => (string)GetValue(CurrentVisibleItemRangeTextProperty);
            protected set => SetValue(CurrentVisibleItemRangeTextPropertyKey, value);
        }
        private static readonly DependencyPropertyKey CurrentVisibleItemRangeTextPropertyKey =
            DependencyProperty.RegisterReadOnly(nameof(CurrentVisibleItemRangeText), typeof(string), typeof(ExtendedDataGrid), new PropertyMetadata(default(string)));
        /// <summary>
        /// Registers <see cref="CurrentVisibleItemRangeText"/> as a readonly dependency property.
        /// </summary>
        public static readonly DependencyProperty CurrentVisibleItemRangeTextProperty = CurrentVisibleItemRangeTextPropertyKey.DependencyProperty;

        /// <summary>
        /// Gets the text to be displayed between current page count and total page count.
        /// </summary>
        public string PageCountsSeparatorText
        {
            get => (string)GetValue(PageCountsSeparatorTextProperty);
            set => SetValue(PageCountsSeparatorTextProperty, value);
        }
        /// <summary>
        /// Registers <see cref="PageCountsSeparatorText"/> as a dependency property.
        /// </summary>
        public static readonly DependencyProperty PageCountsSeparatorTextProperty =
            DependencyProperty.Register(nameof(PageCountsSeparatorText), typeof(string), typeof(ExtendedDataGrid), new FrameworkPropertyMetadata(" of ", PageCountsSeparatorTextChanged));

        /// <summary>
        /// Called whenever the <see cref="PageCountsSeparatorText"/> property changes.
        /// </summary>
        /// <param name="sender">The object whose property changed.</param>
        /// <param name="args">Information about the property change.</param>
        public static void PageCountsSeparatorTextChanged(DependencyObject sender, DependencyPropertyChangedEventArgs args)
        {
            if (sender is ExtendedDataGrid casted && args.NewValue is string)
                casted.UpdateCurrentVisibleItemRangeText();
        }

        /// <summary>
        /// Updates the current page item count text.
        /// </summary>
        private void UpdateCurrentVisibleItemRangeText()
        {
            CurrentVisibleItemRangeText = 
                PagedTable.SourceItemsCount >= 0 ? (PagedTable.CurrentPageRange.Item1 + "-" + PagedTable.CurrentPageRange.Item2 + " " + PageCountsSeparatorText + " " + PagedTable.SourceItemsCount) : string.Empty;
        }
        #endregion

        #region Additional content properties
        /// <summary>
        /// Gets or sets a value indicating if more options can be used for the control.
        /// </summary>
        public bool CanShowAdditionalOptions
        {
            get => (bool)GetValue(CanShowAdditionalOptionsProperty);
            set => SetCurrentValue(CanShowAdditionalOptionsProperty, value);
        }
        /// <summary>
        /// Registers <see cref="CanShowAdditionalOptions"/> as a dependency property.
        /// </summary>
        public static readonly DependencyProperty CanShowAdditionalOptionsProperty
            = DependencyProperty.Register(nameof(CanShowAdditionalOptions), typeof(bool), typeof(ExtendedDataGrid), new FrameworkPropertyMetadata(default(bool), CanShowAdditionalOptionsChanged));

        /// <summary>
        /// Called whenever the <see cref="CanShowAdditionalOptions"/> property changes.
        /// </summary>
        /// <param name="sender">The object whose property changed.</param>
        /// <param name="args">Information about the property change.</param>
        public static void CanShowAdditionalOptionsChanged(DependencyObject sender, DependencyPropertyChangedEventArgs args)
        {
            if (sender is ExtendedDataGrid casted && args.NewValue is bool?)
            {
                if (args.NewValue as bool? == true)
                    casted.AdditionalOptionsVisibility = Visibility.Visible;
                else casted.AdditionalOptionsVisibility = Visibility.Collapsed;
            }
        }

        /// <summary>
        /// Gets the visibility of the additional options.
        /// </summary>
        public Visibility AdditionalOptionsVisibility
        {
            get => (Visibility)GetValue(AdditionalOptionsVisibilityProperty);
            protected set => SetValue(AdditionalOptionsVisibilityPropertyKey, value);
        }
        private static readonly DependencyPropertyKey AdditionalOptionsVisibilityPropertyKey =
            DependencyProperty.RegisterReadOnly(nameof(AdditionalOptionsVisibility), typeof(Visibility), typeof(ExtendedDataGrid), new PropertyMetadata(Visibility.Collapsed));
        /// <summary>
        /// Registers <see cref="AdditionalOptionsVisibility"/> as a readonly dependency property.
        /// </summary>
        public static readonly DependencyProperty AdditionalOptionsVisibilityProperty = AdditionalOptionsVisibilityPropertyKey.DependencyProperty;

        /// <summary>
        /// Gets or sets the template to be used for additional options.
        /// </summary>
        public DataTemplate AdditionalOptionsTemplate
        {
            get => (DataTemplate)GetValue(AdditionalOptionsTemplateProperty);
            set => SetCurrentValue(AdditionalOptionsTemplateProperty, value);
        }
        /// <summary>
        /// Registers <see cref="AdditionalOptionsTemplate"/> as a dependency property.
        /// </summary>
        public static readonly DependencyProperty AdditionalOptionsTemplateProperty
            = DependencyProperty.Register(nameof(AdditionalOptionsTemplate), typeof(DataTemplate), typeof(ExtendedDataGrid), new FrameworkPropertyMetadata(null));
        #endregion

        #region Checkmarks and ID columns settings
        /// <summary>
        /// Gets or sets a value indicating if user can reorder index and selector.
        /// </summary>
        /// <remarks>No effect while CanUserReorderColumns is false.</remarks>
        public bool CanUserReorderIDsAndCheckMarksColumns
        {
            get => (bool)GetValue(CanUserReorderIDsAndCheckMarksColumnsProperty);
            set => SetCurrentValue(CanUserReorderIDsAndCheckMarksColumnsProperty, value);
        }
        /// <summary>
        /// Registers <see cref="CanUserReorderIDsAndCheckMarksColumns"/> as a dependency property.
        /// </summary>
        public static readonly DependencyProperty CanUserReorderIDsAndCheckMarksColumnsProperty
            = DependencyProperty.Register(nameof(CanUserReorderIDsAndCheckMarksColumns), typeof(bool), typeof(ExtendedDataGrid), new FrameworkPropertyMetadata(default(bool), CanUserReorderIDsAndCheckMarksColumnsChanged));

        /// <summary>
        /// Called whenever the <see cref="CanUserReorderIDsAndCheckMarksColumns"/> property changes.
        /// </summary>
        /// <param name="sender">The object whose property changed.</param>
        /// <param name="args">Information about the property change.</param>
        public static void CanUserReorderIDsAndCheckMarksColumnsChanged(DependencyObject sender, DependencyPropertyChangedEventArgs args)
        {
            if (sender is ExtendedDataGrid casted)
            {
                casted.CancelEdit();
                casted.PagedTable.UpdateCurrentPage();  // force update to force column autogeneration
            }
        }

        #region CheckMarks processing part
        /// <summary>
        /// Gets source items that are checkmarked with selector column.
        /// </summary>
        public IEnumerable<object> CheckMarkedItems
        {
            get => (IEnumerable<object>)GetValue(CheckMarkedItemsProperty);
            set => SetCurrentValue(CheckMarkedItemsProperty, value);
        }
        private static readonly DependencyProperty CheckMarkedItemsProperty =
            DependencyProperty.Register(nameof(CheckMarkedItems), typeof(IEnumerable<object>), typeof(ExtendedDataGrid), new PropertyMetadata(null, CheckMarkedItemsChanged));

        /// <summary>
        /// Called whenever the <see cref="CheckMarkedItems"/> property changes.
        /// </summary>
        /// <param name="sender">The object whose property changed.</param>
        /// <param name="args">Information about the property change.</param>
        public static void CheckMarkedItemsChanged(DependencyObject sender, DependencyPropertyChangedEventArgs args)
        {
            if (sender is ExtendedDataGrid casted)
            {
                if (args.NewValue != casted.PagedTable.CheckMarkedRows)
                {
                    throw new NotSupportedException(nameof(CheckMarkedItems) + " property of " + nameof(ExtendedDataGrid) +
                        " is not set as read-only for conveniance but must be treated as it is. Set binding mode to OneWayToSource or avoid changing this property value.");
                }
            }
        }

        /// <summary>
        /// Gets or sets a value indicating the selection state of rows.
        /// </summary>
        public bool? SelectAllRowsState
        {
            get => (bool?)GetValue(SelectAllRowsStateProperty);
            set => SetCurrentValue(SelectAllRowsStateProperty, value);
        }
        /// <summary>
        /// Registers <see cref="ShowsCheckMarks"/> as a dependency property.
        /// </summary>
        public static readonly DependencyProperty SelectAllRowsStateProperty
            = DependencyProperty.Register(nameof(SelectAllRowsState), typeof(bool?), typeof(ExtendedDataGrid), new FrameworkPropertyMetadata(true, SelectAllRowsStateChanged));

        /// <summary>
        /// Called whenever the <see cref="SelectAllRowsState"/> property changes.
        /// </summary>
        /// <param name="sender">The object whose property changed.</param>
        /// <param name="args">Information about the property change.</param>
        public static void SelectAllRowsStateChanged(DependencyObject sender, DependencyPropertyChangedEventArgs args)
        {
            if (sender is ExtendedDataGrid casted && args.NewValue is bool new_value)
            {
                casted.CancelEdit();
                if (new_value)
                    casted.PagedTable.CheckMarkAllRows();
                else
                    casted.PagedTable.UncheckMarkAllRows();
            }
        }

        /// <summary>
        /// Gets the list of indexes currently displayed on the grid.
        /// </summary>
        public bool SelectAllRowsIsTristated
        {
            get => (bool)GetValue(SelectAllRowsIsTristatedProperty);
            protected set => SetValue(SelectAllRowsIsTristatedPropertyKey, value);
        }
        private static readonly DependencyPropertyKey SelectAllRowsIsTristatedPropertyKey =
            DependencyProperty.RegisterReadOnly(nameof(SelectAllRowsIsTristated), typeof(bool), typeof(ExtendedDataGrid), new PropertyMetadata(default(bool)));
        /// <summary>
        /// Registers <see cref="SelectAllRowsIsTristated"/> as a readonly dependency property.
        /// </summary>
        public static readonly DependencyProperty SelectAllRowsIsTristatedProperty = SelectAllRowsIsTristatedPropertyKey.DependencyProperty;
        #endregion

        #region CheckMarks part
        /// <summary>
        /// Gets or sets a value indicating if the row selection column must be displayed.
        /// </summary>
        public bool ShowsCheckMarks
        {
            get => (bool)GetValue(ShowsCheckMarksProperty);
            set => SetCurrentValue(ShowsCheckMarksProperty, value);
        }
        /// <summary>
        /// Registers <see cref="ShowsCheckMarks"/> as a dependency property.
        /// </summary>
        public static readonly DependencyProperty ShowsCheckMarksProperty
            = DependencyProperty.Register(nameof(ShowsCheckMarks), typeof(bool), typeof(ExtendedDataGrid), new FrameworkPropertyMetadata(default(bool), ShowsCheckMarksChanged));

        /// <summary>
        /// Called whenever the <see cref="ShowsCheckMarks"/> property changes.
        /// </summary>
        /// <param name="sender">The object whose property changed.</param>
        /// <param name="args">Information about the property change.</param>
        public static void ShowsCheckMarksChanged(DependencyObject sender, DependencyPropertyChangedEventArgs args)
        {
            if (sender is ExtendedDataGrid casted && args.NewValue is bool new_value)
            {
                casted.CancelEdit();
                casted.PagedTable.HasCheckmarks = new_value;

                // In case this property disabled paging,
                // then force source update to original list:
                if (!new_value && !casted.UsesPagingInternal)
                    casted.InvalidateProperty(ItemsSourceProperty);
            }
        }

        /// <summary>
        /// Gets or sets a value indicating if the row selection column must be displayed.
        /// </summary>
        public IList<bool> CheckMarksValues
        {
            get => (IList<bool>)GetValue(CheckMarksValuesProperty);
            set => SetCurrentValue(CheckMarksValuesProperty, value);
        }
        /// <summary>
        /// Registers <see cref="CheckMarksValues"/> as a dependency property.
        /// </summary>
        public static readonly DependencyProperty CheckMarksValuesProperty
            = DependencyProperty.Register(nameof(CheckMarksValues), typeof(IList<bool>), typeof(ExtendedDataGrid), 
                new FrameworkPropertyMetadata(default(IList<bool>), FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, CheckMarksValuesChanged));

        /// <summary>
        /// Called whenever the <see cref="CheckMarksValues"/> property changes.
        /// </summary>
        /// <param name="sender">The object whose property changed.</param>
        /// <param name="args">Information about the property change.</param>
        public static void CheckMarksValuesChanged(DependencyObject sender, DependencyPropertyChangedEventArgs args)
        {
            if (sender is ExtendedDataGrid casted && args.NewValue is IList<bool> newValue)
            {
                if (!casted.IsLoaded)
                    casted.CheckMarksValuesBackup = new List<bool>(newValue);  // keep a backup to restore at loading.
                else
                {
                    casted.CancelEdit();
                    casted.PagedTable.SetRowCheckMarksValues(newValue);  // else set selector values normally.
                }
            }
        }

        /// <summary>
        /// Gets or sets the column index of the selectors columnn.
        /// </summary>
        public int CheckMarksColumnIndex
        {
            get => (int)GetValue(CheckMarksColumnIndexProperty);
            set => SetCurrentValue(CheckMarksColumnIndexProperty, value);
        }
        /// <summary>
        /// Registers <see cref="CheckMarksColumnIndex"/> as a dependency property.
        /// </summary>
        public static readonly DependencyProperty CheckMarksColumnIndexProperty
            = DependencyProperty.Register(nameof(CheckMarksColumnIndex), typeof(int), typeof(ExtendedDataGrid), new FrameworkPropertyMetadata(0, CheckMarksColumnIndexChanged));

        /// <summary>
        /// Called whenever the <see cref="CheckMarksColumnIndex"/> property changes.
        /// </summary>
        /// <param name="sender">The object whose property changed.</param>
        /// <param name="args">Information about the property change.</param>
        public static void CheckMarksColumnIndexChanged(DependencyObject sender, DependencyPropertyChangedEventArgs args)
        {
            if (sender is ExtendedDataGrid casted && args.NewValue is int new_value)
            {
                casted.CancelEdit();
                casted.PagedTable.CheckMarksColumnPosition = new_value;
            }
        }

        /// <summary>
        /// Gets or sets the style of the selector column header.
        /// </summary>
        public Style CheckMarksColumnHeaderStyle
        {
            get => (Style)GetValue(CheckMarksColumnHeaderStyleProperty);
            set => SetCurrentValue(CheckMarksColumnHeaderStyleProperty, value);
        }
        /// <summary>
        /// Registers <see cref="CheckMarksColumnHeaderStyle"/> as a dependency property.
        /// </summary>
        public static readonly DependencyProperty CheckMarksColumnHeaderStyleProperty
            = DependencyProperty.Register(nameof(CheckMarksColumnHeaderStyle), typeof(Style), typeof(ExtendedDataGrid), new FrameworkPropertyMetadata(null));

        /// <summary>
        /// Gets or sets the template for the selector column header.
        /// </summary>
        public DataTemplate CheckMarksColumnHeaderTemplate
        {
            get => (DataTemplate)GetValue(CheckMarksColumnHeaderTemplateProperty);
            set => SetCurrentValue(CheckMarksColumnHeaderTemplateProperty, value);
        }
        /// <summary>
        /// Registers <see cref="CheckMarksColumnHeaderTemplate"/> as a dependency property.
        /// </summary>
        public static readonly DependencyProperty CheckMarksColumnHeaderTemplateProperty
            = DependencyProperty.Register(nameof(CheckMarksColumnHeaderTemplate), typeof(DataTemplate), typeof(ExtendedDataGrid), new FrameworkPropertyMetadata(null));

        /// <summary>
        /// Gets or sets a template selector for the selector column header.
        /// </summary>
        public DataTemplateSelector CheckMarksColumnHeaderTemplateSelector
        {
            get => (DataTemplateSelector)GetValue(CheckMarksColumnHeaderTemplateSelectorProperty);
            set => SetCurrentValue(CheckMarksColumnHeaderTemplateSelectorProperty, value);
        }
        /// <summary>
        /// Registers <see cref="CheckMarksColumnHeaderTemplateSelector"/> as a dependency property.
        /// </summary>
        public static readonly DependencyProperty CheckMarksColumnHeaderTemplateSelectorProperty
            = DependencyProperty.Register(nameof(CheckMarksColumnHeaderTemplateSelector), typeof(DataTemplateSelector), typeof(ExtendedDataGrid), new FrameworkPropertyMetadata(default(DataTemplateSelector)));

        /// <summary>
        /// Gets or sets the style of the selector cell.
        /// </summary>
        public Style CheckMarkCellStyle
        {
            get => (Style)GetValue(CheckMarkCellStyleProperty);
            set => SetCurrentValue(CheckMarkCellStyleProperty, value);
        }
        /// <summary>
        /// Registers <see cref="CheckMarkCellStyle"/> as a dependency property.
        /// </summary>
        public static readonly DependencyProperty CheckMarkCellStyleProperty
            = DependencyProperty.Register(nameof(CheckMarkCellStyle), typeof(Style), typeof(ExtendedDataGrid), new FrameworkPropertyMetadata(default(Style)));

        /// <summary>
        /// Gets or sets a specific template for selector cells.
        /// </summary>
        public DataTemplate CheckMarkCellTemplate
        {
            get => (DataTemplate)GetValue(CheckMarkCellTemplateProperty);
            set => SetCurrentValue(CheckMarkCellTemplateProperty, value);
        }
        /// <summary>
        /// Registers <see cref="CheckMarkCellTemplate"/> as a dependency property.
        /// </summary>
        public static readonly DependencyProperty CheckMarkCellTemplateProperty
            = DependencyProperty.Register(nameof(CheckMarkCellTemplate), typeof(DataTemplate), typeof(ExtendedDataGrid), new FrameworkPropertyMetadata(default(DataTemplate)));

        /// <summary>
        /// Gets or sets a template selector for selector cells.
        /// </summary>
        public DataTemplateSelector CheckMarkCellTemplateSelector
        {
            get => (DataTemplateSelector)GetValue(CheckMarkCellTemplateSelectorProperty);
            set => SetCurrentValue(CheckMarkCellTemplateSelectorProperty, value);
        }
        /// <summary>
        /// Registers <see cref="CheckMarkCellTemplateSelector"/> as a dependency property.
        /// </summary>
        public static readonly DependencyProperty CheckMarkCellTemplateSelectorProperty
            = DependencyProperty.Register(nameof(CheckMarkCellTemplateSelector), typeof(DataTemplateSelector), typeof(ExtendedDataGrid), new FrameworkPropertyMetadata(default(DataTemplateSelector)));

        /// <summary>
        /// Gets or sets a template to be applied when selector cells are being edited.
        /// </summary>
        public DataTemplate CheckMarkCellEditingTemplate
        {
            get => (DataTemplate)GetValue(CheckMarkCellEditingTemplateProperty);
            set => SetCurrentValue(CheckMarkCellEditingTemplateProperty, value);
        }
        /// <summary>
        /// Registers <see cref="CheckMarkCellEditingTemplate"/> as a dependency property.
        /// </summary>
        public static readonly DependencyProperty CheckMarkCellEditingTemplateProperty
            = DependencyProperty.Register(nameof(CheckMarkCellEditingTemplate), typeof(DataTemplate), typeof(ExtendedDataGrid), new FrameworkPropertyMetadata(default(DataTemplate)));

        /// <summary>
        /// Gets or sets a template selector for selector cells in edit mode.
        /// </summary>
        public DataTemplateSelector CheckMarkCellEditingTemplateSelector
        {
            get => (DataTemplateSelector)GetValue(CheckMarkCellEditingTemplateSelectorProperty);
            set => SetCurrentValue(CheckMarkCellEditingTemplateSelectorProperty, value);
        }
        /// <summary>
        /// Registers <see cref="CheckMarkCellEditingTemplateSelector"/> as a dependency property.
        /// </summary>
        public static readonly DependencyProperty CheckMarkCellEditingTemplateSelectorProperty
            = DependencyProperty.Register(nameof(CheckMarkCellEditingTemplateSelector), typeof(DataTemplateSelector), typeof(ExtendedDataGrid), new FrameworkPropertyMetadata(default(DataTemplateSelector)));
        #endregion

        #region ID part
        /// <summary>
        /// Gets or sets a value indicating if a column showing indexes must be shown.
        /// </summary>
        public bool ShowsIDs
        {
            get => (bool)GetValue(ShowsIDsProperty);
            set => SetCurrentValue(ShowsIDsProperty, value);
        }
        /// <summary>
        /// Registers <see cref="ShowsIDs"/> as a dependency property.
        /// </summary>
        public static readonly DependencyProperty ShowsIDsProperty
            = DependencyProperty.Register(nameof(ShowsIDs), typeof(bool), typeof(ExtendedDataGrid), new FrameworkPropertyMetadata(default(bool), ShowsIDsChanged));

        /// <summary>
        /// Called whenever the <see cref="ShowsIDs"/> property changes.
        /// </summary>
        /// <param name="sender">The object whose property changed.</param>
        /// <param name="args">Information about the property change.</param>
        public static void ShowsIDsChanged(DependencyObject sender, DependencyPropertyChangedEventArgs args)
        {
            if (sender is ExtendedDataGrid casted && args.NewValue is bool new_value)
            {
                casted.CancelEdit();
                casted.PagedTable.HasIndexes = new_value;

                // In case this property disabled paging,
                // then force source update to original list:
                if (!new_value && !casted.UsesPagingInternal)
                    casted.InvalidateProperty(ItemsSourceProperty);
            }
        }

        /// <summary>
        /// Gets the list of indexes currently displayed on the grid.
        /// </summary>
        public ReadOnlyCollection<int> DisplayedIDs
        {
            get => (ReadOnlyCollection<int>)GetValue(DisplayedIDsProperty);
            protected set => SetValue(DisplayedIDsPropertyKey, value);
        }
        private static readonly DependencyPropertyKey DisplayedIDsPropertyKey =
            DependencyProperty.RegisterReadOnly(nameof(DisplayedIDs), typeof(ReadOnlyCollection<int>), typeof(ExtendedDataGrid), new PropertyMetadata(default(ReadOnlyCollection<int>)));
        /// <summary>
        /// Registers <see cref="DisplayedIDs"/> as a readonly dependency property.
        /// </summary>
        public static readonly DependencyProperty DisplayedIDsProperty = DisplayedIDsPropertyKey.DependencyProperty;

        /// <summary>
        /// Gets or sets a value indicating if ID numbers 
        /// should start from 0 instead of 1.
        /// </summary>
        public bool IDsStartFromZero
        {
            get => (bool)GetValue(IDsStartFromZeroProperty);
            set => SetCurrentValue(IDsStartFromZeroProperty, value);
        }
        /// <summary>
        /// Registers <see cref="IDsStartFromZero"/> as a dependency property.
        /// </summary>
        public static readonly DependencyProperty IDsStartFromZeroProperty
            = DependencyProperty.Register(nameof(IDsStartFromZero), typeof(bool), typeof(ExtendedDataGrid), new FrameworkPropertyMetadata(default(bool), IDsStartFromZeroChanged));

        /// <summary>
        /// Called whenever the <see cref="IDsStartFromZero"/> property changes.
        /// </summary>
        /// <param name="sender">The object whose property changed.</param>
        /// <param name="args">Information about the property change.</param>
        public static void IDsStartFromZeroChanged(DependencyObject sender, DependencyPropertyChangedEventArgs args)
        {
            if (sender is ExtendedDataGrid casted)
                casted.PagedTable.IndexesStartAtZero = args.NewValue as bool? == true;
        }

        /// <summary>
        /// Gets or sets the column index of the IDs columnn.
        /// </summary>
        public int IDsColumnIndex
        {
            get => (int)GetValue(IDsColumnIndexProperty);
            set => SetCurrentValue(IDsColumnIndexProperty, value);
        }
        /// <summary>
        /// Registers <see cref="IDsColumnIndex"/> as a dependency property.
        /// </summary>
        public static readonly DependencyProperty IDsColumnIndexProperty
            = DependencyProperty.Register(nameof(IDsColumnIndex), typeof(int), typeof(ExtendedDataGrid), new FrameworkPropertyMetadata(1, IDsColumnIndexChanged));

        /// <summary>
        /// Called whenever the <see cref="IDsColumnIndex"/> property changes.
        /// </summary>
        /// <param name="sender">The object whose property changed.</param>
        /// <param name="args">Information about the property change.</param>
        public static void IDsColumnIndexChanged(DependencyObject sender, DependencyPropertyChangedEventArgs args)
        {
            if (sender is ExtendedDataGrid casted && args.NewValue is int new_value)
                casted.PagedTable.IndexesColumnPosition = new_value;
        }

        /// <summary>
        /// Gets or sets the style of the index column header.
        /// </summary>
        public Style IDsColumnHeaderStyle
        {
            get => (Style)GetValue(IDsColumnHeaderStyleProperty);
            set => SetCurrentValue(IDsColumnHeaderStyleProperty, value);
        }
        /// <summary>
        /// Registers <see cref="IDsColumnHeaderStyle"/> as a dependency property.
        /// </summary>
        public static readonly DependencyProperty IDsColumnHeaderStyleProperty
            = DependencyProperty.Register(nameof(IDsColumnHeaderStyle), typeof(Style), typeof(ExtendedDataGrid), new FrameworkPropertyMetadata(null));

        /// <summary>
        /// Gets or sets the template for the index column header.
        /// </summary>
        public DataTemplate IDsColumnHeaderTemplate
        {
            get => (DataTemplate)GetValue(IDsColumnHeaderTemplateProperty);
            set => SetCurrentValue(IDsColumnHeaderTemplateProperty, value);
        }
        /// <summary>
        /// Registers <see cref="IDsColumnHeaderTemplate"/> as a dependency property.
        /// </summary>
        public static readonly DependencyProperty IDsColumnHeaderTemplateProperty
            = DependencyProperty.Register(nameof(IDsColumnHeaderTemplate), typeof(DataTemplate), typeof(ExtendedDataGrid), new FrameworkPropertyMetadata(null));

        /// <summary>
        /// Gets or sets a template selector for the index column header.
        /// </summary>
        public DataTemplateSelector IDsColumnHeaderTemplateSelector
        {
            get => (DataTemplateSelector)GetValue(IDsColumnHeaderTemplateSelectorProperty);
            set => SetCurrentValue(IDsColumnHeaderTemplateSelectorProperty, value);
        }
        /// <summary>
        /// Registers <see cref="IDsColumnHeaderTemplateSelector"/> as a dependency property.
        /// </summary>
        public static readonly DependencyProperty IDsColumnHeaderTemplateSelectorProperty
            = DependencyProperty.Register(nameof(IDsColumnHeaderTemplateSelector), typeof(DataTemplateSelector), typeof(ExtendedDataGrid), new FrameworkPropertyMetadata(default(DataTemplateSelector)));

        /// <summary>
        /// Gets or sets the style of the ID cell.
        /// </summary>
        public Style IDCellStyle
        {
            get => (Style)GetValue(IDCellStyleProperty);
            set => SetCurrentValue(IDCellStyleProperty, value);
        }
        /// <summary>
        /// Registers <see cref="IDCellStyle"/> as a dependency property.
        /// </summary>
        public static readonly DependencyProperty IDCellStyleProperty
            = DependencyProperty.Register(nameof(IDCellStyle), typeof(Style), typeof(ExtendedDataGrid), new FrameworkPropertyMetadata(default(Style)));

        /// <summary>
        /// Gets or sets a specific template for ID cells.
        /// </summary>
        public DataTemplate IDCellTemplate
        {
            get => (DataTemplate)GetValue(IDCellTemplateProperty);
            set => SetCurrentValue(IDCellTemplateProperty, value);
        }
        /// <summary>
        /// Registers <see cref="IDCellTemplate"/> as a dependency property.
        /// </summary>
        public static readonly DependencyProperty IDCellTemplateProperty
            = DependencyProperty.Register(nameof(IDCellTemplate), typeof(DataTemplate), typeof(ExtendedDataGrid), new FrameworkPropertyMetadata(default(DataTemplate)));

        /// <summary>
        /// Gets or sets a template selector for ID cells.
        /// </summary>
        public DataTemplateSelector IDCellTemplateSelector
        {
            get => (DataTemplateSelector)GetValue(IDCellTemplateSelectorProperty);
            set => SetCurrentValue(IDCellTemplateSelectorProperty, value);
        }
        /// <summary>
        /// Registers <see cref="IDCellTemplateSelector"/> as a dependency property.
        /// </summary>
        public static readonly DependencyProperty IDCellTemplateSelectorProperty
            = DependencyProperty.Register(nameof(IDCellTemplateSelector), typeof(DataTemplateSelector), typeof(ExtendedDataGrid), new FrameworkPropertyMetadata(default(DataTemplateSelector)));
        #endregion

        #endregion

        #region 'Normal' cells part
        /// <summary>
        /// Gets or sets a uniform template for cells.
        /// </summary>
        public DataTemplate CellTemplate
        {
            get => (DataTemplate)GetValue(CellTemplateProperty);
            set => SetCurrentValue(CellTemplateProperty, value);
        }
        /// <summary>
        /// Registers <see cref="CellTemplate"/> as a dependency property.
        /// </summary>
        public static readonly DependencyProperty CellTemplateProperty
            = DependencyProperty.Register(nameof(CellTemplate), typeof(DataTemplate), typeof(ExtendedDataGrid), new FrameworkPropertyMetadata(default(DataTemplate)));

        /// <summary>
        /// Gets or sets a template selector for cells.
        /// </summary>
        public DataTemplateSelector CellTemplateSelector
        {
            get => (DataTemplateSelector)GetValue(CellTemplateSelectorProperty);
            set => SetCurrentValue(CellTemplateSelectorProperty, value);
        }
        /// <summary>
        /// Registers <see cref="CellTemplateSelector"/> as a dependency property.
        /// </summary>
        public static readonly DependencyProperty CellTemplateSelectorProperty
            = DependencyProperty.Register(nameof(CellTemplateSelector), typeof(DataTemplateSelector), typeof(ExtendedDataGrid), new FrameworkPropertyMetadata(default(DataTemplateSelector)));

        /// <summary>
        /// Gets or sets a uniform template to be applied when cells are being edited.
        /// </summary>
        public DataTemplate CellEditingTemplate
        {
            get => (DataTemplate)GetValue(CellEditingTemplateProperty);
            set => SetCurrentValue(CellEditingTemplateProperty, value);
        }
        /// <summary>
        /// Registers <see cref="CellEditingTemplate"/> as a dependency property.
        /// </summary>
        public static readonly DependencyProperty CellEditingTemplateProperty
            = DependencyProperty.Register(nameof(CellEditingTemplate), typeof(DataTemplate), typeof(ExtendedDataGrid), new FrameworkPropertyMetadata(default(DataTemplate)));

        /// <summary>
        /// Gets or sets a template selector for cells in edit mode.
        /// </summary>
        public DataTemplateSelector CellEditingTemplateSelector
        {
            get => (DataTemplateSelector)GetValue(CellEditingTemplateSelectorProperty);
            set => SetCurrentValue(CellEditingTemplateSelectorProperty, value);
        }
        /// <summary>
        /// Registers <see cref="CellEditingTemplateSelector"/> as a dependency property.
        /// </summary>
        public static readonly DependencyProperty CellEditingTemplateSelectorProperty
            = DependencyProperty.Register(nameof(CellEditingTemplateSelector), typeof(DataTemplateSelector), typeof(ExtendedDataGrid), new FrameworkPropertyMetadata(default(DataTemplateSelector)));

        /// <summary>
        /// Gets or sets a template for column headers.
        /// </summary>
        public DataTemplate ColumnHeaderTemplate
        {
            get => (DataTemplate)GetValue(ColumnHeaderTemplateProperty);
            set => SetCurrentValue(ColumnHeaderTemplateProperty, value);
        }
        /// <summary>
        /// Registers <see cref="ColumnHeaderTemplate"/> as a dependency property.
        /// </summary>
        public static readonly DependencyProperty ColumnHeaderTemplateProperty
            = DependencyProperty.Register(nameof(ColumnHeaderTemplate), typeof(DataTemplate), typeof(ExtendedDataGrid), new FrameworkPropertyMetadata(default(DataTemplate)));

        /// <summary>
        /// Gets or sets a template selector for column headers.
        /// </summary>
        public DataTemplateSelector ColumnHeaderTemplateSelector
        {
            get => (DataTemplateSelector)GetValue(ColumnHeaderTemplateSelectorProperty);
            set => SetCurrentValue(ColumnHeaderTemplateSelectorProperty, value);
        }
        /// <summary>
        /// Registers <see cref="ColumnHeaderTemplateSelector"/> as a dependency property.
        /// </summary>
        public static readonly DependencyProperty ColumnHeaderTemplateSelectorProperty
            = DependencyProperty.Register(nameof(ColumnHeaderTemplateSelector), typeof(DataTemplateSelector), typeof(ExtendedDataGrid), new FrameworkPropertyMetadata(default(DataTemplateSelector)));
        #endregion

        #region Add/delete items and columns reordering
        /// <summary>
        /// Called whenver the datagrid attempts to create a new item. Tries to update source accordingly.
        /// </summary>
        /// <param name="e">Item creation event information.</param>
        protected override void OnInitializingNewItem(InitializingNewItemEventArgs e)
        {
            // If we use the internal paging system:
            if (UsesPagingInternal && e.NewItem is DataRowView rowview)
            {
                CommitEdit();  // commit any edit in progress before we go.

                // Get current cell position:
                var item_added_column_index = CurrentCell != null ? Columns.IndexOf(CurrentCell.Column) : -1;  // keep in mind.
                var page_was_full = PagedTable.CurrentPageIsFull;

                // Call later (after all 'add item' process is over):
                Dispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.Render, new Action(() =>
                {
                    if (Columns.Count > item_added_column_index && Items.Count > 1)
                    {
                        if (PagedTable.AddRow() && item_added_column_index >= 0)
                        {
                            if (page_was_full)
                                PagedTable.GoLast();                         
                            else PagedTable.UpdateCurrentPage();

                            // Reproduce the behavior of the base datagrid here by putting last line into edit mode:
                            CurrentCell = new DataGridCellInfo(Items[Items.Count - 2], Columns[item_added_column_index]);
                            BeginEdit();
                        }
                    }
                }));
            }

            // Let base class do/finish the job:
            base.OnInitializingNewItem(e);
        }

        /// <summary>
        /// Called whenever a change is validated in a cell.
        /// </summary>
        /// <param name="e">Information about value change.</param>
        protected override void OnExecutedCommitEdit(ExecutedRoutedEventArgs e)
        {
            // Desynchronism with base list can occur and trigger an exception here
            // Normally every cases were checked and CancelEdit() placed everywhere there might be an edition desynchronism,
            // but we let the try/catch here in case we missed one:
            try
            {
                base.OnExecutedCommitEdit(e);
            }
            catch (Exception except)
            {
                if (!(except is ArgumentNullException argnullexcept) || argnullexcept.ParamName != "element")  // very specific error appearing for desynchronism.
                    throw except;
            }
        }

        /// <summary>
        /// Called whenever an item is deleted from the datagrid. Updates source accordingly.
        /// </summary>
        /// <param name="e">Delete event information.</param>
        protected override void OnExecutedDelete(ExecutedRoutedEventArgs e)
        {
            // Ask table to delete this row if possible when using it as source:
            if (UsesPagingInternal && SelectedItems.Count > 0 && SelectedItems[0] is DataRowView)
            {
                foreach (var selected in SelectedItems)
                    PagedTable.DeleteRow(selected as DataRowView);
                PagedTable.UpdateCurrentPage();
            }
            // or let base class do the job when normal item source is used:
            else base.OnExecutedDelete(e);
        }

        /// <summary>
        /// Processes column reorderings, especially for index and selector columns 
        /// when allowed to do so.
        /// </summary>
        /// <param name="e">Event info.</param>
        protected override void OnColumnReordered(DataGridColumnEventArgs e)
        {
            if (ShowsCheckMarks || ShowsIDs)
            {
                var moved_index = e.Column.DisplayIndex;
                var is_select = ShowsCheckMarks && moved_index == CheckMarksColumnIndex;
                var is_id = ShowsIDs && moved_index == IDsColumnIndex;
                if ((is_select || is_id) && CanUserReorderColumns && CanUserReorderIDsAndCheckMarksColumns)
                {
                    if (is_select)
                        PagedTable.IndexesColumnPosition = moved_index;
                    else
                        PagedTable.CheckMarksColumnPosition = moved_index;
                }
                else if (!CanUserReorderColumns || !CanUserReorderIDsAndCheckMarksColumns)
                {
                    if (moved_index <= CheckMarksColumnIndex || moved_index <= IDsColumnIndex)
                        PagedTable.UpdateCurrentPage();  // forces reupdate in case something not authorized happened.
                }
            }
            base.OnColumnReordered(e);
        }
        #endregion
    }
}
using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Data;
using System.Dynamic;
using System.Linq;
using System.Windows;

namespace EMA.MaterialDesignInXAMLExtender.Utils
{
    /// <summary>
    /// A class to manage paging of items placed into an <see cref="IEnumerable"/>.
    /// </summary>
    public class DataTablePagingManager : IWeakEventListener
    {
        #region Private attributes
        private const string checkmark_column_name = "*Selectors";  // Global name of the selectors column (better start with special chars)
        private const string id_column_name = "*IDs";               // Global name of the IDs column (better start with special chars)

        private static readonly EventArgs emptyEventArgs = new EventArgs();  // empty args created once but used multiple times.

        private IEnumerable<object> source;                      // reference to source data.
        private bool _paging_enabled = true;                     // indicates if pages should be created, enabled by default.
        private int _records_per_page = -1;                      // stores the number of records each page should keep, undefined by default.
        private bool _has_indexes;                               // stores HasIndexes state.
        private bool _has_checkmarks;                            // stores HasSelectors state.
        private int _indexes_column_position;                    // stores index for IDs column.
        private int _checkmarks_column_position;                 // stores index for selectors column.
        private bool _indexes_start_at_zero;                     // tells if indexes should start at zero or one.
        private ObservableCollection<CheckMark> _rowCheckMarks;  // stores line checkmarks.
        private bool no_reentrancy;                              // disables reentrancy for some methods.
        private bool no_reentrancy_weak_event;                   // disables reentrancy in collection weak event handling.
        private bool no_checkmarkedrows_update;                  // disables update notification of the checkmarked rows changes.
        IEnumerable<string> dynamicproperties;                   // stores dynamic properties that had been found on source (used for Expando objects).
        IEnumerable<PropertyDescriptor> properties;              // stores 'normal' properties that had been found on source.
        #endregion

        #region Public properties
        /// <summary>
        /// Gets or sets a value indicating if the <see cref="RecordsPerPage"/>
        /// property should be respected when producing the current page
        /// (if not, all source items will be returned in the current page).
        /// </summary>
        public bool PagingEnabled
        {
            get => _paging_enabled;
            set
            {
                if (value != _paging_enabled)
                {
                    _paging_enabled = value;
                    UpdateCurrentPage();
                }
            }
        }

        /// <summary>
        /// Gets the total number of items in source.
        /// </summary>
        public int SourceItemsCount { get; private set; }

        /// <summary>
        /// Gets or sets the current page as datable.
        /// </summary>
        public DataTable CurrentPage { get; private set; }

        /// <summary>
        /// Gets or sets the number of records per page.
        /// A zero or negative value let paging but will force
        /// display of all source items on one page.
        /// </summary>
        public int RecordsPerPage
        {
            get => _records_per_page;
            set
            {
                if (value != _records_per_page)
                {
                    _records_per_page = value;
                    UpdateCurrentPage();
                }
            }
        }

        /// <summary>
        /// Gets the current page number.
        /// </summary>
        public uint CurrentPageIndex { get; private set; }

        /// <summary>
        /// Gets the current minimum
        /// </summary>
        public (int, int) CurrentPageRange { get; private set; }

        /// <summary>
        /// Gets a value indicating if the current page is the first available page.
        /// </summary>
        public bool CurrentPageIsFirstPage => !PagingEnabled || CurrentPageIndex == 0;

        /// <summary>
        /// Gets a value indicating if the current page is the last available page.
        /// </summary>
        public bool CurrentPageIsLastPage => !PagingEnabled || CurrentPageIndex == GetMaxCurrentPageIndex();

        /// <summary>
        /// Gets a value indicating if the current page is full of items regarding to 
        /// the currently set page capacity.
        /// </summary>
        public bool CurrentPageIsFull => PagingEnabled && CurrentPageRange.Item2 % _records_per_page == 0;

        /// <summary>
        /// Gets a value indicating if table has no items to display,
        /// meaning that the current source is null or cleared.
        /// </summary>
        public bool HasNoItems => SourceItemsCount == 0;

        /// <summary>
        /// Gets a values indicating that more than one page is available.
        /// </summary>
        public bool HasMoreThanAPage => GetMaxCurrentPageIndex() > 1;

        /// <summary>
        /// Indicates if source items are dynamic (Expando, etc.)
        /// </summary>
        public bool AreSourceItemsDynamic { get; private set; }
        
        /// <summary>
        /// Gets or sets a value indicating if a selection column
        /// (also called checkmarks) must be included in returned pages. 
        /// </summary>
        public bool HasCheckmarks
        {
            get => _has_checkmarks;
            set
            {
                if (value != _has_checkmarks)
                {
                    _has_checkmarks = value;

                    // Update ID column index regarding to selector column apparance/disapearance:
                    if (value && IndexesColumnPosition >= CheckMarksColumnPosition)
                        IndexesColumnPosition++;
                    else if (!value && IndexesColumnPosition >= CheckMarksColumnPosition)
                        IndexesColumnPosition--;

                    UpdateCurrentPage();
                }
            }
        }

        /// <summary>
        /// Gets or sets the index of the column holding checkmarks.
        /// </summary>
        public int CheckMarksColumnPosition
        {
            get => _checkmarks_column_position;
            set
            {
                if (value != _checkmarks_column_position)
                {
                    _checkmarks_column_position = value;
                    UpdateCurrentPage();
                }
            }
        }

        /// <summary>
        /// Gets or sets a value indicating if indexes should be 
        /// included in the returned pages.
        /// </summary>
        public bool HasIndexes 
        {
            get => _has_indexes;
            set
            {
                if (value != _has_indexes)
                {
                    _has_indexes = value;

                    if (value && IndexesColumnPosition <= CheckMarksColumnPosition)
                        CheckMarksColumnPosition++;
                    else if (!value && IndexesColumnPosition <= CheckMarksColumnPosition)
                        CheckMarksColumnPosition--;

                    UpdateCurrentPage();
                }
            }
        }

        /// <summary>
        /// Gets or sets the index of the column holding indexes.
        /// </summary>
        public int IndexesColumnPosition
        {
            get => _indexes_column_position;
            set
            {
                if (value != _indexes_column_position)
                {
                    _indexes_column_position = value;
                    UpdateCurrentPage();
                }
            }
        }

        /// <summary>
        /// Gets or sets the currently displayed indexes.
        /// </summary>
        public int[] Indexes { get; private set; } = new int[0];

        /// <summary>
        /// Gets or sets a value indicating if indexes should 
        /// start at zero instead of one.
        /// </summary>
        public bool IndexesStartAtZero
        {
            get => _indexes_start_at_zero;
            set
            {
                if (value != _indexes_start_at_zero)
                {
                    _indexes_start_at_zero = value;
                    UpdateCurrentPage();
                }
            }
        }

        /// <summary>
        /// Gets or sets the current list of row checkmarks.
        /// </summary>
        public ObservableCollection<CheckMark> RowCheckMarks
        {
            get => _rowCheckMarks;
            set
            {
                if (value != _rowCheckMarks)
                {
                    if (_rowCheckMarks != null)
                        _rowCheckMarks.CollectionChanged -= RowCheckMarks_CollectionChanged;
                    _rowCheckMarks = value;
                    if (_rowCheckMarks != null)
                    {
                        _rowCheckMarks.CollectionChanged += RowCheckMarks_CollectionChanged;
                        if (_rowCheckMarks.Count > 0)
                            UpdateCurrentPage();  // update only if fully valid.
                    }
                }
            }
        }

        /// <summary>
        /// Gets or sets the current list of checked items in source list (checked rows).
        /// </summary>
        public IEnumerable<object> CheckMarkedRows { get; private set; } = new List<object>();

        /// <summary>
        /// Gets the outline of row selections, i.e. will be true
        /// if all lines are selected, false if any, and null is some 
        /// are selected and some are not.
        /// </summary>
        public bool? AllRowsCheckMarkState { get; private set; }


        /// <summary>
        /// Occurs every time the current page is refreshed.
        /// </summary>
        public event EventHandler PageChanged;

        /// <summary>
        /// Occurs when the selected row list is update.
        /// </summary>
        public event EventHandler CheckMarkedRowsChanged;
        #endregion

        #region CheckMarks processing
        /// <summary>
        /// Initiates the list of checkmarks.
        /// </summary>
        /// <param name="newValues">A list of boolean values corresponding to checks per row.</param>
        public void SetRowCheckMarksValues(IList<bool> newValues)
        {
            var had_been_updated = SyncRowCheckMarksCount();
            int newValuesCount;
            if (newValues != null && (newValuesCount = newValues.Count) > 0)
            { 
                lock (_rowCheckMarks)
                {
                    while (_rowCheckMarks.Count < newValuesCount)
                        _rowCheckMarks.Add(new CheckMark());

                    if (_rowCheckMarks.Count > 0)
                    {
                        no_checkmarkedrows_update = true;  // lock list notification for better performance.
                        int max = Math.Min(newValuesCount, _rowCheckMarks.Count);
                        for (int i = 0; i < max; i++)
                        {
                            if (_rowCheckMarks[i].IsChecked != newValues[i])
                            {
                                _rowCheckMarks[i].IsChecked = newValues[i];
                                had_been_updated |= true;
                            }
                        }
                        no_checkmarkedrows_update = false;
                    }
                }
            }

            // Now update collection:
            if (had_been_updated)
                SyncRowCheckMarksCount(true); // always force full update when list is externaly refreshed
        }

        /// <summary>
        /// Checks all rows.
        /// </summary>
        public void CheckMarkAllRows() => CheckOrUncheckMarksAllRow(true);

        /// <summary>
        /// Unchecks all rows.
        /// </summary>
        public void UncheckMarkAllRows() => CheckOrUncheckMarksAllRow(false);

        /// <summary>
        /// Checks or unchecks all rows.
        /// </summary>
        /// <param name="check">If true, will check all rows, else will uncheck them.</param>
        private void CheckOrUncheckMarksAllRow(bool check = false)
        {
            var had_been_updated = SyncRowCheckMarksCount();
            lock (_rowCheckMarks)
            {
                had_been_updated |= _rowCheckMarks.Any(x => x.IsChecked != check);  // check if need to be updated.
                if (had_been_updated)
                {
                    no_checkmarkedrows_update = true;
                    for (int i = 0; i < _rowCheckMarks.Count; i++)
                        _rowCheckMarks[i].IsChecked = check;
                    no_checkmarkedrows_update = false;
                    AllRowsCheckMarkState = check;
                }
            }

            // Now update collection:
            if (had_been_updated)
                CheckMarkedRowsChanged?.Invoke(this, new EventArgs());
        }

        /// <summary>
        /// Synchronizes checkmarks lists together. Triggers if count is different from source list count 
        /// (i.e. if source had been updated), except if 'force' parameter is set.
        /// </summary>
        /// <param name="force">Forces synchronization even if counts are the same with source.</param>
        /// <returns>True if lists where correctly synchronized.</returns>
        private bool SyncRowCheckMarksCount(bool force = false)
        {
            if (!force && !(RowCheckMarks == null || RowCheckMarks.Count != SourceItemsCount))
                return false;  // no updates needed.

            if (RowCheckMarks == null)
                RowCheckMarks = new ObservableCollection<CheckMark>();
            while (RowCheckMarks.Count > SourceItemsCount)
                RowCheckMarks.RemoveAt(RowCheckMarks.Count - 1);
            while (RowCheckMarks.Count < SourceItemsCount)
                RowCheckMarks.Add(new CheckMark());

            // Gross initialization of checked marked rows if needed:
            if (force || CheckMarkedRows.Count() != RowCheckMarks.Count(x => x.IsChecked))
            {
                if (source != null)
                {
                    var checkMarkedRows = new List<object>();
                    var sourceAsList = (List<object>)null;
                    lock (source)
                    {
                        sourceAsList = source.ToList();
                        int i = 0;
                        foreach (var check in RowCheckMarks)
                        {
                            if (check.IsChecked)
                                checkMarkedRows.Add(sourceAsList[i]);
                            i++;
                        }
                    }
                    AllRowsCheckMarkState = RowCheckMarks.Any(x => x.IsChecked) ? (bool?)null : false;
                    if (AllRowsCheckMarkState == null)
                        AllRowsCheckMarkState = RowCheckMarks.Any(x => !x.IsChecked) ? (bool?)null : true;
                    CheckMarkedRows = checkMarkedRows;

                    // Update collection here:
                    CheckMarkedRowsChanged?.Invoke(this, new EventArgs());
                }
            }

            return true;
        }

        /// <summary>
        /// Occurs whenever the row check marks collection changes.
        /// </summary>
        /// <param name="sender">The collection that contains row check marks.</param>
        /// <param name="e">Collection changed info.</param>
        private void RowCheckMarks_CollectionChanged(object sender, NotifyCollectionChangedEventArgs e)
        {
            if (sender != RowCheckMarks) return;

            if (e.NewItems != null)
                foreach (CheckMark item in e.NewItems)
                    item.IsCheckedChanged += RowCheckMarks_IsCheckedChanged;

            if (e.OldItems != null)
                foreach (CheckMark item in e.OldItems)
                    item.IsCheckedChanged -= RowCheckMarks_IsCheckedChanged;
        }

        /// <summary>
        /// Occurs whenever a <see cref="CheckMark"/> object updates.
        /// </summary>
        /// <param name="sender">The object that sent the event.</param>
        /// <param name="e">Information about the event.</param>
        private void RowCheckMarks_IsCheckedChanged(object sender, CheckedStateChangedEventArgs e)
        {
            if (!no_checkmarkedrows_update)
                SyncRowCheckMarksCount(force: true);
        }
        #endregion

        #region Source updating
        /// <summary>
        /// Sets the source items on which to build pages.
        /// </summary>
        /// <param name="source">A collection of objects to represent.</param>
        public void SetSource(IEnumerable<object> source)
        {
            // If source had previously subscribed to collection changed, unsubscribe:
            if (this.source is INotifyCollectionChanged asObservable1)
                CollectionChangedEventManager.RemoveListener(asObservable1, this);

            if (this.source != null)
            {
                lock (this.source)
                {
                    this.source = source;
                    SourceItemsCount = source.Count();
                    AreSourceItemsDynamic = source.GetGenericType() == typeof(ExpandoObject);
                }
            }
            else
            {
                this.source = source;
                SourceItemsCount = source.Count();
                AreSourceItemsDynamic = source.GetGenericType() == typeof(ExpandoObject);
            }

            if (source == null)
            {
                SourceItemsCount = 0;
                RowCheckMarks?.Clear();
                AreSourceItemsDynamic = false;
            }

            // If source is notifiable, subscribe to change for paging adaption:
            if (this.source is INotifyCollectionChanged asObservable2)
                CollectionChangedEventManager.AddListener(asObservable2, this);
            
            UpdateCurrentPage();
        }

        /// <summary>
        /// Updates current source data.
        /// </summary>
        /// <param name="update_current_page">Set to true to update current table page.</param>
        private void UpdateSource(bool update_current_page = false)
        {
            if (source != null)
            {
                lock (source)
                {
                    var new_count = source.Count();
                    if (SourceItemsCount != new_count)
                    {
                        SourceItemsCount = new_count;
                        SyncRowCheckMarksCount();
                        CorrectCurrentPageIndex();
                    }
                }
            }

            if (source == null)
            {
                SourceItemsCount = 0;
                RowCheckMarks?.Clear();
            }

            if (update_current_page)
                UpdateCurrentPage();
        }

        /// <summary>
        /// Called if source is notifiable and its collection changed.
        /// </summary>
        /// <param name="managerType">Type of the manager we subscribed to.</param>
        /// <param name="sender">The collection that sent the event.</param>
        /// <param name="e">Information about the event.</param>
        /// <returns>True if was able to perform the required operation.</returns>
        /// <remarks><see cref="IWeakEventListener"/> implementation.</remarks>
        public bool ReceiveWeakEvent(Type managerType, object sender, EventArgs e)
        {
            if (source != null)
            {
                if (managerType == typeof(CollectionChangedEventManager))
                {
                    if (no_reentrancy_weak_event)
                        return true;
                    no_reentrancy_weak_event = true;
                    // Process through dispatcher in case we are invoked from another thread:
                    Application.Current.Dispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.DataBind,
                    (Action)(() =>
                    {
                        if (source != sender)  // should never happen.
                        {
                            if (source != null && source is INotifyCollectionChanged notifycollection)
                                CollectionChangedEventManager.RemoveListener(notifycollection, this);
                            SetSource(sender as IEnumerable<object>);
                            return;
                        }

                        UpdateSource();
                        no_reentrancy_weak_event = false;
                    }));
                    return true;
                }
            }
            else if (sender is INotifyCollectionChanged collection)
            {
                CollectionChangedEventManager.RemoveListener(collection, this);  // our binding expression is not used anymore, we can shut listening down.
            }
            return false;
        }
        #endregion

        #region Core methods
        /// <summary>
        /// Updates the currently available page.
        /// </summary>
        /// <returns>The current page, updated.</returns>
        /// <remarks>Core method.</remarks>
        public DataTable UpdateCurrentPage()
        {
            if (no_reentrancy) 
                return CurrentPage;
            no_reentrancy = true;
            if (source == null || !_paging_enabled)
            {
                CurrentPage = GeneratePagedTable(source);
            }
            else
            {
                // Correct page index:
                CorrectCurrentPageIndex();

                // Check that current page count is ok regarding to list size, rearrange otherwise:
                var page_records_count = _records_per_page > 0 ? (int)CurrentPageIndex * _records_per_page : 0;
                CurrentPageRange =
                    (page_records_count + (SourceItemsCount <= 0 ? 0 : 1),
                    _records_per_page > 0 ? 
                        ((page_records_count + _records_per_page) > SourceItemsCount ? SourceItemsCount : (page_records_count + _records_per_page)) 
                        : SourceItemsCount);
                lock (source)
                    CurrentPage = GeneratePagedTable(source.Skip(page_records_count).Take(_records_per_page > 0 ? _records_per_page : SourceItemsCount));
            }
            
            PageChanged?.Invoke(this, emptyEventArgs);
            no_reentrancy = false;
            return CurrentPage;
        }

        /// <summary>
        /// Generates the current page as a <see cref="DataTable"/>.
        /// </summary>
        /// <param name="partialSource">A portion of the source to be used for table generation
        /// that would represent the page.</param>
        /// <returns>A datable to be used and representing the source as a page.</returns>
        private DataTable GeneratePagedTable(IEnumerable<object> partialSource)
        {
            var toReturn = new DataTable();

            if (CurrentPage != null)
                CurrentPage.ColumnChanged -= CurrentTable_ColumnChanged;

            if (partialSource != null)
            {
                lock (partialSource)
                {
                    if (partialSource.Count() > 0)
                    {
                        // Prepare special columns:
                        var SelectorsColumn = (DataColumn)null;
                        if (HasCheckmarks)
                        {
                            // Note: wildcard character in name because 'normal' autogenerated column headers, 
                            // based on C# property definitions, does not support them so not likely to encounter such user-defined name. 
                            SelectorsColumn = new DataColumn(checkmark_column_name, typeof(CheckMark), "", MappingType.Hidden);
                            toReturn.Columns.Add(SelectorsColumn);
                        }

                        SyncRowCheckMarksCount(force:true);

                        var indexColumn = (DataColumn)null;
                        if (HasIndexes)
                        {
                            indexColumn = new DataColumn(id_column_name, typeof(int))
                            {
                                ReadOnly = true,
                                Unique = true
                            };
                            toReturn.Columns.Add(indexColumn);
                            //toReturn.Columns[0].AutoIncrement = true;
                            //toReturn.Columns[1].AutoIncrementSeed = (int)CurrentPageIndex * records_per_page;
                        }

                        // Prepare properties from source definition based on first object:
                        var first = partialSource.First();

                        var props = TypeDescriptor.GetProperties(first.GetType());
                        properties = props == null ? new List<PropertyDescriptor>() : props.OfType<PropertyDescriptor>();

                        foreach (var property in properties)
                        {
                            var type = (property.PropertyType.IsGenericType
                                && property.PropertyType.GetGenericTypeDefinition() == typeof(Nullable<>) ?
                                    Nullable.GetUnderlyingType(property.PropertyType) : property.PropertyType);
                            toReturn.Columns.Add(property.Name, type);
                        }

                        // Dynamic types, still based on first item:
                        dynamicproperties = null;
                        var asExpandoObject = (IDictionary<string, object>)null;
                        if (first is ExpandoObject)
                        {
                            asExpandoObject = (IDictionary<string, object>)first;
                            dynamicproperties = ((IDictionary<string, object>)first).Keys;
                        }

                        if (dynamicproperties != null)
                        {
                            foreach (var property in dynamicproperties)
                            {
                                if (asExpandoObject != null)
                                    toReturn.Columns.Add(property, asExpandoObject[property].GetType());
                            }
                        }

                        // Reorder special column orders:
                        if (HasCheckmarks && CheckMarksColumnPosition > 0)
                            toReturn.Columns[SelectorsColumn.ColumnName].SetOrdinal(CheckMarksColumnPosition);

                        if (HasIndexes && IndexesColumnPosition > 0)
                            toReturn.Columns[indexColumn.ColumnName].SetOrdinal(IndexesColumnPosition);

                        // Prepare pirmary keys (useful?):
                        //int primary_keys_count = 0 + (HasSelectors ? 1 : 0) + (HasIndexes ? 1 : 0);
                        //if (primary_keys_count > 0)
                        //{
                        //    var PrimaryKeyColumns = new DataColumn[primary_keys_count];
                        //    if (HasSelectors)
                        //        PrimaryKeyColumns[0] = SelectorsColumn;
                        //    if (HasIndexes)
                        //        PrimaryKeyColumns[HasSelectors ? 1 : 0] = indexColumn;
                        //    toReturn.PrimaryKey = PrimaryKeyColumns;
                        //}

                        // Fill rows:
                        int i = IndexesStartAtZero ? 0 : 1;
                        int j = 0;  // j is used as non-shifted index.
                        Indexes = new int[partialSource.Count()];
                        foreach (object item in partialSource)
                        {
                            var row = toReturn.NewRow();
                            if (HasCheckmarks)
                            {
                                var jindex = !PagingEnabledInternal ? j : ((int)CurrentPageIndex * _records_per_page + j);
                                if (RowCheckMarks != null && RowCheckMarks.Count > jindex)
                                    row[SelectorsColumn.ColumnName] = RowCheckMarks[jindex];
                                else // should never happen as we sync selector count with items source count changes.
                                    row[SelectorsColumn.ColumnName] = new CheckMark();
                            }

                            if (HasIndexes)
                                row[indexColumn.ColumnName] = Indexes[j] = !PagingEnabledInternal ? i : ((int)CurrentPageIndex * _records_per_page + i);
                            i++; j++;
                            foreach (var property in properties)
                            {
                                row[property.Name] = property.GetValue(item);
                            }
                            if (dynamicproperties != null)
                            {
                                foreach (var property_name in dynamicproperties)
                                {
                                    if (item is ExpandoObject expandoItem)
                                    {
                                        var asDict = (IDictionary<string, object>)expandoItem;
                                        if (asDict.ContainsKey(property_name))
                                            row[property_name] = asDict[property_name];
                                    }
                                }
                            }
                            toReturn.Rows.Add(row);
                        }
                    }
                }
            }
  
           toReturn.ColumnChanged += CurrentTable_ColumnChanged;

            return toReturn;
        }
        #endregion

        #region Modify cell, add or delete row methods
        /// <summary>
        /// Called whenever a value in the column changed.
        /// </summary>
        /// <param name="sender">Unused.</param>
        /// <param name="e">Unused.</param>
        private void CurrentTable_ColumnChanged(object sender, DataColumnChangeEventArgs e)
        {
            // Do not process selector and ID columns:
            if (HasCheckmarks && e.Column.ColumnName == checkmark_column_name 
                || HasIndexes && e.Column.ColumnName == id_column_name)
                return;

            // Find index:
            var index = (int)(CurrentPage.Rows.IndexOf(e.Row) + (PagingEnabled ? CurrentPageIndex * RecordsPerPage : 0));
            if (index >= 0)
            {
                var propertyname = e.Column.ColumnName;
                if (dynamicproperties != null && dynamicproperties.Contains(propertyname))
                {
                    lock (source)
                    {
                        var item = source.Count() > index ? source.ToList()[index] : null;
                        if (item is ExpandoObject expandoItem)
                        {
                            var asDict = (IDictionary<string, object>)expandoItem;
                            if (asDict.Keys.Contains(propertyname))
                                asDict[propertyname] = e.Row[propertyname];
                            return;
                        }
                    }
                }
                var propertydesc = properties?.FirstOrDefault(x => x.Name == propertyname);
                if (propertydesc != null)
                {
                    lock (source)
                    {
                        var item = source.Count() > index ? source.ToList()[index] : null;
                        if (item != null)
                            propertydesc.SetValue(item, e.Row[propertyname]);
                    }
                }
            }
        }

        /// <summary>
        /// Adds a new empty row in the source list.
        /// </summary>
        /// <returns>True if succeeded to add item, false otherwise.</returns>
        public bool AddRow()
        {
            // Construct new item:
            lock (source)
            {
                if (source is IList list && !list.IsReadOnly)
                {
                    // Find underlying type in our source:
                    var type = source.GetGenericType();
                    if (type != null)
                    {
                        // Find default constructor:
                        var parameters = new Type[0];
                        var constructor = type.GetConstructor(parameters);
                        if (constructor != null)
                        {
                            try  // we do not want the whole app to collapse because of generic UI item creation.
                            {
                                var createdObject = constructor.Invoke(parameters);
                                no_reentrancy_weak_event = true;  // do not update through observables notifications
                                if (list.Add(createdObject) >= 0)
                                {
                                    UpdateSource();
                                    no_reentrancy_weak_event = false;
                                    return true;
                                }
                            }
                            catch
                            { }
                            finally
                            { 
                                no_reentrancy_weak_event = false;
                            }
                        }
                    }
                }
            }

            return false;
        }

        /// <summary>
        /// Deletes a particular row item in the current page, if source type allows for deletion.
        /// </summary>
        /// <param name="rowViewToDelete">The view representation of the row to be deleted.</param>
        public void DeleteRow(DataRowView rowViewToDelete)
        {
            if (rowViewToDelete == null) return;

            var index = CurrentPage.Rows.IndexOf(rowViewToDelete.Row);

            if (index >= 0)
            {
                lock (source)
                {
                    if (source is IList list && !list.IsReadOnly && list.Count > index)
                    {
                        no_reentrancy_weak_event = true;  // for observable collections, do not update...
                        list.RemoveAt(index);
                        UpdateSource();  // ...and let us update the list manualy.
                        no_reentrancy_weak_event = false;
                    }
                }
            }
        }
        #endregion

        #region Navigation methods
        /// <summary>
        /// Indicates if a next page exists.
        /// </summary>
        /// <returns>True if a next page exists, false otherwise.</returns>
        public bool HasNext() => source != null && PagingEnabledInternal && CurrentPageIndex < GetMaxCurrentPageIndex();

        /// <summary>
        /// Navigate to next page.
        /// </summary>
        /// <returns>The page following the current one, if existing.</returns>
        public DataTable GoNext()
        {
            if (source == null || !PagingEnabledInternal) 
                return GeneratePagedTable(source);

            CurrentPageIndex++;
            CorrectCurrentPageIndex();
            return UpdateCurrentPage();
        }

        /// <summary>
        /// Indicates if a previous page exists.
        /// </summary>
        /// <returns>True if a previous page exists, false otherwise.</returns>
        public bool HasPrevious() => source != null && PagingEnabledInternal && CurrentPageIndex > 0;

        /// <summary>
        /// Navigate to previous page.
        /// </summary>
        /// <returns>The page defined before the current one, if existing.</returns>
        public DataTable GoPrevious()
        {
            if (source == null || !PagingEnabledInternal)
                return GeneratePagedTable(source);

            CurrentPageIndex--;
            if (CurrentPageIndex < 0)
                CurrentPageIndex = 0;

            return UpdateCurrentPage();
        }

        /// <summary>
        /// Navigate to the first page.
        /// </summary>
        /// <returns>The first page if existing.</returns>
        public DataTable GoFirst()
        {
            if (source == null || !PagingEnabledInternal)
                return GeneratePagedTable(source);

            CurrentPageIndex = 0;
            return UpdateCurrentPage();
        }

        /// <summary>
        /// Navigate to the last page.
        /// </summary>
        /// <returns>The last page if existing.</returns>
        public DataTable GoLast()
        {
            if (source == null || !PagingEnabledInternal)
                return GeneratePagedTable(source);

            CurrentPageIndex = GetMaxCurrentPageIndex();
            return UpdateCurrentPage();
        }
        #endregion

        #region Private utils for paging 
        /// <summary>
        /// Gets a value indicating is paging is fully enabled.
        /// </summary>
        private bool PagingEnabledInternal => PagingEnabled && RecordsPerPage > 0;

        /// <summary>
        /// Corrects current page index in case it is invalid.
        /// </summary>
        private void CorrectCurrentPageIndex()
        {
            if (_records_per_page > 0 && CurrentPageIndex > GetMaxCurrentPageIndex())
                CurrentPageIndex = GetMaxCurrentPageIndex();
        }

        /// <summary>
        /// Gets the maximum item index of a given page.
        /// </summary>
        /// <returns>The maximum index of the page.</returns>
        private uint GetMaxCurrentPageIndex()
            => (uint)((SourceItemsCount / _records_per_page) - ((SourceItemsCount % _records_per_page) == 0 ? 1 : 0));
        #endregion
    }
}
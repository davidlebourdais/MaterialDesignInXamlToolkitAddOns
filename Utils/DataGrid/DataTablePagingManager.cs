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
using System.Windows.Threading;

namespace EMA.MaterialDesignInXAMLExtender.Utils
{
    /// <summary>
    /// A class to manage paging of items placed into an <see cref="IEnumerable"/>.
    /// </summary>
    public class DataTablePagingManager : IWeakEventListener
    {
        #region Public static constants
        /// <summary>
        /// Indicates how many items are loaded per thread turn (also max size for background loading activation).
        /// </summary>
        public static int DefaultItemLoadSize { get; } = 1000;
        #endregion 

        #region Private attributes
        private const string checkmark_column_name = "*Selectors";  // Global name of the selectors column (better start with special chars)
        private const string id_column_name = "*IDs";               // Global name of the IDs column (better start with special chars)

        private static readonly EventArgs emptyEventArgs = new EventArgs();  // empty args created once but used multiple times.

        private IEnumerable<object> source;                      // reference to source data.
        private IList sourceAsList;                              // cast (99% cases) or copy of the source object as a list.
        private bool _paging_enabled = true;                     // indicates if pages should be created, enabled by default.
        private int _records_per_page = -1;                      // stores the number of records each page should keep, undefined by default.
        private bool _has_indexes;                               // stores HasIndexes state.
        private bool _has_checkmarks;                            // stores HasSelectors state.
        private int _indexes_column_position;                    // stores index for IDs column.
        private int _checkmarks_column_position;                 // stores index for selectors column.
        private bool _indexes_start_at_zero;                     // tells if indexes should start at zero or one.
        private bool no_reentrancy;                              // disables reentrancy for some methods.
        private bool no_reentrancy_prop_changed_weak_event;      // disables reentrancy in property changed weak event handling.
        private bool no_reentrancy_collection_weak_event;        // disables reentrancy in collection weak event handling.
        private bool no_checkmarkedrows_update;                  // disables update notification of the checkmarked rows changes.
        IEnumerable<string> dynamicproperties;                   // stores dynamic properties that had been found on source (used for Expando objects).
        IEnumerable<PropertyDescriptor> properties;              // stores 'normal' properties that had been found on source.
        private int load_size = DefaultItemLoadSize;             // stores how many items are loaded per thread turn (also max size for background loading activation).
        private BackgroundWorker currentTableLoader;             // a worker that feeds table with rows in a background thread.
        private int current_page_token;                          // stores an ID generated for the current page to be used by the background worker.
        private bool _is_sorting_persistent;                     // stores sorting persistency property.
        private bool sorting_sync_lost;                          // when sorting persistency is off, indicates if source had been updated in a way sorting if not valid anymore.
        private bool source_count_was_zero;                      // indicates if at some point after a source update, the source count was zero and then became > 0.
        #endregion

        #region Private struct
        /// <summary>
        /// A data set to be used by backgrond worker to load rows.
        /// </summary>
        private struct RowUpdateInformation
        {
            /// <summary>
            /// Gets the token associated with the stored data.
            /// </summary>
            public int Token { get; }
            /// <summary>
            /// Gets or changes the current index on the partial source to work on.
            /// </summary>
            public int CurrentIndex { get; set; }
            /// <summary>
            /// Gets the offset of the current index regarding to global data source
            /// (i.e. offset of the current page related to source).
            /// </summary>
            public int SourceIndexOffset { get; }
            /// <summary>
            /// The portion of the data source to be used for current page
            /// population.
            /// </summary>
            public IEnumerable<object> PartialSource { get; }
            /// <summary>
            /// Size of the source portions.
            /// </summary>
            public int PartialSourceCount { get; }

            /// <summary>
            /// Initiates a new structure to pass information 
            /// about for row updates.
            /// </summary>
            /// <param name="token">Unique token associated to this dataset.</param>
            /// <param name="current_index">Current index on partial source.</param>
            /// <param name="source_index_offset">Global source start index.</param>
            /// <param name="partialSource">Portion of the global source that must be processed.</param>
            /// <param name="partial_source_count">Optional size of this portion.</param>
            public RowUpdateInformation(int token, int current_index, int source_index_offset, IEnumerable<object> partialSource, int partial_source_count = -1)
            {
                if (current_index < 0 || source_index_offset < 0)
                    throw new IndexOutOfRangeException(nameof(current_index) + " or " + nameof(source_index_offset) + " are not valid to construct a " + nameof(RowUpdateInformation));
                Token = token;
                CurrentIndex = current_index;
                SourceIndexOffset = source_index_offset;
                PartialSource = partialSource ?? throw new ArgumentException("Cannot construct a " + nameof(RowUpdateInformation) + " with a null " + nameof(partialSource) + " reference", nameof(partialSource));
                PartialSourceCount = partial_source_count < 0 ? partialSource.Count() : partial_source_count;
            }
        }
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
        public DataTable CurrentPage { get; private set; } = new DataTable();

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
        /// Indicates if source items implements <see cref="INotifyPropertyChanged"/>.
        /// </summary>
        public bool AreSourceItemsINotifyPropertyChanged { get; private set; }

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

                    if (value)
                    {
                        SyncRowCheckMarksCount();
                        if (IsSorting)
                            UpdateSortedCheckMarks();
                    }
                    // Update ID column index regarding to selector column apparance/disapearance:
                    if (value && IndexesColumnPosition >= CheckMarksColumnPosition)
                        IndexesColumnPosition++;
                    else if (!value && IndexesColumnPosition >= CheckMarksColumnPosition)
                        IndexesColumnPosition--;

                    if (IsSorting)
                    {
                        if (!value && CheckMarksColumnPosition == SortingColumnIndex)
                            StopSorting();
                        else if (!value && CheckMarksColumnPosition < SortingColumnIndex)
                            SortingColumnIndex--;
                        else if (value && CheckMarksColumnPosition <= SortingColumnIndex)
                            SortingColumnIndex++;
                    }

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

                    if (value)
                    {
                        if (!IsSorting)
                            Indexes = Enumerable.Range(IndexesStartAtZero ? 0 : 1, SourceItemsCount).ToList();
                        else
                        {
                            UpdateSortedIndexes();
                            if (IndexesColumnPosition <= SortingColumnIndex)
                                SortingColumnIndex++;
                        }
                    }
                    else
                    {
                        if (IsSorting)
                        {
                            if (IndexesColumnPosition == SortingColumnIndex)
                                StopSorting();
                            else if (IndexesColumnPosition < SortingColumnIndex)
                                SortingColumnIndex--;
                        }

                        Indexes = new List<int>();
                    }

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
        public int[] DisplayedIndexes { get; private set; } = new int[0];

        /// <summary>
        /// Gets the list of all indexes.
        /// </summary>
        public List<int> Indexes { get; private set; } = new List<int>();

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
        public ObservableCollection<CheckMark> CheckMarks { get; private set; }

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
        /// Gets a sorted version of the source.
        /// </summary>
        public IList<object> SortedSource { get; private set; }

        /// <summary>
        /// Gets a sorted version of the internaly used checkmarks.
        /// </summary>
        private List<CheckMark> SortedCheckMarks { get; set; }

        /// <summary>
        /// Gets a value indicating is sorting is occuring on the table.
        /// </summary>
        public bool IsSorting { get; private set; }

        /// <summary>
        /// Gets the index of the column that is used for sorting.
        /// </summary>
        public int SortingColumnIndex { get; private set; }

        /// <summary>
        /// Gets the name of the column that drives sorting.
        /// </summary>
        public string SortingColumnName { get; private set; }

        /// <summary>
        /// Gets or sets a value indicating is persistency must be 
        /// </summary>
        public bool IsSortingPersistent
        {
            get => _is_sorting_persistent;
            set
            {
                if (value != _is_sorting_persistent)
                {
                    _is_sorting_persistent = value;
                    if (IsSorting && sorting_sync_lost && value)
                    {
                        // Force an update as we do not know 
                        // if persistency was lost before setting this 
                        // value:
                        UpdateSortedLists();
                        UpdateCurrentPage();
                        sorting_sync_lost = false;
                    }
                }
            }
        }
        #endregion

        #region Public events
        /// <summary>
        /// Occurs every time the current page is refreshed.
        /// </summary>
        public event EventHandler PageChanged;

        /// <summary>
        /// Occurs when the selected row list is update.
        /// </summary>
        public event EventHandler CheckMarkedRowsChanged;

        /// <summary>
        /// Produced when the current page starts laoding in the background
        /// </summary>
        public event EventHandler PageLoading;

        /// <summary>
        /// Occurs when current page is completely loaded.
        /// </summary>
        public event EventHandler PageLoaded;
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
                lock (CheckMarks)
                {
                    var diff = newValuesCount - CheckMarks.Count;
                    while (diff-- > 0)
                        CheckMarks.Add(new CheckMark());

                    if (CheckMarks.Any())
                    {
                        no_checkmarkedrows_update = true;  // lock list notification for better performance.
                        int max = Math.Min(newValuesCount, CheckMarks.Count);
                        for (int i = 0; i < max; i++)
                        {
                            if (CheckMarks[i].IsChecked != newValues[i])
                            {
                                CheckMarks[i].IsChecked = newValues[i];
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
            lock (CheckMarks)
            {
                had_been_updated |= CheckMarks.Any(x => x.IsChecked != check);  // check if need to be updated.
                if (had_been_updated)
                {
                    no_checkmarkedrows_update = true;
                    for (int i = 0; i < CheckMarks.Count; i++)
                        CheckMarks[i].IsChecked = check;
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
            var checkmarks_count = CheckMarks != null ? CheckMarks.Count : 0;
            if (!force && !(CheckMarks == null || checkmarks_count != SourceItemsCount))
                return false;  // no updates needed.

            // Init if needed:
            if (CheckMarks == null)
            {
                CheckMarks = new ObservableCollection<CheckMark>();
                CheckMarks.CollectionChanged += RowCheckMarks_CollectionChanged;
                SortedCheckMarks = new List<CheckMark>();
            }

            // Adjust count:
            var diff = SourceItemsCount - checkmarks_count;
            if (diff != 0)
            {
                while (diff < 0)
                    CheckMarks.RemoveAt(checkmarks_count + diff++);
                while (diff-- > 0)
                    CheckMarks.Add(new CheckMark());
                if (!IsSorting)
                    SortedCheckMarks = new List<CheckMark>(CheckMarks);
            }

            // Gross initialization of checked marked rows if needed:
            if (force || CheckMarkedRows.Count() != CheckMarks.Count(x => x.IsChecked))
            {
                if (sourceAsList != null)
                {
                    var checkMarkedRows = new List<object>();
                    lock (sourceAsList)
                    {
                        int i = 0;
                        foreach (var check in CheckMarks)
                        {
                            if (check.IsChecked)
                                checkMarkedRows.Add(sourceAsList[i]);
                            i++;
                        }
                    }
                    AllRowsCheckMarkState = CheckMarks.Any(x => x.IsChecked) ? (bool?)null : false;
                    if (AllRowsCheckMarkState == null)
                        AllRowsCheckMarkState = CheckMarks.Any(x => !x.IsChecked) ? (bool?)null : true;
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
            if (sender != CheckMarks) return;

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
                    // First process 1% case where list would be a copy of source:
                    if (sourceAsList != this.source)
                    {
                        lock (sourceAsList)
                            sourceAsList = source?.ToList();
                    }
                    else sourceAsList = source != null ? source as IList : null;

                    this.source = source;

                    if (source != null)
                    { 
                        SourceItemsCount = source.Count();
                        AreSourceItemsDynamic = source.GetGenericType() == typeof(ExpandoObject);
                        AreSourceItemsINotifyPropertyChanged = source.AreItemsINotifyPropertyChanged();
                    }
                }
            }
            else
            {
                this.source = source;
                sourceAsList = source != null ? (source is IList ? source as IList : source.ToList()) : null;
                SourceItemsCount = source.Count();
                AreSourceItemsDynamic = source.GetGenericType() == typeof(ExpandoObject);
                AreSourceItemsINotifyPropertyChanged = source.AreItemsINotifyPropertyChanged();
            }

            if (source == null)
            {
                sourceAsList = null;
                SourceItemsCount = 0;
                CheckMarks?.Clear();
                AreSourceItemsDynamic = false;
                AreSourceItemsINotifyPropertyChanged = false;
                Indexes = new List<int>();
                SortedCheckMarks = new List<CheckMark>();
            }

            if (source != null)
            {
                // Get source underlying class public properties:
                var props = TypeDescriptor.GetProperties(source.GetGenericType());
                if (properties != null)
                    lock (properties)
                        properties = null;
                properties = props == null ? new List<PropertyDescriptor>() : props.OfType<PropertyDescriptor>();

                // Reset dynamic object properties (will be regenerated at first current update with items within):
                if (dynamicproperties != null)
                    lock (dynamicproperties)
                        dynamicproperties = null;
            }

            // Set was zero:
            source_count_was_zero = SourceItemsCount == 0;

            // Reset sorting when a new source is set:
            IsSorting = false;
            SortedSource = source?.ToList();

            // Set checkmarks:
            SyncRowCheckMarksCount(true);
            if (source != null && HasCheckmarks)
                SortedCheckMarks = CheckMarks.ToList();

            // Set indexes:
            if (source != null && HasIndexes)
                Indexes = Enumerable.Range(IndexesStartAtZero ? 0 : 1, SourceItemsCount).ToList();

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
                    // First process 1% case where list would be a copy of source:
                    if (sourceAsList != source)
                    {
                        lock (sourceAsList)
                            sourceAsList = source is IList ? source as IList : source.ToList();
                    }
                    else sourceAsList = source is IList ? source as IList : source.ToList(); // or just cast current source.

                    var new_count = sourceAsList.Count;
                    if (SourceItemsCount != new_count)
                    {
                        SourceItemsCount = new_count;
                       
                        SyncRowCheckMarksCount(); 
                        CorrectCurrentPageIndex();

                        if (IsSorting)
                        {
                            UpdateSortedLists();
                        }
                        else
                        {
                            SortedSource = source.ToList();

                            // Set indexes:
                            if (HasIndexes)
                                Indexes = Enumerable.Range(IndexesStartAtZero ? 0 : 1, SourceItemsCount).ToList();
                        }

                        // Reset dynamic object properties if we are added new first items
                        // to be processed:
                        if (source_count_was_zero && SourceItemsCount > 0)
                        {                      
                            if (dynamicproperties != null)
                                lock (dynamicproperties)
                                    dynamicproperties = null;
                        }
                    }
                }
            }

            if (source == null)
            {
                sourceAsList = null;
                SortedSource = null;
                SourceItemsCount = 0;
                CheckMarks?.Clear();
                SortedCheckMarks = new List<CheckMark>();
                Indexes = new List<int>();
            }

            // Set was zero:
            source_count_was_zero = SourceItemsCount == 0;

            if (update_current_page)
                UpdateCurrentPage();
        }

        /// <summary>
        /// Called if source is notifiable and its collection or a property changed.
        /// </summary>
        /// <param name="managerType">Type of the manager we subscribed to.</param>
        /// <param name="sender">The object that sent the event.</param>
        /// <param name="e">Information about the event.</param>
        /// <returns>True if was able to perform the required operation.</returns>
        /// <remarks><see cref="IWeakEventListener"/> implementation.</remarks>
        public bool ReceiveWeakEvent(Type managerType, object sender, EventArgs e)
        {
            var handled = false;

            /* Case where a INotifyPropertyChanged object (normaly a cell) called */
            if (managerType == typeof(PropertyChangedEventManager))
            {
                if (source != null)
                    ReceiveWeakINotifyPropertyChangedEvent(sender as INotifyPropertyChanged, e as PropertyChangedEventArgs);
                handled = true;
            }
            /* Case where the source a notifiable collection called: */
            else if (managerType == typeof(CollectionChangedEventManager))
            {
                if (source != null && !no_reentrancy_collection_weak_event)
                {
                    no_reentrancy_collection_weak_event = true;
                    // Process through dispatcher in case we are invoked from another thread:
                    Application.Current.Dispatcher.BeginInvoke(DispatcherPriority.DataBind, (Action)(() =>
                    {
                        if (source != sender)  // should never happen.
                        {
                            if (source != null && source is INotifyCollectionChanged notifycollection)
                                CollectionChangedEventManager.RemoveListener(notifycollection, this);
                            SetSource(sender as IEnumerable<object>);
                            return;
                        }

                        UpdateSource(true);
                        no_reentrancy_collection_weak_event = false;
                    }));
                }
                else if (source == null && sender is INotifyCollectionChanged collection)
                    CollectionChangedEventManager.RemoveListener(collection, this);  // our binding expression is not used anymore, we can shut listening down.

                handled = true;
            }

            return handled;  // must be true otherwise will generate an exception.
        }
        #endregion

        #region Set background row load size
        /// <summary>
        /// Sets load size, i.e. how many items are loaded per 
        /// loading method background invocation.
        /// </summary>
        /// <param name="new_load_size">A valid size in row counts.</param>
        public void SetLoadSize(int new_load_size)
        {
            if (new_load_size < 1)
                new_load_size = 1;

            load_size = new_load_size;
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
                lock (CurrentPage)
                    CurrentPage = GeneratePagedTable(SortedSource);
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

                lock (CurrentPage)
                    lock (source)
                        CurrentPage = GeneratePagedTable(SortedSource.Skip(page_records_count).Take(_records_per_page > 0 ? _records_per_page : SourceItemsCount));
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
                lock (CurrentPage)
                    CurrentPage.ColumnChanged -= CurrentTable_ColumnChanged;

            if (partialSource != null)
            {
                lock (partialSource)
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

                    var indexColumn = (DataColumn)null;
                    if (HasIndexes)
                    {
                        indexColumn = new DataColumn(id_column_name, typeof(int));
                        toReturn.Columns.Add(indexColumn);
                    }
                       
                    // Set column names based on underlying generic source type property names:
                    if (properties != null)
                        lock (properties)
                            foreach (var property in properties)
                            {
                                var type = (property.PropertyType.IsGenericType
                                    && property.PropertyType.GetGenericTypeDefinition() == typeof(Nullable<>) ?
                                        Nullable.GetUnderlyingType(property.PropertyType) : property.PropertyType);
                                toReturn.Columns.Add(property.Name, type);
                            }

                    // Dynamic types, based on first item:
                    var asExpandoObject = (IDictionary<string, object>)null;
                    if (partialSource.Any() && partialSource.First() is ExpandoObject firstItem)
                    {
                        asExpandoObject = (IDictionary<string, object>)firstItem;
                        if (dynamicproperties == null)
                            dynamicproperties = asExpandoObject.Keys.ToList();

                        lock (dynamicproperties)
                            foreach (var property in dynamicproperties)
                                toReturn.Columns.Add(property, asExpandoObject[property].GetType());
                    }

                    // Reorder special columns:
                    if (HasCheckmarks && CheckMarksColumnPosition > 0)
                        toReturn.Columns[SelectorsColumn.ColumnName].SetOrdinal(CheckMarksColumnPosition);

                    if (HasIndexes && IndexesColumnPosition > 0)
                        toReturn.Columns[indexColumn.ColumnName].SetOrdinal(IndexesColumnPosition);

                    // Load rows only if source is not empty:
                    int page_row_count = partialSource.Count();
                    if (page_row_count > 0)
                    { 
                        int global_start_index = PagingEnabledInternal ? (int)CurrentPageIndex * _records_per_page : 0;

                        // Build all rows based on a 'model' row without filling user properties:
                        var row = toReturn.NewRow();  // our model row
                        toReturn.Rows.Add(row);  // mandatory to add it in table otherwise importing copies won't work

                        for (int i = global_start_index; i < page_row_count + global_start_index; i++)
                        {
                            // Fill checkmark if has any:
                            if (HasCheckmarks)
                                row[checkmark_column_name] = SortedCheckMarks[i];

                            // Fill ID if has any:
                            if (HasIndexes)
                                row[id_column_name] = Indexes[i];

                            // Import empty row which if much 
                            // faster than adding a new one:
                            toReturn.ImportRow(row);
                        }

                        toReturn.Rows.RemoveAt(0);  // remove 'model' item.

                        // Update token for previous background threads syncing:
                        current_page_token++;

                        // Update as much rows as the load_size parameter allows:
                        var actual_load_size = Math.Min(page_row_count, load_size);
                        InitializeRows(toReturn, 0, partialSource.Take(actual_load_size));

                        // Fill rows in background if there are too many of them:
                        if (page_row_count > load_size)
                        {
                            //For that: create background worker if not existing:
                            if (currentTableLoader == null || currentTableLoader.IsBusy)
                            {
                                currentTableLoader = new BackgroundWorker();
                                currentTableLoader.DoWork += InitializePageTableInBackground;
                                currentTableLoader.RunWorkerCompleted += EndInitializePageTableInBackground;
                            }

                            // Note: start from load size as we already processed load_size items:
                            var rowsToProcess = new RowUpdateInformation(current_page_token, load_size, global_start_index, partialSource, page_row_count);
                            currentTableLoader.RunWorkerAsync(rowsToProcess);
                            PageLoading?.Invoke(this, emptyEventArgs);
                        }
                        else // indicates that page is ready without triggering page loading event:
                        {
                            PageLoaded?.Invoke(this, emptyEventArgs);
                        }
                    }
                }
            }
  
           toReturn.ColumnChanged += CurrentTable_ColumnChanged;
           return toReturn;
        }

        /// <summary>
        /// Updates already created rows with their initial values.
        /// Does not process ID or checkmarks 
        /// (should be processed prior to calling this method).
        /// </summary>
        /// <param name="table">The table to work on.</param>
        /// <param name="start_index">Starting index on the passed partial source.</param>
        /// <param name="partialSource">The source of items where to get values from.</param>
        /// <param name="validychecker">An optional function to check if we should quit or not this method during processing.</param>
        private void InitializeRows(DataTable table, int start_index, IEnumerable<object> partialSource, Func<bool> validychecker = null)
        {
            if (table != null && partialSource != null)
            {
                // Disable table updates notifications:
                table.ColumnChanged -= CurrentTable_ColumnChanged;

                // Fill rows:
                int i = start_index;
                bool has_i_notify = AreSourceItemsINotifyPropertyChanged;
                foreach (object item in partialSource)
                {
                    // Quit without notification if context is not valid anymore:
                    if (validychecker?.Invoke() == false)
                        return;

                    // Get row to update:
                    var row = table.Rows[i];

                    // (Disable table updates again as sometimes they are reactivated by other running threads:)
                    table.ColumnChanged -= CurrentTable_ColumnChanged;

                    // Process normal properties:
                    if (properties != null)
                        lock (properties)
                            foreach (var property in properties)
                            {
                                // Set value:
                                row[property.Name] = property.GetValue(item);

                                // Subscribe to any further changes if possible:
                                if (has_i_notify)
                                    PropertyChangedEventManager.AddListener(item as INotifyPropertyChanged, this, property.Name);
                            }

                    // Process dynamic properties:
                    if (dynamicproperties != null)
                        lock (dynamicproperties)
                            foreach (var property_name in dynamicproperties)
                                if (item is IDictionary<string, object> asDict && asDict.ContainsKey(property_name))  // (as expando object)
                                {
                                    // Set value:
                                    row[property_name] = asDict[property_name];

                                    // Subscribe to any further changes if possible:
                                    if (has_i_notify)
                                        PropertyChangedEventManager.AddListener(item as INotifyPropertyChanged, this, property_name);
                                }

                    // Increment indexes:
                    i++;
                }

                // Set table update notification back:
                table.ColumnChanged += CurrentTable_ColumnChanged;
            }
        }

        /// <summary>
        /// Initializes the current page in a background worker.
        /// </summary>
        /// <param name="sender">Unused.</param>
        /// <param name="e">Contains data information to start background processing.</param>
        private void InitializePageTableInBackground(object sender, DoWorkEventArgs e)
        {
            // Cancel if passed source data are not valid anymore:
            if (!(e.Argument is RowUpdateInformation data)
                || data.PartialSourceCount <= 0
                || data.CurrentIndex >= data.PartialSourceCount)
            {
                e.Result = true;
                return;
            }

            var interlocked = true;

            // Stop if current table is no more valid or if succeded to load every rows of partial source:
            while (current_page_token == data.Token && data.CurrentIndex < data.PartialSourceCount)
            {
                Application.Current.Dispatcher.BeginInvoke(DispatcherPriority.DataBind, new Action(() =>
                {
                    // Calculate load size:
                    var actual_load_size = Math.Min(data.PartialSourceCount - data.CurrentIndex, load_size);

                    // Update rows:
                    if (current_page_token == data.Token)
                    {
                        lock (CurrentPage)
                            InitializeRows(CurrentPage, data.CurrentIndex, data.PartialSource.Skip(data.CurrentIndex).Take(actual_load_size), () => current_page_token == data.Token);
                    }

                    // If not sync anymore with current table (might have been reupdated) then return without finishing:
                    if (current_page_token != data.Token)
                        e.Cancel = true;

                    // Or increment index if every alright:
                    else
                        data.CurrentIndex += actual_load_size;

                    // Unlock next call:
                    interlocked = false;

                }));

                // Wait for next call:
                while (interlocked)
                    System.Threading.Thread.Sleep(2);

                interlocked = true;
            }

            e.Result = data;
        }

        /// <summary>
        /// Called when a background loader is done.
        /// </summary>
        /// <param name="sender">The background loaded.</param>
        /// <param name="e">Background loader info.</param>
        private void EndInitializePageTableInBackground(object sender, RunWorkerCompletedEventArgs e)
        {
            // Invoke end-of-loading if stopped naturally:
            if (!e.Cancelled)
                PageLoaded?.Invoke(this, emptyEventArgs);
        }
        #endregion

        #region Modify cell, add or delete row methods
        /// <summary>
        /// Called whenever a value in the column changed (through user input).
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

                // For class definition properties:
                if (properties != null)
                {
                    lock (properties)
                    {
                        var propertydesc = properties?.FirstOrDefault(x => x.Name == propertyname);
                        if (propertydesc != null)
                        {
                            lock (source)
                            {
                                var item = SourceItemsCount > index ? SortedSource[index] : null;
                                if (item != null)
                                    propertydesc.SetValue(item, e.Row[propertyname]);
                            }
                        }
                    }
                }

                // For dynamic properties:
                if (dynamicproperties != null)
                {
                    lock (dynamicproperties)
                    {
                        if (dynamicproperties.Contains(propertyname))
                        {
                            lock (source)
                            {
                                var item = SourceItemsCount > index ? SortedSource[index] : null;
                                if (item is ExpandoObject expandoItem)
                                {
                                    var asDict = (IDictionary<string, object>)expandoItem;
                                    if (asDict.Keys.Contains(propertyname))
                                        asDict[propertyname] = e.Row[propertyname];
                                    return;
                                }
                            }
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Called if a cell as <see cref="INotifyPropertyChanged"/> notifies a property change.
        /// </summary>
        /// <param name="sender">The <see cref="INotifyPropertyChanged"/> that sent the event.</param>
        /// <param name="e">Information about the event.</param>
        private void ReceiveWeakINotifyPropertyChangedEvent(INotifyPropertyChanged sender, PropertyChangedEventArgs e)
        {
            // Block reentrants:
            if (no_reentrancy_prop_changed_weak_event)
                return;
            
            // Block invalids:
            if (sender == null || e == null) return;

            // Find index in source:
            var index = -1;
            lock (SortedSource)  // should lock source as well if same reference.
                index = SortedSource.IndexOf(sender);  // index related to source.

            // If item is not found, ensure 
            if (index < 0)
                return;

            // Change property values on current page only:
            lock (CurrentPage)
            {
                var current_page_index_offset = PagingEnabledInternal ? (int)CurrentPageIndex * _records_per_page : 0;
                if (index >= current_page_index_offset && index < CurrentPage.Rows.Count + current_page_index_offset)
                {
                    index -= current_page_index_offset;

                    // For type properties:
                    if (properties != null)
                        lock (properties)
                        {
                            var property = properties.FirstOrDefault(x => x.Name == e.PropertyName);
                            if (property != null)
                            {
                                CurrentPage.ColumnChanged -= CurrentTable_ColumnChanged;
                                no_reentrancy_prop_changed_weak_event = true;
                                CurrentPage.Rows[index][property.Name] = property.GetValue(sender);
                                CurrentPage.ColumnChanged += CurrentTable_ColumnChanged;
                                no_reentrancy_prop_changed_weak_event = false;
                            }
                        }

                    // For dynamic object properties:
                    if (dynamicproperties != null)
                        lock (dynamicproperties)  // lock even if not using this list to avoid writing cell at same time as other threads.
                            if (sender is IDictionary<string, object> asDict && asDict.ContainsKey(e.PropertyName))  // (as expando object)
                            {
                                CurrentPage.ColumnChanged -= CurrentTable_ColumnChanged;
                                no_reentrancy_prop_changed_weak_event = true;
                                CurrentPage.Rows[index][e.PropertyName] = asDict[e.PropertyName];
                                CurrentPage.ColumnChanged += CurrentTable_ColumnChanged;
                                no_reentrancy_prop_changed_weak_event = false;
                            }
                }

                // If value had changed on a column that is sorted, then update sorted lists:
                if (IsSorting && SortingColumnName == e.PropertyName)
                {
                    // Update only if persistency is set:
                    if (IsSortingPersistent)
                    {
                        no_reentrancy_prop_changed_weak_event = true;
                        UpdateSortedLists();

                        // Do this in UI thread queue to not lock it in case of too quick updates:
                        Application.Current.Dispatcher.BeginInvoke(DispatcherPriority.DataBind, (Action)(() =>
                        {
                            UpdateCurrentPage();
                            no_reentrancy_prop_changed_weak_event = false;
                        }));
                    }
                    else
                        sorting_sync_lost = true;
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
                                no_reentrancy_collection_weak_event = true;  // do not update through observables notifications
                                if (list.Add(createdObject) >= 0)
                                {
                                    UpdateSource();
                                    no_reentrancy_collection_weak_event = false;
                                    return true;
                                }
                            }
                            catch
                            { }
                            finally
                            {
                                no_reentrancy_collection_weak_event = false;
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
                        no_reentrancy_collection_weak_event = true;  // for observable collections, do not update...
                        list.RemoveAt(index);
                        UpdateSource();  // ...and let us update the list manualy.
                        no_reentrancy_collection_weak_event = false;
                    }
                }
            }
        }
        #endregion

        #region Sorting
        /// <summary>
        /// Base function to enumerate all sortable lists at once (saves processing time).
        /// </summary>
        /// <param name="ienum">The source list that will be processed.</param>
        /// <returns>A collection of ordered tupples with ID, checkmark item and source item.</returns>
        private IEnumerable<(int, CheckMark, object)> SortSourceBase(IEnumerable<object> ienum) 
            => ienum.Select((x, i) => (idx: i + (IndexesStartAtZero ? 0 : 1), check: CheckMarks[i], val: x));

        /// <summary>
        /// Customizable extension of <see cref="SortSourceBase(IEnumerable{object})"/> that provides the effective 
        /// sorting method to be applied on it. 
        /// </summary>
        private Func<IEnumerable<object>, IEnumerable<(int, CheckMark, object)>> sortinternal;

        /// <summary>
        /// Gets the property value of a passed item.
        /// </summary>
        /// <param name="property">A valid property descriptor of the property to be read.</param>
        /// <param name="item">The item on which the property must be read.</param>
        /// <param name="sort_member_path">Optional property to be read on the read main property value.</param>
        /// <returns>A <see cref="IComparable"/> value to be used for sorting.</returns>
        private IComparable GetIComparablePropertyValue(PropertyDescriptor property, object item, string sort_member_path = null)
        {
            if (property != null && item != null)
            {
                var result = property.GetValue(item);

                // If provided, find memberpath value:
                if (!string.IsNullOrEmpty(sort_member_path))
                {
                    var subresult = GetIComparablePropertyRecursively(result, sort_member_path);
                    if (subresult != null)
                        return subresult as IComparable ?? subresult.ToString();
                }

                return result as IComparable ?? result.ToString();  // else return self.
            }
            else return null;
        }

        /// <summary>
        /// Gets the dynamic property value of a passed dynamic item.
        /// </summary>
        /// <param name="property">The name of the property to be read.</param>
        /// <param name="item">The item on which the property must be read.</param>
        /// <param name="sort_member_path">Optional property to be read on the read main property value.</param>
        /// <returns>A <see cref="IComparable"/> value to be used for sorting.</returns>
        private IComparable GetIComparableDynamicPropertyValue(string property, IDictionary<string, object> item, string sort_member_path = null)
        {
            if (item != null && item.TryGetValue(property, out object result))
            {
                // If provided, find memberpath value:
                if (!string.IsNullOrEmpty(sort_member_path))
                {
                    var subresult = GetIComparablePropertyRecursively(result, sort_member_path);
                    if (subresult != null)
                        return subresult as IComparable ?? subresult.ToString();
                }

                return result as IComparable ?? result.ToString();
            }
            else return null;
        }

        /// <summary>
        /// Finds property value based on a member path and source object in a recursive manner.
        /// </summary>
        /// <param name="item">The item on which to follow the sort member path.</param>
        /// <param name="sort_member_path">A property path of indefinite length that leads to the value to be
        /// used for sorting.</param>
        /// <returns>The value pointed out by the sort member path on the passed original item, null if object or path are not valid.</returns>
        private IComparable GetIComparablePropertyRecursively(object item, string sort_member_path)
        {
            // If provided, find memberpath value:
            if (item != null && !string.IsNullOrEmpty(sort_member_path))
            {
                var splitted = sort_member_path.Split('.');
                var property_name = splitted[0];

                // In classical properties:
                var matching = TypeDescriptor.GetProperties(item.GetType())?.OfType<PropertyDescriptor>().FirstOrDefault(x => x.Name == property_name);
                if (matching != null)
                {
                    var result = matching.GetValue(item);
                    if (splitted.Length > 1)
                        return GetIComparablePropertyRecursively(result, sort_member_path.Remove(0, property_name.Length + 1));  // path no over, go further.
                    else return result as IComparable ?? result.ToString();  // en of path, return result.
                }

                // In dynamic object properties:
                if (item is ExpandoObject && item is IDictionary<string, object> expando)
                {
                    if (expando.ContainsKey(property_name))
                    {
                        var result = expando[property_name];
                        if (splitted.Length > 1)
                            return GetIComparablePropertyRecursively(result, sort_member_path.Remove(0, property_name.Length + 1));
                        else return result as IComparable ?? result.ToString();
                    }
                }
            }
            
            return null;
        }

        /// <summary>
        /// Sorts items based on a column list of values and order.
        /// </summary>
        /// <param name="column_index">The index of the column to sort.</param>
        /// <param name="ascending_order">Determines if ordering should be ascending or descending on values.</param>
        /// <param name="sort_member_path">Optional property to be read on the read main property value.</param>
        public void SortItems(int column_index, bool ascending_order = true, string sort_member_path = null)
        {
            if (source == null) return;

            // Get column name out of index:
            var column_name = string.Empty;
            lock (CurrentPage)
            {
                if (column_index >= 0 && CurrentPage.Columns.Count > column_index)
                    column_name = CurrentPage.Columns[column_index].ColumnName;
                else if (IsSorting)  // wrong passed index is a motive to stop sorting.
                    StopSorting();
            }

            var had_been_updated = false;

            // Order by checker:
            if (HasCheckmarks && column_name == checkmark_column_name)
            {
                if (ascending_order)
                    sortinternal = (ienum) => SortSourceBase(ienum).OrderBy(x => x.Item2);
                else sortinternal = (ienum) => SortSourceBase(ienum).OrderByDescending(x => x.Item2);
                had_been_updated = true;
            }

            // Order by id:
            else if (HasIndexes && column_name == id_column_name)
            {
                if (ascending_order)
                {
                    // Special case here, we go back 
                    // to default and stop sorting for 
                    // better performances:
                    StopSorting();
                    UpdateCurrentPage();
                    return;
                }
                else
                {
                    sortinternal = (ienum) => SortSourceBase(ienum).Reverse();
                    had_been_updated = true;
                }
            }
            else
            {
                if (properties != null)
                {
                    lock (properties)
                    {
                        var property = properties.FirstOrDefault(x => x.Name == column_name);
                        if (property != null)
                        {
                            // If ascending required:
                            if (ascending_order)
                                sortinternal = (ienum) => SortSourceBase(ienum).OrderBy(x => GetIComparablePropertyValue(property, x.Item3, sort_member_path));
                            else // else order descending:
                                sortinternal = (ienum) => SortSourceBase(ienum).OrderByDescending(x => GetIComparablePropertyValue(property, x.Item3, sort_member_path));
                            had_been_updated = true;
                        }
                    }

                    if (dynamicproperties != null)
                    {
                        lock (dynamicproperties)
                        {
                            lock (source)
                            {
                                if (source.Any() && source.First() is IDictionary<string, object> asDict && asDict.ContainsKey(column_name))  // as expando object
                                {
                                    // If ascending required:
                                    if (ascending_order)
                                        sortinternal = (ienum) => SortSourceBase(ienum).OrderBy(
                                            x => GetIComparableDynamicPropertyValue(column_name, x.Item3 as IDictionary<string, object>, sort_member_path));
                                    else // else order descending:
                                        sortinternal = (ienum) => SortSourceBase(ienum).OrderByDescending(
                                            x => GetIComparableDynamicPropertyValue(column_name, x.Item3 as IDictionary<string, object>, sort_member_path));
                                    had_been_updated = true;
                                }
                            }
                        }
                    }
                }
            }

            if (had_been_updated)
            {
                IsSorting = true;
                SortingColumnName = column_name;
                SortingColumnIndex = column_index;
                UpdateSortedLists();
                UpdateCurrentPage();
            }
        }

        /// <summary>
        /// Sorts all sortable lists (source, checkmarks, IDs).
        /// </summary>
        private void UpdateSortedLists()
        {
            if (source != null)
                lock (source)
                {
                    var fullValues = sortinternal(source).ToList();
                    if (HasCheckmarks)
                        SortedCheckMarks = fullValues.Select(x => x.Item2).ToList();
                    if (HasIndexes)
                        Indexes = fullValues.Select(x => x.Item1).ToList();
                    SortedSource = fullValues.Select(x => x.Item3).ToList();
                }
        }

        /// <summary>
        /// Sorts checker list only.
        /// </summary>
        private void UpdateSortedCheckMarks()
        {
            if (source != null)
                lock (source)
                {
                    var fullValues = sortinternal(source);
                    lock (SortedCheckMarks)
                        SortedCheckMarks = fullValues.Select(x => x.Item2).ToList();
                }
        }

        /// <summary>
        /// Sorts ID list only.
        /// </summary>
        private void UpdateSortedIndexes()
        {
            if (source != null)
                lock (source)
                {
                    var fullValues = sortinternal(source);
                    Indexes = fullValues.Select(x => x.Item1).ToList();
                }
        }

        /// <summary>
        /// Stops sorting of the whole source data.
        /// </summary>
        public void StopSorting()
        {
            IsSorting = false;
            sorting_sync_lost = false;
            if (source != null)
                lock (source)
                {
                    if (HasCheckmarks)
                        SortedCheckMarks = CheckMarks.ToList();
                    if (HasIndexes)
                        Indexes = Enumerable.Range(IndexesStartAtZero ? 0 : 1, SourceItemsCount).ToList(); 
                    else Indexes = new List<int>();
                    SortedSource = source.ToList();
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
            => SourceItemsCount > 0 ? (uint)((SourceItemsCount / _records_per_page) - ((SourceItemsCount % _records_per_page) == 0 ? 1 : 0)) : 0;
        #endregion
    }
}
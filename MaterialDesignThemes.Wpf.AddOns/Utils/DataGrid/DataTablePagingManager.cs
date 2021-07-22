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
using Dynamitey;
using MaterialDesignThemes.Wpf.AddOns.Extensions;

namespace MaterialDesignThemes.Wpf.AddOns.Utils
{
    /// <summary>
    /// A class to manage paging of items placed into an <see cref="IEnumerable"/>.
    /// </summary>
    internal class DataTablePagingManager : IWeakEventListener
    {
        #region Public static constants
        /// <summary>
        /// Indicates how item pool size for background loading.
        /// </summary>
        public static int DefaultThresholdBgLoadSize { get; } = 10000;
        /// <summary>
        /// Indicates how many items are loaded per thread turn (also max size for background loading activation).
        /// </summary>
        public static int DefaultItemBgLoadSize { get; } = 1000;

        // .NET framework badly handles dynamic objects as it regularly sends RuntimeBinderException that slow down 
        // execution. .NET Core is OK and we can get a bit of better performances using Dynamitey instead of manual handling.
        #if NETFRAMEWORK 
        /// <summary>
        /// Handle <see cref="ExpandoObject"/> objects in this code.
        /// </summary>
        private const bool _handleExpando = true;
        #else
        /// <summary>
        /// Let Dynamitey library handle <see cref="ExpandoObject"/> objects.
        /// </summary>
        private const bool _handleExpando = false;
        #endif
        #endregion

        #region Private attributes
        private const string _checkmarkColumnName = "*Selectors";            // Global name of the selectors column (better start with special chars)
        private const string _idColumnName = "*IDs";                         // Global name of the IDs column (better start with special chars)

        private static readonly EventArgs _emptyEventArgs = new EventArgs(); // empty args created once but used multiple times.

        private IEnumerable<object> _source;                                 // reference to source data.
        private IList _sourceAsList;                                         // cast (99% cases) or copy of the source object as a list.
        private bool _pagingEnabled = true;                                  // indicates if pages should be created, enabled by default.
        private int _recordsPerPage = -1;                                    // stores the number of records each page should keep, undefined by default.
        private bool _hasIndexes;                                            // stores HasIndexes state.
        private bool _hasCheckmarks;                                         // stores HasSelectors state.
        private int _indexesColumnPosition;                                  // stores index for IDs column.
        private int _checkmarksColumnPosition;                               // stores index for selectors column.
        private bool _indexesStartAtZero;                                    // tells if indexes should start at zero or one.
        private bool _noReentrancy;                                          // disables reentrancy for some methods.
        private bool _noReentrancyPropChangedWeakEvent;                      // disables reentrancy in property changed weak event handling.
        private bool _noReentrancyCollectionWeakEvent;                       // disables reentrancy in collection weak event handling.
        private bool _noCheckMarkedRowsUpdate;                               // disables update notification of the check marked rows changes.
        private Dictionary<string, CacheableInvocation> _dynamicProperties;  // stores dynamic object properties and their invocators.
        private List<string> _expandoProperties;                             // stores expando object properties.
        private IEnumerable<PropertyDescriptor> _properties;                 // stores 'normal' properties that had been found on source.
        private int _loadThreshold = DefaultThresholdBgLoadSize;             // stores from how many items background loading should occur.
        private int _loadSize = DefaultItemBgLoadSize;                       // stores how many items are loaded per thread turn (also max size for background loading activation).
        private BackgroundWorker _currentTableLoader;                        // a worker that feeds table with rows in a background thread.
        private int _currentPageToken;                                       // stores an ID generated for the current page to be used by the background worker.
        private bool _isSortingPersistent;                                   // stores sorting persistence property.
        private bool _sortingSyncLost;                                       // when sorting persistence is off, indicates if source had been updated in a way sorting if not valid anymore.
        private bool _sourceCountWasZero;                                    // indicates if at some point after a source update, the source count was zero and then became > 0.
        #endregion

        #region Private struct
        /// <summary>
        /// A data set to be used by background worker to load rows.
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
            /// <param name="currentIndex">Current index on partial source.</param>
            /// <param name="partialSource">Portion of the global source that must be processed.</param>
            /// <param name="partialSourceCount">Optional size of this portion.</param>
            public RowUpdateInformation(int token, int currentIndex, IEnumerable<object> partialSource, int partialSourceCount = -1)
            {
                if (currentIndex < 0)
                    throw new IndexOutOfRangeException(nameof(currentIndex) + " is not valid to construct a " + nameof(RowUpdateInformation));
                Token = token;
                CurrentIndex = currentIndex;
                PartialSource = partialSource ?? throw new ArgumentException("Cannot construct a " + nameof(RowUpdateInformation) + " with a null " + nameof(partialSource) + " reference", nameof(partialSource));
                PartialSourceCount = partialSourceCount < 0 ? partialSource.Count() : partialSourceCount;
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
            get => _pagingEnabled;
            set
            {
                if (value == _pagingEnabled)
                    return;
                
                _pagingEnabled = value;
                UpdateCurrentPage();
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
        /// Gets or sets a common base type for source item properties to be used 
        /// for table generation, so columns would all be of this peculiar type.
        /// </summary>
        /// <remarks>Example: set as typeof(object) and any columns would accepts any input data.</remarks>
        public Type IntermediatePropertyValueType { get; set; }

        /// <summary>
        /// Gets or sets the number of records per page.
        /// A zero or negative value let paging but will force
        /// display of all source items on one page.
        /// </summary>
        public int RecordsPerPage
        {
            get => _recordsPerPage;
            set
            {
                if (value != _recordsPerPage)
                {
                    _recordsPerPage = value;
                    if (PagingEnabledInternal)
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
        public bool CurrentPageIsFull => PagingEnabled && CurrentPageRange.Item2 % _recordsPerPage == 0;

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
        /// Indicates if source items are dynamic 
        /// (i.e. implements IDynamicMetaObjectProvider, like ExpandoObject).
        /// </summary>
        public bool AreSourceItemsDynamic { get; private set; }

        /// <summary>
        /// Indicates if source items implements <see cref="INotifyPropertyChanged"/>.
        /// </summary>
        private bool AreSourceItemsINotifyPropertyChanged { get; set; }

        /// <summary>
        /// Gets or sets a value indicating if a selection column
        /// (also called checkmarks) must be included in returned pages. 
        /// </summary>
        public bool HasCheckmarks
        {
            get => _hasCheckmarks;
            set
            {
                if (value == _hasCheckmarks)
                    return;
                
                _hasCheckmarks = value;

                if (value)
                {
                    SyncRowCheckMarksCount();
                    if (IsSorting)
                        UpdateSortedCheckMarks();
                }

                switch (value)
                {
                    // Update ID column index regarding to selector column appearance/disappearance:
                    case true when IndexesColumnPosition >= CheckMarksColumnPosition:
                        _indexesColumnPosition++;
                        break;
                    case false when IndexesColumnPosition >= CheckMarksColumnPosition:
                        _indexesColumnPosition--;
                        break;
                }

                if (IsSorting)
                {
                    switch (value)
                    {
                        case false when CheckMarksColumnPosition == SortingColumnIndex:
                            StopSorting();
                            break;
                        case false when CheckMarksColumnPosition < SortingColumnIndex:
                            SortingColumnIndex--;
                            break;
                        case true when CheckMarksColumnPosition <= SortingColumnIndex:
                            SortingColumnIndex++;
                            break;
                    }
                }

                UpdateCurrentPage();
            }
        }

        /// <summary>
        /// Gets or sets the index of the column holding checkmarks.
        /// </summary>
        public int CheckMarksColumnPosition
        {
            get => _checkmarksColumnPosition;
            set
            {
                if (value == _checkmarksColumnPosition)
                    return;
                
                _checkmarksColumnPosition = value;
                UpdateCurrentPage();
            }
        }

        /// <summary>
        /// Gets or sets a value indicating if indexes should be 
        /// included in the returned pages.
        /// </summary>
        public bool HasIndexes 
        {
            get => _hasIndexes;
            set
            {
                if (value == _hasIndexes)
                    return;
                
                _hasIndexes = value;

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

                    if (IndexesColumnPosition <= CheckMarksColumnPosition)
                        _checkmarksColumnPosition++;
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

                    if (IndexesColumnPosition <= CheckMarksColumnPosition)
                        _checkmarksColumnPosition--;
                }

                UpdateCurrentPage();
            }
        }

        /// <summary>
        /// Gets or sets the index of the column holding indexes.
        /// </summary>
        public int IndexesColumnPosition
        {
            get => _indexesColumnPosition;
            set
            {
                if (value == _indexesColumnPosition)
                    return;
                
                _indexesColumnPosition = value;
                UpdateCurrentPage();
            }
        }

        /// <summary>
        /// Gets or sets the currently displayed indexes.
        /// </summary>
        public int[] DisplayedIndexes { get; } = new int[0];

        /// <summary>
        /// Gets the list of all indexes.
        /// </summary>
        internal List<int> Indexes { get; private set; } = new List<int>();

        /// <summary>
        /// Gets or sets a value indicating if indexes should 
        /// start at zero instead of one.
        /// </summary>
        public bool IndexesStartAtZero
        {
            get => _indexesStartAtZero;
            set
            {
                if (value == _indexesStartAtZero)
                    return;
                
                _indexesStartAtZero = value;
                
                if (HasIndexes)
                    UpdateCurrentPage();
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
        internal IList<object> SortedSource { get; private set; }

        /// <summary>
        /// Gets a sorted version of the internally used checkmarks.
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
        internal string SortingColumnName { get; private set; }

        /// <summary>
        /// Gets or sets a value indicating is persistence must be 
        /// </summary>
        public bool IsSortingPersistent
        {
            get => _isSortingPersistent;
            set
            {
                if (value == _isSortingPersistent)
                    return;
                
                _isSortingPersistent = value;
                
                if (!IsSorting || !_sortingSyncLost || !value)
                    return;
                
                // Force an update as we do not know 
                // if persistence was lost before setting this 
                // value:
                UpdateSortedLists();
                UpdateCurrentPage();
                _sortingSyncLost = false;
            }
        }
        #endregion

        #region Public events
        /// <summary>
        /// Occurs every time the current page (its content) is changed.
        /// </summary>
        public event EventHandler CurrentPageChanged;

        /// <summary>
        /// Occurs every time information about the main table change.
        /// </summary>
        public event EventHandler TableInformationUpdated;

        /// <summary>
        /// Occurs when the selected row list is update.
        /// </summary>
        public event EventHandler CheckMarkedRowsChanged;

        /// <summary>
        /// Produced when the current page starts loading in the background
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
            var hadBeenUpdated = SyncRowCheckMarksCount();
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
                        _noCheckMarkedRowsUpdate = true;  // lock list notification for better performance.
                        var max = Math.Min(newValuesCount, CheckMarks.Count);
                        for (var i = 0; i < max; i++)
                        {
                            if (CheckMarks[i].IsChecked == newValues[i])
                                continue;
                            
                            CheckMarks[i].IsChecked = newValues[i];
                            hadBeenUpdated = true;
                        }
                        _noCheckMarkedRowsUpdate = false;
                    }
                }
            }

            // Now update collection:
            if (hadBeenUpdated)
                SyncRowCheckMarksCount(true); // always force full update when list is externally refreshed
        }

        /// <summary>
        /// Checks all rows.
        /// </summary>
        public void CheckMarkAllRows() => CheckOrUncheckMarksAllRow(true);

        /// <summary>
        /// Unchecks all rows.
        /// </summary>
        public void UncheckMarkAllRows() => CheckOrUncheckMarksAllRow();

        /// <summary>
        /// Checks or unchecks all rows.
        /// </summary>
        /// <param name="check">If true, will check all rows, else will uncheck them.</param>
        private void CheckOrUncheckMarksAllRow(bool check = false)
        {
            var hadBeenUpdated = SyncRowCheckMarksCount();
            lock (CheckMarks)
            {
                hadBeenUpdated |= CheckMarks.Any(x => x.IsChecked != check);  // check if need to be updated.
                if (hadBeenUpdated)
                {
                    _noCheckMarkedRowsUpdate = true;
                    foreach (var t in CheckMarks)
                        t.IsChecked = check;

                    _noCheckMarkedRowsUpdate = false;
                    AllRowsCheckMarkState = check;
                }
            }

            // Now update collection:
            if (hadBeenUpdated)
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
            var checkmarksCount = CheckMarks?.Count ?? 0;
            if (!force && !(CheckMarks == null || checkmarksCount != SourceItemsCount))
                return false;  // no updates needed.

            // Init if needed:
            if (CheckMarks == null)
            {
                CheckMarks = new ObservableCollection<CheckMark>();
                CheckMarks.CollectionChanged += RowCheckMarks_CollectionChanged;
                SortedCheckMarks = new List<CheckMark>();
            }

            // Adjust count:
            var diff = SourceItemsCount - checkmarksCount;
            if (diff != 0)
            {
                var i = 0;
                while (i-- > diff)
                    CheckMarks.RemoveAt(checkmarksCount + diff);
                while (diff-- > 0)
                    CheckMarks.Add(new CheckMark());
                if (!IsSorting)
                    SortedCheckMarks = new List<CheckMark>(CheckMarks);
            }

            // Gross initialization of checked marked rows if needed:
            if (!force && CheckMarkedRows.Count() == CheckMarks.Count(x => x.IsChecked))
                return true;

            if (_sourceAsList == null)
                return true;

            var checkMarkedRows = new List<object>();
            lock (_sourceAsList)
            {
                var i = 0;
                foreach (var check in CheckMarks)
                {
                    if (check.IsChecked)
                        checkMarkedRows.Add(_sourceAsList[i]);
                    i++;
                }
            }
            AllRowsCheckMarkState = (CheckMarks.Any(x => x.IsChecked) ? (bool?)null : false) ?? (CheckMarks.Any(x => !x.IsChecked) ? (bool?)null : true);
            CheckMarkedRows = checkMarkedRows;

            // Update collection here:
            CheckMarkedRowsChanged?.Invoke(this, new EventArgs());

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

            if (e.OldItems == null)
                return;
                
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
            if (!_noCheckMarkedRowsUpdate)
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
            if (_source is INotifyCollectionChanged asObservable1)
                CollectionChangedEventManager.RemoveListener(asObservable1, this);
            _noReentrancyCollectionWeakEvent = false;

            if (_source != null)
            {
                lock (_source)
                {
                    // First process 1% case where list would be a copy of source:
                    if (_sourceAsList != _source)
                    {
                        lock (_sourceAsList)
                            _sourceAsList = source?.ToList();
                    }
                    else _sourceAsList = source != null ? source as IList : null;

                    _source = source;

                    if (source != null)
                    { 
                        SourceItemsCount = source.Count();
                        AreSourceItemsDynamic = source.GetGenericType().GetInterfaces().Contains(typeof(IDynamicMetaObjectProvider));
                        AreSourceItemsINotifyPropertyChanged = source.AreItemsINotifyPropertyChanged();
                    }
                }
            }
            else
            {
                _source = source;
                _sourceAsList = source != null ? (source is IList list ? list : source.ToList()) : null;
                SourceItemsCount = source?.Count() ?? 0;
                AreSourceItemsDynamic = source?.GetGenericType().GetInterfaces().Contains(typeof(IDynamicMetaObjectProvider)) == true;
                AreSourceItemsINotifyPropertyChanged = source?.AreItemsINotifyPropertyChanged() == true;
            }

            if (source == null)
            {
                _sourceAsList = null;
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
                if (_properties != null)
                    lock (_properties)
                        _properties = null;
                _properties = props.OfType<PropertyDescriptor>();

                // Reset dynamic object properties (will be regenerated at first current update with items within):
                if (_expandoProperties != null)
                    lock (_expandoProperties)
                        _expandoProperties = null;

                if (_dynamicProperties != null)
                    lock (_dynamicProperties)
                    {
                        if (_dynamicProperties.Keys.Any())
                            Dynamic.ClearCaches();
                        _dynamicProperties = null;
                    }

            }

            // Set was zero:
            _sourceCountWasZero = SourceItemsCount == 0;

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
            if (this._source is INotifyCollectionChanged asObservable2)
                CollectionChangedEventManager.AddListener(asObservable2, this);
            
            UpdateCurrentPage();
        }

        /// <summary>
        /// Updates current source data.
        /// </summary>
        /// <param name="updateCurrentPage">Set to true to update current table page.</param>
        private void UpdateSource(bool updateCurrentPage = false)
        {
            var fullPageUpdateRequired = false;
            var wasCurrentPageFull = CurrentPageIsFull;

            if (_source != null)
            {
                lock (_source)
                {
                    // First process 1% case where list would be a copy of source:
                    if (_sourceAsList != _source)
                    {
                        lock (_sourceAsList)
                            _sourceAsList = _source is IList ? _source as IList : _source.ToList();
                    }
                    else _sourceAsList = _source is IList ? _source as IList : _source.ToList(); // or just cast current source.

                    var newCount = _sourceAsList.Count;
                    if (SourceItemsCount != newCount)
                    {
                        SourceItemsCount = newCount;
                        
                        SyncRowCheckMarksCount(); 
                        CorrectCurrentPageIndex();
                        UpdateCurrentPageRange();

                        if (IsSorting)
                        {
                            UpdateSortedLists();
                            fullPageUpdateRequired = true;
                        }
                        else
                        {
                            SortedSource = _source.ToList();

                            // Set indexes:
                            if (HasIndexes)
                                Indexes = Enumerable.Range(IndexesStartAtZero ? 0 : 1, SourceItemsCount).ToList();

                            // Full update if our page is not full:
                            if (!(wasCurrentPageFull && CurrentPageIsFull))
                                fullPageUpdateRequired = true;
                        }

                        // Reset dynamic object properties if we are added new first items
                        // to be processed:
                        if (_sourceCountWasZero && SourceItemsCount > 0)
                        {
                            if (_expandoProperties != null)
                                lock (_expandoProperties)
                                    _expandoProperties = null;

                            if (_dynamicProperties != null)
                                lock (_dynamicProperties)
                                {
                                    if (_dynamicProperties.Keys.Any())
                                        Dynamic.ClearCaches();
                                    _dynamicProperties = null;
                                }
                            fullPageUpdateRequired = true;
                        }
                    }
                }
            }

            if (_source == null)
            {
                _sourceAsList = null;
                SortedSource = null;
                SourceItemsCount = 0;
                CheckMarks?.Clear();
                SortedCheckMarks = new List<CheckMark>();
                Indexes = new List<int>();
                fullPageUpdateRequired = true;
            }

            // Set was zero:
            _sourceCountWasZero = SourceItemsCount == 0;

            if (updateCurrentPage)
            {
                if (fullPageUpdateRequired)
                    UpdateCurrentPage();  // Update current page that will trigger the paged changed event.
                else TableInformationUpdated?.Invoke(this, _emptyEventArgs);  // Indicate table main info only had updated.
            }
                
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

            /* Case where a INotifyPropertyChanged object (normally a cell) called */
            if (managerType == typeof(PropertyChangedEventManager))
            {
                if (_source != null)
                    ReceiveWeakINotifyPropertyChangedEvent(sender as INotifyPropertyChanged, e as PropertyChangedEventArgs);
                handled = true;
            }
            /* Case where the source a notifiable collection called: */
            else if (managerType == typeof(CollectionChangedEventManager))
            {
                if (_source != null && !_noReentrancyCollectionWeakEvent)
                {
                    _noReentrancyCollectionWeakEvent = true;
                    // Process through dispatcher in case we are invoked from another thread:
                    Application.Current.Dispatcher.BeginInvoke(DispatcherPriority.DataBind, (Action)(() =>
                    {
                        try
                        {
                            if (_source != sender)  // cancel if source changed between callback setup and execution.
                                return;

                            UpdateSource(true);
                        }
                        finally
                        {
                            _noReentrancyCollectionWeakEvent = false;
                        }
                    }));
                }
                else if (_source == null && sender is INotifyCollectionChanged collection)
                    CollectionChangedEventManager.RemoveListener(collection, this);  // our binding expression is not used anymore, we can shut listening down.

                handled = true;
            }

            return handled;  // must be true otherwise will generate an exception.
        }
        #endregion

        #region Set background row load size
        /// <summary>
        /// Sets load size threshold, i.e. how many items there should be
        /// in a page to trigger background loading.
        /// </summary>
        /// <param name="newValue">A valid size in row counts.</param>
        public void SetLoadSizeThreshold(int newValue)
        {
            if (newValue < 1)
                newValue = 1;

            _loadThreshold = newValue;
        }

        /// <summary>
        /// Sets load size, i.e. how many items are loaded per 
        /// loading method background invocation.
        /// </summary>
        /// <param name="newValue">A valid size in row counts.</param>
        public void SetLoadSize(int newValue)
        {
            if (newValue < 1)
                newValue = 1;

            _loadSize = newValue;
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
            if (_noReentrancy) 
                return CurrentPage;
            _noReentrancy = true;
            if (_source == null || !_pagingEnabled)
            {
                lock (CurrentPage)
                    CurrentPage = GeneratePagedTable(SortedSource);
            }
            else
            {
                // Correct page index:
                CorrectCurrentPageIndex();

                // Update page range:
                UpdateCurrentPageRange();

                // Set current page:
                var pageRecordsCount = _recordsPerPage > 0 ? (int)CurrentPageIndex * _recordsPerPage : 0;
                lock (CurrentPage)
                    lock (_source)
                        CurrentPage = GeneratePagedTable(SortedSource.Skip(pageRecordsCount).Take(_recordsPerPage > 0 ? _recordsPerPage : SourceItemsCount));
            }

            TableInformationUpdated?.Invoke(this, _emptyEventArgs);
            CurrentPageChanged?.Invoke(this, _emptyEventArgs);
            _noReentrancy = false;
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
                    var selectorsColumn = (DataColumn)null;
                    if (HasCheckmarks)
                    {
                        // Note: wildcard character in name because 'normal' autogenerated column headers, 
                        // based on C# property definitions, does not support them so not likely to encounter such user-defined name. 
                        selectorsColumn = new DataColumn(_checkmarkColumnName, typeof(CheckMark), "", MappingType.Hidden);
                        toReturn.Columns.Add(selectorsColumn);
                    }

                    var indexColumn = (DataColumn)null;
                    if (HasIndexes)
                    {
                        indexColumn = new DataColumn(_idColumnName, typeof(int));
                        toReturn.Columns.Add(indexColumn);
                    }

                    // Set column names based on underlying generic source type property names:
                    if (_properties != null)
                        lock (_properties)
                            foreach (var property in _properties)
                            {
                                var type = (property.PropertyType.IsGenericType
                                    && property.PropertyType.GetGenericTypeDefinition() == typeof(Nullable<>) ?
                                        Nullable.GetUnderlyingType(property.PropertyType) : property.PropertyType);
                                if (IntermediatePropertyValueType != null && type.IsSubclassOf(IntermediatePropertyValueType))
                                    type = IntermediatePropertyValueType;
                                toReturn.Columns.Add(property.Name, type);
                            }

                    // Dynamic types, based on first item:
                    if (partialSource.Any() && partialSource.First() is IDynamicMetaObjectProvider)
                    {
                        // If expando:
                        if (_handleExpando && partialSource.First() is ExpandoObject asExpando)
                        {
                            var asExpandoDict = (IDictionary<string, object>)asExpando;
                            if (_expandoProperties == null)
                                _expandoProperties = asExpandoDict.Keys.ToList();

                            if (_expandoProperties != null)
                            {
                                var expandoToRemove = _expandoProperties.Except(asExpandoDict.Keys).ToList();
                                foreach (var property in _expandoProperties.Intersect(asExpandoDict.Keys))
                                    if (asExpandoDict.ContainsKey(property))
                                    {
                                        var value = asExpandoDict[property];
                                        if (value != null)
                                        {
                                            var type = value.GetType();
                                            if (IntermediatePropertyValueType != null && type.IsSubclassOf(IntermediatePropertyValueType))
                                                type = IntermediatePropertyValueType;
                                            toReturn.Columns.Add(property, type);
                                        }
                                        else expandoToRemove.Add(property);
                                    }
                                expandoToRemove.ForEach(x => _expandoProperties.Remove(x));
                            }
                        }
                        // if any other dynamic object:
                        else
                        {
                            if (_dynamicProperties == null)
                            {
                                _dynamicProperties = new Dictionary<string, CacheableInvocation>();
                                var dynProps = Dynamic.GetMemberNames(partialSource.First(), dynamicOnly: true);  // do not pull out class members that we already have.
                                foreach (var property in dynProps)
                                    _dynamicProperties[property] = new CacheableInvocation(InvocationKind.Get, property);
                            }

                            if (_dynamicProperties != null)
                            {
                                var dynamicToRemove = new List<string>();
                                lock (_dynamicProperties)
                                {
                                    foreach (var getter in _dynamicProperties)
                                    {
                                        try
                                        {
                                            var propertyValue = Dynamic.InvokeGet(partialSource.First(), getter.Key);
                                            if (propertyValue != null)
                                            {
                                                var type = ((object)propertyValue).GetType();
                                                if (IntermediatePropertyValueType != null && type.IsSubclassOf(IntermediatePropertyValueType))
                                                    type = IntermediatePropertyValueType;
                                                toReturn.Columns.Add(getter.Key, type);
                                            }
                                            else dynamicToRemove.Add(getter.Key);
                                        }
                                        catch
                                        {
                                            dynamicToRemove.Add(getter.Key); // remove properties for which getter fails for any reason.
                                        }
                                    }
                                    dynamicToRemove.ForEach(x => _dynamicProperties.Remove(x));
                                }
                            }
                        }
                    }

                    // Reorder special columns:
                    if (HasCheckmarks && CheckMarksColumnPosition > 0)
                        toReturn.Columns[selectorsColumn.ColumnName].SetOrdinal(CheckMarksColumnPosition);

                    if (HasIndexes && IndexesColumnPosition > 0)
                        toReturn.Columns[indexColumn.ColumnName].SetOrdinal(IndexesColumnPosition);

                    // Load rows only if source is not empty:
                    var pageRowCount = partialSource.Count();
                    if (pageRowCount > 0)
                    { 
                        var globalStartIndex = PagingEnabledInternal ? (int)CurrentPageIndex * _recordsPerPage : 0;

                        // Build all rows based on a 'model' row without filling user properties:
                        var row = toReturn.NewRow();  // our model row
                        toReturn.Rows.Add(row);  // mandatory to add it in table otherwise importing copies won't work

                        for (var i = globalStartIndex; i < pageRowCount + globalStartIndex; i++)
                        {
                            // Fill checkmark if has any:
                            if (HasCheckmarks)
                                row[_checkmarkColumnName] = SortedCheckMarks[i];

                            // Fill ID if has any:
                            if (HasIndexes)
                                row[_idColumnName] = Indexes[i];

                            // Import empty row which if much 
                            // faster than adding a new one:
                            toReturn.ImportRow(row);
                        }

                        toReturn.Rows.RemoveAt(0);  // remove 'model' item.

                        // Update token for previous background threads syncing:
                        _currentPageToken++;

                        // Update as much rows as the load_size parameter allows:
                        var actualLoadSize = Math.Min(pageRowCount, _loadThreshold);
                        InitializeRows(toReturn, 0, partialSource.Take(actualLoadSize));

                        // Fill rows in background if there are too many of them:
                        if (pageRowCount > _loadThreshold)
                        {
                            //For that: create background worker if not existing:
                            if (_currentTableLoader == null || _currentTableLoader.IsBusy)
                            {
                                _currentTableLoader = new BackgroundWorker();
                                _currentTableLoader.DoWork += InitializePageTableInBackground;
                                _currentTableLoader.RunWorkerCompleted += EndInitializePageTableInBackground;
                            }

                            // Note: start from load size as we already processed actual_load_size items:
                            var rowsToProcess = new RowUpdateInformation(_currentPageToken, actualLoadSize, partialSource, pageRowCount);
                            _currentTableLoader.RunWorkerAsync(rowsToProcess);
                            PageLoading?.Invoke(this, _emptyEventArgs);
                        }
                        else // indicates that page is ready without triggering page loading event:
                        {
                            PageLoaded?.Invoke(this, _emptyEventArgs);
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
        /// <param name="startIndex">Starting index on the passed partial source.</param>
        /// <param name="partialSource">The source of items where to get values from.</param>
        /// <param name="validityChecker">An optional function to check if we should quit or not this method during processing.</param>
        private void InitializeRows(DataTable table, int startIndex, IEnumerable<object> partialSource, Func<bool> validityChecker = null)
        {
            if (table == null || partialSource == null)
                return;
            
            // Disable table updates notifications:
            table.ColumnChanged -= CurrentTable_ColumnChanged;

            // Fill rows:
            var i = startIndex;
            var hasINotify = AreSourceItemsINotifyPropertyChanged;
            foreach (var item in partialSource)
            {
                // Quit without notification if context is not valid anymore:
                if (validityChecker?.Invoke() == false)
                    return;

                // Get row to update:
                var row = table.Rows[i];

                // (Disable table updates again as sometimes they are reactivated by other running threads:)
                table.ColumnChanged -= CurrentTable_ColumnChanged;

                // Process normal properties:
                if (_properties != null)
                    lock (_properties)
                        foreach (var property in _properties)
                        {
                            // Set value:
                            row[property.Name] = property.GetValue(item);

                            // Subscribe to any further changes if possible:
                            if (hasINotify)
                                PropertyChangedEventManager.AddListener((item as INotifyPropertyChanged), this, property.Name);
                        }

                // Process dynamic properties:
                if (_handleExpando && _expandoProperties != null && item is ExpandoObject asExpando)
                    lock (_expandoProperties)
                    {
                        var asDict = (IDictionary<string, object>)asExpando;
                        foreach (var property in _expandoProperties.Intersect(asDict.Keys))
                        {
                            // Set value:
                            row[property] = asDict[property];

                            // Subscribe to any further changes if possible:
                            if (hasINotify)
                                PropertyChangedEventManager.AddListener(asExpando, this, property);
                        }
                    }

                if (_dynamicProperties != null)
                    lock (_dynamicProperties)
                        foreach (var getter in _dynamicProperties)
                        {
                            // Set value:
                            row[getter.Key] = getter.Value.Invoke(item);

                            // Subscribe to any further changes if possible:
                            if (hasINotify)
                                PropertyChangedEventManager.AddListener((item as INotifyPropertyChanged), this, getter.Key);
                        }

                // Increment indexes:
                i++;
            }

            // Set table update notification back:
            table.ColumnChanged += CurrentTable_ColumnChanged;
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

            // Stop if current table is no more valid or if succeeded to load every rows of partial source:
            while (_currentPageToken == data.Token && data.CurrentIndex < data.PartialSourceCount)
            {
                Application.Current.Dispatcher.BeginInvoke(DispatcherPriority.DataBind, new Action(() =>
                {
                    // Calculate load size:
                    var actualLoadSize = Math.Min(data.PartialSourceCount - data.CurrentIndex, _loadSize);

                    // Update rows:
                    if (_currentPageToken == data.Token)
                    {
                        lock (CurrentPage)
                            InitializeRows(CurrentPage, data.CurrentIndex, data.PartialSource.Skip(data.CurrentIndex).Take(actualLoadSize), () => _currentPageToken == data.Token);
                    }

                    // If not sync anymore with current table (might have been re-updated) then return without finishing:
                    if (_currentPageToken != data.Token)
                        e.Cancel = true;

                    // Or increment index if every alright:
                    else
                        data.CurrentIndex += actualLoadSize;

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
                PageLoaded?.Invoke(this, _emptyEventArgs);
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
            if (HasCheckmarks && e.Column.ColumnName == _checkmarkColumnName 
                || HasIndexes && e.Column.ColumnName == _idColumnName)
                return;

            // Find index:
            var index = (int)(CurrentPage.Rows.IndexOf(e.Row) + (PagingEnabled ? CurrentPageIndex * RecordsPerPage : 0));
            if (index < 0)
                return;
            
            var propertyName = e.Column.ColumnName;

            // For class definition properties:
            if (_properties != null)
            {
                lock (_properties)
                {
                    var propertyDescriptor = _properties?.FirstOrDefault(x => x.Name == propertyName);
                    if (propertyDescriptor != null)
                    {
                        lock (_source)
                        {
                            var item = SourceItemsCount > index ? SortedSource[index] : null;
                            if (item != null)
                                propertyDescriptor.SetValue(item, e.Row[propertyName]);
                        }
                    }
                }
            }

            // For dynamic properties:
            if (_handleExpando && _expandoProperties != null)
                lock (_expandoProperties)
                    if (_expandoProperties.Contains(propertyName))
                        lock (_source)
                        {
                            var item = SourceItemsCount > index ? SortedSource[index] as IDictionary<string, object> : null;
                            if (item != null)
                                item[propertyName] = e.Row[propertyName];
                        }

            if (_dynamicProperties == null)
                return;

            lock (_dynamicProperties)
                if (_dynamicProperties.Keys.Contains(propertyName))
                    lock (_source)
                    {
                        var item = SourceItemsCount > index ? SortedSource[index] : null;
                        if (item != null)
                            Dynamic.InvokeSet(item, propertyName, e.Row[propertyName]);
                    }
        }

        /// <summary>
        /// Called if a cell as <see cref="INotifyPropertyChanged"/> notifies a property change.
        /// </summary>
        /// <param name="sender">The <see cref="INotifyPropertyChanged"/> that sent the event.</param>
        /// <param name="e">Information about the event.</param>
        private void ReceiveWeakINotifyPropertyChangedEvent(INotifyPropertyChanged sender, PropertyChangedEventArgs e)
        {
            // Block re-entrants:
            if (_noReentrancyPropChangedWeakEvent)
                return;
            
            // Block invalids:
            if (sender == null || e == null) return;

            // Find index in source:
            int index;
            lock (SortedSource)  // should lock source as well if same reference.
                index = SortedSource.IndexOf(sender);  // index related to source.

            // If item is not found, ensure 
            if (index < 0)
                return;

            // Change property values on current page only:
            lock (CurrentPage)
            {
                var currentPageIndexOffset = PagingEnabledInternal ? (int)CurrentPageIndex * _recordsPerPage : 0;
                if (index >= currentPageIndexOffset && index < CurrentPage.Rows.Count + currentPageIndexOffset)
                {
                    index -= currentPageIndexOffset;

                    // For type properties:
                    if (_properties != null)
                        lock (_properties)
                        {
                            var property = _properties.FirstOrDefault(x => x.Name == e.PropertyName);
                            if (property != null)
                            {
                                CurrentPage.ColumnChanged -= CurrentTable_ColumnChanged;
                                _noReentrancyPropChangedWeakEvent = true;
                                CurrentPage.Rows[index][property.Name] = property.GetValue(sender);
                                CurrentPage.ColumnChanged += CurrentTable_ColumnChanged;
                                _noReentrancyPropChangedWeakEvent = false;
                            }
                        }

                    // For dynamic object properties:
                    if (_handleExpando && _expandoProperties != null)
                        lock (_expandoProperties)  // lock even if not using this list to avoid writing cell at same time as other threads.
                            if (sender is IDictionary<string, object> asDict)
                                if (asDict.ContainsKey(e.PropertyName))
                                {
                                    CurrentPage.ColumnChanged -= CurrentTable_ColumnChanged;
                                    _noReentrancyPropChangedWeakEvent = true;
                                    var value = asDict[e.PropertyName];
                                    var valueType = value.GetType();
                                    var columnDataType = CurrentPage.Columns[e.PropertyName].DataType;
                                    // Must check type as dynamic objects might have different ones from line to line:
                                    if (valueType == columnDataType || valueType.IsSubclassOf(columnDataType)) 
                                        CurrentPage.Rows[index][e.PropertyName] = value;
                                    CurrentPage.ColumnChanged += CurrentTable_ColumnChanged;
                                    _noReentrancyPropChangedWeakEvent = false;
                                }

                    if (_dynamicProperties != null)
                        lock (_dynamicProperties)  // lock even if not using this list to avoid writing cell at same time as other threads.
                            if (sender is IDynamicMetaObjectProvider)
                            {
                                CurrentPage.ColumnChanged -= CurrentTable_ColumnChanged;
                                _noReentrancyPropChangedWeakEvent = true;
                                var value = _dynamicProperties[e.PropertyName].Invoke(sender);       
                                var valueType = value.GetType();
                                var columnDataType = CurrentPage.Columns[e.PropertyName].DataType;
                                // Must check type as dynamic objects might have different ones from line to line:
                                if (valueType == columnDataType || valueType.IsSubclassOf(columnDataType))
                                    CurrentPage.Rows[index][e.PropertyName] = value;
                                CurrentPage.ColumnChanged += CurrentTable_ColumnChanged;
                                _noReentrancyPropChangedWeakEvent = false;
                            }
                }

                // If value had changed on a column that is sorted, then update sorted lists:
                if (!IsSorting || SortingColumnName != e.PropertyName)
                    return;
                
                // Update only if persistence is set:
                if (IsSortingPersistent)
                {
                    _noReentrancyPropChangedWeakEvent = true;
                    UpdateSortedLists();

                    // Do this in UI thread queue to not lock it in case of too quick updates:
                    Application.Current.Dispatcher.BeginInvoke(DispatcherPriority.DataBind, (Action)(() =>
                    {
                        UpdateCurrentPage();
                        _noReentrancyPropChangedWeakEvent = false;
                    }));
                }
                else
                    _sortingSyncLost = true;
            }
        }

        /// <summary>
        /// Adds a new empty row in the source list.
        /// </summary>
        /// <returns>True if succeeded to add item, false otherwise.</returns>
        public bool AddRow()
        {
            // Construct new item:
            lock (_source)
            {
                if (!(_source is IList list) || list.IsReadOnly)
                    return false;
                
                // Find underlying type in our source:
                var type = _source.GetGenericType();
                if (type == null)
                    return false;
                
                // Find default constructor:
                var parameters = new Type[0];
                var constructor = type.GetConstructor(parameters);
                if (constructor == null)
                    return false;
                
                try  // we do not want the whole app to collapse because of generic UI item creation.
                {
                    var createdObject = constructor.Invoke(parameters);
                    _noReentrancyCollectionWeakEvent = true;  // do not update through observables notifications

                    // For dynamic objects, try to set dynamic properties on the new object using first object
                    // definition (properties used to pattern new object and default values provided by reflection):
                    if (AreSourceItemsDynamic)
                    {
                        if (_handleExpando && _expandoProperties != null 
                                            && _source.Any() && _source.First() is IDictionary<string, object> firstAsDict 
                                            && createdObject is IDictionary<string, object> createdAsDict)
                        {
                            foreach (var property in _expandoProperties.Intersect(firstAsDict.Keys))
                            {
                                var value = firstAsDict[property];
                                if (value != null)
                                    createdAsDict[property] = value.GetType().GetDefault();
                            }
                        }
                        else if(_dynamicProperties != null
                                && _source.Any() && _source.First() is IDynamicMetaObjectProvider firstItem
                                && createdObject is IDynamicMetaObjectProvider)
                        {
                            foreach (var property in _dynamicProperties)
                            {
                                var value = property.Value.Invoke(firstItem);
                                if (value != null)
                                    Dynamic.InvokeSet(createdObject, property.Key, value.GetType().GetDefault());
                            }
                        }
                    }

                    if (list.Add(createdObject) >= 0)
                    {
                        UpdateSource();
                        _noReentrancyCollectionWeakEvent = false;
                        return true;
                    }
                }
                catch
                {
                    // ignored
                }
                finally
                {
                    _noReentrancyCollectionWeakEvent = false;
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

            if (index < 0)
                return;
            
            lock (_source)
            {
                if (!(_source is IList list) || list.IsReadOnly || list.Count <= index)
                    return;
                
                _noReentrancyCollectionWeakEvent = true;  // for observable collections, do not update...
                list.RemoveAt(index);
                UpdateSource();  // ...and let us update the list manually.
                _noReentrancyCollectionWeakEvent = false;
            }
        }
        #endregion

        #region Sorting
        /// <summary>
        /// Base function to enumerate all sortable lists at once (saves processing time).
        /// </summary>
        /// <param name="iEnumerable">The source list that will be processed.</param>
        /// <returns>A collection of ordered tuples with ID, checkmark item and source item.</returns>
        private IEnumerable<(int, CheckMark, object)> SortSourceBase(IEnumerable<object> iEnumerable) 
            => iEnumerable.Select((x, i) => (idx: i + (IndexesStartAtZero ? 0 : 1), check: CheckMarks[i], val: x));

        /// <summary>
        /// Customizable extension of <see cref="SortSourceBase(IEnumerable{object})"/> that provides the effective 
        /// sorting method to be applied on it. 
        /// </summary>
        private Func<IEnumerable<object>, IEnumerable<(int, CheckMark, object)>> _sortInternal;

        /// <summary>
        /// Gets the property value of a passed item.
        /// </summary>
        /// <param name="property">A valid property descriptor of the property to be read.</param>
        /// <param name="item">The item on which the property must be read.</param>
        /// <param name="sortMemberPath">Optional property to be read on the read main property value.</param>
        /// <returns>A <see cref="IComparable"/> value to be used for sorting.</returns>
        private IComparable GetIComparablePropertyValue(PropertyDescriptor property, object item, string sortMemberPath = null)
        {
            if (property == null || item == null)
                return null;
            
            var result = property.GetValue(item);
            if (result == null)
                return null;

            // If provided, find member path value:
            if (string.IsNullOrEmpty(sortMemberPath))
                return result as IComparable ?? result.ToString(); // else return self.
                
            var subResult = GetIComparablePropertyRecursively(result, sortMemberPath);
            if (subResult != null)
                return subResult;

            return result as IComparable ?? result.ToString();  // else return self.
        }

        /// <summary>
        /// Gets the dynamic property value of a passed expando item.
        /// </summary>
        /// <param name="propertyName">The name of the property to retrieve.</param>
        /// <param name="item">The item on which the property must be read.</param>
        /// <param name="sortMemberPath">Optional property to be read on the read main property value.</param>
        /// <returns>A <see cref="IComparable"/> value to be used for sorting.</returns>
        private IComparable GetIComparableExpandoPropertyValue(string propertyName, ExpandoObject item, string sortMemberPath = null)
        {
            if (string.IsNullOrEmpty(propertyName) || !(item is IDictionary<string, object> asDict) || !asDict.TryGetValue(propertyName, out var result))
                return null;
            
            // If provided, find member path value:
            if (string.IsNullOrEmpty(sortMemberPath))
                return result as IComparable ?? result.ToString();
            var subResult = GetIComparablePropertyRecursively(result, sortMemberPath);
            if (subResult != null)
                return subResult;

            return result as IComparable ?? result.ToString();

        }

        /// <summary>
        /// Gets the dynamic property value of a passed dynamic item.
        /// </summary>
        /// <param name="getter">Cached getter function to be used for property value retrieval.</param>
        /// <param name="item">The item on which the property must be read.</param>
        /// <param name="sortMemberPath">Optional property to be read on the read main property value.</param>
        /// <returns>A <see cref="IComparable"/> value to be used for sorting.</returns>
        private IComparable GetIComparableDynamicPropertyValue(Invocation getter, IDynamicMetaObjectProvider item, string sortMemberPath = null)
        {
            if (item == null || getter == null)
                return null;
            
            try
            {
                var result = getter.Invoke(item);

                // If provided, find member path value:
                if (string.IsNullOrEmpty(sortMemberPath))
                    return result as IComparable ?? result.ToString();
                    
                var subResult = GetIComparablePropertyRecursively(result, sortMemberPath);
                if (subResult != null)
                    return subResult;

                return result as IComparable ?? result.ToString();
            }
            catch { return null; }
        }

        /// <summary>
        /// Finds property value based on a member path and source object in a recursive manner.
        /// </summary>
        /// <param name="item">The item from which to follow the sort member path.</param>
        /// <param name="sortMemberPath">A property path of indefinite length that leads to the value to be
        /// used for sorting.</param>
        /// <returns>The value pointed out by the sort member path on the passed original item, null if object or path are not valid.</returns>
        private IComparable GetIComparablePropertyRecursively(object item, string sortMemberPath)
        {
            // If provided, find member path value:
            if (item == null || string.IsNullOrEmpty(sortMemberPath))
                return null;
            
            var split = sortMemberPath.Split('.');
            var propertyName = split[0];

            // In classical properties:
            var matching = TypeDescriptor.GetProperties(item.GetType()).OfType<PropertyDescriptor>().FirstOrDefault(x => x.Name == propertyName);
            if (matching != null)
            {
                var result = matching.GetValue(item);
                if (result == null)
                    return null;
                
                if (split.Length > 1)
                    return GetIComparablePropertyRecursively(result, sortMemberPath.Remove(0, propertyName.Length + 1));  // path no over, go further.
                return result as IComparable ?? result.ToString();  // en of path, return result.
            }

            // As Expando object:
            if (_handleExpando && item is ExpandoObject asExpando)
            {
                var asDict = (IDictionary<string, object>)asExpando;
                if (!asDict.ContainsKey(propertyName))
                    return null;
                
                var result = asDict[propertyName];
                if (split.Length > 1)
                    return GetIComparablePropertyRecursively(result, sortMemberPath.Remove(0, propertyName.Length + 1));
                return result as IComparable ?? result.ToString();
            }

            // In dynamic object properties:
            else if (item is IDynamicMetaObjectProvider asDynamic)
            {
                var names = Dynamic.GetMemberNames(asDynamic, dynamicOnly:true);
                if (!names.Contains(propertyName))
                    return null;
                var result = Dynamic.InvokeGet(asDynamic, propertyName);
                if (split.Length > 1)
                    return GetIComparablePropertyRecursively(result, sortMemberPath.Remove(0, propertyName.Length + 1));
                
                return result as IComparable ?? result.ToString();
            }

            return null;
        }

        /// <summary>
        /// Sorts items based on a column list of values and order.
        /// </summary>
        /// <param name="columnIndex">The index of the column to sort.</param>
        /// <param name="ascendingOrder">Determines if ordering should be ascending or descending on values.</param>
        /// <param name="sortMemberPath">Optional property to be read on the read main property value.</param>
        public void SortItems(int columnIndex, bool ascendingOrder = true, string sortMemberPath = null)
        {
            if (_source == null) return;

            // Get column name out of index:
            var columnName = string.Empty;
            lock (CurrentPage)
            {
                if (columnIndex >= 0 && CurrentPage.Columns.Count > columnIndex)
                    columnName = CurrentPage.Columns[columnIndex].ColumnName;
                else if (IsSorting)  // wrong passed index is a motive to stop sorting.
                    StopSorting();
            }

            var hadBeenUpdated = false;

            // Order by checker:
            if (HasCheckmarks && columnName == _checkmarkColumnName)
            {
                if (ascendingOrder)
                    _sortInternal = (value) => SortSourceBase(value).OrderBy(x => x.Item2);
                else _sortInternal = (value) => SortSourceBase(value).OrderByDescending(x => x.Item2);
                hadBeenUpdated = true;
            }

            // Order by id:
            else if (HasIndexes && columnName == _idColumnName)
            {
                if (ascendingOrder)
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
                    _sortInternal = (value) => SortSourceBase(value).Reverse();
                    hadBeenUpdated = true;
                }
            }
            else
            {
                if (_properties != null)
                {
                    lock (_properties)
                    {
                        var property = _properties.FirstOrDefault(x => x.Name == columnName);
                        if (property != null)
                        {
                            // If ascending required:
                            if (ascendingOrder)
                                _sortInternal = (value) => SortSourceBase(value).OrderBy(x => GetIComparablePropertyValue(property, x.Item3, sortMemberPath));
                            else // else order descending:
                                _sortInternal = (value) => SortSourceBase(value).OrderByDescending(x => GetIComparablePropertyValue(property, x.Item3, sortMemberPath));
                            hadBeenUpdated = true;
                        }
                    }

                    if (_expandoProperties != null)
                    {
                        lock (_expandoProperties)
                        {
                            lock (_source)
                            {
                                if (_expandoProperties.Contains(columnName))
                                {
                                    // If ascending required:
                                    if (ascendingOrder)
                                        _sortInternal = (value) => SortSourceBase(value).OrderBy(
                                            x => GetIComparableExpandoPropertyValue(columnName, x.Item3 as ExpandoObject, sortMemberPath));
                                    else // else order descending:
                                        _sortInternal = (value) => SortSourceBase(value).OrderByDescending(
                                            x => GetIComparableExpandoPropertyValue(columnName, x.Item3 as ExpandoObject, sortMemberPath));
                                    hadBeenUpdated = true;
                                }
                            }
                        }
                    }

                    if (_dynamicProperties != null)
                    {
                        lock (_dynamicProperties)
                        {
                            lock (_source)
                            {
                                if (_dynamicProperties.TryGetValue(columnName, out CacheableInvocation getter))
                                {
                                    // If ascending required:
                                    if (ascendingOrder)
                                        _sortInternal = (value) => SortSourceBase(value).OrderBy(
                                            x => GetIComparableDynamicPropertyValue(getter, x.Item3 as IDynamicMetaObjectProvider, sortMemberPath));
                                    else // else order descending:
                                        _sortInternal = (value) => SortSourceBase(value).OrderByDescending(
                                            x => GetIComparableDynamicPropertyValue(getter, x.Item3 as IDynamicMetaObjectProvider, sortMemberPath));
                                    hadBeenUpdated = true;
                                }
                            }
                        }
                    }
                }
            }

            if (!hadBeenUpdated)
                return;
            
            IsSorting = true;
            SortingColumnName = columnName;
            SortingColumnIndex = columnIndex;
            UpdateSortedLists();
            UpdateCurrentPage();
        }

        /// <summary>
        /// Sorts all sortable lists (source, checkmarks, IDs).
        /// </summary>
        private void UpdateSortedLists()
        {
            if (_source == null)
                return;
            
            lock (_source)
            {
                var fullValues = _sortInternal(_source).ToList();
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
            if (_source == null)
                return;
            
            lock (_source)
            {
                var fullValues = _sortInternal(_source);
                lock (SortedCheckMarks)
                    SortedCheckMarks = fullValues.Select(x => x.Item2).ToList();
            }
        }

        /// <summary>
        /// Sorts ID list only.
        /// </summary>
        private void UpdateSortedIndexes()
        {
            if (_source == null)
                return;
            
            lock (_source)
            {
                var fullValues = _sortInternal(_source);
                Indexes = fullValues.Select(x => x.Item1).ToList();
            }
        }

        /// <summary>
        /// Stops sorting of the whole source data.
        /// </summary>
        private void StopSorting()
        {
            IsSorting = false;
            _sortingSyncLost = false;
            if (_source == null)
                return;
            
            lock (_source)
            {
                if (HasCheckmarks)
                    SortedCheckMarks = CheckMarks.ToList();
                Indexes = HasIndexes ? Enumerable.Range(IndexesStartAtZero ? 0 : 1, SourceItemsCount).ToList() : new List<int>();
                SortedSource = _source.ToList();
            }
        }
        #endregion

        #region Navigation methods
        /// <summary>
        /// Indicates if a next page exists.
        /// </summary>
        /// <returns>True if a next page exists, false otherwise.</returns>
        public bool HasNext() => _source != null && PagingEnabledInternal && CurrentPageIndex < GetMaxCurrentPageIndex();

        /// <summary>
        /// Navigate to next page.
        /// </summary>
        /// <returns>The page following the current one, if existing.</returns>
        public DataTable GoNext()
        {
            if (_source == null || !PagingEnabledInternal) 
                return GeneratePagedTable(_source);
            CurrentPageIndex++;
            CorrectCurrentPageIndex();
            return UpdateCurrentPage();
        }

        /// <summary>
        /// Indicates if a previous page exists.
        /// </summary>
        /// <returns>True if a previous page exists, false otherwise.</returns>
        public bool HasPrevious() => _source != null && PagingEnabledInternal && CurrentPageIndex > 0;

        /// <summary>
        /// Navigate to previous page.
        /// </summary>
        /// <returns>The page defined before the current one, if existing.</returns>
        public DataTable GoPrevious()
        {
            if (_source == null || !PagingEnabledInternal)
                return GeneratePagedTable(_source);

            CurrentPageIndex--;

            return UpdateCurrentPage();
        }

        /// <summary>
        /// Navigate to the first page.
        /// </summary>
        /// <returns>The first page if existing.</returns>
        public DataTable GoFirst()
        {
            if (_source == null || !PagingEnabledInternal)
                return GeneratePagedTable(_source);

            CurrentPageIndex = 0;
            return UpdateCurrentPage();
        }

        /// <summary>
        /// Navigate to the last page.
        /// </summary>
        /// <returns>The last page if existing.</returns>
        public DataTable GoLast()
        {
            if (_source == null || !PagingEnabledInternal)
                return GeneratePagedTable(_source);

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
            if (_recordsPerPage > 0 && CurrentPageIndex > GetMaxCurrentPageIndex())
                CurrentPageIndex = GetMaxCurrentPageIndex();
        }

        /// <summary>
        /// Gets the maximum item index of a given page.
        /// </summary>
        /// <returns>The maximum index of the page.</returns>
        private uint GetMaxCurrentPageIndex()
            => SourceItemsCount > 0 ? (uint)((SourceItemsCount / _recordsPerPage) - ((SourceItemsCount % _recordsPerPage) == 0 ? 1 : 0)) : 0;

        /// <summary>
        /// Updates the current page range.
        /// </summary>
        private void UpdateCurrentPageRange()
        {
            // Check that current page count is ok regarding to list size, rearrange otherwise:
            var pageRecordsCount = _recordsPerPage > 0 ? (int)CurrentPageIndex * _recordsPerPage : 0;
            CurrentPageRange =
                (pageRecordsCount + (SourceItemsCount <= 0 ? 0 : 1),
                _recordsPerPage > 0 ?
                    ((pageRecordsCount + _recordsPerPage) > SourceItemsCount ? SourceItemsCount : (pageRecordsCount + _recordsPerPage))
                    : SourceItemsCount);
        }
        #endregion
    }
}
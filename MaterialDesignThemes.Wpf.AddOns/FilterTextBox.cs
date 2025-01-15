using System;
using System.Collections;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using MaterialDesignThemes.Wpf.AddOns.Helpers;
using MaterialDesignThemes.Wpf.AddOns.Utils;
using MaterialDesignThemes.Wpf.AddOns.Utils.Filtering;
using MaterialDesignThemes.Wpf.AddOns.Utils.Reflection;

namespace MaterialDesignThemes.Wpf.AddOns
{
    /// <summary>
    /// A <see cref="TextBox"/> that can be used to filter items out
    /// from an associated items control pointed by <see cref="AssociatedItemsControl"/>.
    /// </summary>
    public class FilterTextBox : TextBox
    {
        /// <summary>
        /// Current filter value.
        /// </summary>
        protected string _filterCache;

        private CollectionChangedWeakEventListener _sourceCollectionListener;

        private int _totalAssociatedControlItemsCount;

        private Predicate<object> _filter;
        private PropertyGetter[] _itemFilterPropertyGetters = Array.Empty<PropertyGetter>();

        #region Constructors and initializations
        /// <summary>
        /// Static constructor for <see cref="FilterTextBox"/> type.
        /// Override some base dependency properties.
        /// </summary>
        static FilterTextBox()
        {
            DefaultStyleKeyProperty.OverrideMetadata(typeof(FilterTextBox), new FrameworkPropertyMetadata(typeof(FilterTextBox)));
        }
        #endregion

        #region Filtering part
        /// <summary>
        /// Applies current filter on items.
        /// </summary>
        /// <param name="force">Forces filtering even if filter value did not change.</param>
        protected virtual void ApplyFilter(bool force = false)
        {
            Dispatcher.BeginInvoke(DispatcherPriority.Input, new Action(() =>
            {
                var trimmedFilter = Filter?.TrimStart().TrimEnd();
                if (!force && _filterCache == trimmedFilter)
                    return;

                _sourceCollectionListener?.Pause();
                _filterCache = trimmedFilter;

                ResetFilter();

                _filter = item => TextFilter.IsItemMatchingFilter(item,
                                                                  _itemFilterPropertyGetters,
                                                                  _filterCache,
                                                                  IgnoreCase,
                                                                  AlsoMatchFilterWordsAcrossBoundProperties,
                                                                  AlsoMatchFilterWordsAcrossBoundProperties,
                                                                  ConvertValuesToBeFilteredToString);
                
                var groupFilter = (GroupFilter)AssociatedItemsControl.Items.Filter;
                groupFilter += _filter;

                AssociatedItemsControl.Items.Filter = groupFilter;

                ItemsCount = AssociatedItemsControl.Items.Count;
                UpdateItemsCountSummary();
                OnFilterApplied();
                TryToApplyFilterPropertiesOnAssociatedItemsControlItems();

                IsFilterActive = !string.IsNullOrWhiteSpace(_filterCache);
                _sourceCollectionListener?.Resume();
            }));
        }

        /// <summary>
        /// Called whenever the filter value changes and filtering is processed.
        /// </summary>
        protected virtual void OnFilterApplied()
        {
            if (!(AssociatedItemsControl is Selector selector))
                return;

            if (selector.SelectedItem != null && !selector.Items.Contains(selector.SelectedItem) && selector.Items.Count > 0)
                selector.SetCurrentValue(Selector.SelectedItemProperty, selector.Items[0]);
        }

        private void TryToApplyFilterPropertiesOnAssociatedItemsControlItems()
        {
            var itemStyle = AssociatedItemsControl.ItemContainerStyle;
            if (itemStyle == null)
                return;

            var overridenStyle = new Style(itemStyle.TargetType, itemStyle);
            if (itemStyle.Setters
                         .Where(x => x is Setter)
                         .Cast<Setter>()
                         .Any(x => x.Property == FilterTextBoxHelper.TextFilterProperty))
                overridenStyle = new Style(itemStyle.TargetType, itemStyle.BasedOn);

            overridenStyle.Setters.Add(new Setter(FilterTextBoxHelper.TextFilterProperty, _filterCache));
            overridenStyle.Setters.Add(new Setter(FilterTextBoxHelper.HighlightPerWordProperty, AlsoMatchWithFirstWordLetters || AlsoMatchFilterWordsAcrossBoundProperties));
            overridenStyle.Setters.Add(new Setter(FilterTextBoxHelper.IgnoreCaseProperty, IgnoreCase));

            AssociatedItemsControl.ItemContainerStyle = overridenStyle;
        }
        #endregion

        #region Associated control' source updates
        private void UnsubscribeFromAssociatedControlItemsEvents(FrameworkElement itemsControl)
        {
            itemsControl.Loaded -= OnAssociatedControlLoaded;
            itemsControl.DataContextChanged -= OnAssociatedControlItemsSourcePropertyChanged;
        }

        private void SubscribeToAssociatedControlItemsEvents()
        {
            if (!AssociatedItemsControl.IsLoaded)
            {
                IsLoadingItemsInBackground = true;
                AssociatedItemsControl.Loaded += OnAssociatedControlLoaded;
            }

            AssociatedItemsControl.DataContextChanged += OnAssociatedControlItemsSourcePropertyChanged;
        }

        private void OnAssociatedControlLoaded(object sender, EventArgs e)
        {
            AssociatedItemsControl.Loaded -= OnAssociatedControlLoaded;
            HandleAssociatedControlSourceChange();
        }

        private void OnAssociatedControlItemsSourcePropertyChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            HandleAssociatedControlSourceChange();
        }

        private void HandleAssociatedControlSourceChange()
        {
            SetItemFilterSetMemberPaths(ItemFilterMemberPaths);

            _sourceCollectionListener?.Dispose();

            OnSourceCollectionChange(null);

            if (AssociatedItemsControl != null)
                _sourceCollectionListener = new CollectionChangedWeakEventListener(AssociatedItemsControl.Items, OnSourceCollectionChange);

            IsLoadingItemsInBackground = false;
        }

        private void OnSourceCollectionChange(IEnumerable obj)
        {
            _sourceCollectionListener?.Pause();

            ResetFilterAndSetSourceCollectionCounts();

            ApplyFilter(true);

            OnItemsSourceChanged();
        }

        private void ResetFilterAndSetSourceCollectionCounts()
        {
            ResetFilter();

            _totalAssociatedControlItemsCount = AssociatedItemsControl.Items.Count;
            AssociatedItemsControlIsEmpty = _totalAssociatedControlItemsCount == 0;
        }

        private void ResetFilter()
        {
            var groupFilter = (GroupFilter)AssociatedItemsControl.Items.Filter;
            groupFilter -= _filter;

            AssociatedItemsControl.Items.Filter = groupFilter;
        }

        /// <summary>
        /// Occurs whenever the item source property changes.
        /// </summary>
        protected virtual void OnItemsSourceChanged()
        { }
        #endregion

        #region Keyboard key pressed management
        /// <summary>
        /// Occurs whenever a key from keyboard is pressed.
        /// </summary>
        /// <param name="e">Event args.</param>
        protected override void OnPreviewKeyDown(KeyEventArgs e)
        {
            if (ProcessClearFilterTextBox(e))
                return;

            base.OnPreviewKeyDown(e);
        }

        private bool ProcessClearFilterTextBox(KeyEventArgs e)
        {
            if (e.Key != Key.Space || Keyboard.Modifiers != ModifierKeys.Control)
                return false;

            SetCurrentValue(FilterProperty, string.Empty);
            e.Handled = true;
            return true;
        }
        #endregion

        #region Dependency properties
        #region Associated control
        /// <summary>
        /// Gets or sets the control on which filtering applies.
        /// </summary>
        public ItemsControl AssociatedItemsControl
        {
            get => (ItemsControl)GetValue(AssociatedItemsControlProperty);
            set => SetValue(AssociatedItemsControlProperty, value);
        }
        /// <summary>
        /// Registers <see cref="AssociatedItemsControl"/> as a dependency property.
        /// </summary>
        public static readonly DependencyProperty AssociatedItemsControlProperty
            = DependencyProperty.Register(nameof(AssociatedItemsControl), typeof(ItemsControl), typeof(FilterTextBox), new FrameworkPropertyMetadata(default(ItemsControl), AssociatedItemsControlPropertyChanged));

        /// <summary>
        /// Called whenever the <see cref="AssociatedItemsControl"/> property changes.
        /// </summary>
        /// <param name="sender">The object whose property changed.</param>
        /// <param name="args">Information about the property change.</param>
        private static void AssociatedItemsControlPropertyChanged(DependencyObject sender, DependencyPropertyChangedEventArgs args)
        {
            if (!(sender is FilterTextBox filterTextBox))
                return;

            if (args.OldValue is ItemsControl oldAssociatedItemsControl)
            {
                filterTextBox.UnsubscribeFromAssociatedControlItemsEvents(oldAssociatedItemsControl);
            }

            filterTextBox.SubscribeToAssociatedControlItemsEvents();
            filterTextBox.ApplyFilter();
        }

        /// <summary>
        /// Gets a boolean indicating if the associated control has no items to be filtered.
        /// </summary>
        public bool AssociatedItemsControlIsEmpty
        {
            get => (bool)GetValue(AssociatedItemsControlIsEmptyProperty);
            protected set => SetValue(_associatedItemsControlIsEmptyPropertyKey, value);
        }
        private static readonly DependencyPropertyKey _associatedItemsControlIsEmptyPropertyKey =
            DependencyProperty.RegisterReadOnly(nameof(AssociatedItemsControlIsEmpty), typeof(bool), typeof(FilterTextBox), new PropertyMetadata(true));
        /// <summary>
        /// Registers <see cref="AssociatedItemsControlIsEmpty"/> as a readonly dependency property.
        /// </summary>
        public static readonly DependencyProperty AssociatedItemsControlIsEmptyProperty = _associatedItemsControlIsEmptyPropertyKey.DependencyProperty;

        /// <summary>
        /// Gets or sets the hint to be shown when they are no items to be filtered.
        /// </summary>
        public string AssociatedItemsControlIsEmptyHint
        {
            get => (string)GetValue(AssociatedItemsControlIsEmptyHintProperty);
            set => SetCurrentValue(AssociatedItemsControlIsEmptyHintProperty, value);
        }
        /// <summary>
        /// Registers <see cref="AssociatedItemsControlIsEmptyHint"/> as a dependency property.
        /// </summary>
        public static readonly DependencyProperty AssociatedItemsControlIsEmptyHintProperty
            = DependencyProperty.Register(nameof(AssociatedItemsControlIsEmptyHint), typeof(string), typeof(FilterTextBox), new FrameworkPropertyMetadata("No items to be searched"));

        /// <summary>
        /// Gets a boolean indicating if items are being initialized in background.
        /// </summary>
        public bool IsLoadingItemsInBackground
        {
            get => (bool)GetValue(IsLoadingItemsInBackgroundProperty);
            protected set => SetValue(_isLoadingItemsInBackgroundPropertyKey, value);
        }
        private static readonly DependencyPropertyKey _isLoadingItemsInBackgroundPropertyKey =
            DependencyProperty.RegisterReadOnly(nameof(IsLoadingItemsInBackground), typeof(bool), typeof(FilterTextBox), new PropertyMetadata(default(bool)));
        /// <summary>
        /// Registers <see cref="IsLoadingItemsInBackground"/> as a readonly dependency property.
        /// </summary>
        public static readonly DependencyProperty IsLoadingItemsInBackgroundProperty = _isLoadingItemsInBackgroundPropertyKey.DependencyProperty;

        /// <summary>
        /// Gets or sets the hint to be shown when items are being loaded in background.
        /// </summary>
        public string IsLoadingItemsInBackgroundHint
        {
            get => (string)GetValue(IsLoadingItemsInBackgroundHintProperty);
            set => SetCurrentValue(IsLoadingItemsInBackgroundHintProperty, value);
        }
        /// <summary>
        /// Registers <see cref="IsLoadingItemsInBackgroundHint"/> as a dependency property.
        /// </summary>
        public static readonly DependencyProperty IsLoadingItemsInBackgroundHintProperty
            = DependencyProperty.Register(nameof(IsLoadingItemsInBackgroundHint), typeof(string), typeof(FilterTextBox), new FrameworkPropertyMetadata("Loading your data..."));
        #endregion

        #region Item filtering
        /// <summary>
        /// Gets or sets the paths to item string properties on
        /// which filtering must be applied.
        /// </summary>
        /// <remarks>You can either bind a collection or use ',' to separate member paths.</remarks>
        public object ItemFilterMemberPaths
        {
            get => GetValue(ItemFilterMemberPathsProperty);
            set => SetCurrentValue(ItemFilterMemberPathsProperty, value);
        }
        /// <summary>
        /// Registers <see cref="ItemFilterMemberPaths"/> as a dependency property.
        /// </summary>
        public static readonly DependencyProperty ItemFilterMemberPathsProperty
            = DependencyProperty.Register(nameof(ItemFilterMemberPaths), typeof(object), typeof(FilterTextBox), new FrameworkPropertyMetadata(null, FilterMemberPathsChanged));

        /// <summary>
        /// Called whenever the <see cref="ItemFilterMemberPaths"/> property changes.
        /// </summary>
        /// <param name="sender">The object whose property changed.</param>
        /// <param name="args">Information about the property change.</param>
        private static void FilterMemberPathsChanged(DependencyObject sender, DependencyPropertyChangedEventArgs args)
        {
            if (!(sender is FilterTextBox item) || !(args.NewValue is string rawMemberPaths))
                return;

            item.SetItemFilterSetMemberPaths(rawMemberPaths);
        }

        private void SetItemFilterSetMemberPaths(object rawMemberPaths)
        {
            if (AssociatedItemsControl == null)
                return;

            var memberPaths = MemberPaths.ExtractFromCollectionOrCharacterSeparatedInput(rawMemberPaths);
            if (memberPaths.Length == 0)
                return;

            _itemFilterPropertyGetters = ItemPropertyExtractor.BuildPropertyGetters(AssociatedItemsControl.Items.SourceCollection)
                                                              .Where(x => memberPaths.Contains(x.PropertyName))
                                                              .ToArray();

            ApplyFilter(true);
        }
        #endregion

        #region Filtering configuration
        /// <summary>
        /// Gets or sets the string used for filtering.
        /// </summary>
        public string Filter
        {
            get => (string)GetValue(FilterProperty);
            set => SetValue(FilterProperty, value);
        }
        /// <summary>
        /// Registers <see cref="Filter"/> as a dependency property.
        /// </summary>
        public static readonly DependencyProperty FilterProperty
            = DependencyProperty.Register(nameof(Filter), typeof(string), typeof(FilterTextBox), new FrameworkPropertyMetadata(default(string), FilterRulesChanged));

        /// <summary>
        /// Gets or sets the hint to be set on the filter.
        /// </summary>
        public string FilterHint
        {
            get => (string)GetValue(FilterHintProperty);
            set => SetCurrentValue(FilterHintProperty, value);
        }
        /// <summary>
        /// Registers <see cref="FilterHint"/> as a dependency property.
        /// </summary>
        public static readonly DependencyProperty FilterHintProperty
            = DependencyProperty.Register(nameof(FilterHint), typeof(string), typeof(FilterTextBox), new FrameworkPropertyMetadata("Search..."));

        /// <summary>
        /// Gets or sets a value indicating if text comparisons during
        /// filtering should ignore case or not.
        /// </summary>
        public bool IgnoreCase
        {
            get => (bool)GetValue(IgnoreCaseProperty);
            set => SetValue(IgnoreCaseProperty, value);
        }
        /// <summary>
        /// Registers <see cref="IgnoreCase"/> as a dependency property.
        /// </summary>
        public static readonly DependencyProperty IgnoreCaseProperty
            = DependencyProperty.Register(nameof(IgnoreCase), typeof(bool), typeof(FilterTextBox), new FrameworkPropertyMetadata(true, FilterRulesChanged));

        /// <summary>
        /// Gets or sets a value indicating if text comparisons during
        /// filtering can work when matching first letter of item content words.
        /// </summary>
        public bool AlsoMatchWithFirstWordLetters
        {
            get => (bool)GetValue(AlsoMatchWithFirstWordLettersProperty);
            set => SetValue(AlsoMatchWithFirstWordLettersProperty, value);
        }
        /// <summary>
        /// Registers <see cref="AlsoMatchWithFirstWordLetters"/> as a dependency property.
        /// </summary>
        public static readonly DependencyProperty AlsoMatchWithFirstWordLettersProperty
            = DependencyProperty.Register(nameof(AlsoMatchWithFirstWordLetters), typeof(bool), typeof(FilterTextBox), new FrameworkPropertyMetadata(true, FilterRulesChanged));

        /// <summary>
        /// Gets or sets a value indicating if text comparisons during
        /// filtering can work only on a per member path basis for a given item,
        /// and or across member paths, thus allowing to select a word into one property
        /// and another into another property.
        /// Ex: Bound item is Data { Property1="Some data", Property2="other date" }, filter "Some oth" will match
        /// if this option is true.
        /// </summary>
        public bool AlsoMatchFilterWordsAcrossBoundProperties
        {
            get => (bool)GetValue(AlsoMatchFilterWordsAcrossBoundPropertiesProperty);
            set => SetValue(AlsoMatchFilterWordsAcrossBoundPropertiesProperty, value);
        }
        /// <summary>
        /// Registers <see cref="AlsoMatchFilterWordsAcrossBoundProperties"/> as a dependency property.
        /// </summary>
        public static readonly DependencyProperty AlsoMatchFilterWordsAcrossBoundPropertiesProperty
            = DependencyProperty.Register(nameof(AlsoMatchFilterWordsAcrossBoundProperties), typeof(bool), typeof(FilterTextBox), new FrameworkPropertyMetadata(true, FilterRulesChanged));

        /// <summary>
        /// Gets or sets a value indicating if non-string property values on the target
        /// items must be converted to string.
        /// </summary>
        /// <remarks><see cref="object.ToString"/> method might be called without formatting or culture.</remarks>
        public bool ConvertValuesToBeFilteredToString
        {
            get => (bool)GetValue(ConvertValuesToBeFilteredToStringProperty);
            set => SetValue(ConvertValuesToBeFilteredToStringProperty, value);
        }
        /// <summary>
        /// Registers <see cref="ConvertValuesToBeFilteredToString"/> as a dependency property.
        /// </summary>
        public static readonly DependencyProperty ConvertValuesToBeFilteredToStringProperty
            = DependencyProperty.Register(nameof(ConvertValuesToBeFilteredToString), typeof(bool), typeof(FilterTextBox), new FrameworkPropertyMetadata(true, FilterRulesChanged));

        /// <summary>
        /// Called whenever the <see cref="Filter"/>, <see cref="IgnoreCase"/>, <see cref="AlsoMatchWithFirstWordLetters"/>
        /// or <see cref="AlsoMatchFilterWordsAcrossBoundProperties"/> properties change.
        /// </summary>
        /// <param name="sender">The object whose property changed.</param>
        /// <param name="args">Information about the property change.</param>
        private static void FilterRulesChanged(DependencyObject sender, DependencyPropertyChangedEventArgs args)
        {
            if (!(sender is FilterTextBox filterTextBox))
                return;

            filterTextBox.ApplyFilter(true);
        }
        #endregion

        #region Filtering results
        /// <summary>
        /// Gets the total number of items available for selection.
        /// </summary>
        public int ItemsCount
        {
            get => (int)GetValue(ItemsCountProperty);
            protected set => SetValue(_itemsCountPropertyKey, value);
        }
        private static readonly DependencyPropertyKey _itemsCountPropertyKey =
            DependencyProperty.RegisterReadOnly(nameof(ItemsCount), typeof(int), typeof(FilterTextBox),
                                                new PropertyMetadata(default(int), ItemsCountChanged));

        /// <summary>
        /// Registers <see cref="ItemsCount"/> as a readonly dependency property.
        /// </summary>
        public static readonly DependencyProperty ItemsCountProperty = _itemsCountPropertyKey.DependencyProperty;

        /// <summary>
        /// Called whenever the <see cref="ItemsCountChanged"/> property changes.
        /// </summary>
        /// <param name="sender">The object whose property changed.</param>
        /// <param name="args">Information about the property change.</param>
        private static void ItemsCountChanged(DependencyObject sender, DependencyPropertyChangedEventArgs args)
        {
            if (!(sender is FilterTextBox item) || !(args.NewValue is int newCount))
                return;

            item.HasItemsToDisplay = newCount != 0;
        }

        /// <summary>
        /// Gets a boolean indicating if control has items to display.
        /// </summary>
        public bool HasItemsToDisplay
        {
            get => (bool)GetValue(HasItemsToDisplayProperty);
            protected set => SetValue(_hasItemsToDisplayPropertyKey, value);
        }
        private static readonly DependencyPropertyKey _hasItemsToDisplayPropertyKey =
            DependencyProperty.RegisterReadOnly(nameof(HasItemsToDisplay), typeof(bool), typeof(FilterTextBox), new PropertyMetadata(default(bool)));
        /// <summary>
        /// Registers <see cref="HasItemsToDisplay"/> as a readonly dependency property.
        /// </summary>
        public static readonly DependencyProperty HasItemsToDisplayProperty = _hasItemsToDisplayPropertyKey.DependencyProperty;

        /// <summary>
        /// Gets or sets the hint to be set when there is no items to be displayed.
        /// </summary>
        public string NothingToDisplayHint
        {
            get => (string)GetValue(NothingToDisplayHintProperty);
            set => SetCurrentValue(NothingToDisplayHintProperty, value);
        }
        /// <summary>
        /// Registers <see cref="NothingToDisplayHint"/> as a dependency property.
        /// </summary>
        public static readonly DependencyProperty NothingToDisplayHintProperty
            = DependencyProperty.Register(nameof(NothingToDisplayHint), typeof(string), typeof(FilterTextBox), new FrameworkPropertyMetadata("Nothing to display"));

        /// <summary>
        /// Gets a string displaying the summary of currently filtered items vs. total items.
        /// </summary>
        public string ItemsCountSummary
        {
            get => (string)GetValue(ItemsCountSummaryProperty);
            protected set => SetValue(_itemsCountSummaryPropertyKey, value);
        }
        private static readonly DependencyPropertyKey _itemsCountSummaryPropertyKey =
            DependencyProperty.RegisterReadOnly(nameof(ItemsCountSummary), typeof(string), typeof(FilterTextBox), new PropertyMetadata(default(string)));
        /// <summary>
        /// Registers <see cref="ItemsCountSummary"/> as a readonly dependency property.
        /// </summary>
        public static readonly DependencyProperty ItemsCountSummaryProperty = _itemsCountSummaryPropertyKey.DependencyProperty;

        private void UpdateItemsCountSummary()
        {
            ItemsCountSummary = ItemsCount + " / " + _totalAssociatedControlItemsCount;
        }
        #endregion

        #region Icons
        /// <summary>
        /// Gets or sets the kind of the icon.
        /// </summary>
        public PackIconKind IconKind
        {
            get => (PackIconKind)GetValue(IconKindProperty);
            set => SetCurrentValue(IconKindProperty, value);
        }
        /// <summary>
        /// Registers <see cref="IconKind"/> as a dependency property.
        /// </summary>
        public static readonly DependencyProperty IconKindProperty
            = DependencyProperty.Register(nameof(IconKind), typeof(PackIconKind), typeof(FilterTextBox), new FrameworkPropertyMetadata(default(PackIconKind)));

        /// <summary>
        /// Gets or sets the foreground for toggle icon.
        /// </summary>
        public Brush IconForeground
        {
            get => (Brush)GetValue(IconForegroundProperty);
            set => SetCurrentValue(IconForegroundProperty, value);
        }
        /// <summary>
        /// Registers <see cref="IconForeground"/> as a dependency property.
        /// </summary>
        public static readonly DependencyProperty IconForegroundProperty
            = DependencyProperty.Register(nameof(IconForeground), typeof(Brush), typeof(FilterTextBox), new FrameworkPropertyMetadata(default(Brush)));

        /// <summary>
        /// Gets or sets the kind of the icon when the control is open.
        /// </summary>


        /// <summary>
        /// Gets a boolean indicating if filtering is currently active.
        /// </summary>
        public bool IsFilterActive
        {
            get => (bool)GetValue(IsFilterActiveProperty);
            protected set => SetValue(_isFilterActivePropertyKey, value);
        }
        private static readonly DependencyPropertyKey _isFilterActivePropertyKey =
            DependencyProperty.RegisterReadOnly(nameof(IsFilterActive), typeof(bool), typeof(FilterTextBox), new PropertyMetadata(default(bool)));
        /// <summary>
        /// Registers <see cref="IsFilterActive"/> as a readonly dependency property.
        /// </summary>
        public static readonly DependencyProperty IsFilterActiveProperty = _isFilterActivePropertyKey.DependencyProperty;

        /// <summary>
        /// Gets or sets the kind of the icon to display when the filter is active.
        /// </summary>
        public PackIconKind IconKindWhenFilterIsActive
        {
            get => (PackIconKind)GetValue(IconKindWhenFilterIsActiveProperty);
            set => SetCurrentValue(IconKindWhenFilterIsActiveProperty, value);
        }
        /// <summary>
        /// Registers <see cref="IconKindWhenFilterIsActive"/> as a dependency property.
        /// </summary>
        public static readonly DependencyProperty IconKindWhenFilterIsActiveProperty
            = DependencyProperty.Register(nameof(IconKindWhenFilterIsActive), typeof(PackIconKind), typeof(FilterTextBox), new FrameworkPropertyMetadata(default(PackIconKind)));

        /// <summary>
        /// Gets or sets the kind of the icon to display when the filter is active but all items are filtered out.
        /// </summary>
        public PackIconKind IconKindWhenNoItemAfterFiltering
        {
            get => (PackIconKind)GetValue(IconKindWhenNoItemAfterFilteringProperty);
            set => SetCurrentValue(IconKindWhenNoItemAfterFilteringProperty, value);
        }
        /// <summary>
        /// Registers <see cref="IconKindWhenNoItemAfterFiltering"/> as a dependency property.
        /// </summary>
        public static readonly DependencyProperty IconKindWhenNoItemAfterFilteringProperty
            = DependencyProperty.Register(nameof(IconKindWhenNoItemAfterFiltering), typeof(PackIconKind), typeof(FilterTextBox), new FrameworkPropertyMetadata(default(PackIconKind)));

        /// <summary>
        /// Gets or sets the kind of the icon when the associated control is open (used for results in popup).
        /// </summary>
        public PackIconKind IconKindWhenOpen
        {
            get => (PackIconKind)GetValue(IconKindWhenOpenProperty);
            set => SetCurrentValue(IconKindWhenOpenProperty, value);
        }
        /// <summary>
        /// Registers <see cref="IconKindWhenOpen"/> as a dependency property.
        /// </summary>
        public static readonly DependencyProperty IconKindWhenOpenProperty
            = DependencyProperty.Register(nameof(IconKindWhenOpen), typeof(PackIconKind), typeof(FilterTextBox), new FrameworkPropertyMetadata(default(PackIconKind)));
        #endregion
        #endregion
    }
}

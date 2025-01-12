using System;
using System.Collections.Specialized;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using MaterialDesignThemes.Wpf.AddOns.Utils.Filtering;
using MaterialDesignThemes.Wpf.AddOns.Utils.Reflection;

namespace MaterialDesignThemes.Wpf.AddOns
{
    /// <summary>
    /// Base class for selectors with filter.
    /// </summary>
    public class FilterBox : ItemsControl
    {
        /// <summary>
        /// Current filter value.
        /// </summary>
        protected string _filterCache;
        
        private PropertyGetter[] _itemFilterPropertyGetters = Array.Empty<PropertyGetter>();

        #region Constructors and initializations
        /// <summary>
        /// Static constructor for <see cref="FilterBox"/> type.
        /// Override some base dependency properties.
        /// </summary>
        static FilterBox()
        {
            DefaultStyleKeyProperty.OverrideMetadata(typeof(FilterBox), new FrameworkPropertyMetadata(typeof(FilterBox)));
            ItemsSourceProperty.OverrideMetadata(typeof(FilterBox), new FrameworkPropertyMetadata(null, ItemsSourcePropertyChanged));
        }
        #endregion

        #region Source updates
        /// <summary>
        /// Occurs whenever a property change. Preview event, meaning this is the first
        /// pass through before value processing.
        /// </summary>
        /// <param name="e">Information about the property change.</param>
        protected override void OnItemsChanged(NotifyCollectionChangedEventArgs e)
        {
            if (ItemsSource == null)
                IsLoadingItemsInBackground = true;
            base.OnItemsChanged(e);
        }
        
        /// <summary>
        /// Called whenever the <see cref="ItemsControl.ItemsSource"/> property changes.
        /// </summary>
        /// <param name="sender">The object whose property changed.</param>
        /// <param name="args">Information about the property change.</param>
        private static void ItemsSourcePropertyChanged(DependencyObject sender, DependencyPropertyChangedEventArgs args)
        {
            if (!(sender is FilterBox filterBox))
                return;
            
            filterBox.SetItemFilterSetMemberPaths(filterBox.ItemFilterMemberPaths);
            filterBox.ItemsCount = filterBox.Items.Count;
            filterBox.OnItemsSourceChanged();
            filterBox.IsLoadingItemsInBackground = false;
        }
        
        /// <summary>
        /// Occurs whenever the item source property changes.
        /// </summary>
        protected virtual void OnItemsSourceChanged()
        { }
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

                _filterCache = trimmedFilter;
                Items.Filter = item => TextFilter.IsItemMatchingFilter(item, 
                                                                       _itemFilterPropertyGetters, 
                                                                       _filterCache, 
                                                                       IgnoreCase, 
                                                                       AlsoMatchFilterWordsAcrossBoundProperties,
                                                                       AlsoMatchFilterWordsAcrossBoundProperties,
                                                                       ConvertValuesToBeFilteredToString);

                ItemsCount = Items.Count;
                OnFilterApplied();
            }));
        }

        /// <summary>
        /// Called whenever the filter value changes and filtering is processed.
        /// </summary>
        protected virtual void OnFilterApplied()
        { }
        #endregion
        
        #region Dependency properties
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
            = DependencyProperty.Register(nameof(Filter), typeof(string), typeof(FilterBox), new FrameworkPropertyMetadata(default(string), FilterPropertyChanged));

        /// <summary>
        /// Called whenever the <see cref="Filter"/> property changes.
        /// </summary>
        /// <param name="sender">The object whose property changed.</param>
        /// <param name="args">Information about the property change.</param>
        private static void FilterPropertyChanged(DependencyObject sender, DependencyPropertyChangedEventArgs args)
        {
            if (!(sender is FilterBox filterBox))
                return;

            filterBox.ApplyFilter();
        }

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
            = DependencyProperty.Register(nameof(IgnoreCase), typeof(bool), typeof(FilterBox), new FrameworkPropertyMetadata(true, FilterRulesChanged));

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
            = DependencyProperty.Register(nameof(AlsoMatchWithFirstWordLetters), typeof(bool), typeof(FilterBox), new FrameworkPropertyMetadata(true, FilterRulesChanged));
        
        /// <summary>
        /// Gets or sets a value indicating if text comparisons during
        /// filtering can work only on a per member path basis for a given item,
        /// and or across member paths, thus allowing to select a word into one property
        /// and another into another property.
        /// Ex: Bound item is Data { Property1="Some data", Property2="other date"), filter "Some oth" will match
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
            = DependencyProperty.Register(nameof(AlsoMatchFilterWordsAcrossBoundProperties), typeof(bool), typeof(FilterBox), new FrameworkPropertyMetadata(true, FilterRulesChanged));
        
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
            = DependencyProperty.Register(nameof(ConvertValuesToBeFilteredToString), typeof(bool), typeof(FilterBox), new FrameworkPropertyMetadata(true, FilterRulesChanged));
        
        /// <summary>
        /// Called whenever the <see cref="IgnoreCase"/>, <see cref="AlsoMatchWithFirstWordLetters"/>
        /// or <see cref="AlsoMatchFilterWordsAcrossBoundProperties"/> properties change.
        /// </summary>
        /// <param name="sender">The object whose property changed.</param>
        /// <param name="args">Information about the property change.</param>
        private static void FilterRulesChanged(DependencyObject sender, DependencyPropertyChangedEventArgs args)
        {
            if (!(sender is FilterBox filterBox))
                return;

            filterBox.ApplyFilter(true);
        }
        
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
            = DependencyProperty.Register(nameof(FilterHint), typeof(string), typeof(FilterBox), new FrameworkPropertyMetadata("Search and add..."));

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
            = DependencyProperty.Register(nameof(NothingToDisplayHint), typeof(string), typeof(FilterBox), new FrameworkPropertyMetadata("Nothing to display"));

        /// <summary>
        /// Gets or sets a value indicating the path to item string properties on
        /// which filtering must be applied. Use ',' to separate member paths.
        /// </summary>
        public string ItemFilterMemberPaths
        {
            get => (string)GetValue(ItemFilterMemberPathsProperty);
            set => SetCurrentValue(ItemFilterMemberPathsProperty, value);
        }
        /// <summary>
        /// Registers <see cref="ItemFilterMemberPaths"/> as a dependency property.
        /// </summary>
        public static readonly DependencyProperty ItemFilterMemberPathsProperty
            = DependencyProperty.Register(nameof(ItemFilterMemberPaths), typeof(string), typeof(FilterBox), new FrameworkPropertyMetadata(default(string), FilterMemberPathsChanged));

        /// <summary>
        /// Called whenever the <see cref="ItemFilterMemberPaths"/> property changes.
        /// </summary>
        /// <param name="sender">The object whose property changed.</param>
        /// <param name="args">Information about the property change.</param>
        private static void FilterMemberPathsChanged(DependencyObject sender, DependencyPropertyChangedEventArgs args)
        {
            if (!(sender is FilterBox item) || !(args.NewValue is string rawMemberPaths))
                return;

            item.SetItemFilterSetMemberPaths(rawMemberPaths);
        }

        private void SetItemFilterSetMemberPaths(string rawMemberPaths)
        {
            if (string.IsNullOrEmpty(rawMemberPaths))
                return;

            var memberPaths = rawMemberPaths.Replace(" ", "").Split(',').ToArray();

            _itemFilterPropertyGetters = ItemPropertyExtractor.BuildPropertyGetters(Items.SourceCollection)
                                                              .Where(x => memberPaths.Contains(x.PropertyName))
                                                              .ToArray();

            ApplyFilter(true);
        }
        
        /// <summary>
        /// Gets the total number of items available for selection.
        /// </summary>
        public int ItemsCount
        {
            get => (int)GetValue(ItemsCountProperty);
            protected set => SetValue(_itemsCountPropertyKey, value);
        }
        private static readonly DependencyPropertyKey _itemsCountPropertyKey =
            DependencyProperty.RegisterReadOnly(nameof(ItemsCount), typeof(int), typeof(FilterBox),
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
            if (!(sender is FilterBox item) || !(args.NewValue is int newCount))
                return;

            item.HasItemsToDisplay = newCount != 0;
            item.OnItemsCountChanged();
        }

        /// <summary>
        /// Called whenever the item count is updated.
        /// </summary>
        protected virtual void OnItemsCountChanged()
        { }

        /// <summary>
        /// Gets a boolean indicating if control has items to display.
        /// </summary>
        public bool HasItemsToDisplay
        {
            get => (bool)GetValue(HasItemsToDisplayProperty);
            protected set => SetValue(_hasItemsToDisplayPropertyKey, value);
        }
        private static readonly DependencyPropertyKey _hasItemsToDisplayPropertyKey =
            DependencyProperty.RegisterReadOnly(nameof(HasItemsToDisplay), typeof(bool), typeof(FilterBox), new PropertyMetadata(default(bool)));
        /// <summary>
        /// Registers <see cref="HasItemsToDisplay"/> as a readonly dependency property.
        /// </summary>
        public static readonly DependencyProperty HasItemsToDisplayProperty = _hasItemsToDisplayPropertyKey.DependencyProperty;
        
        /// <summary>
        /// Gets a boolean indicating if items are being initialized in background.
        /// </summary>
        public bool IsLoadingItemsInBackground
        {
            get => (bool)GetValue(IsLoadingItemsInBackgroundProperty);
            protected set => SetValue(_isLoadingItemsInBackgroundPropertyKey, value);
        }
        private static readonly DependencyPropertyKey _isLoadingItemsInBackgroundPropertyKey =
            DependencyProperty.RegisterReadOnly(nameof(IsLoadingItemsInBackground), typeof(bool), typeof(FilterBox), new PropertyMetadata(default(bool)));
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
            = DependencyProperty.Register(nameof(IsLoadingItemsInBackgroundHint), typeof(string), typeof(FilterBox), new FrameworkPropertyMetadata("Loading your data..."));
        #endregion

        #region ItemContainer management
        /// <summary>
        /// Indicates if the child item is its own container (i.e. a visual object).
        /// </summary>
        /// <param name="item">The item to be checked.</param>
        /// <returns>True is item is its own container.</returns>
        protected override bool IsItemItsOwnContainerOverride(object item)
        {
            return item is FilterBoxItem;
        }

        /// <summary>
        /// Provides a visual object to a given item.
        /// </summary>
        /// <returns>A pre-initialized <see cref="FilterBoxItem"/>.</returns>
        protected override DependencyObject GetContainerForItemOverride()
        {
            return new FilterBoxItem(_filterCache, AlsoMatchWithFirstWordLetters || AlsoMatchFilterWordsAcrossBoundProperties, IgnoreCase);
        }
        #endregion
    }
}

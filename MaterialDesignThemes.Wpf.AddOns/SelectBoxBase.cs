using System;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using MaterialDesignThemes.Wpf.AddOns.Extensions;
using MaterialDesignThemes.Wpf.AddOns.Utils.Filtering;

namespace MaterialDesignThemes.Wpf.AddOns
{
    /// <summary>
    /// Base class for selectors with filter.
    /// </summary>
    public abstract class SelectBoxBase : ItemsControl
    {
        /// <summary>
        /// Current filter value.
        /// </summary>
        protected string _filterCache;

        /// <summary>
        /// Holds description of the property bound to item's <see cref="SelectBoxItem.IsSelected"/> property.
        /// </summary>
        protected PropertyDescriptor _itemIsSelectedProperty;
        
        private PropertyDescriptor[] _itemFilterMemberPaths = Array.Empty<PropertyDescriptor>();

        #region Constructors and initializations
        /// <summary>
        /// Static constructor for <see cref="SelectBoxBase"/> type.
        /// Override some base dependency properties.
        /// </summary>
        static SelectBoxBase()
        {
            DefaultStyleKeyProperty.OverrideMetadata(typeof(SelectBoxBase), new FrameworkPropertyMetadata(typeof(SelectBoxBase)));
            ItemsSourceProperty.OverrideMetadata(typeof(SelectBoxBase), new FrameworkPropertyMetadata(null, ItemsSourcePropertyChanged));
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
            if (!(sender is SelectBoxBase selectBox))
                return;
            
            selectBox.SetItemFilterSetMemberPaths(selectBox.ItemFilterMemberPaths);
            selectBox.SetItemIsSelectedMemberPath(selectBox.IsSelectedMemberPath);
            selectBox.ItemsCount = selectBox.Items.Count;
            selectBox.OnItemsSourceChanged();
            selectBox.IsLoadingItemsInBackground = false;
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
        protected void ApplyFilter(bool force = false)
        {
            Dispatcher.BeginInvoke(DispatcherPriority.Input, new Action(() =>
            {
                var trimmedFilter = Filter?.TrimStart().TrimEnd();
                if (!force && _filterCache == trimmedFilter)
                    return;

                _filterCache = trimmedFilter;
                Items.Filter = item => TextFilter.IsItemMatchingFilter(item, 
                                                                       _itemFilterMemberPaths, 
                                                                       _filterCache, 
                                                                       IgnoreCase, 
                                                                       AlsoMatchFilterWordsAcrossBoundProperties,
                                                                       AlsoMatchFilterWordsAcrossBoundProperties);
                
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
        
        #region Item selection
        /// <summary>
        /// Clears selection state of a given item.
        /// </summary>
        /// <param name="item">The item to be unselected.</param>
        protected void SetAsUnSelected(object item)
        {
            if (item == null)
                return;

            var selectBoxItem = GetSelectBoxItem(item);
            if (selectBoxItem != null)
                selectBoxItem.IsSelected = false;
            else
                _itemIsSelectedProperty?.SetValue(item, false);
        }
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
            = DependencyProperty.Register(nameof(Filter), typeof(string), typeof(SelectBoxBase), new FrameworkPropertyMetadata(default(string), FilterPropertyChanged));

        /// <summary>
        /// Called whenever the <see cref="Filter"/> property changes.
        /// </summary>
        /// <param name="sender">The object whose property changed.</param>
        /// <param name="args">Information about the property change.</param>
        private static void FilterPropertyChanged(DependencyObject sender, DependencyPropertyChangedEventArgs args)
        {
            if (!(sender is SelectBoxBase selectBox))
                return;

            selectBox.ApplyFilter();
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
            = DependencyProperty.Register(nameof(IgnoreCase), typeof(bool), typeof(SelectBoxBase), new FrameworkPropertyMetadata(true, FilterRulesChanged));

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
            = DependencyProperty.Register(nameof(AlsoMatchWithFirstWordLetters), typeof(bool), typeof(SelectBoxBase), new FrameworkPropertyMetadata(true, FilterRulesChanged));
        
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
            = DependencyProperty.Register(nameof(AlsoMatchFilterWordsAcrossBoundProperties), typeof(bool), typeof(SelectBoxBase), new FrameworkPropertyMetadata(true, FilterRulesChanged));
        
        /// <summary>
        /// Called whenever the <see cref="IgnoreCase"/>, <see cref="AlsoMatchWithFirstWordLetters"/>
        /// or <see cref="AlsoMatchFilterWordsAcrossBoundProperties"/> properties change.
        /// </summary>
        /// <param name="sender">The object whose property changed.</param>
        /// <param name="args">Information about the property change.</param>
        private static void FilterRulesChanged(DependencyObject sender, DependencyPropertyChangedEventArgs args)
        {
            if (!(sender is SelectBoxBase selectBox))
                return;

            selectBox.ApplyFilter(true);
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
            = DependencyProperty.Register(nameof(FilterHint), typeof(string), typeof(SelectBoxBase), new FrameworkPropertyMetadata("Search and add..."));

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
            = DependencyProperty.Register(nameof(NothingToDisplayHint), typeof(string), typeof(SelectBoxBase), new FrameworkPropertyMetadata("Nothing to display"));

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
            = DependencyProperty.Register(nameof(ItemFilterMemberPaths), typeof(string), typeof(SelectBoxBase), new FrameworkPropertyMetadata(default(string), FilterMemberPathsChanged));

        /// <summary>
        /// Called whenever the <see cref="ItemFilterMemberPaths"/> property changes.
        /// </summary>
        /// <param name="sender">The object whose property changed.</param>
        /// <param name="args">Information about the property change.</param>
        private static void FilterMemberPathsChanged(DependencyObject sender, DependencyPropertyChangedEventArgs args)
        {
            if (!(sender is SelectBoxBase item) || !(args.NewValue is string rawMemberPaths))
                return;

            item.SetItemFilterSetMemberPaths(rawMemberPaths);
        }

        private void SetItemFilterSetMemberPaths(string rawMemberPaths)
        {
            if (string.IsNullOrEmpty(rawMemberPaths))
                return;

            var memberPaths = rawMemberPaths.Replace(" ", "").Split(',').ToArray();

            var props = TypeDescriptor.GetProperties(Items.SourceCollection.GetGenericType());
            _itemFilterMemberPaths = props.OfType<PropertyDescriptor>()
                                          .Where(x => memberPaths.Contains(x.Name))
                                          .ToArray();

            ApplyFilter(true);
        }

        /// <summary>
        /// Gets or sets a value indicating the path to item string properties on
        /// which the IsSelected state of items is bound.
        /// </summary>
        public string IsSelectedMemberPath
        {
            get => (string)GetValue(IsSelectedMemberPathProperty);
            set => SetCurrentValue(IsSelectedMemberPathProperty, value);
        }
        /// <summary>
        /// Registers <see cref="IsSelectedMemberPath"/> as a dependency property.
        /// </summary>
        public static readonly DependencyProperty IsSelectedMemberPathProperty
            = DependencyProperty.Register(nameof(IsSelectedMemberPath), typeof(string), typeof(SelectBoxBase), new FrameworkPropertyMetadata(default(string), ItemIsSelectedMemberPathChanged));

        /// <summary>
        /// Called whenever the <see cref="IsSelectedMemberPath"/> property changes.
        /// </summary>
        /// <param name="sender">The object whose property changed.</param>
        /// <param name="args">Information about the property change.</param>
        private static void ItemIsSelectedMemberPathChanged(DependencyObject sender, DependencyPropertyChangedEventArgs args)
        {
            if (!(sender is SelectBoxBase item) || !(args.NewValue is string memberPath))
                return;

            item.SetItemIsSelectedMemberPath(memberPath);
        }

        private void SetItemIsSelectedMemberPath(string memberPath)
        {
            memberPath = memberPath?.Replace(" ", "") ?? string.Empty;

            var props = TypeDescriptor.GetProperties(Items.SourceCollection.GetGenericType());
            _itemIsSelectedProperty = props.OfType<PropertyDescriptor>()
                                           .SingleOrDefault(x => memberPath == x.Name);

            foreach (var item in Items)
            {
                GetSelectBoxItem(item)?.TrySetIsSelectedBinding(memberPath);
            }
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
            DependencyProperty.RegisterReadOnly(nameof(ItemsCount), typeof(int), typeof(SelectBoxBase),
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
            if (!(sender is SelectBoxBase item) || !(args.NewValue is int newCount))
                return;

            item.HasItemsToDisplay = newCount != 0;
            item.OnItemCountChanged();
        }

        /// <summary>
        /// Called whenever the item count is updated.
        /// </summary>
        protected virtual void OnItemCountChanged()
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
            DependencyProperty.RegisterReadOnly(nameof(HasItemsToDisplay), typeof(bool), typeof(SelectBoxBase), new PropertyMetadata(default(bool)));
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
            DependencyProperty.RegisterReadOnly(nameof(IsLoadingItemsInBackground), typeof(bool), typeof(SelectBoxBase), new PropertyMetadata(default(bool)));
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
            = DependencyProperty.Register(nameof(IsLoadingItemsInBackgroundHint), typeof(string), typeof(SelectBoxBase), new FrameworkPropertyMetadata("Loading your data..."));
        #endregion

        #region ItemContainer management
        /// <summary>
        /// Indicates if the child item is its own container (i.e. a visual object).
        /// </summary>
        /// <param name="item">The item to be checked.</param>
        /// <returns>True is item is its own container.</returns>
        protected override bool IsItemItsOwnContainerOverride(object item)
        {
            return item is SelectBoxItem;
        }

        /// <summary>
        /// Provides a visual object to a given item.
        /// </summary>
        /// <returns>A pre-initialized <see cref="SelectBoxItem"/>.</returns>
        protected override DependencyObject GetContainerForItemOverride()
        {
            return new SelectBoxItem(_filterCache, AlsoMatchWithFirstWordLetters || AlsoMatchFilterWordsAcrossBoundProperties, false, _itemIsSelectedProperty?.Name);
        }
        
        /// <summary>
        /// Gets the visual item of a given source item.
        /// </summary>
        /// <param name="item">The item to be retrieved.</param>
        /// <returns>The item itself if a visual item or the generated visual item if of other type.</returns>
        protected SelectBoxItem GetSelectBoxItem(object item)
        {
            if (!(item is SelectBoxItem selectBoxItem))
            {
                selectBoxItem = ItemContainerGenerator.ContainerFromItem(item) as SelectBoxItem;
            }

            return selectBoxItem;
        }
        #endregion
    }
}

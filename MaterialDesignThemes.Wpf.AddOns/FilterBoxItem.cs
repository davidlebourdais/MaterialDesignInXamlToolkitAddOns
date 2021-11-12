using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using EMA.ExtendedWPFVisualTreeHelper;
using MaterialDesignThemes.Wpf.AddOns.Extensions;

namespace MaterialDesignThemes.Wpf.AddOns
{
    /// <summary>
    /// Item for <see cref="FilterBox"/> control.
    /// </summary>
    [TemplatePart(Name = "GridWrapper", Type = typeof(Grid))]
    public class FilterBoxItem : ContentControl
    {
        private const bool _ignoreCaseWhenFiltering = true;

        private static readonly Dictionary<DependencyProperty, object> _highLightPropertiesAndValues = new Dictionary<DependencyProperty, object>()
        {
            { TextElement.BackgroundProperty, new SolidColorBrush(Colors.Yellow) }
        };

        private string _isSelectedSourceMemberPath;
        
        /// <summary>
        /// The grid on which the item visual state is attached to.
        /// </summary>
        protected Grid _gridWrapper;

        #region Constructors
        /// <summary>
        /// Creates a new instance of <see cref="FilterBoxItem"/>.
        /// </summary>
        public FilterBoxItem()
        { }

        /// <summary>
        /// Creates a new instance of <see cref="FilterBoxItem"/>.
        /// </summary>
        /// <param name="initialFilter">Initial filter to be applied on the item.</param>
        /// <param name="highlightPerFilterWord">Indicates if highlight must occur on a per filter word basis or on the whole filter string.</param>
        /// <param name="isSelectedSourceMemberPath">Member path to the <see cref="IsSelected"/> property.</param>
        public FilterBoxItem(string initialFilter, bool highlightPerFilterWord, string isSelectedSourceMemberPath = null)
        {
            _isSelectedSourceMemberPath = isSelectedSourceMemberPath;
            Loaded += (_, unused) => Initialize(initialFilter, highlightPerFilterWord, isSelectedSourceMemberPath);
        }

        /// <summary>
        /// Static constructor for <see cref="FilterBoxItem"/> type.
        /// </summary>
        static FilterBoxItem()
        {
            DefaultStyleKeyProperty.OverrideMetadata(typeof(FilterBoxItem), new FrameworkPropertyMetadata(typeof(FilterBoxItem)));
            Selector.IsSelectedProperty.OverrideMetadata(typeof(FilterBoxItem), new FrameworkPropertyMetadata(false, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, IsSelectedPropertyChanged));
        }
        #endregion

        #region Initialization with Filtering
        private void Initialize(string filter, bool highlightPerFilterWord, string isSelectedSourceMemberPath)
        {
            UpdateVisualState();

            TrySetIsSelectedBinding(isSelectedSourceMemberPath);

            AdaptTextBlockChildren();

            var textElements = this.FindAllChildren<TextElement>().ToArray();
            foreach (var textElement in textElements)
            {
                textElement.FindAndHighlightTextMatches(filter, _highLightPropertiesAndValues, _ignoreCaseWhenFiltering, highlightPerFilterWord);
            }
        }

        private void AdaptTextBlockChildren()
        {
            var textBlocks = this.FindAllChildren<TextBlock>().ToList();

            for (var i = 0; i < VisualChildrenCount; i++)
            {
                var visualChild = GetVisualChild(i);
                textBlocks.AddRange(visualChild.FindAllChildren<TextBlock>());
            }

            foreach (var textBlock in textBlocks.Distinct())
            {
                textBlock.DecomposeIntoTextElements();
            }
        }
        #endregion

        #region IsSelected Binding
        internal void TrySetIsSelectedBinding(string isSelectedSourceMemberPath)
        {
            if (string.IsNullOrWhiteSpace(isSelectedSourceMemberPath))
                return;

            if (string.IsNullOrEmpty(_isSelectedSourceMemberPath))
                DataContextChanged += OnDataContextChanged;

            _isSelectedSourceMemberPath = isSelectedSourceMemberPath;

            var binding = new Binding(_isSelectedSourceMemberPath)
            {
                Mode = BindingMode.TwoWay, Source = DataContext
            };

            SetBinding(IsSelectedProperty, binding);
        }

        private void OnDataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            TrySetIsSelectedBinding(_isSelectedSourceMemberPath);
        }
        #endregion
        
        #region Selection and visual state management
        /// <summary>
        /// Occurs on template application.
        /// </summary>
        /// <exception cref="Exception">Thrown when a template part cannot be found.</exception>
        public override void OnApplyTemplate()
        {
            base.OnApplyTemplate();

            var grid = Template.FindName("GridWrapper", this);
            if (grid == null || (_gridWrapper = grid as Grid) == null)
                throw new Exception(nameof(FilterBoxItem) + " template must contain a Grid named 'GridWrapper'.");
        }

        /// <summary>
        /// Called whenever a key from keyboard is pressed on the item.
        /// </summary>
        /// <param name="e">Keyboard event args.</param>
        protected override void OnKeyDown(KeyEventArgs e)
        {
            base.OnKeyDown(e);

            if (e.Key == Key.Space || e.Key == Key.Enter)
                ToggleSelection();
        }

        /// <summary>
        /// Called whenever mouse is pressed on the item.
        /// </summary>
        /// <param name="e">Mouse event args.</param>
        protected override void OnMouseDown(MouseButtonEventArgs e)
        {
            base.OnMouseDown(e);
            ToggleSelection();
        }

        /// <summary>
        /// Occurs whenever the mouse comes over the control.
        /// </summary>
        /// <param name="e">Event args.</param>
        protected override void OnMouseEnter(MouseEventArgs e)
        {
            base.OnMouseEnter(e);
            UpdateVisualState();
        }

        /// <summary>
        /// Occurs whenever leaves the control.
        /// </summary>
        /// <param name="e">Event args.</param>
        protected override void OnMouseLeave(MouseEventArgs e)
        {
            base.OnMouseLeave(e);
            UpdateVisualState();
        }

        /// <summary>
        /// Occurs whenever the control gets focused.
        /// </summary>
        /// <param name="e">Event args.</param>
        protected override void OnGotFocus(RoutedEventArgs e)
        {
            base.OnGotFocus(e);
            UpdateVisualState();
        }

        /// <summary>
        /// Occurs whenever the control looses focus.
        /// </summary>
        /// <param name="e">Event args.</param>
        protected override void OnLostFocus(RoutedEventArgs e)
        {
            base.OnLostFocus(e);
            UpdateVisualState();
        }

        /// <summary>
        /// Updates the current item visual state.
        /// </summary>
        protected virtual void UpdateVisualState()
        {
            if (_gridWrapper == null)
                return;

            VisualStateManager.GoToElementState(_gridWrapper, IsSelected ? "Selected" : "Unselected", true);
            VisualStateManager.GoToElementState(_gridWrapper, IsMouseOver ? "MouseOver" : "Normal", true);
            VisualStateManager.GoToElementState(_gridWrapper, IsFocused ? "Focused" : "Unfocused", true);
        }

        /// <summary>
        /// Toggle the selected state of the item.
        /// </summary>
        private void ToggleSelection()
        {
            IsSelected = !IsSelected;
        }
        #endregion

        #region Dependency properties
        /// <summary>
        /// Gets or sets a value indicating if the item is selected.
        /// </summary>
        public bool IsSelected
        {
            get => (bool)GetValue(IsSelectedProperty);
            set => SetValue(IsSelectedProperty, value);
        }

        /// <summary>
        /// Registers <see cref="IsSelected"/> as a dependency property.
        /// </summary>
        public static readonly DependencyProperty IsSelectedProperty = Selector.IsSelectedProperty.AddOwner(typeof(FilterBoxItem));

        /// <summary>
        /// Called whenever the <see cref="IsSelected"/> property changes.
        /// </summary>
        /// <param name="sender">The object whose property changed.</param>
        /// <param name="args">Information about the property change.</param>
        private static void IsSelectedPropertyChanged(DependencyObject sender, DependencyPropertyChangedEventArgs args)
        {
            if (!(sender is FilterBoxItem item))
                return;

            item.RaiseEvent(new RoutedEventArgs(IsSelectedChangedEvent));
            item.UpdateVisualState();
        }

        /// <summary>
        /// Gets or sets a value indicating if the item must be selected with a <see cref="CheckBox"/>.
        /// </summary>
        public bool CanUseCheckBoxForSelection
        {
            get => (bool)GetValue(CanUseCheckBoxForSelectionProperty);
            set => SetValue(CanUseCheckBoxForSelectionProperty, value);
        }

        /// <summary>
        /// Registers <see cref="CanUseCheckBoxForSelection"/> as a dependency property.
        /// </summary>
        public static readonly DependencyProperty CanUseCheckBoxForSelectionProperty
            = DependencyProperty.Register(nameof(CanUseCheckBoxForSelection), typeof(bool), typeof(FilterBoxItem), new FrameworkPropertyMetadata(default(bool)));
        #endregion

        #region Routed events
        /// <summary>
        /// Adds or removes event handlers for selection changed events.
        /// </summary>
        [Category("Behavior")]
        public event RoutedEventHandler IsSelectedChanged
        {
            add => AddHandler(IsSelectedChangedEvent, value);
            remove => RemoveHandler(IsSelectedChangedEvent, value);
        }

        /// <summary>
        /// Registers <see cref="IsSelectedChanged"/> as a routed event.
        /// </summary>
        public static readonly RoutedEvent IsSelectedChangedEvent = EventManager.RegisterRoutedEvent(
            nameof(IsSelectedChangedEvent), RoutingStrategy.Bubble, typeof(RoutedEventHandler), typeof(FilterBoxItem));
        #endregion of routed events
    }
}

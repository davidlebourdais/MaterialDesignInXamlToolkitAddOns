using System;
using System.ComponentModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using EMA.ExtendedWPFVisualTreeHelper;
using MaterialDesignThemes.Wpf.AddOns.Extensions;
using MaterialDesignThemes.Wpf.AddOns.Utils.Caching;
using MaterialDesignThemes.Wpf.AddOns.Utils.Commands;

namespace MaterialDesignThemes.Wpf.AddOns
{
    /// <summary>
    /// A <see cref="FilterBox"/> implementation with a <see cref="SelectBoxPopup"/> for
    /// items display. 
    /// </summary>
    [TemplatePart(Name = "PART_FilterTextBox", Type = typeof(TextBox))]
    [TemplatePart(Name = "PART_Popup", Type = typeof(Popup))]
    [TemplatePart(Name = "ItemsScrollViewer", Type = typeof(ScrollViewer))]
    [StyleTypedProperty(Property = "ItemContainerStyle", StyleTargetType = typeof(SelectBoxItem))]
    public abstract class SelectBox : FilterBox
    {
        /// <summary>
        /// The current <see cref="TextBox"/> holding the filter.
        /// </summary>
        protected TextBox _currentFilterTextBox;
        
        /// <summary>
        /// Holds description of the property bound to item's <see cref="SelectBoxItem.IsSelected"/> property.
        /// </summary>
        protected PropertyDescriptor _itemIsSelectedProperty;
        
        /// <summary>
        /// The control's popup.
        /// </summary>
        protected SelectBoxPopup _popup;
        
        private TextBox _ownFilterTextBox;
        private ScrollViewer _itemsScrollViewer;
        
        private readonly TextBoxCache _textBoxCache;

        private bool _isDroppingFocus;

        #region Constructors and initializations
        /// <summary>
        /// Creates a new instance of <see cref="SelectBox"/>.
        /// </summary>
        protected SelectBox()
        {
            _textBoxCache = new TextBoxCache();
            
            ToggleOpenStateCommand = new SimpleCommand(() => IsOpen = !IsOpen, () => IsEnabled);
        }

        /// <summary>
        /// Static constructor for <see cref="SelectBox"/> type.
        /// Override some base dependency properties.
        /// </summary>
        static SelectBox()
        {
            DefaultStyleKeyProperty.OverrideMetadata(typeof(SelectBox), new FrameworkPropertyMetadata(typeof(SelectBox)));
        }
        
        /// <summary>
        /// Occurs whenever the item source property changes.
        /// </summary>
        protected override void OnItemsSourceChanged() => SetItemIsSelectedMemberPath(IsSelectedMemberPath);

        /// <summary>
        /// Occurs on template application.
        /// </summary>
        /// <exception cref="Exception">Thrown when a template part cannot be found.</exception>
        public override void OnApplyTemplate()
        {
            base.OnApplyTemplate();

            InitializePopUpOrThrow();
            InitializeItemsScrollViewerIfAny();
            InitializeOwnFilterTextBoxIfAny();
        }

        private void InitializePopUpOrThrow()
        {
            var popup = Template.FindName("PART_Popup", this);
            if (popup == null || (_popup = popup as SelectBoxPopup) == null)
                throw new Exception(nameof(SelectBox) + " template must contain a " + nameof(SelectBoxPopup) + " named 'PART_Popup'.");

            _popup.Opened += PopupOnOpened;
            _popup.Closed += PopupOnClosed;
            _popup.FilterTextBoxChanged += PopupOnFilterTextBoxChanged;
            _popup.CloseOnInnerButtonClicks(() => ShouldCloseOnPopupButtonsClicks);
        }
        
        private void InitializeItemsScrollViewerIfAny()
        {
            var scrollViewer = Template.FindName("ItemsScrollViewer", this);
            _itemsScrollViewer = scrollViewer as ScrollViewer;
        }

        private void InitializeOwnFilterTextBoxIfAny()
        {
            _ownFilterTextBox = Template.FindName("PART_FilterTextBox", this) as TextBox;
            if (_ownFilterTextBox != null)
                _ownFilterTextBox.GotKeyboardFocus += OwnFilterTextBoxOnGotKeyboardFocus;

            ReloadCurrentFilterTextBox();
        }
        
        private void PopupOnFilterTextBoxChanged(object sender, RoutedEventArgs e)
        {
            ReloadCurrentFilterTextBox();
        }
        
        private bool IsOwnFilterTextBox => _currentFilterTextBox == _ownFilterTextBox;

        private void ReloadCurrentFilterTextBox()
        {
            _currentFilterTextBox = _popup.CurrentPopupFilterTextBox ?? _ownFilterTextBox;

            if (_currentFilterTextBox == null)
                return;

            if (!IsOwnFilterTextBox)
                ClearOwnTextBoxText();

            InitializeCurrentTextBoxText();

            if (IsLoaded)
                SetFocusOnFilterTextBox();
        }

        private void ClearOwnTextBoxText()
        {
            if (_ownFilterTextBox != null)
                _ownFilterTextBox.Text = string.Empty;
        }

        private void InitializeCurrentTextBoxText()
        {
            if (_currentFilterTextBox != null)
                _textBoxCache.SetFromCache(_currentFilterTextBox);
        }

        private void ClearCurrentTextBoxText()
        {
            if (_currentFilterTextBox != null)
                _currentFilterTextBox.Text = string.Empty;
        }
        
        /// <summary>
        /// Clears the filters along with TextBox cache.
        /// </summary>
        protected void ClearAllFilterText()
        {
            if (_currentFilterTextBox != null)
                _currentFilterTextBox.Text = string.Empty;
            if (_ownFilterTextBox != null)
                _ownFilterTextBox.Text = string.Empty;
            
            _textBoxCache.ClearCache();
        }
        #endregion

        #region Popup management
        private void OwnFilterTextBoxOnGotKeyboardFocus(object sender, KeyboardFocusChangedEventArgs e)
        {
            if (!_isDroppingFocus)
                IsOpen = true;
        }

        private void PopupOnOpened(object sender, EventArgs e)
        {
            InitializeCurrentTextBoxText();
            SetFocusOnFilterTextBox();
        }

        private void PopupOnClosed(object sender, EventArgs e)
        {
            IsOpen = false;
            DropFocus();
        }

        private void ClosePopup()
        {
            IsOpen = false;
            DropFocus();
        }
        
        private void DropFocus()
        {
            _isDroppingFocus = true;

            UnRegisterFromMainWindowInputEvents();
            
            if (Application.Current?.MainWindow != null)
                Application.Current.MainWindow.Focus();
            else
                Focus();
            
            _textBoxCache.Cache(_currentFilterTextBox);
            ClearCurrentTextBoxText();

            Keyboard.ClearFocus();

            _isDroppingFocus = false;
            
            OnFocusDropped();
        }
        
        /// <summary>
        /// Called whenever the control closes on focus drop.
        /// </summary>
        protected virtual void OnFocusDropped()
        { }
        #endregion

        #region Keyboard key pressed management
        /// <summary>
        /// Occurs whenever a key from keyboard is pressed.
        /// </summary>
        /// <param name="e">Event args.</param>
        protected override void OnPreviewKeyDown(KeyEventArgs e)
        {
            if (IsOpen != true)
            {
                _lastKeyWasPageChange = false;
                base.OnPreviewKeyDown(e);
                return;
            }
            
            var isFromFilterTextBox = Equals(e.OriginalSource, _currentFilterTextBox);

            if (!ProcessRepetitivePageKeyPressed(ref e, isFromFilterTextBox))
                return;
            
            if (ProcessTabNavigation(e, isFromFilterTextBox))
                return;

            if (ProcessClearFilterTextBox(e, isFromFilterTextBox))
                return;

            switch (e.Key)
            {
                case Key.Escape:
                    ClosePopup();
                    break;

                case Key.Space:
                case Key.Enter:
                    if (!isFromFilterTextBox)
                        return;
                    break;
                
                default:
                    if (!TryToNavigateAmongItems(ref e, isFromFilterTextBox))
                    {
                        if (!isFromFilterTextBox && !e.Handled)
                        {
                            SetFocusOnFilterTextBox();
                            e.Handled = true;
                        }
                    }
                    break;
            }

            base.OnPreviewKeyDown(e);
        }

        private bool ProcessTabNavigation(KeyEventArgs e, bool isFromFilterTextBox)
        {
            if (e.Key != Key.Tab)
                return false;

            if (isFromFilterTextBox)
                SetFocusOnPopup();

            if (!(e.OriginalSource is FrameworkElement source))
                return false;
            
            var nextFocus = source.PredictFocus(FocusNavigationDirection.Right) ?? source.PredictFocus(FocusNavigationDirection.Down);
            var nextFocusParent = nextFocus as SelectBoxItem ?? nextFocus.FindParent<SelectBoxItem>();
            if (nextFocusParent == null)
            {
                e.Handled = false;
                return true;
            }

            SetFocusOnItem(nextFocusParent);
            e.Handled = true;
            return true;
        }
        
        private bool ProcessClearFilterTextBox(KeyEventArgs e, bool isFromFilterTextBox)
        {
            if (!isFromFilterTextBox)
                return false;

            if (e.Key != Key.Space || Keyboard.Modifiers != ModifierKeys.Control)
                return false;

            _currentFilterTextBox.Clear();
            e.Handled = true;
            return true;
        }
        #endregion

        #region Navigation among items using keyboard
        private bool TryToNavigateAmongItems(ref KeyEventArgs e, bool isFromFilterTextBox)
        {
            switch (e.Key)
            {
                case Key.PageDown:
                    if (NavigatePageDownItems(e.OriginalSource, isFromFilterTextBox))
                        e.Handled = true;
                    return true;

                case Key.Down:
                    if (NavigateDownItems(e.OriginalSource, isFromFilterTextBox))
                        e.Handled = true;
                    return true;
                
                case Key.PageUp:
                    if (NavigatePageUpItems(e.OriginalSource, isFromFilterTextBox))
                        e.Handled = true;
                    return true;
                
                case Key.Up:
                    if (NavigateUpItems(e.OriginalSource, isFromFilterTextBox))
                        e.Handled = true;
                    return true;
            }

            return false;
        }

        private DateTime _lastPageChangeTimeStamp;
        private bool _lastKeyWasPageChange;
        
        private bool ProcessRepetitivePageKeyPressed(ref KeyEventArgs e, bool isFromFilterTextBox)
        {
            if (e.Key != Key.PageDown && e.Key != Key.PageUp)
            {
                _lastKeyWasPageChange = false;
                return true;
            }

            if (isFromFilterTextBox && _lastKeyWasPageChange)
                return false;
            
            _lastKeyWasPageChange = true;
            
            var now = DateTime.Now;
            if ((now - _lastPageChangeTimeStamp) < TimeSpan.FromMilliseconds(200))
            {
                e.Handled = true;
                return false;
            }

            _lastPageChangeTimeStamp = now;

            return true;
        }
        
        private bool NavigateDownItems(object originalItemSource, bool isFromFilterTextBox)
        {
            if (isFromFilterTextBox || Items.IsCurrentAfterLast)
            {
                SetFocusOnFirstItem();
                return true;
            }
            
            if (!(originalItemSource is SingleSelectBox) &&
                !Items.Contains(GetDataContext(originalItemSource)))
            {
                SetFocusOnFirstItem();
                return true;
            }
            
            var visualItem = GetSelectBoxItem(Items.CurrentItem);
            if (visualItem != originalItemSource)
            {
                SetFocusOnItem(originalItemSource);
                visualItem = originalItemSource as SelectBoxItem;
            }
            
            if (visualItem != null)
            {
                SetFocusOnNextItem();
                return true;
            }
            
            if (_itemsScrollViewer != null)
                SetFocusOnItem((int)_itemsScrollViewer.ContentVerticalOffset + 1);
            
            return false;
        }
        
        private bool NavigatePageDownItems(object originalItemSource, bool isFromFilterTextBox)
        {
            if (isFromFilterTextBox)
            {
                SetFocusOnFirstItem();
                return true;
            }
            
            if (!(originalItemSource is SingleSelectBox) &&
                !Items.Contains(GetDataContext(originalItemSource)))
            {
                SetFocusOnFirstItem();
                return true;
            }

            if (_itemsScrollViewer != null)
                ExecuteInBackground(() => SetFocusOnItem((int) _itemsScrollViewer.ContentVerticalOffset + (int) _itemsScrollViewer.ViewportHeight - 1));
    
            return false;
        }

        private bool NavigateUpItems(object originalItemSource, bool isFromFilterTextBox)
        {
            if (isFromFilterTextBox)
            {
                SetFocusOnLastItem();
                return true;
            }

            if (Items.IsCurrentBeforeFirst ||
                (!(originalItemSource is SingleSelectBox) &&
                !Items.Contains(GetDataContext(originalItemSource))))
            {
                SetFocusOnFilterTextBox();
                return true;
            }
            
            var visualItem = GetSelectBoxItem(Items.CurrentItem);
            if (visualItem != originalItemSource)
            {
                SetFocusOnItem(originalItemSource);
                visualItem = originalItemSource as SelectBoxItem;
            }
            
            if (visualItem != null)
            {
                SetFocusOnPreviousItem();
                return true;
            }
            
            return false;
        }
        
        private bool NavigatePageUpItems(object originalItemSource, bool isFromFilterTextBox)
        {
            if (isFromFilterTextBox)
            {
                SetFocusOnLastItem();
                return true;
            }

            if (Items.IsCurrentBeforeFirst ||
                (!(originalItemSource is SingleSelectBox) &&
                 !Items.Contains(GetDataContext(originalItemSource))))
            {
                SetFocusOnFilterTextBox();
                return true;
            }

            if (_itemsScrollViewer != null && !_focusOnItemGenerationRequested)
                ExecuteInBackground(() => SetFocusOnItem((int)_itemsScrollViewer.ContentVerticalOffset));

            return false;
        }
        
        private void SetFocusOnFilterTextBox()
        {
            MaintainPopupOpenWhileFocusingOnFilterTextBox();
            RegisterToMainWindowInputEvents();
            
            _currentFilterTextBox?.Focus();
        }
        
        private void SetFocusOnItem(int itemIndex)
        {
            if (itemIndex < Items.Count)
                Items.MoveCurrentToPosition(itemIndex);

            if (_focusOnItemGenerationRequested)
                return;
            
            if (_itemsScrollViewer != null && GetSelectBoxItem(Items.CurrentItem) == null)
                _itemsScrollViewer.ScrollToVerticalOffset(itemIndex - _itemsScrollViewer.ViewportHeight / 2);

            FocusOnCurrentItemOnceGenerated();
        }
        
        /// <summary>
        /// Sets the current focus on a given item.
        /// </summary>
        /// <param name="item">The item to focus on.</param>
        protected void SetFocusOnItem(object item)
        {
            if (item == null)
                return;
            
            var index = Items.IndexOf(item);
            if (index < 0)
            {
                var casted = GetDataContext(item);
                if (casted != null)
                    index = Items.IndexOf(casted);
            }
            
            if (index >= 0)
                SetFocusOnItem(index);
        }
        
        private void SetFocusOnFirstItem()
        {
            Items.MoveCurrentToFirst();

            var current = GetSelectBoxItem(Items.CurrentItem);
            if (current == null)
                _itemsScrollViewer?.ScrollToTop();

            FocusOnCurrentItemOnceGenerated();
        }

        private void SetFocusOnNextItem()
        {
            Items.MoveCurrentToNext();
            FocusOnCurrentItemOnceGenerated();
        }

        private void SetFocusOnPreviousItem()
        {
            Items.MoveCurrentToPrevious();
            FocusOnCurrentItemOnceGenerated();
        }

        private void SetFocusOnLastItem()
        {
            Items.MoveCurrentToLast();
            
            var current = GetSelectBoxItem(Items.CurrentItem);
            if (current == null)
                _itemsScrollViewer?.ScrollToBottom();

            FocusOnCurrentItemOnceGenerated();
        }
        
        private static object GetDataContext(object item)
        {
            if (item is FrameworkElement frameworkElement)
                return frameworkElement.DataContext;
            return null;
        }
        
        private void ExecuteInBackground(Action action)
            => Dispatcher.BeginInvoke(DispatcherPriority.Background, action);
        
        private bool _focusOnItemGenerationRequested;
        
        private void FocusOnCurrentItemOnceGenerated()
        {
            var visual = GetSelectBoxItem(Items.CurrentItem);
            if (visual != null)
                visual.Focus();
            else if (!_focusOnItemGenerationRequested)
            {
                _focusOnItemGenerationRequested = true;
                ItemContainerGenerator.StatusChanged += ItemContainerGeneratorOnStatusChanged;
            }
        }

        private void ItemContainerGeneratorOnStatusChanged(object sender, EventArgs e)
        {
            if (ItemContainerGenerator.Status != GeneratorStatus.ContainersGenerated)
                return;
            
            GetSelectBoxItem(Items.CurrentItem)?.Focus();
            
            ItemContainerGenerator.StatusChanged -= ItemContainerGeneratorOnStatusChanged;
            _focusOnItemGenerationRequested = false;
        }
        #endregion
        
        #region Popup control
        /// <summary>
        /// Gives focus to the managed popup.
        /// </summary>
        protected void SetFocusOnPopup()
        {
            _popup.Focus();
        }
        
        private void RegisterToMainWindowInputEvents()
        {
            if (Application.Current?.MainWindow == null)
                return;

            UnRegisterFromMainWindowInputEvents();
            Application.Current.MainWindow.PreviewMouseDown += MainWindowOnPreviewMouseDown;
            Application.Current.MainWindow.PreviewTouchDown += MainWindowOnPreviewTouchDown;
        }
        
        private void UnRegisterFromMainWindowInputEvents()
        {
            if (Application.Current?.MainWindow == null)
                return;
            
            Application.Current.MainWindow.PreviewMouseDown -= MainWindowOnPreviewMouseDown;
            Application.Current.MainWindow.PreviewTouchDown -= MainWindowOnPreviewTouchDown;
        }

        private void MaintainPopupOpenWhileFocusingOnFilterTextBox()
        {
            if (_popup.StaysOpen || _currentFilterTextBox == null)
                return;
            
            _popup.StaysOpen = true;
            _currentFilterTextBox.IsKeyboardFocusedChanged += CurrentFilterTextBoxOnIsKeyboardFocusedChanged;
        }

        private void CurrentFilterTextBoxOnIsKeyboardFocusedChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            _popup.StaysOpen = false;
            _currentFilterTextBox.IsKeyboardFocusedChanged -= CurrentFilterTextBoxOnIsKeyboardFocusedChanged;
        }

        private void MainWindowOnPreviewMouseDown(object sender, MouseButtonEventArgs e)
            => CloseIfInputOccuredOutside(e.GetPosition);

        private void MainWindowOnPreviewTouchDown(object sender, TouchEventArgs e)
            => CloseIfInputOccuredOutside(obj => e.GetTouchPoint(obj).Position);

        private void CloseIfInputOccuredOutside(Func<IInputElement, Point> positionGetter)
        {
            if (IsInputOutsideThis(positionGetter) && IsInputOutsidePopup(positionGetter))
                IsOpen = false;
        }

        private bool IsInputOutsideThis(Func<IInputElement, Point> positionGetter)
        {
            var input = positionGetter(this);
            return input.X < 0 || input.X > ActualWidth || input.Y < 0 || input.Y > ActualHeight; 
        }

        private bool IsInputOutsidePopup(Func<IInputElement, Point> positionGetter)
        {
            if (!(_popup.Child is FrameworkElement popupChild))
                return true;
            
            var input = positionGetter(popupChild);
            return input.X < 0 || input.X > popupChild.ActualWidth || input.Y < 0 || input.Y > popupChild.ActualHeight; 
        }
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

            var filterBoxItem = GetSelectBoxItem(item);
            if (filterBoxItem != null)
                filterBoxItem.IsSelected = false;
            else
                _itemIsSelectedProperty?.SetValue(item, false);
        }
        #endregion

        #region Commands
        /// <summary>
        /// Gets the command to toggle the <see cref="IsOpen"/> state.
        /// </summary>
        public ICommand ToggleOpenStateCommand { get; }
        #endregion

        #region Dependency properties
        /// <summary>
        /// Gets or sets a value indicating if the select box is open.
        /// </summary>
        public bool? IsOpen
        {
            get => (bool?)GetValue(IsOpenProperty);
            set => SetCurrentValue(IsOpenProperty, value);
        }
        /// <summary>
        /// Registers <see cref="IsOpen"/> as a dependency property.
        /// </summary>
        public static readonly DependencyProperty IsOpenProperty
            = DependencyProperty.Register(nameof(IsOpen), typeof(bool?), typeof(SelectBox), new FrameworkPropertyMetadata(false, IsOpenPropertyChanged));

        /// <summary>
        /// Called whenever the <see cref="IsOpen"/> property changes.
        /// </summary>
        /// <param name="sender">The object whose property changed.</param>
        /// <param name="args">Information about the property change.</param>
        private static void IsOpenPropertyChanged(DependencyObject sender, DependencyPropertyChangedEventArgs args)
        {
            if (!(sender is SelectBox selectBox) || !(args.NewValue is bool newValue))
                return;

            if (selectBox._popup.IsOpen != newValue)
                selectBox._popup.IsOpen = newValue;
            
            if (selectBox.IsOpen == true)
                selectBox.ApplyFilter(true, true);
        }
        
        /// <summary>
        /// Gets or sets a value indicating the path to item bool property on
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
            = DependencyProperty.Register(nameof(IsSelectedMemberPath), typeof(string), typeof(SelectBox), new FrameworkPropertyMetadata(default(string), ItemIsSelectedMemberPathChanged));

        /// <summary>
        /// Called whenever the <see cref="IsSelectedMemberPath"/> property changes.
        /// </summary>
        /// <param name="sender">The object whose property changed.</param>
        /// <param name="args">Information about the property change.</param>
        private static void ItemIsSelectedMemberPathChanged(DependencyObject sender, DependencyPropertyChangedEventArgs args)
        {
            if (!(sender is SelectBox item) || !(args.NewValue is string memberPath))
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
        /// Gets or sets a value indicating if popup should automatically close when
        /// one of its child buttons is pressed. 
        /// </summary>
        public bool ShouldCloseOnPopupButtonsClicks
        {
            get => (bool)GetValue(ShouldCloseOnPopupButtonsClicksProperty);
            set => SetCurrentValue(ShouldCloseOnPopupButtonsClicksProperty, value);
        }
        /// <summary>
        /// Registers <see cref="ShouldCloseOnPopupButtonsClicks"/> as a dependency property.
        /// </summary>
        public static readonly DependencyProperty ShouldCloseOnPopupButtonsClicksProperty
            = DependencyProperty.Register(nameof(ShouldCloseOnPopupButtonsClicks), typeof(bool), typeof(SelectBox), new FrameworkPropertyMetadata(true));
        
        /// <summary>
        /// Gets or sets the maximum height for the popup.
        /// </summary>
        [TypeConverter(typeof(LengthConverter))]
        public double MaxDropDownHeight
        {
            get => (double)GetValue(MaxDropDownHeightProperty);
            set => SetCurrentValue(MaxDropDownHeightProperty, value);
        }
        /// <summary>
        /// Registers <see cref="MaxDropDownHeight"/> as a dependency property.
        /// </summary>
        public static readonly DependencyProperty MaxDropDownHeightProperty
            = DependencyProperty.Register(nameof(MaxDropDownHeight), typeof(double), typeof(SelectBox), new FrameworkPropertyMetadata(SystemParameters.PrimaryScreenHeight / 3));
        
        /// <summary>
        /// Gets or sets the template of the additional content.
        /// </summary>
        public DataTemplate AdditionalContentTemplate
        {
            get => (DataTemplate)GetValue(AdditionalContentTemplateProperty);
            set => SetCurrentValue(AdditionalContentTemplateProperty, value);
        }
        /// <summary>
        /// Registers <see cref="AdditionalContentTemplate"/> as a dependency property.
        /// </summary>
        public static readonly DependencyProperty AdditionalContentTemplateProperty
            = DependencyProperty.Register(nameof(AdditionalContentTemplate), typeof(DataTemplate), typeof(SelectBox),
                                          new FrameworkPropertyMetadata(default(DataTemplate)));
        
        /// <summary>
        /// Gets or sets the template selector of the additional content.
        /// </summary>
        public DataTemplateSelector AdditionalContentTemplateSelector
        {
            get => (DataTemplateSelector)GetValue(AdditionalContentTemplateSelectorProperty);
            set => SetCurrentValue(AdditionalContentTemplateSelectorProperty, value);
        }
        /// <summary>
        /// Registers <see cref="AdditionalContentTemplateSelector"/> as a dependency property.
        /// </summary>
        public static readonly DependencyProperty AdditionalContentTemplateSelectorProperty
            = DependencyProperty.Register(nameof(AdditionalContentTemplateSelector), typeof(DataTemplateSelector), typeof(SelectBox), 
                                          new FrameworkPropertyMetadata(default(DataTemplateSelector)));
        
        /// <summary>
        /// Gets or sets the command to be called when the additional action button is hit.
        /// </summary>
        public ICommand AdditionalContentActionCommand
        {
            get => (ICommand)GetValue(AdditionalContentActionCommandProperty);
            set => SetCurrentValue(AdditionalContentActionCommandProperty, value);
        }
        /// <summary>
        /// Registers <see cref="AdditionalContentActionCommand"/> as a dependency property.
        /// </summary>
        public static readonly DependencyProperty AdditionalContentActionCommandProperty
            = DependencyProperty.Register(nameof(AdditionalContentActionCommand), typeof(ICommand), typeof(SelectBox), 
                                          new FrameworkPropertyMetadata(default(ICommand)));
        
        /// <summary>
        /// Gets or sets the text to be displayed on the additional content when no items are selected.
        /// </summary>
        public string AdditionalContentNoSelectionText
        {
            get => (string)GetValue(AdditionalContentNoSelectionTextProperty);
            set => SetCurrentValue(AdditionalContentNoSelectionTextProperty, value);
        }
        /// <summary>
        /// Registers <see cref="AdditionalContentNoSelectionText"/> as a dependency property.
        /// </summary>
        public static readonly DependencyProperty AdditionalContentNoSelectionTextProperty
            = DependencyProperty.Register(nameof(AdditionalContentNoSelectionText), typeof(string), typeof(SelectBox), 
                                          new FrameworkPropertyMetadata(default(string)));

        /// <summary>
        /// Gets or sets the text to be displayed on the additional content action button when an item is selected.
        /// </summary>
        public string AdditionalContentOnSelectionText
        {
            get => (string)GetValue(AdditionalContentOnSelectionTextProperty);
            set => SetCurrentValue(AdditionalContentOnSelectionTextProperty, value);
        }
        /// <summary>
        /// Registers <see cref="AdditionalContentOnSelectionText"/> as a dependency property.
        /// </summary>
        public static readonly DependencyProperty AdditionalContentOnSelectionTextProperty
            = DependencyProperty.Register(nameof(AdditionalContentOnSelectionText), typeof(string), typeof(SelectBox), 
                                          new FrameworkPropertyMetadata(default(string)));
        
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
            = DependencyProperty.Register(nameof(IconKind), typeof(PackIconKind), typeof(SelectBox), new FrameworkPropertyMetadata(default(PackIconKind)));
        
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
            = DependencyProperty.Register(nameof(IconForeground), typeof(Brush), typeof(SelectBox), new FrameworkPropertyMetadata(default(Brush)));
        
        /// <summary>
        /// Gets or sets the kind of the icon when the control is open.
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
            = DependencyProperty.Register(nameof(IconKindWhenOpen), typeof(PackIconKind), typeof(SelectBox), new FrameworkPropertyMetadata(default(PackIconKind)));
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
            return new SelectBoxItem(_filterCache, AlsoMatchWithFirstWordLetters || AlsoMatchFilterWordsAcrossBoundProperties, _itemIsSelectedProperty?.Name);
        }
        
        /// <summary>
        /// Gets the visual item of a given source item.
        /// </summary>
        /// <param name="item">The item to be retrieved.</param>
        /// <returns>The item itself if a visual item or the generated visual item if of other type.</returns>
        protected SelectBoxItem GetSelectBoxItem(object item)
        {
            if (!(item is SelectBoxItem visualItem))
            {
                visualItem = ItemContainerGenerator.ContainerFromItem(item) as SelectBoxItem;
            }

            return visualItem;
        }
        #endregion
    }
}

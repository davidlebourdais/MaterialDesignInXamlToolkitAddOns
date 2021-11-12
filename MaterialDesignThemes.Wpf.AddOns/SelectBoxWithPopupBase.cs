using System;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Threading;
using EMA.ExtendedWPFVisualTreeHelper;
using MaterialDesignThemes.Wpf.AddOns.Utils.Caching;
using MaterialDesignThemes.Wpf.AddOns.Utils.Commands;

namespace MaterialDesignThemes.Wpf.AddOns
{
    /// <summary>
    /// A <see cref="SelectBoxBase"/> implementation with a <see cref="SelectBoxPopup"/> for
    /// items display. 
    /// </summary>
    [TemplatePart(Name = "PART_FilterTextBox", Type = typeof(TextBox))]
    [TemplatePart(Name = "PART_Popup", Type = typeof(Popup))]
    [TemplatePart(Name = "ItemsScrollViewer", Type = typeof(ScrollViewer))]
    [StyleTypedProperty(Property = "ItemContainerStyle", StyleTargetType = typeof(SelectBoxItem))]
    public abstract class SelectBoxWithPopupBase : SelectBoxBase
    {
        /// <summary>
        /// The current <see cref="TextBox"/> holding the filter.
        /// </summary>
        protected TextBox _currentFilterTextBox;
        
        private TextBox _ownFilterTextBox;
        private SelectBoxPopup _popup;
        private ScrollViewer _itemsScrollViewer;
        
        private readonly TextBoxCache _textBoxCache;

        private bool _isDroppingFocus;

        #region Constructors and initializations
        /// <summary>
        /// Creates a new instance of <see cref="SelectBoxWithPopupBase"/>.
        /// </summary>
        protected SelectBoxWithPopupBase()
        {
            _textBoxCache = new TextBoxCache();
            
            ToggleOpenStateCommand = new SimpleCommand(() => IsOpen = !IsOpen, () => IsEnabled);
        }

        /// <summary>
        /// Static constructor for <see cref="SelectBoxWithPopupBase"/> type.
        /// Override some base dependency properties.
        /// </summary>
        static SelectBoxWithPopupBase()
        {
            DefaultStyleKeyProperty.OverrideMetadata(typeof(SelectBoxWithPopupBase), new FrameworkPropertyMetadata(typeof(SelectBoxWithPopupBase)));
        }

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
                throw new Exception(nameof(SelectBoxWithPopupBase) + " template must contain a " + nameof(SelectBoxPopup) + " named 'PART_Popup'.");

            _popup.Opened += PopupOnOpened;
            _popup.Closed += PopupOnClosed;
            _popup.FilterTextBoxChanged += PopupOnFilterTextBoxChanged;
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
        }
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
        
        #region Popup management
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
            = DependencyProperty.Register(nameof(IsOpen), typeof(bool?), typeof(SelectBoxWithPopupBase), new FrameworkPropertyMetadata(false, IsOpenPropertyChanged));

        /// <summary>
        /// Called whenever the <see cref="IsOpen"/> property changes.
        /// </summary>
        /// <param name="sender">The object whose property changed.</param>
        /// <param name="args">Information about the property change.</param>
        private static void IsOpenPropertyChanged(DependencyObject sender, DependencyPropertyChangedEventArgs args)
        {
            if (!(sender is SelectBoxWithPopupBase selectBoxWithPopup) || !(args.NewValue is bool newValue))
                return;

            if (selectBoxWithPopup._popup.IsOpen != newValue)
                selectBoxWithPopup._popup.IsOpen = newValue;
            selectBoxWithPopup.ApplyFilter(true);
        }
        
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
            = DependencyProperty.Register(nameof(MaxDropDownHeight), typeof(double), typeof(SelectBoxWithPopupBase), new FrameworkPropertyMetadata(SystemParameters.PrimaryScreenHeight / 3));
        #endregion
    }
}

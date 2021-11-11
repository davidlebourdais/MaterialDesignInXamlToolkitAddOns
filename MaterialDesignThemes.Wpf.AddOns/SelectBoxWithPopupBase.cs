using System;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Documents;
using System.Windows.Input;
using MaterialDesignThemes.Wpf.AddOns.Utils.Caching;
using MaterialDesignThemes.Wpf.AddOns.Utils.Commands;

namespace MaterialDesignThemes.Wpf.AddOns
{
    /// <summary>
    /// A <see cref="SelectBoxBase"/> implementation with a <see cref="SelectBoxPopup"/> for
    /// items display. 
    /// </summary>
    [StyleTypedProperty(Property = "PART_FilterTextBox", StyleTargetType = typeof(TextBox))]
    [StyleTypedProperty(Property = "PART_Popup", StyleTargetType = typeof(Popup))]
    [StyleTypedProperty(Property = "ItemContainerStyle", StyleTargetType = typeof(SelectBoxItem))]
    public abstract class SelectBoxWithPopupBase : SelectBoxBase
    {
        /// <summary>
        /// The current <see cref="TextBox"/> holding the filter.
        /// </summary>
        protected TextBox _currentFilterTextBox;
        
        private TextBox _ownFilterTextBox;
        private SelectBoxPopup _popup;
        
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
            var isFromFilterTextBox = Equals(e.OriginalSource, _currentFilterTextBox);

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

     
                case Key.PageDown:
                case Key.PageUp:
                    break;

                default:
                    TryToNavigateAmongItems(ref e, isFromFilterTextBox);

                    if (!e.Handled)
                        if (!Equals(e.OriginalSource, _currentFilterTextBox) && !(e.OriginalSource is TextElement))
                            SetFocusOnFilterTextBox();
                    break;
            }

            base.OnPreviewKeyDown(e);
        }

        private bool ProcessTabNavigation(KeyEventArgs e, bool isFromFilterTextBox)
        {
            if (e.Key != Key.Tab || IsOpen != true)
                return false;

            if (isFromFilterTextBox)
                SetFocusOnPopup();
            
            e.Handled = false;
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
        private void TryToNavigateAmongItems(ref KeyEventArgs e, bool isFromFilterTextBox)
        {
            switch (e.Key)
            {
                case Key.Down:
                    if (NavigateDownItems(e.OriginalSource))
                        e.Handled = true;
                    break;

                case Key.Up:
                    if (NavigateUpItems(e.Source, isFromFilterTextBox))
                        e.Handled = true;
                    break;

                default:
                    if (!isFromFilterTextBox && _currentFilterTextBox?.IsFocused == true && Keyboard.Modifiers == ModifierKeys.None)
                    {
                        SetFocusOnFilterTextBox();
                        e.Handled = true;
                    }
                    break;
            }
        }
        
        private bool NavigateDownItems(object originalItemSource)
        {
            if (!Items.Contains(GetSelectBoxItem(originalItemSource)) &&
                !Items.Contains(GetDataContext(originalItemSource)))
            {
                SetFocusOnFirstItem();
                return true;
            }

            if (GetSelectBoxItem(Items.CurrentItem)?.IsFocused == true || Items.IsCurrentBeforeFirst)
            {
                SetFocusOnNextItem();
                return true;
            }

            return false;
        }
        
        private bool NavigateUpItems(object itemSource, bool isFromFilterTextBox)
        {
            var currentItem = GetSelectBoxItem(Items.CurrentItem);
            if (currentItem == null && isFromFilterTextBox)
            {
                SetFocusOnLastItem();
                return true;
            }

            if (currentItem?.IsFocused == true && currentItem == Items[0])
            {
                Items.MoveCurrentToPrevious();
                SetFocusOnFilterTextBox();
                return true;
            }

            if (currentItem?.IsFocused == true || Items.IsCurrentAfterLast && !Equals(itemSource, this))
            {
                SetFocusOnPreviousItem();
                return true;
            }

            return false;
        }

        private void SetFocusOnFilterTextBox()
        {
            MaintainPopupOpenWhileFocusingOnFilterTextBox();
            RegisterToMainWindowInputEvents();
            
            _currentFilterTextBox?.Focus();
        }
        
        private void SetFocusOnFirstItem()
        {
            Items.MoveCurrentToFirst();
            GetSelectBoxItem(Items.CurrentItem)?.Focus();
        }

        private void SetFocusOnNextItem()
        {
            Items.MoveCurrentToNext();
            GetSelectBoxItem(Items.CurrentItem)?.Focus();
        }

        private void SetFocusOnPreviousItem()
        {
            Items.MoveCurrentToPrevious();
            GetSelectBoxItem(Items.CurrentItem)?.Focus();
        }

        private void SetFocusOnLastItem()
        {
            Items.MoveCurrentToLast();
            GetSelectBoxItem(Items.CurrentItem)?.Focus();
        }
        
        private static object GetDataContext(object item)
        {
            if (item is FrameworkElement frameworkElement)
                return frameworkElement.DataContext;
            return null;
        }
        #endregion
        
        #region Popup management
        /// <summary>
        /// Gives focus to the managed popup.
        /// </summary>
        protected virtual void SetFocusOnPopup()
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

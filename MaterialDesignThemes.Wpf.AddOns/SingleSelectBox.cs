using System;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Documents;
using System.Windows.Input;
using MaterialDesignThemes.Wpf.AddOns.Utils.Commands;

namespace MaterialDesignThemes.Wpf.AddOns
{
    /// <summary>
    /// Displays items to select them quickly through a persistent ComboBox-like popup.
    /// </summary>
    [StyleTypedProperty(Property = "PART_FilterTextBox", StyleTargetType = typeof(TextBox))]
    [StyleTypedProperty(Property = "PART_Popup", StyleTargetType = typeof(Popup))]
    [StyleTypedProperty(Property = "ItemContainerStyle", StyleTargetType = typeof(SelectBoxItem))]
    [StyleTypedProperty(Property = "PART_CopyButton", StyleTargetType = typeof(SelectBoxItem))]
    public class SingleSelectBox : SelectBoxBase
    {
        private TextBox _ownFilterTextBox;
        private SelectBoxPopup _popup;
        private TextBox _currentFilterTextBox;
        private Button _copyButton;

        private bool _internalItemTemplateSyncInProgress;
        private bool _areItemAndSelectedItemTemplateSynced = true;

        #region Constructors and initializations
        /// <summary>
        /// Creates a new instance of <see cref="SingleSelectBox"/>.
        /// </summary>
        public SingleSelectBox()
        {
            ToggleOpenStateCommand = new SimpleCommand(() => IsOpen = !IsOpen, () => IsEnabled);
            ClearSelectionCommand = new SimpleCommand(() => { SetAsUnSelected(SelectedItem); SelectedItem = null; }, () => HasASelectedItem);
            GoToSelectedItemCommand = new SimpleCommand(() => GetSelectBoxItem(SelectedItem)?.Focus(), () => HasASelectedItem);

            AddHandler(SelectBoxItem.IsSelectedChangedEvent, new RoutedEventHandler((sender, args) =>
            {
                if (args.OriginalSource is SelectBoxItem selectBoxItem && selectBoxItem.IsSelected)
                    SelectItem(selectBoxItem);

                args.Handled = true;
            }));
        }

        /// <summary>
        /// Static constructor for <see cref="SingleSelectBox"/> type.
        /// Override some base dependency properties.
        /// </summary>
        static SingleSelectBox()
        {
            DefaultStyleKeyProperty.OverrideMetadata(typeof(SingleSelectBox), new FrameworkPropertyMetadata(typeof(SingleSelectBox)));
            ItemTemplateProperty.OverrideMetadata(typeof(SingleSelectBox), new FrameworkPropertyMetadata(null, ItemTemplateOrTemplateSelectorPropertyChanged));
            ItemTemplateSelectorProperty.OverrideMetadata(typeof(SingleSelectBox), new FrameworkPropertyMetadata(null, ItemTemplateOrTemplateSelectorPropertyChanged));
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
            InitializeCopyButtonIfAny();
        }

        private void InitializePopUpOrThrow()
        {
            var popup = Template.FindName("PART_Popup", this);
            if (popup == null || (_popup = popup as SelectBoxPopup) == null)
                throw new Exception(nameof(SingleSelectBox) + " template must contain a " + nameof(SelectBoxPopup) + " named 'PART_Popup'.");

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

        private void InitializeCopyButtonIfAny()
        {
            _copyButton = Template.FindName("PART_CopyButton", this) as Button;
            if (_copyButton != null)
                _copyButton.Click += (sender, args) => CopySelectedItemCommand?.Execute(SelectedItem);
        }

        private void PopupOnFilterTextBoxChanged(object sender, RoutedEventArgs e)
        {
            ReloadCurrentFilterTextBox();
        }

        private void ReloadCurrentFilterTextBox()
        {
            _currentFilterTextBox = _popup.CurrentPopupFilterTextBox ?? _ownFilterTextBox;

            if (_currentFilterTextBox == null)
                return;

            if (IsLoaded)
                SetFocusOnFilterTextBox();
        }
        #endregion

        #region Item template synchronization
        /// <summary>
        /// Called whenever the <see cref="ItemsControl.ItemTemplate"/> property changes.
        /// Synchronizes the <see cref="SelectedItemTemplate"/> and <see cref="SelectedItemTemplateSelector"/> properties when defaulted.
        /// </summary>
        /// <param name="sender">The object whose property changed.</param>
        /// <param name="args">Information about the property change.</param>
        private static void ItemTemplateOrTemplateSelectorPropertyChanged(DependencyObject sender, DependencyPropertyChangedEventArgs args)
        {
            if (!(sender is SingleSelectBox singleSelectBox))
                return;

            if (!singleSelectBox._areItemAndSelectedItemTemplateSynced)
                return;

            if (args.Property == ItemTemplateProperty && args.NewValue is DataTemplate template)
                singleSelectBox.SynchronizeSelectedItemTemplate(template);

            else if (args.Property == ItemTemplateSelectorProperty && args.NewValue is DataTemplateSelector templateSelector)
                singleSelectBox.SynchronizeSelectedItemTemplateSelector(templateSelector);
        }

        private void SynchronizeSelectedItemTemplate(DataTemplate template)
        {
            _internalItemTemplateSyncInProgress = true;
            SelectedItemTemplate = template;
            _internalItemTemplateSyncInProgress = false;
        }

        private void SynchronizeSelectedItemTemplateSelector(DataTemplateSelector templateSelector)
        {
            _internalItemTemplateSyncInProgress = true;
            SelectedItemTemplateSelector = templateSelector;
            _internalItemTemplateSyncInProgress = false;
        }
        #endregion

        #region Popup management
        private void OwnFilterTextBoxOnGotKeyboardFocus(object sender, KeyboardFocusChangedEventArgs e)
        {
            IsOpen = true;
        }

        private void PopupOnOpened(object sender, EventArgs e)
        {
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
            if (Application.Current?.MainWindow != null)
                Application.Current.MainWindow.Focus();
            else
                Focus();

            Keyboard.ClearFocus();
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

            if (ProcessImmediateToggleSelection(e, isFromFilterTextBox))
                return;

            if (ProcessGoToSelection(e))
                return;

            if (ProcessCopySelection(e, isFromFilterTextBox))
                return;

            if (ProcessClearSelection(e))
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

                case Key.Tab:
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

        private bool ProcessGoToSelection(KeyEventArgs e)
        {
            if (IsOpen != true)
                return false;

            if (e.Key != Key.Down || Keyboard.Modifiers != ModifierKeys.Control)
                return false;

            if (!HasASelectedItem || IsSelectedItemFilteredOut)
                return false;

            GoToSelectedItemCommand?.Execute(null);

            e.Handled = true;
            return true;
        }

        private bool ProcessCopySelection(KeyEventArgs e, bool isFromFilterTextBox)
        {
            if (!isFromFilterTextBox && !Equals(e.OriginalSource, _ownFilterTextBox))
                return false;

            if (e.Key != Key.C || Keyboard.Modifiers != ModifierKeys.Control)
                return false;

            if (!HasASelectedItem)
                return false;

            if (_currentFilterTextBox.SelectedText.Length > 0)
                return false;

            CopySelectedItemCommand?.Execute(SelectedItem);

            e.Handled = true;
            return true;
        }

        private bool ProcessClearSelection(KeyEventArgs e)
        {
            if (e.Key != Key.Delete || Keyboard.Modifiers != ModifierKeys.Control)
                return false;

            if (!HasASelectedItem)
                return false;

            ClearSelectionCommand.Execute(null);

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
                    if (!Items.Contains(GetSelectBoxItem(e.OriginalSource)) &&
                            !Items.Contains(GetDataContext(e.OriginalSource)))
                    {
                        SetFocusOnFirstItem();
                        e.Handled = true;
                    }
                    else if (GetSelectBoxItem(Items.CurrentItem)?.IsFocused == true || Items.IsCurrentBeforeFirst)
                    {
                        SetFocusOnNextItem();
                        e.Handled = true;
                    }
                    break;

                case Key.Up:
                    var currentItem = GetSelectBoxItem(Items.CurrentItem);
                    if (currentItem == null && isFromFilterTextBox)
                    {
                        SetFocusOnLastItem();
                        e.Handled = true;e.Handled = true;
                    }
                    else if (currentItem?.IsFocused == true && currentItem == Items[0])
                    {
                        Items.MoveCurrentToPrevious();
                        SetFocusOnFilterTextBox();
                        e.Handled = true;
                    }
                    else if (currentItem?.IsFocused == true || Items.IsCurrentAfterLast && !Equals(e.Source, this))
                    {
                        SetFocusOnPreviousItem();
                        e.Handled = true;
                    }
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

        private void SetFocusOnFilterTextBox()
        {
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

        #region Item immediate selection with CTRL+Enter
        private bool ProcessImmediateToggleSelection(KeyEventArgs e, bool isFromFilterTextBox)
        {
            if (!isFromFilterTextBox)
                return false;

            if (e.Key != Key.Enter || Keyboard.Modifiers != ModifierKeys.Control)
                return false;

            TryToSelectFirstItem();

            e.Handled = true;
            base.OnPreviewKeyDown(e);

            return true;
        }

        private void TryToSelectFirstItem()
        {
            Items.MoveCurrentToFirst();

            var selectBoxItem = GetSelectBoxItem(Items.CurrentItem);
            SelectItem(selectBoxItem);
        }
        #endregion

        #region Item selection
        private void SelectItem(FrameworkElement toSelect)
        {
            foreach (var item in Items)
            {
                var selectedValueToAssign = item.Equals(toSelect) || item.Equals(toSelect.DataContext);

                if (selectedValueToAssign)
                    SelectedItem = item.Equals(toSelect) ? toSelect : toSelect.DataContext;
                else
                    SetAsUnSelected(item);
            }
        }
        #endregion

        #region Commands
        /// <summary>
        /// Gets the command to toggle the <see cref="IsOpen"/> state.
        /// </summary>
        public ICommand ToggleOpenStateCommand { get; }

        /// <summary>
        /// Gets the command that gives focus to the selected item.
        /// </summary>
        public ICommand GoToSelectedItemCommand { get; }

        /// <summary>
        /// Gets the command to clear current selection.
        /// </summary>
        public ICommand ClearSelectionCommand { get; }
        #endregion

        #region Dependency properties
        /// <summary>
        /// Gets or sets the currently selected item.
        /// </summary>
        public object SelectedItem
        {
            get => GetValue(SelectedItemProperty);
            set => SetValue(SelectedItemProperty, value);
        }
        /// <summary>
        /// Registers <see cref="SelectedItem"/> as a dependency property.
        /// </summary>
        public static readonly DependencyProperty SelectedItemProperty
            = DependencyProperty.Register(nameof(SelectedItem), typeof(object), typeof(SingleSelectBox),
                                          new FrameworkPropertyMetadata(default, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, SelectedItemPropertyChanged));

        /// <summary>
        /// Called whenever the <see cref="SelectedItem"/> property changes.
        /// </summary>
        /// <param name="sender">The object whose property changed.</param>
        /// <param name="args">Information about the property change.</param>
        private static void SelectedItemPropertyChanged(DependencyObject sender, DependencyPropertyChangedEventArgs args)
        {
            if (!(sender is SingleSelectBox singleSelectBox))
                return;

            if (args.NewValue != null)
                singleSelectBox.ClosePopup();
            singleSelectBox.HasASelectedItem = singleSelectBox.SelectedItem != null;
        }

        /// <summary>
        /// Gets a value indicating if an item is currently selected.
        /// </summary>
        public bool HasASelectedItem
        {
            get => (bool)GetValue(HasASelectedItemProperty);
            protected set => SetValue(_hasASelectedItemPropertyKey, value);
        }
        private static readonly DependencyPropertyKey _hasASelectedItemPropertyKey
            = DependencyProperty.RegisterReadOnly(nameof(HasASelectedItem), typeof(bool), typeof(SingleSelectBox),
                                                  new FrameworkPropertyMetadata(default(bool)));

        /// <summary>
        /// Registers <see cref="HasASelectedItem"/> as a readonly dependency property.
        /// </summary>
        public static readonly DependencyProperty HasASelectedItemProperty = _hasASelectedItemPropertyKey.DependencyProperty;

        /// <summary>
        /// Gets a value indicating the currently selected item does not pass current filter.
        /// </summary>
        public bool IsSelectedItemFilteredOut
        {
            get => (bool)GetValue(IsSelectedItemFilteredOutProperty);
            protected set => SetValue(_isSelectedItemFilteredOutPropertyKey, value);
        }
        private static readonly DependencyPropertyKey _isSelectedItemFilteredOutPropertyKey
            = DependencyProperty.RegisterReadOnly(nameof(IsSelectedItemFilteredOut), typeof(bool), typeof(SingleSelectBox),
                                                  new FrameworkPropertyMetadata(default(bool)));

        /// <summary>
        /// Registers <see cref="IsSelectedItemFilteredOut"/> as a readonly dependency property.
        /// </summary>
        public static readonly DependencyProperty IsSelectedItemFilteredOutProperty = _isSelectedItemFilteredOutPropertyKey.DependencyProperty;

        /// <summary>
        /// Gets or sets the template of the selected item.
        /// </summary>
        public DataTemplate SelectedItemTemplate
        {
            get => (DataTemplate)GetValue(SelectedItemTemplateProperty);
            set => SetValue(SelectedItemTemplateProperty, value);
        }

        /// <summary>
        /// Registers <see cref="SelectedItemTemplate"/> as a dependency property.
        /// </summary>
        public static readonly DependencyProperty SelectedItemTemplateProperty
            = DependencyProperty.Register(nameof(SelectedItemTemplate), typeof(DataTemplate), typeof(SingleSelectBox), new FrameworkPropertyMetadata(default(DataTemplate), SelectedItemTemplatePropertyChanged));

        /// <summary>
        /// Called whenever the <see cref="SelectedItemTemplate"/> property changes.
        /// </summary>
        /// <param name="sender">The object whose property changed.</param>
        /// <param name="args">Information about the property change.</param>
        private static void SelectedItemTemplatePropertyChanged(DependencyObject sender, DependencyPropertyChangedEventArgs args)
        {
            if (!(sender is SingleSelectBox singleSelectBox))
                return;

            if (!singleSelectBox._internalItemTemplateSyncInProgress)
                singleSelectBox._areItemAndSelectedItemTemplateSynced = false;
        }

        /// <summary>
        /// Gets or sets the template selector of the selected item.
        /// </summary>
        public DataTemplateSelector SelectedItemTemplateSelector
        {
            get => (DataTemplateSelector)GetValue(SelectedItemTemplateSelectorProperty);
            set => SetValue(SelectedItemTemplateSelectorProperty, value);
        }
        /// <summary>
        /// Registers <see cref="SelectedItemTemplateSelector"/> as a dependency property.
        /// </summary>
        public static readonly DependencyProperty SelectedItemTemplateSelectorProperty
            = DependencyProperty.Register(nameof(SelectedItemTemplateSelector), typeof(DataTemplateSelector), typeof(SingleSelectBox), new FrameworkPropertyMetadata(default(DataTemplate), SelectedItemTemplateSelectorPropertyChanged));

        /// <summary>
        /// Called whenever the <see cref="SelectedItemTemplateSelector"/> property changes.
        /// </summary>
        /// <param name="sender">The object whose property changed.</param>
        /// <param name="args">Information about the property change.</param>
        private static void SelectedItemTemplateSelectorPropertyChanged(DependencyObject sender, DependencyPropertyChangedEventArgs args)
        {
            if (!(sender is SingleSelectBox singleSelectBox))
                return;

            if (!singleSelectBox._internalItemTemplateSyncInProgress)
                singleSelectBox._areItemAndSelectedItemTemplateSynced = false;
        }

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
            = DependencyProperty.Register(nameof(IsOpen), typeof(bool?), typeof(SingleSelectBox), new FrameworkPropertyMetadata(false, IsOpenPropertyChanged));

        /// <summary>
        /// Called whenever the <see cref="IsOpen"/> property changes.
        /// </summary>
        /// <param name="sender">The object whose property changed.</param>
        /// <param name="args">Information about the property change.</param>
        private static void IsOpenPropertyChanged(DependencyObject sender, DependencyPropertyChangedEventArgs args)
        {
            if (!(sender is SingleSelectBox singleSelectBox) || !(args.NewValue is bool newValue))
                return;

            singleSelectBox._popup.IsOpen = newValue;

            if (newValue)
                singleSelectBox._popup.Focus();
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
            = DependencyProperty.Register(nameof(MaxDropDownHeight), typeof(double), typeof(SingleSelectBox), new FrameworkPropertyMetadata(SystemParameters.PrimaryScreenHeight / 3));

        /// <summary>
        /// Gets or sets the text to be displayed near the selected item.
        /// </summary>
        public string TextForSelectedItem
        {
            get => (string)GetValue(TextForSelectedItemProperty);
            set => SetCurrentValue(TextForSelectedItemProperty, value);
        }
        /// <summary>
        /// Registers <see cref="TextForSelectedItem"/> as a dependency property.
        /// </summary>
        public static readonly DependencyProperty TextForSelectedItemProperty
            = DependencyProperty.Register(nameof(TextForSelectedItem), typeof(string), typeof(SingleSelectBox), new FrameworkPropertyMetadata("Current"));

        /// <summary>
        /// Gets or sets the text to be displayed when <see cref="IsSelectedItemFilteredOut"/> is true.
        /// </summary>
        public string TextForSelectedItemWhenFilteredOut
        {
            get => (string)GetValue(TextForSelectedItemWhenFilteredOutProperty);
            set => SetCurrentValue(TextForSelectedItemWhenFilteredOutProperty, value);
        }
        /// <summary>
        /// Registers <see cref="TextForSelectedItemWhenFilteredOut"/> as a dependency property.
        /// </summary>
        public static readonly DependencyProperty TextForSelectedItemWhenFilteredOutProperty
            = DependencyProperty.Register(nameof(TextForSelectedItemWhenFilteredOut), typeof(string), typeof(SingleSelectBox), new FrameworkPropertyMetadata("Unavailable"));

        /// <summary>
        /// Gets or sets the hint for selection focus.
        /// </summary>
        public string GoToSelectionHint
        {
            get => (string)GetValue(GoToSelectionHintProperty);
            set => SetCurrentValue(GoToSelectionHintProperty, value);
        }
        /// <summary>
        /// Registers <see cref="GoToSelectionHint"/> as a dependency property.
        /// </summary>
        public static readonly DependencyProperty GoToSelectionHintProperty
            = DependencyProperty.Register(nameof(GoToSelectionHint), typeof(string), typeof(SingleSelectBox), new FrameworkPropertyMetadata("Focus on current selection (CTRL+Down)"));

        /// <summary>
        /// Gets or sets the hint to be displayed when <see cref="IsSelectedItemFilteredOut"/> is true.
        /// </summary>
        public string IsSelectedItemFilteredOutHint
        {
            get => (string)GetValue(IsSelectedItemFilteredOutHintProperty);
            set => SetCurrentValue(IsSelectedItemFilteredOutHintProperty, value);
        }
        /// <summary>
        /// Registers <see cref="IsSelectedItemFilteredOutHint"/> as a dependency property.
        /// </summary>
        public static readonly DependencyProperty IsSelectedItemFilteredOutHintProperty
            = DependencyProperty.Register(nameof(IsSelectedItemFilteredOutHint), typeof(string), typeof(SingleSelectBox), new FrameworkPropertyMetadata("Selection does not pass current filter"));

        /// <summary>
        /// Gets or sets the hint for selection copy.
        /// </summary>
        public string CopySelectionHint
        {
            get => (string)GetValue(CopySelectionHintProperty);
            set => SetCurrentValue(CopySelectionHintProperty, value);
        }
        /// <summary>
        /// Registers <see cref="CopySelectionHint"/> as a dependency property.
        /// </summary>
        public static readonly DependencyProperty CopySelectionHintProperty
            = DependencyProperty.Register(nameof(CopySelectionHint), typeof(string), typeof(SingleSelectBox), new FrameworkPropertyMetadata("Copy current selection (CTRL+C)"));

        /// <summary>
        /// Gets or sets the hint for selection clearing.
        /// </summary>
        public string ClearSelectionHint
        {
            get => (string)GetValue(ClearSelectionHintProperty);
            set => SetCurrentValue(ClearSelectionHintProperty, value);
        }
        /// <summary>
        /// Registers <see cref="ClearSelectionHint"/> as a dependency property.
        /// </summary>
        public static readonly DependencyProperty ClearSelectionHintProperty
            = DependencyProperty.Register(nameof(ClearSelectionHint), typeof(string), typeof(SingleSelectBox), new FrameworkPropertyMetadata("Clear current selection (CTRL+Delete)"));

        /// <summary>
        /// Gets or sets a command that will be triggered when the copy button is pressed.
        /// </summary>
        public ICommand CopySelectedItemCommand
        {
            get => (ICommand)GetValue(CopySelectedItemCommandProperty);
            set => SetCurrentValue(CopySelectedItemCommandProperty, value);
        }
        /// <summary>
        /// Registers <see cref="CopySelectedItemCommandProperty"/> as a dependency property.
        /// </summary>
        public static readonly DependencyProperty CopySelectedItemCommandProperty
            = DependencyProperty.Register(nameof(CopySelectedItemCommand), typeof(ICommand), typeof(SingleSelectBox), new FrameworkPropertyMetadata(default(ICommand)));
        #endregion
    }
}

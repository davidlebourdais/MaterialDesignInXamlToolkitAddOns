using System;
using System.ComponentModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Threading;
using EMA.ExtendedWPFVisualTreeHelper;

namespace MaterialDesignThemes.Wpf.AddOns
{
    /// <summary>
    /// A button that can display a popup when clicking on its inner toggle button.
    /// </summary>
    [TemplatePart(Name = "PART_ToggleButton", Type = typeof(ToggleButton))]
    [TemplatePart(Name = "PART_Popup", Type = typeof(ToggleButton))]
    public class SplitButton : ButtonBase
    {
        private ToggleButton _toggle;
        private Popup _popup;
        private bool _commandIsDisabled;  // used to disable command while inner popup is opened.
        private DispatcherTimer _closePopupWithDelayTimer;
        
        static SplitButton()
        {
            DefaultStyleKeyProperty.OverrideMetadata(typeof(SplitButton), new FrameworkPropertyMetadata(typeof(SplitButton)));
        }

        /// <summary>
        /// Occurs on template application.
        /// </summary>
        public override void OnApplyTemplate()
        {
            base.OnApplyTemplate();

            _toggle = Template.FindName("PART_ToggleButton", this) as ToggleButton;
            if (_toggle == null)
                throw new Exception(nameof(SplitButton) + " needs a toggle button named PART_ToggleButton in its template.");
            
            _toggle.Checked += (_, unused) => SetCurrentValue(IsOpenProperty, true);
            _toggle.Unchecked += (_, unused) => SetCurrentValue(IsOpenProperty, false);
            
            _popup = Template.FindName("PART_Popup", this) as Popup;
            if (_popup == null)
                throw new Exception(nameof(SplitButton) + " needs a popup named PART_Popup in its template.");
            
            _popup.AddHandler(ButtonBase.ClickEvent, new RoutedEventHandler(InnerButton_Clicked));
        }
        
        private void InnerButton_Clicked(object sender, RoutedEventArgs e)
        {
            if (!CloseIfAnyButtonInPopupIsClicked)
                return;
            
            if (sender == null || Equals(sender, this))
                return;

            var children = this.FindAllChildrenByType(sender.GetType());
            if (!children.Contains(sender))
                return;

            if (_closePopupWithDelayTimer == null)
                _closePopupWithDelayTimer = new DispatcherTimer(TimeSpan.FromMilliseconds(350), DispatcherPriority.Background, ClosePopup, Dispatcher.CurrentDispatcher);
            _closePopupWithDelayTimer.Start();
            
            e.Handled = true;
        }

        private void ClosePopup(object sender, EventArgs args)
        {
            _closePopupWithDelayTimer.Stop();
            _popup.IsOpen = false;
        }
        
        /// <summary>
        /// Occurs on button click.
        /// </summary>
        protected override void OnClick()
        {
            if(_commandIsDisabled)
                _commandIsDisabled = false;
            else
                base.OnClick();
        }
        
        /// <summary>
        /// Called whenever the control is clicked.
        /// </summary>
        /// <param name="e">Information about the click event.</param>
        protected override void OnPreviewMouseDown(MouseButtonEventArgs e)
        {
            base.OnPreviewMouseDown(e);

            if(IsOpen)
                _commandIsDisabled = true;  // disable commands on the click processing if combobox is opened.
            else
            {
                var position = e.GetPosition(_toggle);
                if (position.X >= 0 && position.Y >= -1 && position.X < _toggle.ActualWidth + 1 && position.Y < _toggle.ActualHeight + 1)
                    _commandIsDisabled = true;
            }
        }

        /// <summary>
        /// Called whenever the control is touched.
        /// </summary>
        /// <param name="e">Information about the touch event.</param>
        protected override void OnPreviewTouchDown(TouchEventArgs e)
        {
            base.OnPreviewTouchDown(e);

            if (IsOpen)
                _commandIsDisabled = true;  // disable commands on the click processing if combobox is opened.
            else
            {
                var position = e.GetTouchPoint(_toggle).Position;
                if (position.X >= 0 && position.Y >= -1 && position.X < _toggle.ActualWidth + 1 && position.Y < _toggle.ActualHeight + 1)
                    _commandIsDisabled = true;
            }
        }
        
        /// <summary>
        /// Gets or sets a value indicating if popup is open.
        /// </summary>
        public bool IsOpen
        {
            get => (bool)GetValue(IsOpenProperty);
            set => SetCurrentValue(IsOpenProperty, value);
        }
        /// <summary>
        /// Registers <see cref="IsOpen"/> as a dependency property.
        /// </summary>
        public static readonly DependencyProperty IsOpenProperty =
            DependencyProperty.Register(nameof(IsOpen), typeof(bool), typeof(SplitButton), new PropertyMetadata(default(bool)));
        
        /// <summary>
        /// Gets or sets the content to display in the popup.
        /// </summary>
        public object PopupContent
        {
            get => GetValue(PopupContentProperty);
            set => SetValue(PopupContentProperty, value);
        }
        /// <summary>
        /// Registers <see cref="PopupContent"/> as a dependency property.
        /// </summary>
        public static readonly DependencyProperty PopupContentProperty = 
            DependencyProperty.Register(nameof(PopupContent), typeof(object), typeof(SplitButton), new PropertyMetadata(default(object)));

        /// <summary>
        /// Gets or sets the template of the content to display in the popup.
        /// </summary>
        public DataTemplate PopupContentTemplate
        {
            get => (DataTemplate)GetValue(PopupContentTemplateProperty);
            set => SetValue(PopupContentTemplateProperty, value);
        }
        /// <summary>
        /// Registers <see cref="PopupContentTemplate"/> as a dependency property.
        /// </summary>
        public static readonly DependencyProperty PopupContentTemplateProperty = 
            DependencyProperty.Register(nameof(PopupContentTemplate), typeof(DataTemplate), typeof(SplitButton), new PropertyMetadata(default(DataTemplate)));
        
        /// <summary>
        /// Gets or sets the template selector of the content to display in the popup.
        /// </summary>
        public DataTemplateSelector PopupContentTemplateSelector
        {
            get => (DataTemplateSelector)GetValue(PopupContentTemplateSelectorProperty);
            set => SetValue(PopupContentTemplateSelectorProperty, value);
        }
        /// <summary>
        /// Registers <see cref="PopupContentTemplateSelector"/> as a dependency property.
        /// </summary>
        public static readonly DependencyProperty PopupContentTemplateSelectorProperty = 
            DependencyProperty.Register(nameof(PopupContentTemplateSelector), typeof(DataTemplateSelector), typeof(SplitButton), new PropertyMetadata(default(DataTemplate)));
        
        /// <summary>
        /// The maximum height of the popup.
        /// </summary>
        [Bindable(true), Category("Layout")]
        [TypeConverter(typeof(LengthConverter))]
        public double MaxDropDownHeight
        {
            get => (double)GetValue(MaxDropDownHeightProperty);
            set => SetValue(MaxDropDownHeightProperty, value);
        }
        /// <summary>
        ///  Registers <see cref="MaxDropDownHeight"/> as a dependency property.
        /// </summary>
        public static readonly DependencyProperty MaxDropDownHeightProperty
            = DependencyProperty.Register(nameof(MaxDropDownHeight), typeof(double), typeof(SplitButton),
                                          new FrameworkPropertyMetadata(SystemParameters.PrimaryScreenHeight / 3));

        /// <summary>
        /// Gets or sets the style of the toggle button that opens the popup.
        /// </summary>
        public Style ToggleStyle
        {
            get => (Style)GetValue(ToggleStyleProperty);
            set => SetValue(ToggleStyleProperty, value);
        }
        /// <summary>
        /// Registers <see cref="ToggleStyle"/> as a dependency property.
        /// </summary>
        public static readonly DependencyProperty ToggleStyleProperty =
            DependencyProperty.Register(nameof(ToggleStyle), typeof(Style), typeof(SplitButton), new PropertyMetadata(default(Style)));
        
        /// <summary>
        /// Gets or sets a value indicating if the popup should be closed
        /// when one of its inner button is clicked.
        /// </summary>
        public bool CloseIfAnyButtonInPopupIsClicked
        {
            get => (bool)GetValue(CloseIfAnyButtonInPopupIsClickedProperty);
            set => SetValue(CloseIfAnyButtonInPopupIsClickedProperty, value);
        }
        /// <summary>
        /// Registers <see cref="CloseIfAnyButtonInPopupIsClicked"/> as a dependency property.
        /// </summary>
        public static readonly DependencyProperty CloseIfAnyButtonInPopupIsClickedProperty =
            DependencyProperty.Register(nameof(CloseIfAnyButtonInPopupIsClicked), typeof(bool), typeof(SplitButton), new PropertyMetadata(true));
    }
}
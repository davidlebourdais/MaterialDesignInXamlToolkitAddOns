using System;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls.Primitives;
using System.Windows.Input;

namespace MaterialDesignThemes.Wpf.AddOns
{
    /// <summary>
    /// A button that can display an additional popup when clicking on its toggle button.
    /// </summary>
    public class SplitButton : ButtonBase
    {
        ToggleButton toggle;
        const string togglebutton_name = "PART_ToggleButton";
        const string popup_name = "PART_Popup";

        private bool command_is_disabled = false;  // used to disable command while inner popup is opened.

        static SplitButton()
        {
            DefaultStyleKeyProperty.OverrideMetadata(typeof(SplitButton), new FrameworkPropertyMetadata(typeof(SplitButton)));
        }

        public override void OnApplyTemplate()
        {
            base.OnApplyTemplate();

            var temp = this.Template.FindName(togglebutton_name, this);
            if (temp == null || (toggle = temp as ToggleButton) == null)
                throw new Exception(nameof(SplitButton) + " needs a toggle button named \"" + togglebutton_name + "\" in its template.");
            else
            {
                toggle.Checked += (_, unused) => SetCurrentValue(SplitButtonOpenedProperty, true);;
                toggle.Unchecked += (_, unused) => SetCurrentValue(SplitButtonOpenedProperty, false);
            }

            var popup = this.Template.FindName(popup_name, this);
            if (popup == null || !(popup is Popup))
                throw new Exception(nameof(SplitButton) + " needs a popup named \"" + popup_name + "\" in its template.");
        }
        
        /// <summary>
        /// Gets or sets a value indicating if user triggered the inner toggle button to see the options in the popup.
        /// </summary>
        public bool SplitButtonOpened
        {
            get => (bool)GetValue(SplitButtonOpenedProperty);
            set => SetCurrentValue(SplitButtonOpenedProperty, value);
        }
        public static readonly DependencyProperty SplitButtonOpenedProperty =
            DependencyProperty.Register(nameof(SplitButtonOpened), typeof(bool), typeof(SplitButton), new PropertyMetadata(default(bool)));


        protected override void OnClick()
        {
            if(command_is_disabled)
                command_is_disabled = false;
            else
                base.OnClick();
        }

        protected override void OnPreviewTouchDown(TouchEventArgs e)
        {
            base.OnPreviewTouchDown(e);

            if (SplitButtonOpened)
            {
                command_is_disabled = true;  // disable commands on the click processing if combobox is opened.
            }
            else
            {
                var position = e.GetTouchPoint(toggle).Position;
                if (position.X >= 0 && position.Y >= -1 && position.X < toggle.ActualWidth + 1 && position.Y < toggle.ActualHeight + 1)
                    command_is_disabled = true;
            }
        }

        protected override void OnPreviewMouseDown(MouseButtonEventArgs e)
        {
            base.OnPreviewMouseDown(e);

            if(SplitButtonOpened)
            {
                command_is_disabled = true;  // disable commands on the click processing if combobox is opened.
            }
            else
            {
                var position = e.GetPosition(toggle);
                if (position.X >= 0 && position.Y >= -1 && position.X < toggle.ActualWidth + 1 && position.Y < toggle.ActualHeight + 1)
                    command_is_disabled = true;
            }
        }

        /// <summary>
        ///  DependencyProperty for MaxDropDownHeight
        /// </summary>
        public static readonly DependencyProperty MaxDropDownHeightProperty
            = DependencyProperty.Register("MaxDropDownHeight", typeof(double), typeof(SplitButton),
                                          new FrameworkPropertyMetadata(SystemParameters.PrimaryScreenHeight / 3));

        /// <summary>
        /// The maximum height of the popup
        /// </summary>
        [Bindable(true), Category("Layout")]
        [TypeConverter(typeof(LengthConverter))]
        public double MaxDropDownHeight
        {
            get
            {
                return (double)GetValue(MaxDropDownHeightProperty);
            }
            set
            {
                SetValue(MaxDropDownHeightProperty, value);
            }
        }

        /// <summary>
        /// Content to display in the popup extension.
        /// </summary>
        public object SplitButtonPopupContent
        {
            get => GetValue(SplitButtonPopupContentProperty);
            set => SetValue(SplitButtonPopupContentProperty, value);
        }
        public static readonly DependencyProperty SplitButtonPopupContentProperty = DependencyProperty.Register(
            nameof(SplitButtonPopupContent), typeof(object), typeof(SplitButton), new PropertyMetadata(default(object)));

        /// <summary>
        /// Popup extension content template.
        /// </summary>
        public DataTemplate SplitButtonPopupContentTemplate
        {
            get => (DataTemplate)GetValue(SplitButtonPopupContentTemplateProperty);
            set => SetValue(SplitButtonPopupContentTemplateProperty, value);
        }
        public static readonly DependencyProperty SplitButtonPopupContentTemplateProperty = DependencyProperty.Register(
            nameof(SplitButtonPopupContentTemplate), typeof(DataTemplate), typeof(SplitButton), new PropertyMetadata(default(DataTemplate)));

        /// <summary>
        /// Gets or sets the style of the toggle button that opens the popup.
        /// </summary>
        public Style SplitButtonInnerToggleStyle
        {
            get => (Style)GetValue(SplitButtonInnerToggleStyleProperty);
            set => SetValue(SplitButtonInnerToggleStyleProperty, value);
        }
        public static readonly DependencyProperty SplitButtonInnerToggleStyleProperty =
            DependencyProperty.Register(nameof(SplitButtonInnerToggleStyle), typeof(Style), typeof(SplitButton), new PropertyMetadata(default(Style)));
    }
}
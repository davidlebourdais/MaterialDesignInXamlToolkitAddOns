using System;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using MaterialDesignThemes.Wpf.AddOns.Extensions;

namespace MaterialDesignThemes.Wpf.AddOns
{
    /// <summary>
    /// A button that can display a popup when clicking on its inner toggle button.
    /// </summary>
    [TemplatePart(Name = "PART_Popup", Type = typeof(Popup))]
    public class SplitToggleButton : ToggleButtonWithIcon
    {
        private Popup _popup;
        
        static SplitToggleButton()
        {
            DefaultStyleKeyProperty.OverrideMetadata(typeof(SplitToggleButton), new FrameworkPropertyMetadata(typeof(SplitToggleButton)));
        }

        /// <summary>
        /// Creates a new instance of <see cref="SplitToggleButton"/>.
        /// </summary>
        public SplitToggleButton()
        {
            Checked += (_, unused) =>
            {
                if (_popup == null)
                    return;

                _popup.IsOpen = true;
            };
            Unchecked += (_, unused) =>
            {
                if (_popup == null)
                    return;

                _popup.IsOpen = false;
            };
        }

        /// <summary>
        /// Occurs on template application.
        /// </summary>
        public override void OnApplyTemplate()
        {
            base.OnApplyTemplate();
            
            _popup = Template.FindName("PART_Popup", this) as Popup;
            if (_popup == null)
                throw new Exception(nameof(SplitToggleButton) + " needs a popup named PART_Popup in its template.");
            
            _popup.CloseOnInnerButtonClicks(() => ShouldCloseOnPopupButtonsClicks);
            _popup.Closed += (_, unused) => IsChecked = false;
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
            DependencyProperty.Register(nameof(IsOpen), typeof(bool), typeof(SplitToggleButton), new PropertyMetadata(default(bool)));
        
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
            DependencyProperty.Register(nameof(PopupContent), typeof(object), typeof(SplitToggleButton), new PropertyMetadata(default(object)));

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
            DependencyProperty.Register(nameof(PopupContentTemplate), typeof(DataTemplate), typeof(SplitToggleButton), new PropertyMetadata(default(DataTemplate)));
        
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
            DependencyProperty.Register(nameof(PopupContentTemplateSelector), typeof(DataTemplateSelector), typeof(SplitToggleButton), new PropertyMetadata(default(DataTemplate)));
        
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
            = DependencyProperty.Register(nameof(MaxDropDownHeight), typeof(double), typeof(SplitToggleButton),
                                          new FrameworkPropertyMetadata(SystemParameters.PrimaryScreenHeight / 3));
        
        /// <summary>
        /// Gets or sets a value indicating if the popup should be closed
        /// when one of its inner buttons is clicked.
        /// </summary>
        public bool ShouldCloseOnPopupButtonsClicks
        {
            get => (bool)GetValue(ShouldCloseOnPopupButtonsClicksProperty);
            set => SetValue(ShouldCloseOnPopupButtonsClicksProperty, value);
        }
        /// <summary>
        /// Registers <see cref="ShouldCloseOnPopupButtonsClicks"/> as a dependency property.
        /// </summary>
        public static readonly DependencyProperty ShouldCloseOnPopupButtonsClicksProperty =
            DependencyProperty.Register(nameof(ShouldCloseOnPopupButtonsClicks), typeof(bool), typeof(SplitToggleButton), new PropertyMetadata(true));
        
        /// <summary>
        /// Gets or sets the kind of the icon to be displayed on the right of the button.
        /// </summary>
        public PackIconKind? TrailingIconKind
        {
            get => (PackIconKind?)GetValue(TrailingIconKindProperty);
            set => SetCurrentValue(TrailingIconKindProperty, value);
        }
        /// <summary>
        /// Registers the <see cref="TrailingIconKind"/> as a dependency property.
        /// </summary>
        public static readonly DependencyProperty TrailingIconKindProperty =
            DependencyProperty.Register(nameof(TrailingIconKind), typeof(PackIconKind?), typeof(SplitToggleButton), new FrameworkPropertyMetadata(default(PackIconKind?)));
        
        /// <summary>
        /// Gets or sets the margin of the trailing icon.
        /// </summary>
        public Thickness TrailingIconMargin
        {
            get => (Thickness)GetValue(TrailingIconMarginProperty);
            set => SetCurrentValue(TrailingIconMarginProperty, value);
        }
        /// <summary>
        /// Registers the <see cref="TrailingIconMargin"/> as a dependency property.
        /// </summary>
        public static readonly DependencyProperty TrailingIconMarginProperty =
            DependencyProperty.Register(nameof(TrailingIconMargin), typeof(Thickness), typeof(SplitToggleButton), new FrameworkPropertyMetadata(new Thickness(0)));
        
        /// <summary>
        /// Gets or sets the size of the trailing icon.
        /// </summary>
        public double TrailingIconSize
        {
            get => (double)GetValue(TrailingIconSizeProperty);
            set => SetCurrentValue(TrailingIconSizeProperty, value);
        }
        /// <summary>
        /// Registers the <see cref="TrailingIconSize"/> as a dependency property.
        /// </summary>
        public static readonly DependencyProperty TrailingIconSizeProperty =
            DependencyProperty.Register(nameof(TrailingIconSize), typeof(double), typeof(ToggleButtonWithIcon), new FrameworkPropertyMetadata(20d));
    }
}
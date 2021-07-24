using System.Windows;
using System.Windows.Controls;

namespace MaterialDesignThemes.Wpf.AddOns
{
    /// <summary>
    /// An extension of the checkbox control.
    /// </summary>
    public class ThreeStateCheckBox : CheckBox
    {
        /// <summary>
        /// Gets or sets a value that allows three state order inversion
        /// when switching state.
        /// </summary>
        public bool IsThreeStateOrderInverted
        {
            get => (bool)GetValue(IsThreeStateOrderInvertedProperty);
            set => SetCurrentValue(IsThreeStateOrderInvertedProperty, value);
        }
        /// <summary>
        /// Registers <see cref="IsThreeStateOrderInverted"/> as a dependency property.
        /// </summary>
        public static readonly DependencyProperty IsThreeStateOrderInvertedProperty
            = DependencyProperty.Register(nameof(IsThreeStateOrderInverted), typeof(bool), typeof(ThreeStateCheckBox), new FrameworkPropertyMetadata(true));

        /// <summary>
        /// Override of toggle to perform custom actions.
        /// </summary>
        protected override void OnToggle()
        {
            // Invert order of three state sequence if required:
            if (IsThreeState && IsThreeStateOrderInverted)
            {
                switch (IsChecked)
                {
                    case true:
                        IsChecked = false;
                        break;
                    case false:
                        IsChecked = null;
                        break;
                    default:
                        IsChecked = true;
                        break;
                }
            }
            else
                base.OnToggle();
        }
    }
}

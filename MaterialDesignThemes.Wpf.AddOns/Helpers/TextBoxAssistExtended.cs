using System.Windows;
using System.Windows.Controls;

namespace MaterialDesignThemes.Wpf.AddOns.Helpers
{   
    /// <summary>
    /// Stores helpers <see cref="TextBox"/> elements.
    /// </summary>
    public static class TextBoxAssistExtended
    {
        #region Override for Material Design textbox border padding (that is not customizable otherwise)
        /// <summary>
        /// Registers an attached property to allow overriding of the inner TextBox border, which is 
        /// not accessible in the current definition of the TextBox style.
        /// </summary>
        public static readonly DependencyProperty OverrideBorderPaddingProperty 
            = DependencyProperty.RegisterAttached("OverrideBorderPadding", typeof(Thickness), typeof(TextBoxAssistExtended), new PropertyMetadata(new Thickness(-1), OnOverrideBorderPadding));

        /// <summary>
        /// Gets the value of the <see cref="OverrideBorderPaddingProperty"/> dependency property.
        /// </summary>
        /// <param name="obj">The object that the dependency property is attached to.</param>
        /// <returns>Returns the border padding.</returns>
        [AttachedPropertyBrowsableForType(typeof(TextBox))]
        public static Thickness GetOverrideBorderPadding(DependencyObject obj)
        {
            return (Thickness)obj.GetValue(OverrideBorderPaddingProperty);
        }

        /// <summary>
        /// Sets the value of the <see cref="OverrideBorderPaddingProperty"/> dependency property.
        /// </summary>
        /// <param name="obj">The object that the dependency property is attached to.</param>
        /// <param name="value">The new value to be set.</param>
        [AttachedPropertyBrowsableForType(typeof(TextBox))]
        public static void SetOverrideBorderPadding(DependencyObject obj, Thickness value)
        {
            obj.SetValue(OverrideBorderPaddingProperty, value);
        }

        /// <summary>
        /// Called whenever the <see cref="OverrideBorderPaddingProperty"/> attached property is set.
        /// </summary>
        /// <param name="d">The dependency object on which the property is attached to.</param>
        /// <param name="e">Property change information.</param>
        private static void OnOverrideBorderPadding(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (!(d is TextBox textBox))
                return;
            
            // If not loaded, report processing when loaded,
            // otherwise, process immediately:
            if (textBox.IsLoaded)
            {
                if (!OverrideTemplateBorderPadding(textBox))
                {
                    // If template not ready, report processing when size changes:
                    textBox.SizeChanged += TextBox_SizeChanged;
                }
            }
            else
                textBox.Loaded += TextBox_Loaded;
        }

        /// <summary>
        /// Called whenever the TextBox is loaded.
        /// </summary>
        /// <param name="sender">The element that loaded.</param>
        /// <param name="e">Information about the event.</param>
        private static void TextBox_Loaded(object sender, RoutedEventArgs e)
        {
            if (!(sender is TextBox textBox))
                return;
            
            textBox.Loaded -= TextBox_Loaded;
            if (!OverrideTemplateBorderPadding(textBox))
            {
                // If template not ready, report processing when size changes:
                textBox.SizeChanged += TextBox_SizeChanged;
            }
        }

        /// <summary>
        /// Called whenever the TextBox size changed.
        /// </summary>
        /// <param name="sender">The element for which size changed.</param>
        /// <param name="e">Information about the event.</param>
        private static void TextBox_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            if (!(sender is TextBox textBox))
                return;
            
            textBox.SizeChanged -= TextBox_SizeChanged;
            OverrideTemplateBorderPadding(textBox);
        }

        /// <summary>
        /// Effectively overrides the TextBox border padding value.
        /// </summary>
        /// <param name="textBox">The TextBox that must see its inner border overriden.</param>
        /// <returns>True if was able to override the padding property of the inner border.</returns>
        private static bool OverrideTemplateBorderPadding(Control textBox)
        {
            var raw = textBox?.Template.FindName("border", textBox);  // invoke as named in MaterialDesignTheme.TextBox.xaml
            if (!(raw is Border border))
                return false;
            
            var newRawPadding = textBox.GetValue(OverrideBorderPaddingProperty);
            
            if (!(newRawPadding is Thickness newPadding))
                return false;
            
            border.Padding = newPadding;
            return true;
        }
        #endregion
    }
}
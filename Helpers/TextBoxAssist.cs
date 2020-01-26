using System.Windows;
using System.Windows.Controls;

namespace EMA.MaterialDesignInXAMLExtender
{   
    /// <summary>
    /// Stores helpers <see cref="TextBox"/> elements.
    /// </summary>
    public static class TextBoxAssist
    {
        #region Override for Material Design textbox border padding (that is not customizable otherwise)
        /// <summary>
        /// Registers an attached property to allow overriding of the inner textbox border, which is 
        /// not accessible in the current definition of the textbox style.
        /// </summary>
        public static readonly DependencyProperty OverrideBorderPaddingProperty 
            = DependencyProperty.RegisterAttached("OverrideBorderPadding", typeof(Thickness), typeof(TextBoxAssist), new PropertyMetadata(new Thickness(-1), OnOverrideBorderPadding));

        /// <summary>
        /// Gets the value of the <see cref="OverrideBorderPaddingProperty"/> dependency property.
        /// </summary>
        /// <param name="obj">The object that the dependency property is attached to.</param>
        /// <returns>Returns true if scrollviewer must scroll to end automaticaly, false otherwise.</returns>
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
            if (d is TextBox textbox)
            {
                // If not loaded, report processing when loaded,
                // otherwise, process immediately:
                if (textbox.IsLoaded)
                {
                    if (!overrideTemplateBorderPadding(textbox))
                    {
                        // If template not ready, report processing when size changes:
                        textbox.SizeChanged += Textbox_SizeChanged;
                    }
                }
                else
                    textbox.Loaded += Textbox_Loaded;
            }
        }

        /// <summary>
        /// Called whenevener the textbox is loaded.
        /// </summary>
        /// <param name="sender">The element that loaded.</param>
        /// <param name="e">Information about the event.</param>
        private static void Textbox_Loaded(object sender, RoutedEventArgs e)
        {
            if (sender is TextBox textbox)
            {
                textbox.Loaded -= Textbox_Loaded;
                if (!overrideTemplateBorderPadding(textbox))
                {
                    // If template not ready, report processing when size changes:
                    textbox.SizeChanged += Textbox_SizeChanged;
                }
            }
        }

        /// <summary>
        /// Called whenever the textbox size changed.
        /// </summary>
        /// <param name="sender">The element for which size changed.</param>
        /// <param name="e">Information about the event.</param>
        private static void Textbox_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            if (sender is TextBox textbox)
            {
                textbox.SizeChanged -= Textbox_SizeChanged;
                overrideTemplateBorderPadding(textbox);
            }
        }

        /// <summary>
        /// Effectively overrides the textbox border padding value.
        /// </summary>
        /// <param name="textbox">The textbox that must see its inner border overriden.</param>
        /// <returns>True if was able to override the padding property of the inner border.</returns>
        private static bool overrideTemplateBorderPadding(TextBox textbox)
        {
            if (textbox == null) return false;

            var raw = textbox.Template.FindName("border", textbox);  // invoke as nammed in MaterialDesignTheme.TextBox.xaml
            if (raw is Border border)
            {
                var newRawPadding = textbox.GetValue(OverrideBorderPaddingProperty);
                if (newRawPadding is Thickness newPadding)
                {
                    border.Padding = newPadding;
                    return true;
                }
            }
            return false;
        }
        #endregion
    }
}
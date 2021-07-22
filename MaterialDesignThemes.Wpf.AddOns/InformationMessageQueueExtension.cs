using System;
using System.Windows.Markup;

namespace MaterialDesignThemes.Wpf.AddOns
{
    /// <summary>
    /// Provides shorthand to initialise a new <see cref="InformationMessageQueue"/>.
    /// </summary>
    [MarkupExtensionReturnType(typeof(InformationMessageQueue))]
    public class InformationMessageQueueExtension : MarkupExtension
    {
        /// <summary>
        /// Provides a new <see cref="InformationMessageQueue"/>.
        /// </summary>
        /// <param name="serviceProvider">Unused.</param>
        /// <returns>A new instance of <see cref="InformationMessageQueue"/>.</returns>
        public override object ProvideValue(IServiceProvider serviceProvider)
        {
            return new InformationMessageQueue();
        }
    }
}
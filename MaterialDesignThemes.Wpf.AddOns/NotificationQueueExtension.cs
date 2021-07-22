using System;
using System.Windows.Markup;

namespace MaterialDesignThemes.Wpf.AddOns
{
    /// <summary>
    /// Provides shorthand to initialise a new <see cref="NotificationQueue"/>.
    /// </summary>
    [MarkupExtensionReturnType(typeof(NotificationQueue))]
    public class NotificationQueueExtension : MarkupExtension
    {
        /// <summary>
        /// Provides a new <see cref="NotificationQueue"/>.
        /// </summary>
        /// <param name="serviceProvider">Unused.</param>
        /// <returns>A new instance of <see cref="NotificationQueue"/>.</returns>
        public override object ProvideValue(IServiceProvider serviceProvider)
        {
            return new NotificationQueue();
        }
    }
}
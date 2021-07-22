using System;
using static MaterialDesignThemes.Wpf.AddOns.Notification;

namespace MaterialDesignThemes.Wpf.AddOns
{
    /// <summary>
    /// A <see cref="InformationMessageQueue"/> that holds a <see cref="MessageType"/> property type.
    /// </summary>
    internal class NotificationQueueItem : InformationMessageQueueItem
    {
        public NotificationQueueItem(object content, MessageType type, TimeSpan duration, object actionContent = null, Action<object> actionHandler = null, object actionArgument = null,
            bool isPromoted = false, bool ignoreDuplicate = false) : base (content, duration, actionContent, actionHandler, actionArgument, isPromoted, ignoreDuplicate)
        {
            NotificationType = type;
        }

        public NotificationQueueItem(object content, MessageType type, TimeSpan duration, object actionContent = null, Action<object> actionHandler = null, object actionArgument = null,
            object secondActionContent = null, Action<object> secondActionHandler = null, object secondActionArgument = null, bool isPromoted = false, bool ignoreDuplicate = false) 
            : base (content, duration, actionContent, actionHandler, actionArgument, secondActionContent, secondActionHandler, secondActionArgument, isPromoted, ignoreDuplicate)
        {
            NotificationType = type;
        }

        /// <summary>
        /// Gets or sets the type of the notification message.
        /// </summary>
        public MessageType NotificationType { get; }
    }
}
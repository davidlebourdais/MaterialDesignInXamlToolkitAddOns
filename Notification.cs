using System.Windows;
using System.Windows.Controls.Primitives;

namespace EMA.MaterialDesignInXAMLExtender
{
    /// <summary>
    /// This control are messages to be included in a <see cref="ToastNotificationBar"/>.
    /// </summary>
    [TemplatePart(Name = ActionButtonPartName, Type = typeof(ButtonBase))]
    [TemplatePart(Name = SecondaryActionButtonPartName, Type = typeof(ButtonBase))]
    public class Notification : InformationMessage
    {
        /// <summary>
        /// Enumerates the different message types for the notification.
        /// </summary>
        public enum MessageType
        {
            /// <summary>
            /// No type.
            /// </summary>
            Undefined,
            /// <summary>
            /// Simple information to be reported.
            /// </summary>
            Information,
            /// <summary>
            /// Message contains a warning.
            /// </summary>
            Warning,
            /// <summary>
            /// Message contains an alert or error.
            /// </summary>
            Alert
        }

        /// <summary>
        /// Static constructor for <see cref="Notification"/>.
        /// </summary>
        static Notification()
        {
            DefaultStyleKeyProperty.OverrideMetadata(typeof(Notification), new FrameworkPropertyMetadata(typeof(Notification)));
        }

        /// <summary>
        /// Called at template loading. 
        /// </summary>
        public override void OnApplyTemplate()
        {
            base.OnApplyTemplate();
        }

        #region Dependency properties
        /// <summary>
        /// Gets or sets the command associated to the secondary button.
        /// </summary>
        public MessageType NotificationType
        {
            get { return (MessageType)GetValue(NotificationTypeProperty); }
            set { SetValue(NotificationTypeProperty, value); }
        }
        /// <summary>
        /// Registers <see cref="NotificationType"/> as a dependency property.
        /// </summary>
        public static readonly DependencyProperty NotificationTypeProperty = DependencyProperty.Register(
            nameof(NotificationType), typeof(MessageType), typeof(Notification), new PropertyMetadata(default(MessageType), NotificationTypeChangedCallback));

        /// <summary>
        /// Occurs whenever the <see cref="NotificationType"/> property changes.
        /// </summary>
        /// <param name="sender">Event triggerer.</param>
        /// <param name="args">Event info.</param>
        private static void NotificationTypeChangedCallback(DependencyObject sender, DependencyPropertyChangedEventArgs args)
        {
            if (sender is Notification notification)
                notification.RaiseEvent(new RoutedEventArgs(NotificationTypeChangedEvent, sender));
        }
        #endregion

        #region Routed events
        /// <summary>
        /// Adds or removes the notification type changed event handler.
        /// </summary>
        public event RoutedEventHandler NotificationTypeChanged { add { AddHandler(NotificationTypeChangedEvent, value); } remove { RemoveHandler(NotificationTypeChangedEvent, value); } }

        /// <summary>
        /// Registers <see cref="NotificationTypeChanged"/> as a routed event.
        /// </summary>
        public static readonly RoutedEvent NotificationTypeChangedEvent = EventManager.RegisterRoutedEvent(
            nameof(NotificationTypeChanged), RoutingStrategy.Bubble, typeof(RoutedEventHandler), typeof(Notification));
        #endregion of routed events
    }
}
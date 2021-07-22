using System;
using System.Windows;
using static MaterialDesignThemes.Wpf.AddOns.Notification;

namespace MaterialDesignThemes.Wpf.AddOns
{
    /// <summary>
    /// A toast message that can appear anywhere and can be dismissed.
    /// </summary>
    /// <remarks>Overrides MD's snackbar, which has the similar behavior.</remarks>
    public class ToastNotificationBar : Snackbar
    {
        #region Private attributes and properties
        private Action _messageQueueRegistrationCleanUp;  // used for base attribute override.
        #endregion

        #region Constructors
        /// <summary>
        /// Initiates a new instance of <see cref="ToastNotificationBar"/>.
        /// </summary>
        public ToastNotificationBar()
        {
            if (DismissOnActionButtonClick) // auto close bar by default when any button is clicked
                RegisterDismissOnClickEvents();
        }
        
        /// <summary>
        /// Static constructor for <see cref="ToastNotificationBar"/> type.
        /// </summary>
        static ToastNotificationBar()
        {
            DefaultStyleKeyProperty.OverrideMetadata(typeof(ToastNotificationBar), new FrameworkPropertyMetadata(typeof(ToastNotificationBar)));
            Snackbar.MessageProperty.OverrideMetadata(typeof(ToastNotificationBar), new PropertyMetadata(default(SnackbarMessage), InnerMessageChangedCallback));
        }

        /// <summary>
        /// Occurs whenever the <see cref="Snackbar.Message"/> property changes.
        /// </summary>
        /// <param name="sender">Object that triggered the event.</param>
        /// <param name="args">Event info.</param>
        private static void InnerMessageChangedCallback(DependencyObject sender, DependencyPropertyChangedEventArgs args)
        {
            // Propagate base message changes:
            if (sender is ToastNotificationBar toast && args.NewValue is Notification notification)
                toast.SetCurrentValue(ToastNotificationBar.MessageProperty, notification);
        }
        #endregion

        #region Protected methods
        /// <summary>
        /// Called when the ToastNotification must be closed.
        /// </summary>
        private static void Close(object sender, RoutedEventArgs e)
        {
            if (sender is ToastNotificationBar toastNotification)
                toastNotification.SetCurrentValue(IsActiveProperty, false);
        }
        #endregion

        #region Dependency properties
        /// <summary>
        /// MessageQueue property override to match our own message queue <see cref="NotificationQueue"/> type.
        /// </summary>
        public MessageType CurrentNotificationType
        {
            get => (MessageType)GetValue(CurrentNotificationTypeProperty);
            internal set => SetValue(CurrentNotificationTypeProperty, value);
        }

        /// <summary>
        /// Registers <see cref="MessageQueue"/> as a dependency property.
        /// </summary>
        public static readonly DependencyProperty CurrentNotificationTypeProperty = DependencyProperty.Register(
            nameof(CurrentNotificationType), typeof(MessageType), typeof(ToastNotificationBar), new PropertyMetadata(default(MessageType)));

        /// <summary>
        /// Gets or sets the message.
        /// </summary>
        public new Notification Message
        {
            get => (Notification)GetValue(MessageProperty);
            set => SetValue(MessageProperty, value);
        }

        /// <summary>
        /// Registers <see cref="Message"/> as a dependency property.
        /// </summary>
        public new static readonly DependencyProperty MessageProperty = DependencyProperty.Register(
            nameof(Message), typeof(Notification), typeof(ToastNotificationBar), new PropertyMetadata(default(Notification), MessageChangedCallback));

        /// <summary>
        /// Occurs whenever the <see cref="Message"/> property changes.
        /// </summary>
        /// <param name="sender">Object that triggered the event.</param>
        /// <param name="args">Event info.</param>
        private static void MessageChangedCallback(DependencyObject sender, DependencyPropertyChangedEventArgs args)
        {
            // Update type of the bar when message changes or its type changes:
            if (!(sender is ToastNotificationBar toast) || !(args.NewValue is Notification notification))
                return;
            
            toast.CurrentNotificationType = notification.NotificationType;
            notification.AddHandler(NotificationTypeChangedEvent, new RoutedEventHandler(
                                        (send, _) =>
                                        {
                                            if (send is Notification notify)
                                                toast.CurrentNotificationType = notify.NotificationType;
                                        }
                                    ), true);
        }

        /// <summary>
        /// MessageQueue property override to match our own message queue <see cref="NotificationQueue"/> type.
        /// </summary>
        public new NotificationQueue MessageQueue
        {
            get => (NotificationQueue)GetValue(MessageQueueProperty);
            set
            {
                if (value != null && value.Dispatcher != Dispatcher)
                    throw new InvalidOperationException("Objects must be created by the same thread.");
                SetValue(MessageQueueProperty, value);
            }
        }
        /// <summary>
        /// Registers <see cref="MessageQueue"/> as a dependency property.
        /// </summary>
        public new static readonly DependencyProperty MessageQueueProperty = DependencyProperty.Register(
            nameof(MessageQueue), typeof(NotificationQueue), typeof(ToastNotificationBar), new PropertyMetadata(default(NotificationQueue), MessageQueuePropertyChangedCallback));

        /// <summary>
        /// Override of base private handler so we correctly pair to our own definition of <see cref="NotificationQueue"/>.
        /// </summary>
        /// <param name="dependencyObject">Bar that triggered the event.</param>
        /// <param name="dependencyPropertyChangedEventArgs">New property value information.</param>
        private static void MessageQueuePropertyChangedCallback(DependencyObject dependencyObject, DependencyPropertyChangedEventArgs dependencyPropertyChangedEventArgs)
        {
            var toast = (ToastNotificationBar)dependencyObject;
            toast._messageQueueRegistrationCleanUp?.Invoke();
            var messageQueue = dependencyPropertyChangedEventArgs.NewValue as NotificationQueue;
            toast._messageQueueRegistrationCleanUp = messageQueue?.Pair(toast);
        }

        /// <summary>
        /// Gets or sets the style to be applied to the secondary button.
        /// </summary>
        public Style SecondaryActionButtonStyle
        {
            get => (Style)GetValue(SecondaryActionButtonStyleProperty);
            set => SetValue(SecondaryActionButtonStyleProperty, value);
        }
        /// <summary>
        /// Registers <see cref="SecondaryActionButtonStyle"/> as a dependency property.
        /// </summary>
        public static readonly DependencyProperty SecondaryActionButtonStyleProperty = DependencyProperty.Register(
            nameof(SecondaryActionButtonStyle), typeof(Style), typeof(ToastNotificationBar), new PropertyMetadata(default(Style)));

        /// <summary>
        /// Gets or sets a value indicating if the action buttons must be inverted in template.
        /// </summary>
        public bool AreActionButtonPositionsInverted
        {
            get => (bool)GetValue(AreActionButtonPositionsInvertedProperty);
            set => SetValue(AreActionButtonPositionsInvertedProperty, value);
        }
        /// <summary>
        /// Registers <see cref="AreActionButtonPositionsInverted"/> as a dependency property.
        /// </summary>
        public static readonly DependencyProperty AreActionButtonPositionsInvertedProperty = DependencyProperty.Register(
            nameof(AreActionButtonPositionsInverted), typeof(bool), typeof(ToastNotificationBar), new PropertyMetadata(default(bool)));

        /// <summary>
        /// Gets or sets a value indicating if dismissing is active when clicking action buttons (defaults to true).
        /// </summary>
        public bool DismissOnActionButtonClick
        {
            get => (bool)GetValue(DismissOnActionButtonClickProperty);
            set => SetValue(DismissOnActionButtonClickProperty, value);
        }
        /// <summary>
        /// Registers <see cref="DismissOnActionButtonClick"/> as a dependency property.
        /// </summary>
        public static readonly DependencyProperty DismissOnActionButtonClickProperty = DependencyProperty.Register(
            nameof(DismissOnActionButtonClick), typeof(bool), typeof(ToastNotificationBar), new PropertyMetadata(true, DismissOnActionButtonClickChanged));

        /// <summary>
        /// Called whenever the <see cref="DismissOnActionButtonClick"/> property changes.
        /// </summary>
        /// <param name="sender">The object whose property changed.</param>
        /// <param name="args">Information about the property change.</param>
        private static void DismissOnActionButtonClickChanged(DependencyObject sender, DependencyPropertyChangedEventArgs args)
        {
            if (!(sender is ToastNotificationBar casted))
                return;
            
            if (args.NewValue as bool? == true)
                casted.RegisterDismissOnClickEvents();
            else
                casted.UnregisterDismissOnClickEvents();
        }

        /// <summary>
        /// Registers event handlers for message clicks management.
        /// </summary>
        private void RegisterDismissOnClickEvents()
        {
            AddHandler(SnackbarMessage.ActionClickEvent, new RoutedEventHandler(Close), true);
            AddHandler(InformationMessage.SecondaryActionClickEvent, new RoutedEventHandler(Close), true);
        }

        /// <summary>
        /// Clear event handlers for message clicks management.
        /// </summary>
        private void UnregisterDismissOnClickEvents()
        {
            RemoveHandler(SnackbarMessage.ActionClickEvent, new RoutedEventHandler(Close));
            RemoveHandler(InformationMessage.SecondaryActionClickEvent, new RoutedEventHandler(Close));
        }
        #endregion
    }
}
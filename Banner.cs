using System;
using System.Windows;
using MaterialDesignThemes.Wpf;

namespace EMA.MaterialDesignInXAMLExtender
{
    /// <summary>
    /// A banner to display messages with one or two action buttons.
    /// </summary>
    /// <remarks>Overrides MD's snackbar, which has the similar behavior.</remarks>
    public class Banner : Snackbar
    {
        #region Private attributes and properties
        private Action _messageQueueRegistrationCleanUp;  // used for base attribute override.
        #endregion

        #region Constructors
        /// <summary>
        /// Creates a new instance of <see cref="Banner"/>.
        /// </summary>
        public Banner()
        {
            if (DismissOnActionButtonClick) // auto close banner when button is click by default
                RegisterDismissOnClickEvents();
        }

        /// <summary>
        /// Static constructor for <see cref="Banner"/> type.
        /// Override some base dependency properties.
        /// </summary>
        static Banner()
        {
            DefaultStyleKeyProperty.OverrideMetadata(typeof(Banner), new FrameworkPropertyMetadata(typeof(Banner)));
        }
        #endregion

        #region Protected methods
        /// <summary>
        /// Called when the banner must be closed.
        /// </summary>
        protected static void Close(object sender, RoutedEventArgs e)
        {
            if (sender is Banner banner)
                banner.SetCurrentValue(Banner.IsActiveProperty, false);
        }
        #endregion

        #region Dependency properties
        /// <summary>
        /// MessageQueue property override to match our own message queue <see cref="BannerMessageQueue"/> type.
        /// </summary>
        public new BannerMessageQueue MessageQueue
        {
            get => (BannerMessageQueue)GetValue(MessageQueueProperty);
            set
            {
                if (!(value is null) && value.Dispatcher != Dispatcher)
                    throw new InvalidOperationException("Objects must be created by the same thread.");
                SetValue(MessageQueueProperty, value);
            }
        }
        /// <summary>
        /// Registers <see cref="MessageQueue"/> as a dependency property.
        /// </summary>
        public static new readonly DependencyProperty MessageQueueProperty = DependencyProperty.Register(
            nameof(MessageQueue), typeof(SnackbarMessageQueue), typeof(Banner), new PropertyMetadata(default(SnackbarMessageQueue), MessageQueuePropertyChangedCallback));

        /// <summary>
        /// Override of base private handler so we correctly pair to our own definition of <see cref="BannerMessageQueue"/>.
        /// </summary>
        /// <param name="dependencyObject">Snackbar that triggered the event.</param>
        /// <param name="dependencyPropertyChangedEventArgs">New property value information.</param>
        private static void MessageQueuePropertyChangedCallback(DependencyObject dependencyObject, DependencyPropertyChangedEventArgs dependencyPropertyChangedEventArgs)
        {
            var snackbar = (Banner)dependencyObject;
            snackbar._messageQueueRegistrationCleanUp?.Invoke();
            var messageQueue = dependencyPropertyChangedEventArgs.NewValue as BannerMessageQueue;
            snackbar._messageQueueRegistrationCleanUp = messageQueue?.Pair(snackbar);
        }

        /// <summary>
        /// Gets or sets the style to be applied to the secondary button.
        /// </summary>
        public Style SecondaryActionButtonStyle
        {
            get { return (Style)GetValue(SecondaryActionButtonStyleProperty); }
            set { SetValue(SecondaryActionButtonStyleProperty, value); }
        }
        /// <summary>
        /// Registers <see cref="SecondaryActionButtonStyle"/> as a dependency property.
        /// </summary>
        public static readonly DependencyProperty SecondaryActionButtonStyleProperty = DependencyProperty.Register(
            nameof(SecondaryActionButtonStyle), typeof(Style), typeof(Banner), new PropertyMetadata(default(Style)));

        /// <summary>
        /// Gets or sets a value indicating if the action buttons must be inverted in template.
        /// </summary>
        public bool AreActionButtonPositionsInverted
        {
            get { return (bool)GetValue(AreActionButtonPositionsInvertedProperty); }
            set { SetValue(AreActionButtonPositionsInvertedProperty, value); }
        }
        /// <summary>
        /// Registers <see cref="AreActionButtonPositionsInverted"/> as a dependency property.
        /// </summary>
        public static readonly DependencyProperty AreActionButtonPositionsInvertedProperty = DependencyProperty.Register(
            nameof(AreActionButtonPositionsInverted), typeof(bool), typeof(Banner), new PropertyMetadata(default(bool)));

        /// <summary>
        /// Gets or sets a value indicating if dismissing is active when clicking action buttons (defaults to true).
        /// </summary>
        public bool DismissOnActionButtonClick
        {
            get { return (bool)GetValue(DismissOnActionButtonClickProperty); }
            set { SetValue(DismissOnActionButtonClickProperty, value); }
        }
        /// <summary>
        /// Registers <see cref="DismissOnActionButtonClick"/> as a dependency property.
        /// </summary>
        public static readonly DependencyProperty DismissOnActionButtonClickProperty = DependencyProperty.Register(
            nameof(DismissOnActionButtonClick), typeof(bool), typeof(Banner), new PropertyMetadata(true, DismissOnActionButtonClickChanged));

        /// <summary>
        /// Called whenever the <see cref="DismissOnActionButtonClick"/> property changes.
        /// </summary>
        /// <param name="sender">The object whose property changed.</param>
        /// <param name="args">Information about the property change.</param>
        public static void DismissOnActionButtonClickChanged(DependencyObject sender, DependencyPropertyChangedEventArgs args)
        {
            if (sender is Banner casted)
            {
                if (args.NewValue as bool? == true)
                    casted.RegisterDismissOnClickEvents();
                else
                    casted.UnregisterDismissOnClickEvents();
            }
        }

        /// <summary>
        /// Registers event handlers for message clicks management.
        /// </summary>
        private void RegisterDismissOnClickEvents()
        {
            AddHandler(BannerMessage.ActionClickEvent, new RoutedEventHandler(Close), true);
            AddHandler(BannerMessage.SecondaryActionClickEvent, new RoutedEventHandler(Close), true);
        }

        /// <summary>
        /// Clear event handlers for message clicks management.
        /// </summary>
        private void UnregisterDismissOnClickEvents()
        {
            RemoveHandler(BannerMessage.ActionClickEvent, new RoutedEventHandler(Close));
            RemoveHandler(BannerMessage.SecondaryActionClickEvent, new RoutedEventHandler(Close));
        }
        #endregion
    }
}
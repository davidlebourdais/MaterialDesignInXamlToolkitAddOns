using System;
using static MaterialDesignThemes.Wpf.AddOns.Notification;

namespace MaterialDesignThemes.Wpf.AddOns
{
    /// <summary>
    /// Represents a message to be asynchronously displayed.
    /// </summary>
    /// <remarks>
    /// Code here is reproduced and adapted from the <see cref="SnackbarMessageQueue"/> source in MaterialDesignInXamlToolkit
    /// under the MIT license - Copyright (c) James Willock, Mulholland Software and Contributors the MaterialDesignInXaml 
    /// </remarks>
    public class NotificationQueue : InformationMessageQueue
    {
        /// <summary>
        /// Initiates a new instance of <see cref="NotificationQueue"/>.
        /// </summary>
        public NotificationQueue() : this(TimeSpan.FromSeconds(3))
        { }

        /// <summary>
        /// Initiates a new instance of <see cref="NotificationQueue"/>.
        /// </summary>
        /// <param name="messageDuration">Custom message duration.</param>
        public NotificationQueue(TimeSpan messageDuration) : base(messageDuration)
        { }

        /// <summary>
        /// Enqueues a message in the display list.
        /// </summary>
        /// <param name="content">Message to display.</param>
        /// <param name="type">Type of information to show.</param>
        public void Enqueue(object content, MessageType type) => Enqueue(content, type, false);

        /// <summary>
        /// Enqueues a message in the display list.
        /// </summary>
        /// <param name="content">Message to display.</param>
        /// <param name="type">Type of information to show.</param>
        /// <param name="neverConsiderToBeDuplicate">Never skip, even if duplicate exists in queue.</param>
        public void Enqueue(object content, MessageType type, bool neverConsiderToBeDuplicate)
            => Enqueue(content, type, null, null, null, promote: false, neverConsiderToBeDuplicate: neverConsiderToBeDuplicate);

        /// <summary>
        /// Enqueues a message in the display list.
        /// </summary>
        /// <param name="content">Message to display.</param>
        /// <param name="type">Type of information to show.</param>
        /// <param name="actionContent">Content of the action button.</param>
        /// <param name="actionHandler">Callback of the action button.</param>
        public void Enqueue(object content, MessageType type, object actionContent, Action actionHandler)
            => Enqueue(content, type, actionContent, actionHandler, promote: false);

        /// <summary>
        /// Enqueues a message in the display list.
        /// </summary>
        /// <param name="content">Message to display.</param>
        /// <param name="type">Type of information to show.</param>
        /// <param name="actionContent">Content of the action button.</param>
        /// <param name="actionHandler">Callback of the action button.</param>
        /// <param name="secondActionContent">Content of the second action button.</param>
        /// <param name="secondActionHandler">Callback of the second action button.</param>
        public void Enqueue(object content, MessageType type, object actionContent, Action actionHandler, object secondActionContent, Action secondActionHandler)
            => Enqueue(content, type, actionContent, actionHandler, secondActionContent: secondActionContent, secondActionHandler: secondActionHandler, promote: false);

        /// <summary>
        /// Enqueues a message in the display list.
        /// </summary>
        /// <param name="content">Message to display.</param>
        /// <param name="type">Type of information to show.</param>
        /// <param name="actionContent">Content of the action button.</param>
        /// <param name="actionHandler">Callback of the action button.</param>
        /// <param name="promote">Set to true if message should be prioritized.</param>
        public void Enqueue(object content, MessageType type, object actionContent, Action actionHandler, bool promote)
        {
            if (content is null) throw new ArgumentNullException(nameof(content));
            if (actionContent is null) throw new ArgumentNullException(nameof(actionContent));
            if (actionHandler is null) throw new ArgumentNullException(nameof(actionHandler));

            Enqueue(content, type, actionContent, _ => actionHandler(), actionArgument: null, promote, false);
        }

        /// <summary>
        /// Enqueues a message in the display list.
        /// </summary>
        /// <param name="content">Message to display.</param>
        /// <param name="type">Type of information to show.</param>
        /// <param name="actionContent">Content of the action button.</param>
        /// <param name="actionHandler">Callback of the action button.</param>
        /// <param name="secondActionContent">Content of the second action button.</param>
        /// <param name="secondActionHandler">Callback of the second action button.</param>
        /// <param name="promote">Set to true if message should be prioritized.</param>
        public void Enqueue(object content, MessageType type, object actionContent, Action actionHandler, object secondActionContent, Action secondActionHandler, bool promote)
        {
            if (secondActionContent is null) Enqueue(content, type, actionContent, actionHandler, promote);
            if (content is null) throw new ArgumentNullException(nameof(content));
            if (actionContent is null) throw new ArgumentNullException(nameof(actionContent));
            if (actionHandler is null) throw new ArgumentNullException(nameof(actionHandler));
            if (secondActionHandler is null) throw new ArgumentNullException(nameof(secondActionHandler));

            Enqueue(content, type, actionContent, _ => actionHandler(), actionArgument: null, secondActionContent: secondActionContent, secondActionHandler: _ => secondActionHandler(), null, promote, false);
        }

        /// <summary>
        /// Enqueues a message in the display list.
        /// </summary>
        /// <param name="content">Message to display.</param>
        /// <param name="type">Type of information to show.</param>
        /// <param name="actionContent">Content of the action button.</param>
        /// <param name="actionHandler">Callback of the action button.</param>
        /// <param name="actionArgument">Argument to be passed to the callback of the action button.</param>
        /// <typeparam name="TArgument">Type of the object passed as argument to the action button callback.</typeparam>
        public void Enqueue<TArgument>(object content, MessageType type, object actionContent, Action<TArgument> actionHandler,TArgument actionArgument)
            => Enqueue(content, type, actionContent, actionHandler, actionArgument, false, false);

        /// <summary>
        /// Enqueues a message in the display list.
        /// </summary>
        /// <param name="content">Message to display.</param>
        /// <param name="type">Type of information to show.</param>
        /// <param name="actionContent">Content of the action button.</param>
        /// <param name="actionHandler">Callback of the action button.</param>
        /// <param name="actionArgument">Argument to be passed to the callback of the action button.</param>
        /// <param name="secondActionContent">Content of the second action button.</param>
        /// <param name="secondActionHandler">Callback of the second action button.</param>
        /// <param name="secondActionArgument">Argument to be passed to the callback of the second action button.</param>
        /// <typeparam name="TArgument">Type of the object passed as argument to the action button callback.</typeparam>
        /// <typeparam name="TArgument2">Type of the object passed as argument to the second action button callback.</typeparam>
        public void Enqueue<TArgument, TArgument2>(object content, MessageType type, object actionContent, Action<TArgument> actionHandler, TArgument actionArgument,
            object secondActionContent, Action<TArgument2> secondActionHandler, TArgument2 secondActionArgument)
            => Enqueue(content, type, actionContent, actionHandler, actionArgument, secondActionContent, secondActionHandler, secondActionArgument, false, false);

        /// <summary>
        /// Enqueues a message in the display list.
        /// </summary>
        /// <param name="content">Message to display.</param>
        /// <param name="type">Type of information to show.</param>
        /// <param name="actionContent">Content of the action button.</param>
        /// <param name="actionHandler">Callback of the action button.</param>
        /// <param name="actionArgument">Argument to be passed to the callback of the action button.</param>
        /// <param name="promote">Set to true if message should be prioritized.</param>
        /// <typeparam name="TArgument">Type of the object passed as argument to the action button callback.</typeparam>
        public void Enqueue<TArgument>(object content, MessageType type, object actionContent, Action<TArgument> actionHandler,
            TArgument actionArgument, bool promote) =>
            Enqueue(content, type, actionContent, actionHandler, actionArgument, promote, promote);

        /// <summary>
        /// Enqueues a message in the display list.
        /// </summary>
        /// <param name="content">Message to display.</param>
        /// <param name="type">Type of information to show.</param>
        /// <param name="actionContent">Content of the action button.</param>
        /// <param name="actionHandler">Callback of the action button.</param>
        /// <param name="actionArgument">Argument to be passed to the callback of the action button.</param>
        /// <param name="secondActionContent">Content of the second action button.</param>
        /// <param name="secondActionHandler">Callback of the second action button.</param>
        /// <param name="secondActionArgument">Argument to be passed to the callback of the second action button.</param>
        /// <param name="promote">Set to true if message should be prioritized.</param>
        /// <typeparam name="TArgument">Type of the object passed as argument to the action button callback.</typeparam>
        /// <typeparam name="TArgument2">Type of the object passed as argument to the second action button callback.</typeparam>
        public void Enqueue<TArgument, TArgument2>(object content, MessageType type, object actionContent, Action<TArgument> actionHandler, TArgument actionArgument,
            object secondActionContent, Action<TArgument2> secondActionHandler, TArgument2 secondActionArgument, bool promote)
            => Enqueue(content, type, actionContent, actionHandler, actionArgument, secondActionContent, secondActionHandler, secondActionArgument, promote, promote);

        /// <summary>
        /// Enqueues a message in the display list.
        /// </summary>
        /// <param name="content">Message to display.</param>
        /// <param name="type">Type of information to show.</param>
        /// <param name="actionContent">Content of the action button.</param>
        /// <param name="actionHandler">Callback of the action button.</param>
        /// <param name="actionArgument">Argument to be passed to the callback of the action button.</param>
        /// <param name="promote">Set to true if message should be prioritized.</param>
        /// <param name="neverConsiderToBeDuplicate">Never skip, even if duplicate exists in queue.</param>
        /// <param name="durationOverride">Custom persistence time.</param>
        /// <typeparam name="TArgument">Type of the object passed as argument to the action button callback.</typeparam>
        public void Enqueue<TArgument>(object content, MessageType type, object actionContent, Action<TArgument> actionHandler,
            TArgument actionArgument, bool promote, bool neverConsiderToBeDuplicate, TimeSpan? durationOverride = null)
        {
            if (content is null) throw new ArgumentNullException(nameof(content));

            if (actionContent is null ^ actionHandler is null)
            {
                throw new ArgumentException("All action arguments must be provided if any are provided.",
                    actionContent != null ? nameof(actionContent) : nameof(actionHandler));
            }

            var handler = actionHandler != null
                ? new Action<object>(argument => actionHandler((TArgument)argument))
                : null;
            Enqueue(content, type, actionContent, handler, actionArgument, promote, neverConsiderToBeDuplicate);
        }

        /// <summary>
        /// Enqueues a message in the display list.
        /// </summary>
        /// <param name="content">Message to display.</param>
        /// <param name="type">Type of information to show.</param>
        /// <param name="actionContent">Content of the action button.</param>
        /// <param name="actionHandler">Callback of the action button.</param>
        /// <param name="actionArgument">Argument to be passed to the callback of the action button.</param>
        /// <param name="secondActionContent">Content of the second action button.</param>
        /// <param name="secondActionHandler">Callback of the second action button.</param>
        /// <param name="secondActionArgument">Argument to be passed to the callback of the second action button.</param>
        /// <param name="promote">Set to true if message should be prioritized.</param>
        /// <param name="neverConsiderToBeDuplicate">Never skip, even if duplicate exists in queue.</param>
        /// <param name="durationOverride">Custom persistence time.</param>
        /// <typeparam name="TArgument">Type of the object passed as argument to the action button callback.</typeparam>
        /// <typeparam name="TArgument2">Type of the object passed as argument to the second action button callback.</typeparam>
        private void Enqueue<TArgument, TArgument2>(object content, MessageType type, object actionContent, Action<TArgument> actionHandler,
                                                    TArgument actionArgument, object secondActionContent, Action<TArgument2> secondActionHandler, TArgument2 secondActionArgument,
                                                    bool promote, bool neverConsiderToBeDuplicate, TimeSpan? durationOverride = null)
        {
            if (secondActionContent is null)
                Enqueue(content, type, actionContent, actionHandler, actionArgument, promote, neverConsiderToBeDuplicate, durationOverride);

            if (content is null) throw new ArgumentNullException(nameof(content));

            if (actionContent is null ^ actionHandler is null)
            {
                throw new ArgumentException("All action arguments must be provided if any are provided.",
                    actionContent != null ? nameof(actionContent) : nameof(actionHandler));
            }

            var handler = actionHandler != null
                ? new Action<object>(argument => actionHandler((TArgument)argument))
                : null;
            var secondHandler = actionHandler != null
                ? new Action<object>(_ => secondActionHandler(secondActionArgument))
                : null;
            Enqueue(content, type, actionContent, handler, actionArgument, secondActionContent, secondHandler, secondActionArgument, promote, neverConsiderToBeDuplicate);
        }

        /// <summary>
        /// Enqueues a message in the display list.
        /// </summary>
        /// <param name="content">Message to display.</param>
        /// <param name="type">Type of information to show.</param>
        /// <param name="actionContent">Content of the action button.</param>
        /// <param name="actionHandler">Callback of the action button.</param>
        /// <param name="actionArgument">Argument to be passed to the callback of the action button.</param>
        /// <param name="promote">Set to true if message should be prioritized.</param>
        /// <param name="neverConsiderToBeDuplicate">Never skip, even if duplicate exists in queue.</param>
        /// <param name="durationOverride">Custom persistence time.</param>
        public void Enqueue(object content, MessageType type, object actionContent, Action<object> actionHandler,
                            object actionArgument, bool promote, bool neverConsiderToBeDuplicate, TimeSpan? durationOverride = null)
        {
            if (content is null) throw new ArgumentNullException(nameof(content));

            if (actionContent is null ^ actionHandler is null)
            {
                throw new ArgumentException("All action arguments must be provided if any are provided.",
                    actionContent != null ? nameof(actionContent) : nameof(actionHandler));
            }

            var messageQueueItem = new NotificationQueueItem(content, type, durationOverride ?? _messageDuration,
                actionContent, actionHandler, actionArgument, promote, neverConsiderToBeDuplicate);
            InsertItem(messageQueueItem);
        }

        /// <summary>
        /// Enqueues a message in the display list.
        /// </summary>
        /// <param name="content">Message to display.</param>
        /// <param name="type">Type of information to show.</param>
        /// <param name="actionContent">Content of the action button.</param>
        /// <param name="actionHandler">Callback of the action button.</param>
        /// <param name="actionArgument">Argument to be passed to the callback of the action button.</param>
        /// <param name="secondActionContent">Content of the second action button.</param>
        /// <param name="secondActionHandler">Callback of the second action button.</param>
        /// <param name="secondActionArgument">Argument to be passed to the callback of the second action button.</param>
        /// <param name="promote">Set to true if message should be prioritized.</param>
        /// <param name="neverConsiderToBeDuplicate">Never skip, even if duplicate exists in queue.</param>
        /// <param name="durationOverride">Custom persistence time.</param>
        public void Enqueue(object content, MessageType type, object actionContent, Action<object> actionHandler,
                            object actionArgument, object secondActionContent, Action<object> secondActionHandler, object secondActionArgument, 
                            bool promote, bool neverConsiderToBeDuplicate, TimeSpan? durationOverride = null)
        {
            if (secondActionContent is null)  // redirect to simple form if no second content is provided.
                Enqueue(content, type, actionContent, actionHandler, actionArgument, promote: promote, neverConsiderToBeDuplicate: neverConsiderToBeDuplicate, durationOverride: durationOverride);

            if (content is null) throw new ArgumentNullException(nameof(content));

            if (actionContent is null ^ actionHandler is null)
            {
                throw new ArgumentException("All action arguments must be provided if any are provided.",
                    actionContent != null ? nameof(actionContent) : nameof(actionHandler));
            }

            var messageQueueItem = new NotificationQueueItem(content, type, durationOverride ?? _messageDuration,
                actionContent, actionHandler, actionArgument, secondActionContent, secondActionHandler, secondActionArgument, promote, neverConsiderToBeDuplicate);
            InsertItem(messageQueueItem);
        }

        /// <summary>
        /// Creates a message based on a passed <see cref="NotificationQueueItem"/>.
        /// </summary>
        /// <param name="messageQueueItem">The seed information message item.</param>
        internal override InformationMessage GenerateMessage(InformationMessageQueueItem messageQueueItem)
        {
            if (messageQueueItem is NotificationQueueItem notificationItem)
                return new Notification
                {
                    Content = notificationItem.Content,
                    NotificationType = notificationItem.NotificationType,
                    ActionContent = notificationItem.ActionContent,
                    SecondaryActionContent = notificationItem.SecondActionContent
                };
            else return base.GenerateMessage(messageQueueItem);
        }
    }
}
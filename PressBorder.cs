using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using EMA.ExtendedWPFVisualTreeHelper;

namespace EMA.MaterialDesignInXAMLExtender
{
    /// <summary>
    /// A container that catches click/touch and understands 
    /// if user is pressing an object inside it or not.
    /// Basicaly gives "pressed" state capabality to all contained objects. 
    /// Use a transparent background for its style to catch clicks/touches
    /// in blank zones within.
    /// </summary>
    public class PressBorder : Border
    {
        private DispatcherTimer evaluatePressTimer;     // used to evaluate if click or touch is still active
        private FrameworkElement parent;                // a reference to the top parent that potentially holds the list having this object, null if such list does not exist

        static PressBorder()
        {
            DefaultStyleKeyProperty.OverrideMetadata(typeof(PressBorder), new FrameworkPropertyMetadata(typeof(PressBorder)));
        }

        /// <summary>
        /// Exploit render property to find parent and bypass its preview events.
        /// </summary>
        /// <param name="drawingContext">Drawing context info.</param>
        protected override void OnRender(DrawingContext drawingContext)
        {
            base.OnRender(drawingContext);

            // Find parent that holds the drag & drop properties:
            // First go up in list:
            var listHolder = this.FindParent<ItemsControl>();
            while (listHolder != null && listHolder is HeaderedItemsControl)
                listHolder = listHolder.FindParent<ItemsControl>();

            // Then take parent of this list:
            if (listHolder != null && listHolder.Parent != null)
                parent = listHolder.FindParent<ItemsControl>();

            // Finaly bind to preview mouse down and touch down events of parent:
            if (parent != null)
            {
                parent.PreviewMouseDown += Parent_PreviewMouseDown;
                parent.PreviewTouchDown += Parent_PreviewTouchDown;
                this.Unloaded += PressControl_Unloaded;      // used to unsubscribe when this object is killed.
            }
        }

        /// <summary>
        /// Unsubscribe event handlers from parent if it exists.
        /// </summary>
        /// <param name="sender">The event sender.</param>
        /// <param name="e">Event info.</param>
        private void PressControl_Unloaded(object sender, RoutedEventArgs e)
        {
            if (parent != null)
            {
                parent.PreviewMouseDown -= Parent_PreviewMouseDown;
                parent.PreviewTouchDown -= Parent_PreviewTouchDown;
            }
        }

        /// <summary>
        /// Handles event of parent when the later is being clicked.
        /// </summary>
        /// <param name="sender">The event sender.</param>
        /// <param name="e">Event info.</param>
        private void Parent_PreviewMouseDown(object sender, MouseButtonEventArgs e)
        {
            if (IsMouseOver)
                triggerPressEvent(false);
        }

        /// <summary>
        /// Handles touch event from parent when the latter is being clicked.
        /// </summary>
        /// <param name="sender">The event sender.</param>
        /// <param name="e">Event info.</param>
        private void Parent_PreviewTouchDown(object sender, TouchEventArgs e)
        {
            if (IsStylusOver)
                triggerPressEvent(true);
        }

        /// <summary>
        /// In case no parent is found, launches press command execution for click event.
        /// </summary>
        /// <param name="e"></param>
        protected override void OnPreviewMouseDown(MouseButtonEventArgs e)
        {
            base.OnPreviewMouseDown(e);

            if (parent == null)
                triggerPressEvent(false);
        }

        /// <summary>
        /// In case no parent is found, launches press command execution for touch event.
        /// </summary>
        /// <param name="e">Event info.</param>
        protected override void OnPreviewTouchDown(TouchEventArgs e)
        {
            base.OnPreviewTouchDown(e);

            if (parent == null)
                triggerPressEvent(true);
        }

        /// <summary>
        /// Evaluates if the user is still pressing the mouse or touch and launches release command execution if existing.
        /// </summary>
        /// <param name="sender">The event sender.</param>
        /// <param name="e">Event info.</param>
        private void evaluateClickState(object sender, EventArgs e)
        {
            bool inputPressed = Mouse.LeftButton == MouseButtonState.Pressed || Mouse.RightButton == MouseButtonState.Pressed;
            if (!inputPressed)
            {
                evaluatePressTimer.Stop();
                triggerReleaseEvent();
            }
        }

        /// <summary>
        /// Monitors touch state to execute release command when touch is released.
        /// </summary>
        /// <param name="sender">The event sender.</param>
        /// <param name="e">Event info.</param>
        private void Touch_FrameReported(object sender, TouchFrameEventArgs e)
        {
            var relativeToApplication = e.GetPrimaryTouchPoint(Application.Current.MainWindow);
            if (relativeToApplication.Action == TouchAction.Up)
            {
                Touch.FrameReported -= Touch_FrameReported;
                triggerReleaseEvent();
            }
        }

        /// <summary>
        /// Execute press command when existing, starts timer to handle release if release command exists.
        /// </summary>
        /// <param name="used_touch">Indicates if touch was used to press.</param>
        private void triggerPressEvent(bool used_touch)
        {
            if (PressCommand?.CanExecute(this.DataContext) == true)
                PressCommand.Execute(this.DataContext);

            IsPressed = true;

            if (!used_touch)
            {
                if (evaluatePressTimer == null)
                    evaluatePressTimer = new DispatcherTimer(TimeSpan.FromMilliseconds(100), DispatcherPriority.Input, new EventHandler(evaluateClickState), Application.Current.Dispatcher);
                evaluatePressTimer.Start();
            }
            else
                Touch.FrameReported += Touch_FrameReported;
        }

        /// <summary>
        /// Execute release command if existing.
        /// </summary>
        private void triggerReleaseEvent()
        {
            if (ReleaseCommand?.CanExecute(this.DataContext) == true)
                ReleaseCommand.Execute(this.DataContext);

            IsPressed = false;
        }

        /// <summary>
        /// Gets or sets the command that must be invoked when control is pressed.
        /// </summary>
        public ICommand PressCommand
        {
            get { return (ICommand)GetValue(PressCommandProperty); }
            set { SetValue(PressCommandProperty, value); }
        }
        /// <summary>
        /// Registers the <see cref="PressCommand"/> as a dependency property.
        /// </summary>
        public static readonly DependencyProperty PressCommandProperty =
            DependencyProperty.Register(nameof(PressCommand), typeof(ICommand), typeof(PressBorder), new FrameworkPropertyMetadata(null));

        /// <summary>
        /// Gets or sets the command that must be invoked when button is released.
        /// </summary>
        public ICommand ReleaseCommand
        {
            get { return (ICommand)GetValue(ReleaseCommandProperty); }
            set { SetValue(ReleaseCommandProperty, value); }
        }
        /// <summary>
        /// Registers the <see cref="ReleaseCommand"/> as a dependency property.
        /// </summary>
        public static readonly DependencyProperty ReleaseCommandProperty =
            DependencyProperty.Register(nameof(ReleaseCommand), typeof(ICommand), typeof(PressBorder), new FrameworkPropertyMetadata(null));


        /// <summary>
        /// Gets the pressed status of the control.
        /// </summary>
        /// <remarks>A bit different than Button.IsPressed as it does not handle
        /// space bar and event system.</remarks>
        public bool IsPressed
        {
            get { return (bool)GetValue(IsPressedProperty); }
            set { SetValue(IsPressedPropertyKey, value); }
        }
        private static readonly DependencyPropertyKey IsPressedPropertyKey =
            DependencyProperty.RegisterReadOnly(nameof(IsPressed), typeof(bool), typeof(PressBorder), new FrameworkPropertyMetadata(default(bool)));
        /// <summary>
        /// Registers the <see cref="IsPressed"/> as a readonly dependency property.
        /// </summary>
        public static readonly DependencyProperty IsPressedProperty = IsPressedPropertyKey.DependencyProperty;
    }
}

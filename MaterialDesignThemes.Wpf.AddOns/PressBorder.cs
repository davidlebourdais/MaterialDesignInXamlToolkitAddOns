using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using EMA.ExtendedWPFVisualTreeHelper;

namespace MaterialDesignThemes.Wpf.AddOns
{
    /// <summary>
    /// A container that catches click/touch and understands 
    /// if user is pressing an object inside it or not.
    /// Basically gives "pressed" state capability to all contained objects. 
    /// Use a transparent background for its style to catch clicks/touches
    /// in blank zones within.
    /// </summary>
    public class PressBorder : Border
    {
        private DispatcherTimer _evaluatePressTimer;     // used to evaluate if click or touch is still active
        private FrameworkElement _borderParent;                 // a reference to the top parent that potentially holds the list having this object, null if such list does not exist

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
            while (listHolder is HeaderedItemsControl)
                listHolder = listHolder.FindParent<ItemsControl>();

            // Then take parent of this list:
            if (listHolder?.Parent != null)
                _borderParent = listHolder.FindParent<ItemsControl>();

            // Finally bind to preview mouse down and touch down events of parent:
            if (_borderParent == null)
                return;
            
            _borderParent.PreviewMouseDown += Parent_PreviewMouseDown;
            _borderParent.PreviewTouchDown += Parent_PreviewTouchDown;
            this.Unloaded += PressControl_Unloaded;      // used to unsubscribe when this object is killed.
        }

        /// <summary>
        /// Unsubscribe event handlers from parent if it exists.
        /// </summary>
        /// <param name="sender">The event sender.</param>
        /// <param name="e">Event info.</param>
        private void PressControl_Unloaded(object sender, RoutedEventArgs e)
        {
            if (_borderParent == null)
                return;
            
            _borderParent.PreviewMouseDown -= Parent_PreviewMouseDown;
            _borderParent.PreviewTouchDown -= Parent_PreviewTouchDown;
        }

        /// <summary>
        /// Handles event of parent when the later is being clicked.
        /// </summary>
        /// <param name="sender">The event sender.</param>
        /// <param name="e">Event info.</param>
        private void Parent_PreviewMouseDown(object sender, MouseButtonEventArgs e)
        {
            if (IsMouseOver)
                TriggerPressEvent(false);
        }

        /// <summary>
        /// Handles touch event from parent when the latter is being clicked.
        /// </summary>
        /// <param name="sender">The event sender.</param>
        /// <param name="e">Event info.</param>
        private void Parent_PreviewTouchDown(object sender, TouchEventArgs e)
        {
            if (IsStylusOver)
                TriggerPressEvent(true);
        }

        /// <summary>
        /// In case no parent is found, launches press command execution for click event.
        /// </summary>
        /// <param name="e"></param>
        protected override void OnPreviewMouseDown(MouseButtonEventArgs e)
        {
            base.OnPreviewMouseDown(e);

            if (_borderParent == null)
                TriggerPressEvent(false);
        }

        /// <summary>
        /// In case no parent is found, launches press command execution for touch event.
        /// </summary>
        /// <param name="e">Event info.</param>
        protected override void OnPreviewTouchDown(TouchEventArgs e)
        {
            base.OnPreviewTouchDown(e);

            if (_borderParent == null)
                TriggerPressEvent(true);
        }

        /// <summary>
        /// Evaluates if the user is still pressing the mouse or touch and launches release command execution if existing.
        /// </summary>
        /// <param name="sender">The event sender.</param>
        /// <param name="e">Event info.</param>
        private void EvaluateClickState(object sender, EventArgs e)
        {
            var inputPressed = Mouse.LeftButton == MouseButtonState.Pressed || Mouse.RightButton == MouseButtonState.Pressed;
            if (inputPressed)
                return;
            
            _evaluatePressTimer.Stop();
            TriggerReleaseEvent();
        }

        /// <summary>
        /// Monitors touch state to execute release command when touch is released.
        /// </summary>
        /// <param name="sender">The event sender.</param>
        /// <param name="e">Event info.</param>
        private void Touch_FrameReported(object sender, TouchFrameEventArgs e)
        {
            var relativeToApplication = e.GetPrimaryTouchPoint(Application.Current.MainWindow);
            if (relativeToApplication == null || relativeToApplication.Action != TouchAction.Up)
                return;
            
            Touch.FrameReported -= Touch_FrameReported;
            TriggerReleaseEvent();
        }

        /// <summary>
        /// Execute press command when existing, starts timer to handle release if release command exists.
        /// </summary>
        /// <param name="usedTouch">Indicates if touch was used to press.</param>
        private void TriggerPressEvent(bool usedTouch)
        {
            if (PressCommand?.CanExecute(DataContext) == true)
                PressCommand.Execute(DataContext);

            IsPressed = true;

            if (!usedTouch)
            {
                if (_evaluatePressTimer == null)
                    _evaluatePressTimer = new DispatcherTimer(TimeSpan.FromMilliseconds(100), DispatcherPriority.Input, EvaluateClickState, Application.Current.Dispatcher);
                _evaluatePressTimer.Start();
            }
            else
                Touch.FrameReported += Touch_FrameReported;
        }

        /// <summary>
        /// Execute release command if existing.
        /// </summary>
        private void TriggerReleaseEvent()
        {
            if (ReleaseCommand?.CanExecute(DataContext) == true)
                ReleaseCommand.Execute(DataContext);

            IsPressed = false;
        }

        /// <summary>
        /// Gets or sets the command that must be invoked when control is pressed.
        /// </summary>
        public ICommand PressCommand
        {
            get => (ICommand)GetValue(PressCommandProperty);
            set => SetCurrentValue(PressCommandProperty, value);
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
            get => (ICommand)GetValue(ReleaseCommandProperty);
            set => SetCurrentValue(ReleaseCommandProperty, value);
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
            get => (bool)GetValue(IsPressedProperty);
            set => SetValue(_isPressedPropertyKey, value);
        }
        private static readonly DependencyPropertyKey _isPressedPropertyKey =
            DependencyProperty.RegisterReadOnly(nameof(IsPressed), typeof(bool), typeof(PressBorder), new FrameworkPropertyMetadata(default(bool)));
        /// <summary>
        /// Registers the <see cref="IsPressed"/> as a readonly dependency property.
        /// </summary>
        public static readonly DependencyProperty IsPressedProperty = _isPressedPropertyKey.DependencyProperty;
    }
}

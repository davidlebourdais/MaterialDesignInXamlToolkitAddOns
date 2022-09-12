using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using System.Windows.Media.Media3D;
using EMA.ExtendedWPFVisualTreeHelper;
using MaterialDesignThemes.Wpf.AddOns.Extensions;
using MaterialDesignThemes.Wpf.AddOns.Utils.Screen;

namespace MaterialDesignThemes.Wpf.AddOns
{
    /// <summary>
    /// Defines the internal placement of the combobox.
    /// </summary>
    public enum SelectBoxPopupPlacement
    {
        /// <summary>
        /// Unforced.
        /// </summary>
        Undefined,
        /// <summary>
        /// Forced to Down.
        /// </summary>
        Down,
        /// <summary>
        /// Forced to Up.
        /// </summary>
        Up,
        /// <summary>
        /// Forced to classic mode.
        /// </summary>
        Classic
    }

    /// <summary>
    /// A popup to be used by <see cref="SelectBox"/> controls and derived controls.
    /// Holds a <see cref="TextBox"/>.
    /// </summary>
    /// <remarks>Adapted from https://github.com/MaterialDesignInXAML/MaterialDesignInXamlToolkit ComboBoxPopup.</remarks>
    public class SelectBoxPopup : Popup
    {
        /// <summary>
        /// Gets the current filtering TextBox within this popup is any.
        /// </summary>
        public TextBox CurrentPopupFilterTextBox { get; private set; }

        /// <summary>
        /// Static constructor for <see cref="SelectBoxPopup"/> type.
        /// Override some base dependency properties.
        /// </summary>
        static SelectBoxPopup()
        {
            DefaultStyleKeyProperty.OverrideMetadata(typeof(SelectBoxPopup), new FrameworkPropertyMetadata(typeof(SelectBoxPopup)));
        }
        
        /// <summary>
        /// Occurs whenever a dependency property changes.
        /// </summary>
        /// <param name="e">Information about property value change.</param>
        protected override void OnPropertyChanged(DependencyPropertyChangedEventArgs e)
        {
            base.OnPropertyChanged(e);

            if (_dependencyPropertiesThatChangeFilterTextBox.Contains(e.Property))
            {
                SetCurrentPopupFilterTextBox(e.Property == IsOpenProperty);
            }

            if (e.Property != ChildProperty)
                return;

            if (PopupPlacement != SelectBoxPopupPlacement.Undefined)
            {
                UpdateChildTemplate(PopupPlacement);
            }
        }

        private void SetCurrentPopupFilterTextBox(bool fromIsOpenProperty)
        {
            var previous = CurrentPopupFilterTextBox;

            RefreshCurrentPopupFilterTextBox(fromIsOpenProperty);

            if (previous != CurrentPopupFilterTextBox)
                RaiseEvent(new RoutedEventArgs(_filterTextBoxChangedEvent, this));
        }

        private void RefreshCurrentPopupFilterTextBox(bool fromIsOpenProperty = false)
        {
            CurrentPopupFilterTextBox = null;

            if (!(Child is ContentControl child) || child.Template == null)
                return;

            if (!child.IsLoaded)
            {
                child.Loaded += ChildOnLoaded;
                return;
            }

            CurrentPopupFilterTextBox = child.FindChild<TextBox>("PART_PopupFilterTextBox");

            if (CurrentPopupFilterTextBox != null)
                return;

            if (!fromIsOpenProperty)
                return;

            if (child.Template == DownContentTemplate)
                throw new Exception(nameof(DownContentTemplate) + " must contain a TextBox named 'PART_PopupFilterTextBox'.");
            if (child.Template == UpContentTemplate)
                throw new Exception(nameof(UpContentTemplate) + " must contain a TextBox named 'PART_PopupFilterTextBox'.");
        }

        private void ChildOnLoaded(object sender, RoutedEventArgs e)
        {
            RefreshCurrentPopupFilterTextBox();
        }

        #region Dependency properties

        /// <summary>
        /// Adds or removes event handlers for template change event.
        /// </summary>
        public event RoutedEventHandler FilterTextBoxChanged
        {
            add => AddHandler(_filterTextBoxChangedEvent, value);
            remove => RemoveHandler(_filterTextBoxChangedEvent, value);
        }

        /// <summary>
        /// Registers <see cref="FilterTextBoxChanged"/> as a routed event.
        /// </summary>
        private static readonly RoutedEvent _filterTextBoxChangedEvent = EventManager.RegisterRoutedEvent(
            nameof(FilterTextBoxChanged),
            RoutingStrategy.Direct,
            typeof(RoutedEventHandler),
            typeof(SelectBoxPopup));

        /// <summary>
        /// The template to be used when the popup opens at the bottom of the main TextBox.
        /// </summary>
        public ControlTemplate DownContentTemplate
        {
            get => (ControlTemplate) GetValue(DownContentTemplateProperty);
            set => SetValue(DownContentTemplateProperty, value);
        }

        /// <summary>
        /// Registers <see cref="DownContentTemplate"/> as a dependency property.
        /// </summary>
        public static readonly DependencyProperty DownContentTemplateProperty
            = DependencyProperty.Register(nameof(DownContentTemplate), typeof(ControlTemplate), typeof(SelectBoxPopup), new UIPropertyMetadata(null, CreateTemplatePropertyChangedCallback(SelectBoxPopupPlacement.Down)));

        /// <summary>
        /// The template to be used when the popup opens on top of the main TextBox by
        /// lack of screen space below. 
        /// </summary>
        public ControlTemplate UpContentTemplate
        {
            get => (ControlTemplate) GetValue(UpContentTemplateProperty);
            set => SetValue(UpContentTemplateProperty, value);
        }

        /// <summary>
        /// Registers <see cref="UpContentTemplate"/> as a dependency property.
        /// </summary>
        public static readonly DependencyProperty UpContentTemplateProperty
            = DependencyProperty.Register(nameof(UpContentTemplate), typeof(ControlTemplate), typeof(SelectBoxPopup), new UIPropertyMetadata(null, CreateTemplatePropertyChangedCallback(SelectBoxPopupPlacement.Classic)));

        /// <summary>
        /// The template to be used when the popup opens separately from the main TextBox. Always overrides
        /// <see cref="DownContentTemplate"/> and <see cref="UpContentTemplate"/> when <see cref="ClassicMode"/>
        /// is set to true.
        /// </summary>
        public ControlTemplate ClassicContentTemplate
        {
            get => (ControlTemplate) GetValue(ClassicContentTemplateProperty);
            set => SetValue(ClassicContentTemplateProperty, value);
        }

        /// <summary>
        /// Registers <see cref="ClassicContentTemplate"/> as a dependency property.
        /// </summary>
        public static readonly DependencyProperty ClassicContentTemplateProperty
            = DependencyProperty.Register(nameof(ClassicContentTemplate), typeof(ControlTemplate), typeof(SelectBoxPopup), new UIPropertyMetadata(null, CreateTemplatePropertyChangedCallback(SelectBoxPopupPlacement.Up)));

        /// <summary>
        /// Vertical offset in pixels for the popup when opening above the main TextBox.
        /// </summary>
        public double UpVerticalOffset
        {
            get => (double) GetValue(UpVerticalOffsetProperty);
            set => SetValue(UpVerticalOffsetProperty, value);
        }

        /// <summary>
        /// Registers <see cref="UpVerticalOffset"/> as a dependency property.
        /// </summary>
        public static readonly DependencyProperty UpVerticalOffsetProperty
            = DependencyProperty.Register(nameof(UpVerticalOffset), typeof(double), typeof(SelectBoxPopup), new PropertyMetadata(0.0));

        /// <summary>
        /// Vertical offset in pixels for the popup when opening below the main TextBox.
        /// </summary>
        public double DownVerticalOffset
        {
            get => (double) GetValue(DownVerticalOffsetProperty);
            set => SetValue(DownVerticalOffsetProperty, value);
        }

        /// <summary>
        /// Registers <see cref="DownVerticalOffset"/> as a dependency property.
        /// </summary>
        public static readonly DependencyProperty DownVerticalOffsetProperty
            = DependencyProperty.Register(nameof(DownVerticalOffset), typeof(double), typeof(SelectBoxPopup), new PropertyMetadata(0.0));

        /// <summary>
        /// Sets the default offset of the popup.
        /// </summary>
        public double DefaultVerticalOffset
        {
            get => (double) GetValue(DefaultVerticalOffsetProperty);
            set => SetValue(DefaultVerticalOffsetProperty, value);
        }

        /// <summary>
        /// Registers <see cref="DefaultVerticalOffset"/> as a dependency property.
        /// </summary>
        public static readonly DependencyProperty DefaultVerticalOffsetProperty
            = DependencyProperty.Register(nameof(DefaultVerticalOffset), typeof(double), typeof(SelectBoxPopup), new PropertyMetadata(0.0));


        /// <summary>
        /// Gets or sets the horizontal offset of the popup.
        /// </summary>
        public double RelativeHorizontalOffset
        {
            get => (double) GetValue(RelativeHorizontalOffsetProperty);
            set => SetValue(RelativeHorizontalOffsetProperty, value);
        }

        /// <summary>
        /// Registers <see cref="RelativeHorizontalOffset"/> as a dependency property.
        /// </summary>
        public static readonly DependencyProperty RelativeHorizontalOffsetProperty
            = DependencyProperty.Register(nameof(RelativeHorizontalOffset), typeof(double), typeof(SelectBoxPopup), new FrameworkPropertyMetadata(default(double)));

        /// <summary>
        /// Gets or sets the placement of the popup around the main TextBox.
        /// </summary>
        public SelectBoxPopupPlacement PopupPlacement
        {
            get => (SelectBoxPopupPlacement) GetValue(PopupPlacementProperty);
            set => SetValue(PopupPlacementProperty, value);
        }

        /// <summary>
        /// Registers <see cref="PopupPlacement"/> as a dependency property.
        /// </summary>
        public static readonly DependencyProperty PopupPlacementProperty
            = DependencyProperty.Register(nameof(PopupPlacement), typeof(SelectBoxPopupPlacement), typeof(SelectBoxPopup), new PropertyMetadata(SelectBoxPopupPlacement.Undefined, PopupPlacementPropertyChangedCallback));

        /// <summary>
        /// Indicates if classic mode should be used.
        /// </summary>
        public bool ClassicMode
        {
            get => (bool) GetValue(ClassicModeProperty);
            set => SetValue(ClassicModeProperty, value);
        }

        /// <summary>
        /// Registers <see cref="ClassicMode"/> as a dependency property.
        /// </summary>
        public static readonly DependencyProperty ClassicModeProperty
            = DependencyProperty.Register(nameof(ClassicMode), typeof(bool), typeof(SelectBoxPopup), new FrameworkPropertyMetadata(true));

        /// <summary>
        /// Gets or sets the corner radius of the popup.
        /// </summary>
        public CornerRadius CornerRadius
        {
            get => (CornerRadius) GetValue(CornerRadiusProperty);
            set => SetValue(CornerRadiusProperty, value);
        }

        /// <summary>
        /// Registers <see cref="CornerRadius"/> as a dependency property.
        /// </summary>
        public static readonly DependencyProperty CornerRadiusProperty
            = DependencyProperty.Register(nameof(CornerRadius), typeof(CornerRadius), typeof(SelectBoxPopup), new FrameworkPropertyMetadata(default(CornerRadius)));

        /// <summary>
        /// Gets or sets the margin for the popup content.
        /// </summary>
        public Thickness ContentMargin
        {
            get => (Thickness) GetValue(ContentMarginProperty);
            set => SetValue(ContentMarginProperty, value);
        }

        /// <summary>
        /// Registers <see cref="ContentMargin"/> as a dependency property.
        /// </summary>
        public static readonly DependencyProperty ContentMarginProperty
            = DependencyProperty.Register(nameof(ContentMargin), typeof(Thickness), typeof(SelectBoxPopup), new FrameworkPropertyMetadata(default(Thickness)));

        /// <summary>
        /// Gets or sets the minimum width for the content of the popup.
        /// </summary>
        public double ContentMinWidth
        {
            get => (double) GetValue(ContentMinWidthProperty);
            set => SetValue(ContentMinWidthProperty, value);
        }

        /// <summary>
        /// Registers <see cref="ContentMinWidth"/> as a dependency property.
        /// </summary>
        public static readonly DependencyProperty ContentMinWidthProperty
            = DependencyProperty.Register(nameof(ContentMinWidth), typeof(double), typeof(SelectBoxPopup), new FrameworkPropertyMetadata(default(double)));
        
        /// <summary>
        /// Gets or sets the popup background.
        /// </summary>
        public Brush Background
        {
            get => (Brush) GetValue(BackgroundProperty);
            set => SetValue(BackgroundProperty, value);
        }
        
        /// <summary>
        /// Registers <see cref="Background"/> as a dependency property.
        /// </summary>
        public static readonly DependencyProperty BackgroundProperty
            = DependencyProperty.Register(nameof(Background), typeof(Brush), typeof(SelectBoxPopup), new PropertyMetadata(default(Brush)));
        #endregion
        
        private static readonly DependencyProperty[] _dependencyPropertiesThatChangeFilterTextBox =
        {
            ClassicContentTemplateProperty,
            DownContentTemplateProperty,
            UpContentTemplateProperty,
            PopupPlacementProperty,
            ChildProperty,
            IsOpenProperty
        };

        #region Popup logic
        /// <summary>
        /// Initiates a new instance of <see cref="SelectBoxPopup"/>.
        /// </summary>
        public SelectBoxPopup()
            => CustomPopupPlacementCallback = ComboBoxCustomPopupPlacementCallback;

        private CustomPopupPlacement[] ComboBoxCustomPopupPlacementCallback(Size popupSize, Size targetSize, Point offset)
        {
            var visualAncestry = GetVisualAncestry(PlacementTarget).ToArray();

            var data = GetPositioningData(visualAncestry, popupSize, targetSize);
            var preferUpIfSafe = data.LocationY + data.PopupSize.Height > data.ScreenHeight;

            if (ClassicMode
                || data.PopupLocationX + data.PopupSize.Width > data.ScreenWidth
                || data.PopupLocationX < 0
                || !preferUpIfSafe && data.LocationY + data.NewDownY < 0)
            {
                SetCurrentValue(PopupPlacementProperty, SelectBoxPopupPlacement.Classic);
                return new[] {GetClassicPopupPlacement(this, data)};
            }

            if (preferUpIfSafe)
            {
                SetCurrentValue(PopupPlacementProperty, SelectBoxPopupPlacement.Up);
                return new[] {GetUpPopupPlacement(data)};
            }

            SetCurrentValue(PopupPlacementProperty, SelectBoxPopupPlacement.Down);
            return new[] {GetDownPopupPlacement(data)};
        }

        private void SetChildTemplateIfNeed(ControlTemplate template)
        {
            if (!(Child is ContentControl contentControl))
                return;

            if (!ReferenceEquals(contentControl.Template, template))
            {
                contentControl.Template = template;
            }
        }

        private PositioningData GetPositioningData(ICollection<DependencyObject> visualAncestry, Size popupSize, Size targetSize)
        {
            var locationFromScreen = PlacementTarget.PointToScreen(new Point(0, 0));

            var mainVisual = visualAncestry.OfType<Visual>().LastOrDefault();
            if (mainVisual is null)
                throw new ArgumentException($"{nameof(visualAncestry)} must contains unless one {nameof(Visual)} control inside.");

            var controlVisual = visualAncestry.OfType<Visual>().FirstOrDefault();
            if (controlVisual == null)
                throw new ArgumentException($"{nameof(visualAncestry)} must contains unless one {nameof(Visual)} control inside.");

            var screen = Screen.FromPoint(locationFromScreen);
            var screenWidth = (int) screen.Bounds.Width;
            var screenHeight = (int) screen.Bounds.Height;

            //Adjust the location to be in terms of the current screen
            var locationX = (int) (locationFromScreen.X - screen.Bounds.X) % screenWidth;
            var locationY = (int) (locationFromScreen.Y - screen.Bounds.Y) % screenHeight;

            var upVerticalOffsetIndependent = mainVisual.TransformToDeviceY(UpVerticalOffset) * controlVisual.GetTotalTransformScaleY();
            var newUpY = upVerticalOffsetIndependent - popupSize.Height + targetSize.Height;
            var newDownY = mainVisual.TransformToDeviceY(DownVerticalOffset) * controlVisual.GetTotalTransformScaleY();
            var offsetX = mainVisual.TransformToDeviceX(RelativeHorizontalOffset) * controlVisual.GetTotalTransformScaleX();
            offsetX = FlowDirection == FlowDirection.LeftToRight ? Round(offsetX) : Math.Truncate(offsetX - targetSize.Width);

            return new PositioningData(
                mainVisual,
                offsetX,
                newUpY,
                newDownY,
                popupSize,
                targetSize,
                locationX,
                locationY,
                screenHeight,
                screenWidth);
        }

        private static double Round(double val) => val < 0 ? (int) (val - 0.5) : (int) (val + 0.5);

        private static PropertyChangedCallback CreateTemplatePropertyChangedCallback(SelectBoxPopupPlacement popupPlacement)
        {
            return delegate(DependencyObject d, DependencyPropertyChangedEventArgs e)
            {
                var popup = d as SelectBoxPopup;
                if (popup is null)
                    return;

                var template = e.NewValue as ControlTemplate;
                if (template is null)
                    return;

                if (popup.PopupPlacement == popupPlacement)
                {
                    popup.SetChildTemplateIfNeed(template);
                }
            };
        }

        private void UpdateChildTemplate(SelectBoxPopupPlacement placement)
        {
            switch (placement)
            {
                case SelectBoxPopupPlacement.Classic:
                    SetChildTemplateIfNeed(ClassicContentTemplate);
                    break;
                case SelectBoxPopupPlacement.Down:
                    SetChildTemplateIfNeed(DownContentTemplate);
                    break;
                case SelectBoxPopupPlacement.Up:
                    SetChildTemplateIfNeed(UpContentTemplate);
                    break;
            }
        }

        private static void PopupPlacementPropertyChangedCallback(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var popup = d as SelectBoxPopup;
            if (popup is null)
                return;

            if (!(e.NewValue is SelectBoxPopupPlacement placement))
                return;
            popup.UpdateChildTemplate(placement);
        }

        private static CustomPopupPlacement GetClassicPopupPlacement(SelectBoxPopup popup, PositioningData data)
        {
            var defaultVerticalOffsetIndependent = data.MainVisual.TransformToDeviceY(popup.DefaultVerticalOffset);
            var newY = data.LocationY + data.PopupSize.Height > data.ScreenHeight
                ? -(defaultVerticalOffsetIndependent + data.PopupSize.Height)
                : defaultVerticalOffsetIndependent + data.TargetSize.Height;

            return new CustomPopupPlacement(new Point(data.OffsetX, newY), PopupPrimaryAxis.Horizontal);
        }

        private static CustomPopupPlacement GetDownPopupPlacement(PositioningData data)
            => new CustomPopupPlacement(new Point(data.OffsetX, data.NewDownY), PopupPrimaryAxis.None);

        private static CustomPopupPlacement GetUpPopupPlacement(PositioningData data)
            => new CustomPopupPlacement(new Point(data.OffsetX, data.NewUpY), PopupPrimaryAxis.None);

        private static IEnumerable<DependencyObject> GetVisualAncestry(DependencyObject leaf)
        {
            while (leaf != null)
            {
                yield return leaf;
                leaf = leaf is Visual || leaf is Visual3D
                    ? VisualTreeHelper.GetParent(leaf)
                    : LogicalTreeHelper.GetParent(leaf);
            }
        }

        private readonly struct PositioningData
        {
            public Visual MainVisual { get; }
            public double OffsetX { get; }
            public double NewUpY { get; }
            public double NewDownY { get; }
            public double PopupLocationX => LocationX + OffsetX;
            public Size PopupSize { get; }
            public Size TargetSize { get; }
            private double LocationX { get; }
            public double LocationY { get; }
            public double ScreenHeight { get; }
            public double ScreenWidth { get; }

            public PositioningData(Visual mainVisual, double offsetX, double newUpY, double newDownY, Size popupSize, Size targetSize, double locationX, double locationY, double screenHeight, double screenWidth)
            {
                MainVisual = mainVisual;
                OffsetX = Round(offsetX);
                NewUpY = Round(newUpY);
                NewDownY = Round(newDownY);
                PopupSize = popupSize;
                TargetSize = targetSize;
                LocationX = locationX;
                LocationY = locationY;
                ScreenWidth = screenWidth;
                ScreenHeight = screenHeight;
            }
        }
        #endregion
    }
}

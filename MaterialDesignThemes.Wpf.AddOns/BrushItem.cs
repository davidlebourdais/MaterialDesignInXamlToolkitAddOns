using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace MaterialDesignThemes.Wpf.AddOns
{
    /// <summary>
    /// A item that represents a given color with 
    /// an optional name and an opacity.
    /// </summary>
    public class BrushItem : Control
    {
        /// <summary>
        /// Gets the brush name.
        /// </summary>
        public string BrushName { get; protected set; }

        /// <summary>
        /// Gets the brush.
        /// </summary>
        public Brush Brush { get; }

        /// <summary>
        /// Gets the color associated to the brush, if any.
        /// </summary>
        public Color Color { get; }
        
        /// <summary>
        /// Gets <see cref="SolidColorBrush"/> value of the stored color.
        /// </summary>
        public SolidColorBrush AsSolidColorBrush
        {
            get => (SolidColorBrush)GetValue(AsSolidColorBrushProperty);
            protected set => SetValue(_asSolidColorBrushPropertyKey, value);
        }
        private static readonly DependencyPropertyKey _asSolidColorBrushPropertyKey =
            DependencyProperty.RegisterReadOnly(nameof(AsSolidColorBrush), typeof(SolidColorBrush), typeof(BrushItem), new FrameworkPropertyMetadata(default(SolidColorBrush)));
        /// <summary>
        /// Registers <see cref="AsSolidColorBrush"/> as a dependency property.
        /// </summary>
        public static readonly DependencyProperty AsSolidColorBrushProperty = _asSolidColorBrushPropertyKey.DependencyProperty;

        /// <summary>
        /// Gets a simplified string representation of the stored color.
        /// </summary>
        public string AsRgbText
        {
            get => (string)GetValue(AsRgbTextProperty);
            protected set => SetValue(_asRgbTextPropertyKey, value);
        }
        private static readonly DependencyPropertyKey _asRgbTextPropertyKey =
            DependencyProperty.RegisterReadOnly(nameof(AsRgbText), typeof(string), typeof(BrushItem), new FrameworkPropertyMetadata(default(string)));
        /// <summary>
        /// Registers <see cref="AsRgbText"/> as a dependency property.
        /// </summary>
        public static readonly DependencyProperty AsRgbTextProperty = _asRgbTextPropertyKey.DependencyProperty;

        /// <summary>
        /// Gets a string representation of the stored color with alpha representation.
        /// </summary>
        public string AsArgbText
        {
            get => (string)GetValue(AsArgbTextProperty);
            protected set => SetValue(_asArgbTextPropertyKey, value);
        }
        private static readonly DependencyPropertyKey _asArgbTextPropertyKey =
            DependencyProperty.RegisterReadOnly(nameof(AsArgbText), typeof(string), typeof(BrushItem), new FrameworkPropertyMetadata(default(string)));
        /// <summary>
        /// Registers <see cref="AsArgbText"/> as a dependency property.
        /// </summary>
        public static readonly DependencyProperty AsArgbTextProperty = _asArgbTextPropertyKey.DependencyProperty;

        /// <summary>
        /// Gets the opacity of the brush. 
        /// </summary>
        public double BrushOpacity
        {
            get => (double)GetValue(BrushOpacityProperty);
            protected set => SetValue(_brushOpacityPropertyKey, value);
        }
        private static readonly DependencyPropertyKey _brushOpacityPropertyKey =
            DependencyProperty.RegisterReadOnly(nameof(BrushOpacity), typeof(double), typeof(BrushItem), new FrameworkPropertyMetadata(1.0d));
        /// <summary>
        /// Registers <see cref="BrushOpacity"/> as a dependency property.
        /// </summary>
        public static readonly DependencyProperty BrushOpacityProperty = _brushOpacityPropertyKey.DependencyProperty;

        /// <summary>
        /// Gets or sets a value indicating if the item is 
        /// selected or not.
        /// </summary>
        public bool IsSelected
        {
            get => (bool)GetValue(IsSelectedProperty);
            set => SetCurrentValue(IsSelectedProperty, value);
        }
        /// <summary>
        /// Registers <see cref="IsSelected"/> as a dependency property.
        /// </summary>
        public static readonly DependencyProperty IsSelectedProperty =
            DependencyProperty.Register(nameof(IsSelected), typeof(bool), typeof(BrushItem), new FrameworkPropertyMetadata(default(bool)));

        /// <summary>
        /// Creates a new instance of <see cref="BrushItem"/>.
        /// </summary>
        /// <param name="color">The color to be stored.</param>
        /// <param name="opacity">The opacity of the .</param>
        /// <param name="name">An optional name to be given to the color.</param>
        public BrushItem(Color color, double opacity = 1.0d, string name = "")
        {
            BrushName = name;
            Color = color;
            BrushOpacity = opacity;
            ReevaluateSolidColorBrush();
        }

        /// <summary>
        /// Creates a new instance of <see cref="BrushItem"/>.
        /// </summary>
        /// <param name="brush">The brush to be stored.</param>
        /// <param name="name">An optional name to be given to the color.</param>
        public BrushItem(Brush brush, string name = "")
        {
            BrushName = name;
            if (brush is SolidColorBrush solid)
                Color = solid.Color;
            BrushOpacity = brush.Opacity;
            ReevaluateSolidColorBrush();
        }

        /// <summary>
        /// Sets the opacity of the brush item.
        /// </summary>
        /// <param name="newValue">The new opacity value.</param>
        /// <returns>Returns true if opacity was successfully changed.</returns>
        public bool SetBrushOpacity(double newValue)
        {
            if (newValue < 0 || newValue > 1.0d || newValue == BrushOpacity) return false;
            BrushOpacity = newValue;
            ReevaluateSolidColorBrush();
            return true;
        }

        /// <summary>
        /// Sets a name for the brush item.
        /// </summary>
        /// <param name="newName">The new name value.</param>
        /// <returns>Returns true if brush name was successfully changed.</returns>
        public void SetBrushName(string newName)
        {
            if (BrushName == newName)
                return;
            
            BrushName = newName;
            AsRgbText = "#" + Color.ToString().Remove(0, 3) + (string.IsNullOrEmpty(BrushName) ? "" : " (" + BrushName + ")");
            AsArgbText = Color.ToString();
        }

        /// <summary>
        /// Recreates a solid color brush that fits current parameters.
        /// </summary>
        private void ReevaluateSolidColorBrush()
        {
            AsSolidColorBrush = new SolidColorBrush(Color) { Opacity = BrushOpacity };
            AsRgbText = "#" + Color.ToString().Remove(0, 3) + (string.IsNullOrEmpty(BrushName) ? "" : " (" + BrushName + ")");
            AsArgbText = Color.ToString();
        }

        /// <summary>
        /// Gets the color as an aRGB string.
        /// </summary>
        /// <returns>The hold color as an aRGB string.</returns>
        public override string ToString()
        {
            return Color.ToString();
        }
    }
}

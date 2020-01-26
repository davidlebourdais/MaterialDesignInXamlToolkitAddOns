using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace EMA.MaterialDesignInXAMLExtender
{
    /// <summary>
    /// A templatable item that represents a given color with 
    /// an optionnal name and an opacity.
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
        public Color Color { get; protected set; }
        
        /// <summary>
        /// Gets <see cref="SolidColorBrush"/> value of the stored color.
        /// </summary>
        public SolidColorBrush AsSolidColorBrush
        {
            get { return (SolidColorBrush)GetValue(AsSolidColorBrushProperty); }
            protected set { SetValue(AsSolidColorBrushPropertyKey, value); }
        }
        private static readonly DependencyPropertyKey AsSolidColorBrushPropertyKey =
            DependencyProperty.RegisterReadOnly(nameof(AsSolidColorBrush), typeof(SolidColorBrush), typeof(BrushItem), new FrameworkPropertyMetadata(default(SolidColorBrush)));
        /// <summary>
        /// Registers the <see cref="AsSolidColorBrush"/> as a dependency property.
        /// </summary>
        public static readonly DependencyProperty AsSolidColorBrushProperty = AsSolidColorBrushPropertyKey.DependencyProperty;

        /// <summary>
        /// Gets a simplified string representation of the stored color.
        /// </summary>
        public string AsRGBText
        {
            get { return (string)GetValue(AsRGBTextProperty); }
            protected set { SetValue(AsRGBTextPropertyKey, value); }
        }
        private static readonly DependencyPropertyKey AsRGBTextPropertyKey =
            DependencyProperty.RegisterReadOnly(nameof(AsRGBText), typeof(string), typeof(BrushItem), new FrameworkPropertyMetadata(default(string)));
        /// <summary>
        /// Registers the <see cref="AsRGBText"/> as a dependency property.
        /// </summary>
        public static readonly DependencyProperty AsRGBTextProperty = AsRGBTextPropertyKey.DependencyProperty;

        /// <summary>
        /// Gets a string representation of the stored color with alpha representation.
        /// </summary>
        public string AsARGBText
        {
            get { return (string)GetValue(AsARGBTextProperty); }
            protected set { SetValue(AsARGBTextPropertyKey, value); }
        }
        private static readonly DependencyPropertyKey AsARGBTextPropertyKey =
            DependencyProperty.RegisterReadOnly(nameof(AsARGBText), typeof(string), typeof(BrushItem), new FrameworkPropertyMetadata(default(string)));
        /// <summary>
        /// Registers the <see cref="AsARGBText"/> as a dependency property.
        /// </summary>
        public static readonly DependencyProperty AsARGBTextProperty = AsARGBTextPropertyKey.DependencyProperty;

        /// <summary>
        /// Gets or sets a value indicating if the item is 
        /// selected or not.
        /// </summary>
        public bool IsSelected
        {
            get { return (bool)GetValue(IsSelectedProperty); }
            set { SetValue(IsSelectedProperty, value); }
        }
        private static readonly DependencyProperty IsSelectedProperty =
            DependencyProperty.Register(nameof(IsSelected), typeof(bool), typeof(BrushItem), new FrameworkPropertyMetadata(default(bool)));

        /// <summary>
        /// Creates a new instance of <see cref="BrushItem"/>.
        /// </summary>
        /// <param name="color">The color to be stored.</param>
        /// <param name="opacity">The opacity of the .</param>
        /// <param name="name">An optionnal name to be given to the color.</param>
        public BrushItem(Color color, double opacity = 1.0d, string name = "")
        {
            if (color == null)
                throw new ArgumentNullException(nameof(color));
            BrushName = name;
            Brush = new SolidColorBrush(color);
            Color = color;
            Opacity = opacity;
            reevaluateSolidColorBrush();
        }

        /// <summary>
        /// Creates a new instance of <see cref="BrushItem"/>.
        /// </summary>
        /// <param name="brush">The brush to be stored.</param>
        /// <param name="name">An optionnal name to be given to the color.</param>
        public BrushItem(Brush brush, string name = "")
        {
            if (brush == null)
                throw new ArgumentNullException(nameof(brush));
            BrushName = name;
            Brush = brush;
            if (brush is SolidColorBrush solid)
                Color = solid.Color;
            Opacity = brush.Opacity;
            reevaluateSolidColorBrush();
        }

        /// <summary>
        /// Sets the opacity of the brush item.
        /// </summary>
        /// <param name="new_value">The new opacity value.</param>
        /// <returns>Returns true if opacity was succesfully changed.</returns>
        public bool setOpacity(double new_value)
        {
            if (new_value < 0 || new_value > 1.0d || new_value == Opacity) return false;
            Opacity = new_value;
            reevaluateSolidColorBrush();
            return true;
        }

        /// <summary>
        /// Sets the color of the brush item.
        /// </summary>
        /// <param name="newValue">The new color value.</param>
        /// <returns>Returns true if color was succesfully changed.</returns>
        public bool setColor(Color newValue)
        {
            if (newValue == null || newValue == Color) return false;
            Color = newValue;
            reevaluateSolidColorBrush();
            return true;
        }

        /// <summary>
        /// Sets a name for the brush item.
        /// </summary>
        /// <param name="new_name">The new name value.</param>
        /// <returns>Returns true if brush name was succesfully changed.</returns>
        public bool setBrushName(string new_name)
        {
            if (BrushName != new_name)
            {
                BrushName = new_name;
                AsRGBText = "#" + Color.ToString().Remove(0, 3) + (string.IsNullOrEmpty(BrushName) ? "" : " (" + BrushName + ")");
                AsARGBText = Color.ToString();
                return true;
            }
            return false;
        }

        /// <summary>
        /// Recreates a solid color brush that fits current parameters.
        /// </summary>
        private void reevaluateSolidColorBrush()
        {
            AsSolidColorBrush = new SolidColorBrush(Color) { Opacity = Opacity };
            AsRGBText = "#" + Color.ToString().Remove(0, 3) + (string.IsNullOrEmpty(BrushName) ? "" : " (" + BrushName + ")");
            AsARGBText = Color.ToString();
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

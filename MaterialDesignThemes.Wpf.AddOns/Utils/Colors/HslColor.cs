namespace MaterialDesignThemes.Wpf.AddOns.Utils
{
    /// <summary>
    /// A color that is specified by Hue, Saturation and Luminosity levels (HSL)
    /// and acts as a converter to/for other color systems used in .NET Framework.
    /// </summary>
    /// <remarks>Adapted from: https://richnewman.wordpress.com/about/code-listings-and-diagrams/hslcolor-class/</remarks>
    public class HslColor
    {
        #region Private attributes
        // Private data members below are on scale 0-1
        // They are scaled for use externally based on scale
        private double _hue = 1.0;
        private double _saturation = 1.0;
        private double _luminosity = 1.0;
        private const double _scale = 240.0;
        #endregion 

        #region Main properties
        /// <summary>
        /// Gets or sets the current color Hue.
        /// </summary>
        public double Hue
        {
            get => _hue * _scale;
            set => _hue = CheckRange(value / _scale);
        }

        /// <summary>
        /// Gets or sets the current color saturation.
        /// </summary>
        public double Saturation
        {
            get => _saturation * _scale;
            set => _saturation = CheckRange(value / _scale);
        }

        /// <summary>
        /// Gets or sets the current color luminosity.
        /// </summary>
        public double Luminosity
        {
            get => _luminosity * _scale;
            set => _luminosity = CheckRange(value / _scale);
        }
        #endregion

        #region Converted properties
        /// <summary>
        /// Gets the HSL color as <see cref="System.Drawing.Color"/>.
        /// </summary>
        public System.Drawing.Color AsDrawingColor => this;

        /// <summary>
        /// Gets the HSL color as <see cref="System.Windows.Media.Color"/>.
        /// </summary>
        public System.Windows.Media.Color AsMediaColor => this;
        #endregion

        #region Casts to/from System.Windows.Media.Color
        /// <summary>
        /// Cast from <see cref="System.Windows.Media.Color"/> to <see cref="HslColor"/>.
        /// </summary>
        /// <param name="color">The color to be casted.</param>
        public static implicit operator HslColor(System.Windows.Media.Color color)
        {
            return new HslColor(color);
        }

        /// <summary>
        /// Cast from <see cref="HslColor"/> to <see cref="System.Windows.Media.Color"/>.
        /// </summary>
        /// <param name="hslColor">The HSL color to be casted.</param>
        public static implicit operator System.Windows.Media.Color(HslColor hslColor)
        {
            var asDrawingColor = (System.Drawing.Color)hslColor;
            return new System.Windows.Media.Color() {
                A = asDrawingColor.A,
                R = asDrawingColor.R,
                G = asDrawingColor.G,
                B = asDrawingColor.B
            };
        }
        #endregion

        #region Casts to/from System.Drawing.Color
        /// <summary>
        /// Cast from <see cref="System.Drawing.Color"/> to <see cref="HslColor"/>.
        /// </summary>
        /// <param name="color">The color to be casted.</param>
        public static implicit operator HslColor(System.Drawing.Color color)
        {
            return new HslColor() {
                _hue = color.GetHue() / 360.0, // we store hue as 0-1 as opposed to 0-360 
                _luminosity = color.GetBrightness(),
                _saturation = color.GetSaturation()
            };
        }

        /// <summary>
        /// Cast from <see cref="HslColor"/> to <see cref="System.Drawing.Color"/>.
        /// </summary>
        /// <param name="hslColor">The HSL color to be casted.</param>
        public static implicit operator System.Drawing.Color(HslColor hslColor)
        {
            double r = 0, g = 0, b = 0;
            if (hslColor._luminosity == 0)
                return System.Drawing.Color.FromArgb((int)(255 * r), (int)(255 * g), (int)(255 * b));
            
            if (hslColor._saturation == 0)
                r = g = b = hslColor._luminosity;
            else
            {
                var temp2 = GetTemp2(hslColor);
                var temp1 = 2.0 * hslColor._luminosity - temp2;

                r = GetColorComponent(temp1, temp2, hslColor._hue + 1.0 / 3.0);
                g = GetColorComponent(temp1, temp2, hslColor._hue);
                b = GetColorComponent(temp1, temp2, hslColor._hue - 1.0 / 3.0);
            }
            return System.Drawing.Color.FromArgb((int)(255 * r), (int)(255 * g), (int)(255 * b));
        }
        #endregion

        #region Private methods
        private static double GetColorComponent(double temp1, double temp2, double temp3)
        {
            temp3 = MoveIntoRange(temp3);
            if (temp3 < 1.0 / 6.0)
                return temp1 + (temp2 - temp1) * 6.0 * temp3;
            if (temp3 < 0.5)
                return temp2;
            if (temp3 < 2.0 / 3.0)
                return temp1 + ((temp2 - temp1) * ((2.0 / 3.0) - temp3) * 6.0);
            return temp1;
        }

        private static double MoveIntoRange(double temp3)
        {
            if (temp3 < 0.0)
                temp3 += 1.0;
            else if (temp3 > 1.0)
                temp3 -= 1.0;
            return temp3;
        }

        private static double GetTemp2(HslColor hslColor)
        {
            double temp2;
            if (hslColor.Luminosity < 0.5)  //<=??
                temp2 = hslColor.Luminosity * (1.0 + hslColor.Saturation);
            else
                temp2 = hslColor.Luminosity + hslColor.Saturation - (hslColor.Luminosity * hslColor.Saturation);
            return temp2;
        }

        /// <summary>
        /// Checks if a main property value is in range.
        /// </summary>
        /// <param name="value">The value to be checked.</param>
        /// <returns>True if in range.</returns>
        private static double CheckRange(double value)
        {
            if (value < 0.0)
                value = 0.0;
            else if (value > 1.0)
                value = 1.0;
            return value;
        }
        #endregion

        #region Constructors
        /// <summary>
        /// Sets the current color using RGB code.
        /// </summary>
        /// <param name="red">Red value.</param>
        /// <param name="green">Green value.</param>
        /// <param name="blue">Blue value.</param>
        public void SetRgb(int red, int green, int blue)
        {
            var hslColor = (HslColor)System.Drawing.Color.FromArgb(red, green, blue);
            _hue = hslColor._hue;
            _saturation = hslColor._saturation;
            _luminosity = hslColor._luminosity;
        }

        /// <summary>
        /// Initiates a default instance of <see cref="HslColor"/>.
        /// </summary>
        public HslColor() { }

        /// <summary>
        /// Initiates an instance of <see cref="HslColor"/> from a <see cref="System.Drawing.Color"/> object.
        /// </summary>
        /// <param name="color">Seed color.</param>
        public HslColor(System.Drawing.Color color)
        {
            SetRgb(color.R, color.G, color.B);
        }

        /// <summary>
        /// Initiates an instance of <see cref="HslColor"/> from a <see cref="System.Windows.Media.Color"/> object.
        /// </summary>
        /// <param name="color">Seed color.</param>
        public HslColor(System.Windows.Media.Color color)
        {
            SetRgb(color.R, color.G, color.B);
        }

        /// <summary>
        /// Initiates an instance of <see cref="HslColor"/> from a RGB code.
        /// </summary>
        /// <param name="red">Red value.</param>
        /// <param name="green">Green value.</param>
        /// <param name="blue">Blue value.</param>
        public HslColor(int red, int green, int blue)
        {
            SetRgb(red, green, blue);
        }

        /// <summary>
        /// Initiates an instance of <see cref="HslColor"/> from HSL parameters.
        /// </summary>
        /// <param name="hue">Hue of the color.</param>
        /// <param name="saturation">Saturation of the color.</param>
        /// <param name="luminosity">Luminosity of the color.</param>
        public HslColor(double hue, double saturation, double luminosity)
        {
            Hue = hue;
            Saturation = saturation;
            Luminosity = luminosity;
        }
        #endregion
    }
}
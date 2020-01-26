namespace EMA.MaterialDesignInXAMLExtender.Utils
{
    /// <summary>
    /// A color that is specified by Hue, Saturation and Luminosity levels (HSL)
    /// and acts as a converter to/for other color systems used in .NET Framework.
    /// </summary>
    /// <remarks>Adapted from: https://richnewman.wordpress.com/about/code-listings-and-diagrams/hslcolor-class/</remarks>
    public class HSLColor
    {
        #region Private attributes
        // Private data members below are on scale 0-1
        // They are scaled for use externally based on scale
        private double hue = 1.0;
        private double saturation = 1.0;
        private double luminosity = 1.0;
        private const double scale = 240.0;
        #endregion 

        #region Main properties
        /// <summary>
        /// Gets or sets the current color Hue.
        /// </summary>
        public double Hue
        {
            get { return hue * scale; }
            set { hue = CheckRange(value / scale); }
        }

        /// <summary>
        /// Gets or sets the current color saturation.
        /// </summary>
        public double Saturation
        {
            get { return saturation * scale; }
            set { saturation = CheckRange(value / scale); }
        }

        /// <summary>
        /// Gets or sets the current color luminosity.
        /// </summary>
        public double Luminosity
        {
            get { return luminosity * scale; }
            set { luminosity = CheckRange(value / scale); }
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
        /// Cast from <see cref="System.Windows.Media.Color"/> to <see cref="HSLColor"/>.
        /// </summary>
        /// <param name="color">The color to be casted.</param>
        public static implicit operator HSLColor(System.Windows.Media.Color color)
        {
            return new HSLColor(color);
        }

        /// <summary>
        /// Cast from <see cref="HSLColor"/> to <see cref="System.Windows.Media.Color"/>.
        /// </summary>
        /// <param name="hslColor">The HSL color to be casted.</param>
        public static implicit operator System.Windows.Media.Color(HSLColor hslColor)
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
        /// Cast from <see cref="System.Drawing.Color"/> to <see cref="HSLColor"/>.
        /// </summary>
        /// <param name="color">The color to be casted.</param>
        public static implicit operator HSLColor(System.Drawing.Color color)
        {
            return new HSLColor() {
                hue = color.GetHue() / 360.0, // we store hue as 0-1 as opposed to 0-360 
                luminosity = color.GetBrightness(),
                saturation = color.GetSaturation()
            };
        }

        /// <summary>
        /// Cast from <see cref="HSLColor"/> to <see cref="System.Drawing.Color"/>.
        /// </summary>
        /// <param name="hslColor">The HSL color to be casted.</param>
        public static implicit operator System.Drawing.Color(HSLColor hslColor)
        {
            double r = 0, g = 0, b = 0;
            if (hslColor.luminosity != 0)
            {
                if (hslColor.saturation == 0)
                    r = g = b = hslColor.luminosity;
                else
                {
                    double temp2 = GetTemp2(hslColor);
                    double temp1 = 2.0 * hslColor.luminosity - temp2;

                    r = GetColorComponent(temp1, temp2, hslColor.hue + 1.0 / 3.0);
                    g = GetColorComponent(temp1, temp2, hslColor.hue);
                    b = GetColorComponent(temp1, temp2, hslColor.hue - 1.0 / 3.0);
                }
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
            else if (temp3 < 0.5)
                return temp2;
            else if (temp3 < 2.0 / 3.0)
                return temp1 + ((temp2 - temp1) * ((2.0 / 3.0) - temp3) * 6.0);
            else
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

        private static double GetTemp2(HSLColor hslColor)
        {
            double temp2;
            if (hslColor.luminosity < 0.5)  //<=??
                temp2 = hslColor.luminosity * (1.0 + hslColor.saturation);
            else
                temp2 = hslColor.luminosity + hslColor.saturation - (hslColor.luminosity * hslColor.saturation);
            return temp2;
        }

        /// <summary>
        /// Checks if a main property value is in range.
        /// </summary>
        /// <param name="value">The value to be checked.</param>
        /// <returns>True if in range.</returns>
        private double CheckRange(double value)
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
        public void SetRGB(int red, int green, int blue)
        {
            var hslColor = (HSLColor)System.Drawing.Color.FromArgb(red, green, blue);
            this.hue = hslColor.hue;
            this.saturation = hslColor.saturation;
            this.luminosity = hslColor.luminosity;
        }

        /// <summary>
        /// Initiates a default instance of <see cref="HSLColor"/>.
        /// </summary>
        public HSLColor() { }

        /// <summary>
        /// Initiates an instance of <see cref="HSLColor"/> from a <see cref="System.Drawing.Color"/> object.
        /// </summary>
        /// <param name="color">Seed color.</param>
        public HSLColor(System.Drawing.Color color)
        {
            SetRGB(color.R, color.G, color.B);
        }

        /// <summary>
        /// Initiates an instance of <see cref="HSLColor"/> from a <see cref="System.Windows.Media.Color"/> object.
        /// </summary>
        /// <param name="color">Seed color.</param>
        public HSLColor(System.Windows.Media.Color color)
        {
            SetRGB(color.R, color.G, color.B);
        }

        /// <summary>
        /// Initiates an instance of <see cref="HSLColor"/> from a RGB code.
        /// </summary>
        /// <param name="red">Red value.</param>
        /// <param name="green">Green value.</param>
        /// <param name="blue">Blue value.</param>
        public HSLColor(int red, int green, int blue)
        {
            SetRGB(red, green, blue);
        }

        /// <summary>
        /// Initiates an instance of <see cref="HSLColor"/> from HSL parameters.
        /// </summary>
        /// <param name="hue">Hue of the color.</param>
        /// <param name="saturation">Saturation of the color.</param>
        /// <param name="luminosity">Luminosity of the color.</param>
        public HSLColor(double hue, double saturation, double luminosity)
        {
            Hue = hue;
            Saturation = saturation;
            Luminosity = luminosity;
        }
        #endregion
    }
}
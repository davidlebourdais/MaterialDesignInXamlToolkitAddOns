using System;
using System.Collections.Generic;
using System.Reflection;
using System.Windows.Media;

namespace MaterialDesignThemes.Wpf.AddOns.Utils
{
    /// <summary>
    /// An helper class for color processing.
    /// </summary>
    public static class ColorHelper
    {
        /// <summary>
        /// Default color to be used when color conversions fail.
        /// </summary>
        private static Color DefaultColor { get; } = Colors.Transparent;

        #region System colors
        /// <summary>
        /// Gets the list of system colors as <see cref="BrushItem"/>.
        /// </summary>
        public static List<BrushItem> SystemColors { get; } = GenerateSystemColorsFromBrushes();

        /// <summary>
        /// Generates the list of system colors as <see cref="BrushItem"/>.
        /// </summary>
        /// <returns>The list of system colors.</returns>
        private static List<BrushItem> GenerateSystemColorsFromBrushes()
        {
            var systemColors = new List<BrushItem>();

            // Get WPF brushes static properties that contains standard colors and add them in our list:
            var properties = typeof(Brushes).GetProperties(BindingFlags.Static | BindingFlags.Public);
            foreach (var property in properties)
                systemColors.Add(new BrushItem((Brush)property.GetValue(null, null), property.Name));

            return systemColors;
        }
        #endregion

        #region Internal static methods
        /// <summary>
        /// Converts a ARGB hexadecimal string value to a color
        /// brush that matches it, or sends null is passed value is invalid.
        /// </summary>
        /// <param name="hexValue">The hexadecimal value representing the color.</param>
        /// <param name="opacity">Optional opacity value that will determine the alpha channel value.</param>
        /// <returns>A <see cref="Color"/> that matches the passed string hexadecimal value 
        /// or null is no matching found.</returns>
        internal static Color? GetNullableColorFromArgb(string hexValue, double? opacity = null)
        {
            if (string.IsNullOrEmpty(hexValue)) return null;
            hexValue = hexValue.Replace("#", string.Empty);
            while (hexValue.Length < 6) hexValue = "0" + hexValue;

            var a = (byte)255;
            if (hexValue.Length > 6)
            {
                while (hexValue.Length < 8) hexValue = "F" + hexValue;
                a = (byte)(Convert.ToUInt32(hexValue.Substring(0, 2), 16));
                hexValue = hexValue.Substring(2, 6);
            }
            if (opacity != null)
            {
                a = (byte)(255 * opacity);
            }
            var r = (byte)(Convert.ToUInt32(hexValue.Substring(0, 2), 16));
            var g = (byte)(Convert.ToUInt32(hexValue.Substring(2, 2), 16));
            var b = (byte)(Convert.ToUInt32(hexValue.Substring(4, 2), 16));

            return Color.FromArgb(a, r, g, b);
        }

        /// <summary>
        /// Converts a ARGB hexadecimal string value to a color
        /// brush that matches it.
        /// </summary>
        /// <param name="hexValue">The hexadecimal value representing the color.</param>
        /// <param name="opacity">Optional opacity value that will determine the alpha channel value.</param>
        /// <returns>A <see cref="Color"/> that matches the passed string hexadecimal value or
        /// default color if no matching is found.</returns>
        internal static Color GetColorFromArgb(string hexValue, double? opacity = null)
        {
            return GetNullableColorFromArgb(hexValue, opacity) ?? DefaultColor;
        }

        /// <summary>
        /// Converts a ARGB hexadecimal string value to a solid color
        /// brush that matches it.
        /// </summary>
        /// <param name="hexValue">The hexadecimal value representing the color.</param>
        /// <param name="setOpacity">Sets opacity based on the alpha channel.</param>
        /// <returns>A <see cref="SolidColorBrush"/> that matches the passed string hexadecimal value.</returns>
        internal static SolidColorBrush GetSolidColorBrushFromArgb(string hexValue, bool setOpacity = true)
        {
            var baseColor = GetColorFromArgb(hexValue);
            if (setOpacity)
            {
                baseColor.A = 255;
                return new SolidColorBrush(baseColor) { Opacity = GetOpacityFromArgb(hexValue) };
            }
            else return new SolidColorBrush(baseColor);
        }

        /// <summary>
        /// Gets an opacity value (between 0.0d and 1.0d) from a color ARGB representation.
        /// </summary>
        /// <param name="hexValue">The hexadecimal value representing the color.</param>
        /// <returns>An opacity value corresponding to the alpha channel of the color.</returns>
        internal static double GetOpacityFromArgb(string hexValue)
        {
            return GetColorFromArgb(hexValue).A / 255.0d;
        }

        /// <summary>
        /// Gets a color from passed HSL (Hue, Saturation and Luminosity) parameters.
        /// </summary>
        /// <param name="hue">The hue of the color to create.</param>
        /// <param name="saturation">The saturation of the color to create.</param>
        /// <param name="luminosity">The luminosity of the color to create.</param>
        /// <returns>A <see cref="Color"/> that matches the passed HSL parameters.</returns>
        private static Color? GetNullableColorFromHslParameters(double hue, double saturation, double luminosity)
        {
            return new HslColor(hue, saturation, luminosity);
        }

        /// <summary>
        /// Gets a color from passed HSL (Hue, Saturation and Luminosity) parameters.
        /// </summary>
        /// <param name="hue">The hue of the color to create.</param>
        /// <param name="saturation">The saturation of the color to create.</param>
        /// <param name="luminosity">The luminosity of the color to create.</param>
        /// <returns>A <see cref="Color"/> that matches the passed HSL parameters or the 
        /// default color in case an error occurs.</returns>
        internal static Color GetColorFromHslParameters(double hue, double saturation, double luminosity)
        {
            return GetNullableColorFromHslParameters(hue, saturation, luminosity) ?? DefaultColor;
        }

        /// <summary>
        /// Converts a <see cref="Color"/> as object into a <see cref="string"/>.
        /// </summary>
        /// <param name="color">The <see cref="Color"/> to be converted.</param>
        /// <returns>The corresponding <see cref="string"/> object.</returns>
        internal static string GetRgbStringFromColor(Color color)
        {
            return GetArgbStringFromColor(color);
        }

        /// <summary>
        /// Converts a <see cref="Color"/> as object into a <see cref="string"/>.
        /// </summary>
        /// <param name="color">The <see cref="Color"/> to be converted.</param>
        /// <param name="opacity">An optional opacity value between 0.0 and 1.0 to be added as alpha channel in the final string.</param>
        /// <returns>The corresponding <see cref="string"/> object.</returns>
        internal static string GetArgbStringFromColor(Color color, double? opacity = null)
        {
            if (opacity >= 0.0f && opacity <= 1.0f)
            {
                color.A = (byte)(int)(opacity * 255);
                return color.ToString();
            }

            return color.ToString().Remove(1, 2);
        }
        #endregion
    }
}

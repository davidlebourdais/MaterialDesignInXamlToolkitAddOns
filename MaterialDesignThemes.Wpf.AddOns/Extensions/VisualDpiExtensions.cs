using System;
using System.Reflection;
using System.Windows;
using System.Windows.Media;

namespace MaterialDesignThemes.Wpf.AddOns.Extensions
{
    /// <summary>
    /// Offers util methods to get information about a <see cref="Visual"/> object length with screen DPI dependency included.
    /// </summary>
    /// <remarks>Adapted from https://github.com/MaterialDesignInXAML/MaterialDesignInXamlToolkit to be made public.</remarks>
    public static class VisualDpiExtensions
    {
        private static readonly int _dpiX;
        private static readonly int _dpiY;

        private const double _standardDpiX = 96.0;
        private const double _standardDpiY = 96.0;

        static VisualDpiExtensions()
        {
            var dpiXProperty = typeof(SystemParameters).GetProperty("DpiX", BindingFlags.NonPublic | BindingFlags.Static)
                               ?? throw new InvalidOperationException($"Could not find DpiX property on {nameof(SystemParameters)}");
            var dpiYProperty = typeof(SystemParameters).GetProperty("Dpi", BindingFlags.NonPublic | BindingFlags.Static)
                               ?? throw new InvalidOperationException($"Could not find Dpi property on {nameof(SystemParameters)}");


            _dpiX = (int)dpiXProperty.GetValue(null, null);
            _dpiY = (int)dpiYProperty.GetValue(null, null);
        }

        /// <summary>
        /// Calculates the DPI independent X scaling of a visual by a given quantity. 
        /// </summary>
        /// <param name="visual">The visual to be measured.</param>
        /// <param name="x">The quantity to be applied for X scaling.</param>
        /// <returns>The result of the transformation.</returns>
        public static double TransformToDeviceX(this Visual visual, double x)
        {
            var source = PresentationSource.FromVisual(visual);
            if (source?.CompositionTarget != null) return x * source.CompositionTarget.TransformToDevice.M11;

            return TransformToDeviceX(x);
        }
        
        /// <summary>
        /// Calculates the DPI independent Y scaling of a visual by a given quantity. 
        /// </summary>
        /// <param name="visual">The visual to be measured.</param>
        /// <param name="y">The quantity to be applied for Y scaling.</param>
        /// <returns>The result of the transformation.</returns>
        public static double TransformToDeviceY(this Visual visual, double y)
        {
            var source = PresentationSource.FromVisual(visual);
            if (source?.CompositionTarget != null) return y * source.CompositionTarget.TransformToDevice.M22;

            return TransformToDeviceY(y);
        }

        private static double TransformToDeviceX(double x) => x * _dpiX / _standardDpiX;
        private static double TransformToDeviceY(double y) => y * _dpiY / _standardDpiY;
    }
}

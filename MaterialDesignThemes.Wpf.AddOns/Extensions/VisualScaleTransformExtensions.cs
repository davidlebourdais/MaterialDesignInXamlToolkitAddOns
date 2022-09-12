using System.Windows;
using System.Windows.Media;

namespace MaterialDesignThemes.Wpf.AddOns.Extensions
{
    /// <summary>
    /// Offers util methods to get information about a <see cref="Visual"/> object inner <see cref="Transform"/> operations.
    /// </summary>
    /// <remarks>Adapted from https://github.com/MaterialDesignInXAML/MaterialDesignInXamlToolkit to be made public.</remarks>
    public static class VisualScaleTransformExtensions
    {
        /// <summary>
        /// Returns the horizontal scale coefficient that is applied over a control after
        /// being transformed by cascading Scales. 
        /// </summary>
        /// <param name="visual">The visual to be measured.</param>
        /// <returns>The combined horizontal scale coefficient of the transforms.</returns>
        public static double GetTotalTransformScaleX(this Visual visual)
        {
            var totalTransform = 1.0d;
            DependencyObject currentVisualTreeElement = visual;
            do
            {
                if (currentVisualTreeElement is Visual element)
                {
                    var transform = VisualTreeHelper.GetTransform(element);
                    if ((transform != null) &&
                        (transform.Value.M12 == 0) &&
                        (transform.Value.OffsetX == 0))
                    {
                        totalTransform *= transform.Value.M11;
                    }
                }
                currentVisualTreeElement = VisualTreeHelper.GetParent(currentVisualTreeElement);
            }
            while (currentVisualTreeElement != null);

            return totalTransform;
        }

        /// <summary>
        /// Returns the vertical scale coefficient that is applied over a control after
        /// being transformed by cascading Scales. 
        /// </summary>
        /// <param name="visual">The visual to be measured.</param>
        /// <returns>The combined vertical scale coefficient of the transforms.</returns>
        public static double GetTotalTransformScaleY(this Visual visual)
        {
            var totalTransform = 1.0d;
            DependencyObject currentVisualTreeElement = visual;
            do
            {
                if (currentVisualTreeElement is Visual element)
                {
                    var transform = VisualTreeHelper.GetTransform(element);
                    if ((transform != null) &&
                        (transform.Value.M21 == 0) &&
                        (transform.Value.OffsetY == 0))
                    {
                        totalTransform *= transform.Value.M22;
                    }
                }
                currentVisualTreeElement = VisualTreeHelper.GetParent(currentVisualTreeElement);
            }
            while (currentVisualTreeElement != null);

            return totalTransform;
        }
    }
}

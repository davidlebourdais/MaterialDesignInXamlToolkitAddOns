using System.Windows;
using System.Windows.Controls;

namespace EMA.MaterialDesignInXAMLExtender
{   
    /// <summary>
    /// Extends <see cref="MaterialDesignThemes.Wpf.ButtonAssist"/> by adding new features.
    /// </summary>
    public static class ButtonAssist
    {
        /// <summary>
        /// Sets the corner radius of a button or border base element with a scalar (double) value, which helps to 
        /// define animations when the corner radius is uniformly changed.
        /// </summary>
        public static readonly DependencyProperty UniformCornerRadiusProperty 
            = DependencyProperty.RegisterAttached("UniformCornerRadius", typeof(double), typeof(ButtonAssist), new PropertyMetadata(2.0, OnUniformCornerRadius));

        /// <summary>
        /// Gets the value of the <see cref="UniformCornerRadiusProperty"/> dependency property.
        /// </summary>
        /// <param name="obj">The object that the dependency property is attached to.</param>
        /// <returns>Returns true if scrollviewer must scroll to end automaticaly, false otherwise.</returns>
        public static double GetUniformCornerRadius(DependencyObject obj)
        {
            return (double)obj.GetValue(UniformCornerRadiusProperty);
        }

        /// <summary>
        /// Sets the value of the <see cref="UniformCornerRadiusProperty"/> dependency property.
        /// </summary>
        /// <param name="obj">The object that the dependency property is attached to.</param>
        /// <param name="value">The new value to be set.</param>
        public static void SetUniformCornerRadius(DependencyObject obj, double value)
        {
            obj.SetValue(UniformCornerRadiusProperty, value);
        }

        /// <summary>
        /// Called whenever the <see cref="UniformCornerRadiusProperty"/> attached property is set.
        /// </summary>
        /// <param name="d">The dependency object on which the property is attached to.</param>
        /// <param name="e">Property change information.</param>
        private static void OnUniformCornerRadius(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is Border border)
                border.CornerRadius = new CornerRadius((double)e.NewValue);
            else
                MaterialDesignThemes.Wpf.ButtonAssist.SetCornerRadius(d, new CornerRadius((double)e.NewValue));
        }
    }
}
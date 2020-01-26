using System.Windows;

namespace EMA.MaterialDesignInXAMLExtender
{
    /// <summary>
    /// A class used to bind DynamicResources on objects that normaly do not allow them.
    /// </summary>
    /// <remarks>From: https://www.thomaslevesque.com/2011/03/21/wpf-how-to-bind-to-data-when-the-datacontext-is-not-inherited/
    /// See also for a more complete solution: https://stackoverflow.com/questions/33816511/how-can-you-bind-to-a-dynamicresource-so-you-can-use-a-converter-or-stringformat
    /// </remarks>
    public class BindingProxy : Freezable
    {
        /// <summary>
        /// Creates a new instance of <see cref="BindingProxy"/>.
        /// </summary>
        /// <returns>A new instance of the class</returns>
        protected override Freezable CreateInstanceCore()
        {
            return new BindingProxy();
        }

        /// <summary>
        /// Gets or sets the data associated to this binding proxy.
        /// </summary>
        public object Data
        {
            get { return (object)GetValue(DataProperty); }
            set { SetValue(DataProperty, value); }
        }

        /// <summary>
        /// Registers <see cref="Data"/> as a dependency property.
        /// </summary>
        public static readonly DependencyProperty DataProperty =
            DependencyProperty.Register(nameof(Data), typeof(object), typeof(BindingProxy), new FrameworkPropertyMetadata(null));
    }
}
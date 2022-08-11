using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using EMA.ExtendedWPFVisualTreeHelper;
using MaterialDesignThemes.Wpf.AddOns.Extensions;

namespace MaterialDesignThemes.Wpf.AddOns.Helpers
{
    /// <summary>
    /// Implements helpers for items controlled by <see cref="FilterTextBox"/> objects.
    /// </summary>
    public static class FilterTextBoxHelper
    {
        private static readonly Dictionary<DependencyProperty, object> _highLightPropertiesAndValues = new Dictionary<DependencyProperty, object>()
        {
            { TextElement.BackgroundProperty, new SolidColorBrush(Colors.Yellow) }
        };
        
        private static readonly Dictionary<DependencyProperty, object> _highLightPropertiesAndValuesForTooltips = new Dictionary<DependencyProperty, object>()
        {
            { TextElement.BackgroundProperty, new SolidColorBrush(Colors.DodgerBlue) }
        };
        
        #region Attached properties
        /// <summary>
        /// A filter that allows the identification of words to be highlighted in attached control text elements.
        /// </summary>
        internal static readonly DependencyProperty TextFilterProperty
            = DependencyProperty.RegisterAttached("TextFilter", typeof(string), typeof(FilterTextBoxHelper), new FrameworkPropertyMetadata(default(string), OnFilterPropertyChanged));

        /// <summary>
        /// Gets the value of the <see cref="TextFilterProperty"/> dependency property.
        /// </summary>
        /// <param name="obj">The object that the dependency property is attached to.</param>
        /// <returns>Returns the text filter that is applied to the element.</returns>
        private static string GetTextFilter(DependencyObject obj)
            => (string)obj.GetValue(TextFilterProperty);

        /// <summary>
        /// Sets the value of the <see cref="TextFilterProperty"/> dependency property.
        /// </summary>
        /// <param name="obj">The object that the dependency property is attached to.</param>
        /// <param name="value">The new text filter value to be set.</param>
        private static void SetTextFilter(DependencyObject obj, string value)
            => obj.SetValue(TextFilterProperty, value);
        
        /// <summary>
        /// To be set to true to activate highlighting for each word of <see cref="TextFilterProperty"/>.
        /// </summary>
        internal static readonly DependencyProperty HighlightPerWordProperty
            = DependencyProperty.RegisterAttached("HighlightPerWord", typeof(bool), typeof(FilterTextBoxHelper), new FrameworkPropertyMetadata(default(bool), OnFilterPropertyChanged));

        /// <summary>
        /// Gets the value of the <see cref="HighlightPerWordProperty"/> dependency property.
        /// </summary>
        /// <param name="obj">The object that the dependency property is attached to.</param>
        /// <returns>Returns a value indicating if <see cref="TextFilterProperty"/> words should be used independently.</returns>
        private static bool GetHighlightPerWord(DependencyObject obj)
            => (bool)obj.GetValue(HighlightPerWordProperty);

        /// <summary>
        /// Sets the value of the <see cref="HighlightPerWordProperty"/> dependency property.
        /// </summary>
        /// <param name="obj">The object that the dependency property is attached to.</param>
        /// <param name="value">The new value for the property.</param>
        private static void SetHighlightPerWord(DependencyObject obj, bool value)
            => obj.SetValue(HighlightPerWordProperty, value);
        
        /// <summary>
        /// To be set to true to ignore casing when applying the <see cref="TextFilterProperty"/> filter.
        /// </summary>
        internal static readonly DependencyProperty IgnoreCaseProperty
            = DependencyProperty.RegisterAttached("IgnoreCase", typeof(bool), typeof(FilterTextBoxHelper), new FrameworkPropertyMetadata(default(bool), OnFilterPropertyChanged));

        /// <summary>
        /// Gets the value of the <see cref="IgnoreCaseProperty"/> dependency property.
        /// </summary>
        /// <param name="obj">The object that the dependency property is attached to.</param>
        /// <returns>Returns the text filter that is applied to the element.</returns>
        private static bool GetIgnoreCase(DependencyObject obj)
            => (bool)obj.GetValue(IgnoreCaseProperty);

        /// <summary>
        /// Sets the value of the <see cref="IgnoreCaseProperty"/> dependency property.
        /// </summary>
        /// <param name="obj">The object that the dependency property is attached to.</param>
        /// <param name="value">The new text filter value to be set.</param>
        private static void SetIgnoreCase(DependencyObject obj, bool value)
            => obj.SetValue(IgnoreCaseProperty, value);
        #endregion
        
        /// <summary>
        /// Called whenever the related attached property changes.
        /// </summary>
        /// <param name="sender">The object whose subscribed to the attached property.</param>
        /// <param name="args">Information about property change.</param>
        private static void OnFilterPropertyChanged(object sender, DependencyPropertyChangedEventArgs args)
        {
            if (!(sender is FrameworkElement casted))
                return;

            if (casted.IsLoaded)
                return;
            
            casted.Loaded += TargetElement_Loaded;
        }

        /// <summary>
        /// Called whenever the related target object is loaded. 
        /// </summary>
        /// <param name="sender">The object that loaded.</param>
        /// <param name="e">Information about property change.</param>
        private static void TargetElement_Loaded(object sender, RoutedEventArgs e)
        {
            if (!(sender is FrameworkElement casted))
                return;
            
            casted.Loaded -= TargetElement_Loaded;

            var filter = GetTextFilter(casted);
            var perWordFilter = GetHighlightPerWord(casted);
            var ignoreCase = GetIgnoreCase(casted);

            if (casted is ToolTip toolTip)
                InitializeForTooltip(toolTip, filter, perWordFilter, ignoreCase);
            else
                Initialize(casted, filter, perWordFilter, ignoreCase);
        }
        
        private static void InitializeForTooltip(ToolTip obj, string filter, bool highlightPerFilterWord, bool ignoreCaseWhenFiltering)
        {
            AdaptTextBlockChildren(obj);
            
            var textElements = obj.FindAllChildren<TextElement>().ToArray();
            foreach (var textElement in textElements)
            {
                textElement.FindAndHighlightTextMatches(filter, _highLightPropertiesAndValuesForTooltips, ignoreCaseWhenFiltering, highlightPerFilterWord);
            }
        }

        private static void Initialize(FrameworkElement obj, string filter, bool highlightPerFilterWord, bool ignoreCaseWhenFiltering)
        {
            AdaptTextBlockChildren(obj);
            
            var textElements = obj.FindAllChildren<TextElement>().ToArray();
            foreach (var textElement in textElements)
            {
                textElement.FindAndHighlightTextMatches(filter, _highLightPropertiesAndValues, ignoreCaseWhenFiltering, highlightPerFilterWord);
            }
            
            var allVisualChildren = obj.FindAllChildren<FrameworkElement>().ToArray();
            foreach (var visualChild in allVisualChildren)
            {
                visualChild.ToolTipOpening += (sender, args) => InitializeTooltipStyle(sender as FrameworkElement, filter, highlightPerFilterWord, ignoreCaseWhenFiltering);
            }
        }
        
        private static void AdaptTextBlockChildren(FrameworkElement obj)
        {
            var textBlocks = obj.FindAllChildren<TextBlock>().ToList();
            
            foreach (var textBlock in textBlocks.Distinct())
            {
                textBlock.DecomposeIntoTextElements();
            }
        }
        
        private static void InitializeTooltipStyle(FrameworkElement root, string filter, bool highlightPerFilterWord, bool ignoreCaseWhenFiltering)
        {
            var shouldRestoreAtElementPlace = root.Resources.Contains(typeof(ToolTip));
            var item = root.FindResource(typeof(ToolTip));

            if (!(item is Style itemStyle))
                return;

            var overridenStyle = new Style(itemStyle.TargetType, itemStyle);
            if (shouldRestoreAtElementPlace || itemStyle.Setters.Where(x => x is Setter).Cast<Setter>().Any(x => x.Property == TextFilterProperty))
            {
                root.Resources.Remove(typeof(ToolTip));
                overridenStyle = new Style(itemStyle.TargetType, itemStyle.BasedOn);
            }
            
            overridenStyle.Setters.Add(new Setter(TextFilterProperty, filter));
            overridenStyle.Setters.Add(new Setter(HighlightPerWordProperty, highlightPerFilterWord));
            overridenStyle.Setters.Add(new Setter(IgnoreCaseProperty, ignoreCaseWhenFiltering));
            
            root.Resources.Add(typeof(ToolTip), overridenStyle);

            if (shouldRestoreAtElementPlace)
                root.ToolTipClosing -= OnToolTipClosingRestoreRootTooltipStyle;
            else
                root.ToolTipClosing += OnToolTipClosingRemoveRootTooltipStyle;
        }

        private static void OnToolTipClosingRemoveRootTooltipStyle(object sender, ToolTipEventArgs e)
        {
            if (!(sender is FrameworkElement root))
                return;

            root.Resources.Remove(typeof(ToolTip));
        }

        private static void OnToolTipClosingRestoreRootTooltipStyle(object sender, ToolTipEventArgs e)
        {
            if (!(sender is FrameworkElement root))
                return;

            if (!(root.FindResource(typeof(ToolTip)) is Style itemStyle))
                return;
            
            var overridenStyle = new Style(itemStyle.TargetType, itemStyle);
            var filterSetter = itemStyle.Setters.Where(x => x is Setter).Cast<Setter>().Single(x => x.Property == TextFilterProperty);
            var highlightPerFilterWordSetter = itemStyle.Setters.Where(x => x is Setter).Cast<Setter>().Single(x => x.Property == HighlightPerWordProperty);
            var ignoreCaseWhenFilteringSetter = itemStyle.Setters.Where(x => x is Setter).Cast<Setter>().Single(x => x.Property == IgnoreCaseProperty);

            overridenStyle.Setters.Remove(filterSetter);
            overridenStyle.Setters.Remove(highlightPerFilterWordSetter);
            overridenStyle.Setters.Remove(ignoreCaseWhenFilteringSetter);
            
            root.Resources.Remove(typeof(ToolTip));
            root.Resources.Add(typeof(ToolTip), overridenStyle);
        }
    }
}

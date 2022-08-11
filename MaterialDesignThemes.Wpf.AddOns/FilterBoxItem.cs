using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using EMA.ExtendedWPFVisualTreeHelper;
using MaterialDesignThemes.Wpf.AddOns.Extensions;

namespace MaterialDesignThemes.Wpf.AddOns
{
    /// <summary>
    /// Item for <see cref="FilterBox"/> control.
    /// </summary>
    [TemplatePart(Name = "GridWrapper", Type = typeof(Grid))]
    public class FilterBoxItem : ContentControl
    {
        private static readonly Dictionary<DependencyProperty, object> _highLightPropertiesAndValues = new Dictionary<DependencyProperty, object>()
        {
            { TextElement.BackgroundProperty, new SolidColorBrush(Colors.Yellow) }
        };
        
        /// <summary>
        /// The grid on which the item visual state is attached to.
        /// </summary>
        protected Grid _gridWrapper;

        #region Constructors

        /// <summary>
        /// Creates a new instance of <see cref="FilterBoxItem"/>.
        /// </summary>
        /// <param name="initialFilter">Initial filter to be applied on the item.</param>
        /// <param name="highlightPerFilterWord">Indicates if highlight must occur on a per filter word basis or on the whole filter string.</param>
        /// <param name="ignoreCaseWhenFiltering">If true, ignores casing during filtering.</param>
        public FilterBoxItem(string initialFilter, bool highlightPerFilterWord, bool ignoreCaseWhenFiltering)
        {
            Loaded += (_, unused) => Initialize(initialFilter, highlightPerFilterWord, ignoreCaseWhenFiltering);
        }

        /// <summary>
        /// Static constructor for <see cref="FilterBoxItem"/> type.
        /// </summary>
        static FilterBoxItem()
        {
            DefaultStyleKeyProperty.OverrideMetadata(typeof(FilterBoxItem), new FrameworkPropertyMetadata(typeof(FilterBoxItem)));
        }
        #endregion

        #region Initialization with Filtering
        /// <summary>
        /// Occurs on template application.
        /// </summary>
        /// <exception cref="Exception">Thrown when a template part cannot be found.</exception>
        public override void OnApplyTemplate()
        {
            base.OnApplyTemplate();

            var grid = Template.FindName("GridWrapper", this);
            if (grid == null || (_gridWrapper = grid as Grid) == null)
                throw new Exception(nameof(FilterBoxItem) + " template must contain a Grid named 'GridWrapper'.");
        }
        
        /// <summary>
        /// Initializes the <see cref="FilterBoxItem"/>.
        /// </summary>
        /// <param name="filter">Initial filter to be applied on the item.</param>
        /// <param name="highlightPerFilterWord">Indicates if highlight must occur on a per filter word basis or on the whole filter string.</param>
        /// <param name="ignoreCaseWhenFiltering">If true, ignores casing during filtering.</param>
        protected void Initialize(string filter, bool highlightPerFilterWord, bool ignoreCaseWhenFiltering)
        {
            AdaptTextBlockChildren();

            var textElements = this.FindAllChildren<TextElement>().ToArray();
            foreach (var textElement in textElements)
            {
                textElement.FindAndHighlightTextMatches(filter, _highLightPropertiesAndValues, ignoreCaseWhenFiltering, highlightPerFilterWord);
            }
        }

        private void AdaptTextBlockChildren()
        {
            var textBlocks = this.FindAllChildren<TextBlock>().ToList();

            for (var i = 0; i < VisualChildrenCount; i++)
            {
                var visualChild = GetVisualChild(i);
                textBlocks.AddRange(visualChild.FindAllChildren<TextBlock>());
            }

            foreach (var textBlock in textBlocks.Distinct())
            {
                textBlock.DecomposeIntoTextElements();
            }
        }
        #endregion
    }
}

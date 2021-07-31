using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Documents;

namespace MaterialDesignThemes.Wpf.AddOns.Extensions
{
    /// <summary>
    /// Extensions for <see cref="TextElement"/> derived classes.
    /// </summary>
    public static class TextElementExtensions
    {
        /// <summary>
        /// Finds a specific string in a <see cref="TextElement"/> and highlights it using passed properties.
        /// </summary>
        /// <param name="textElement">Base text element.</param>
        /// <param name="filter">String filter to be passed.</param>
        /// <param name="highlightPropertiesAndValues">A dictionary holding pair(s) of dependency property/value to be applied on string parts that pass the filter.</param>
        /// <param name="ignoreCase">If set to true, string comparison with filter ignores casing.</param>
        /// <returns>True if the text element holds one or several matches, false otherwise.</returns>
        /// <remarks>Highlighting might create new <see cref="Run"/> items. Use <see cref="ClearAppliedProperties"/> to reset the text element to its initial state.</remarks>
        public static bool FindAndHighlightTextMatches(this TextElement textElement, string filter, IDictionary<DependencyProperty, object> highlightPropertiesAndValues, bool ignoreCase)
        {
            var range = new TextRange(textElement.ContentStart, textElement.ContentEnd);
            range.ClearAllProperties();
            
            if (string.IsNullOrWhiteSpace(filter))
                return true;  // always true is filter is unset

            var isMatch = false;
            var comparisonType = ignoreCase ? StringComparison.InvariantCultureIgnoreCase : StringComparison.InvariantCulture;
            var start = textElement.ContentStart;
            var end = textElement.ContentEnd;

            for (var pointer = start; pointer.CompareTo(end) <= 0; pointer = pointer.GetNextContextPosition(LogicalDirection.Forward) ?? end)
            {
                if (pointer.CompareTo(end) == 0)
                    break;
                
                var subText = pointer.GetTextInRun(LogicalDirection.Forward);
                
                var matchingIndex = subText.IndexOf(filter, comparisonType);
                if (matchingIndex < 0)
                    continue;

                isMatch = true;
                    
                var matchStart = pointer.GetPositionAtOffset(matchingIndex);
                if (matchStart == null)
                    continue;
                
                var matchEnd = pointer.GetPositionAtOffset(matchingIndex + filter.Length);
                if (matchEnd == null)
                    continue;
                
                var matchText = new TextRange(matchStart, matchEnd);
                if (string.IsNullOrEmpty(matchText.Text))
                    continue;
                
                foreach (var (property, value) in highlightPropertiesAndValues.Select(x => (x.Key, x.Value)))
                {
                    matchText.ApplyPropertyValue(property, value);
                }
            }

            return isMatch;
        }
        
        /// <summary>
        /// Clears properties that were previously applied using the <see cref="TextElement"/>'s ApplyPropertyValue method.
        /// </summary>
        /// <param name="textElement">Base text element.</param>
        /// <remarks>Should be used with the <see cref="FindAndHighlightTextMatches"/> method to clear any highlights.</remarks>
        public static void ClearAppliedProperties(this TextElement textElement)
        {
            var range = new TextRange(textElement.ContentStart, textElement.ContentEnd);
            range.ClearAllProperties();
        }
    }
}

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
        /// <param name="splitFilterIntoWords">If set to true, split filter into words and find matches to item content words starting with these filters.</param>
        /// <returns>True if the text element holds one or several matches, false otherwise.</returns>
        /// <remarks>Highlighting might create new <see cref="Run"/> items. Use <see cref="ClearAppliedProperties"/> to reset the text element to its initial state.</remarks>
        public static bool FindAndHighlightTextMatches(this TextElement textElement, string filter, IDictionary<DependencyProperty, object> highlightPropertiesAndValues, bool ignoreCase, bool splitFilterIntoWords = false)
        {
            var range = new TextRange(textElement.ContentStart, textElement.ContentEnd);
            range.ClearAllProperties();

            if (string.IsNullOrWhiteSpace(filter))
                return true;  // always true is filter is unset

            var start = textElement.ContentStart;
            var end = textElement.ContentEnd;

            if (!splitFilterIntoWords)
                return FindAndHighlightText(start, end, filter, ignoreCase, false, highlightPropertiesAndValues);
            
            var filterWords = filter.Split(Array.Empty<char>(), StringSplitOptions.RemoveEmptyEntries);
            if (filterWords.Length > 0 && FilterWordsMatch(filterWords, range, ignoreCase))
            {
                var isMatch = false;

                foreach (var filterWord in filterWords)
                {
                    isMatch |= FindAndHighlightText(start, end, filterWord, ignoreCase, true, highlightPropertiesAndValues);
                }

                if (isMatch)
                    return true;
                
                range.ClearAllProperties();
            }

            return FindAndHighlightText(start, end, filter, ignoreCase, false, highlightPropertiesAndValues);
        }

        private static bool FilterWordsMatch(IEnumerable<string> filterWords, TextRange toCheck, bool ignoreCase)
            => filterWords.Any(x => !string.IsNullOrWhiteSpace(x) && DoesValueMatchFilter(toCheck.Text, x, ignoreCase));
        
        private static StringComparison GetComparison(bool ignoreCase)
            => ignoreCase ? StringComparison.CurrentCultureIgnoreCase : StringComparison.CurrentCulture;
        
        private static bool DoesValueMatchFilter(string value, string filter, bool ignoreCase)
            => value?.IndexOf(filter, GetComparison(ignoreCase)) >= 0;

        private static bool FindAndHighlightText(TextPointer start, TextPointer end, string filter, bool ignoreCase, bool firstLettersOnly, IDictionary<DependencyProperty, object> highlightPropertiesAndValues)
        {
            var comparisonType = GetComparison(ignoreCase);

            var isMatch = false;
            for (var pointer = start; pointer.CompareTo(end) <= 0; pointer = pointer.GetNextContextPosition(LogicalDirection.Forward) ?? end)
            {
                if (pointer.CompareTo(end) == 0)
                    break;

                var text = pointer.GetTextInRun(LogicalDirection.Forward);

                if (firstLettersOnly)
                {
                    for (var i = 0; i < text.Length; i++)
                    {
                        if (char.IsWhiteSpace(text[i]))
                            continue;

                        var subText = text.Remove(0, i).Split(Array.Empty<char>()).FirstOrDefault();

                        if (string.IsNullOrEmpty(subText))
                            continue;

                        if (subText.IndexOf(filter, comparisonType) == 0)
                        {

                            HighLightWords(pointer, i, filter, highlightPropertiesAndValues);
                            return true;
                        }

                        i += subText.Length - 1;
                    }
                }
                else
                {
                    var matchingIndex = text.IndexOf(filter, comparisonType);
                    if (matchingIndex < 0)
                        continue;

                    isMatch = true;
                    HighLightWords(pointer, matchingIndex, filter, highlightPropertiesAndValues);
                }
            }

            return isMatch;
        }

        private static void HighLightWords(TextPointer pointer, int matchingIndex, string filter, IDictionary<DependencyProperty, object> highlightPropertiesAndValues)
        {
            var matchStart = pointer.GetPositionAtOffset(matchingIndex);
            if (matchStart == null)
                return;

            var matchEnd = pointer.GetPositionAtOffset(matchingIndex + filter.Length);
            if (matchEnd == null)
                return;

            var matchText = new TextRange(matchStart, matchEnd);
            if (string.IsNullOrEmpty(matchText.Text))
                return;

            foreach (var (property, value) in highlightPropertiesAndValues.Select(x => (x.Key, x.Value)))
            {
                matchText.ApplyPropertyValue(property, value);
            }
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

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;

namespace MaterialDesignThemes.Wpf.AddOns.Utils.Filtering
{
    /// <summary>
    /// Provides a comparison mechanism to match a filter against an item 
    /// property values.
    /// </summary>
    public static class TextFilter
    {
        internal static bool IsItemMatchingFilter(object item, 
                                                  IEnumerable<PropertyDescriptor> propertyMemberPaths, 
                                                  string filter, 
                                                  bool ignoreCase = false,
                                                  bool matchFilterWordsWithFirstWordLetters = false,
                                                  bool matchFilterWordsFirstWordLettersAcrossProperties = false)
        {
            if (string.IsNullOrEmpty(filter))
                return true;

            if (item == null)
                return true;

            var filterWords = GetWords(filter);
            var matchingFilterAndWords = new List<(string ItemWord, string FilterWord, PropertyDescriptor Property)>();
            foreach (var propertyPath in propertyMemberPaths)
            {
                if (!GetString(item, propertyPath, out var value))
                    continue;

                if (DoesValueMatchFilter(value, filter, ignoreCase))
                    return true;

                if (!matchFilterWordsWithFirstWordLetters && !matchFilterWordsFirstWordLettersAcrossProperties)
                    continue;
                
                if (!DoesValueMatchAnyFilterWords(value, filterWords, ignoreCase))
                    continue;

                var valueWords = GetWords(value);

                foreach (var word in valueWords)
                {
                    foreach (var filterWord in filterWords)
                    {
                        if (DoesStartOfAlphanumericalWordValueMatchFilter(word, filterWord, ignoreCase))
                            matchingFilterAndWords.Add((word, filterWord, propertyPath));
                    }
                }
            }

            if (matchFilterWordsWithFirstWordLetters)
            {
                foreach (var match in matchingFilterAndWords.GroupBy(x => x.Property))
                {
                    var matchingFilterWords = match.Select(x => x.FilterWord).Distinct().ToArray();
                    if (OrderedSequencesAreEqual(filterWords, matchingFilterWords))
                        return true;
                }
            }

            if (matchFilterWordsFirstWordLettersAcrossProperties)
            {
                var matchingFilterWords = matchingFilterAndWords.Select(x => x.FilterWord).Distinct().ToArray();
                if (matchingFilterWords.Length.Equals(filterWords.Length))
                    return true;
            }
            
            return false;
        }

        private static string[] GetWords(string input)
            => input?.Split(Array.Empty<char>(), StringSplitOptions.RemoveEmptyEntries).ToArray();
        
        private static bool GetString(object source, PropertyDescriptor propertyPath, out string value)
        {
            value = null;
            if (!(propertyPath.GetValue(source) is string valueAsString))
                return false;
            
            value = valueAsString;
            return true;
        }
        
        private static bool DoesValueMatchFilter(string value, string filter, bool ignoreCase)
            => value.IndexOf(filter, GetComparison(ignoreCase)) >= 0;
        
        private static bool DoesValueMatchAnyFilterWords(string value, IEnumerable<string> filterWords, bool ignoreCase)
            => filterWords.Any(filterWord => DoesValueMatchFilter(value, filterWord, ignoreCase));
        
        private static bool DoesStartOfAlphanumericalWordValueMatchFilter(string value, string filter, bool ignoreCase)
        {
            var index = value.IndexOf(filter, GetComparison(ignoreCase));
            if (index < 0)
                return false;
            
            if (index == 0)
                return true;

            var count = 0;
            while (count < index && !char.IsLetterOrDigit(value[count]))
            {
                count++;
            }

            return count == index;
        }
        
        private static StringComparison GetComparison(bool ignoreCase)
            => ignoreCase ? StringComparison.CurrentCultureIgnoreCase : StringComparison.CurrentCulture;
        
        private static bool OrderedSequencesAreEqual(string[] reference, string[] toCompare)
        {
            if (!reference.Length.Equals(toCompare.Length))
                return false;

            return !reference.Where((t, i) => t != toCompare[i]).Any();
        }
    }
}

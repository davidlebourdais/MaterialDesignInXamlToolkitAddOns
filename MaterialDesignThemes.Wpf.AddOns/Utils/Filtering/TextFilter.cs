using System;
using System.Collections.Generic;
using System.Linq;
using MaterialDesignThemes.Wpf.AddOns.Utils.Reflection;

namespace MaterialDesignThemes.Wpf.AddOns.Utils.Filtering
{
    /// <summary>
    /// Provides a comparison mechanism to match a filter against an item
    /// property values.
    /// </summary>
    public static class TextFilter
    {
        internal static bool IsItemMatchingFilter(object item,
                                                  IEnumerable<PropertyGetter> propertyGetters,
                                                  string filter,
                                                  bool ignoreCase = false,
                                                  bool matchFilterWordsWithFirstWordLetters = false,
                                                  bool matchFilterWordsFirstWordLettersAcrossProperties = false,
                                                  bool convertValueToString = false)
        {
            if (string.IsNullOrEmpty(filter))
                return true;

            if (item == null)
                return true;

            var filterWords = GetWords(filter);
            var matchingFilterAndWords = new List<(string ItemWord, string FilterWord, PropertyGetter Property)>();
            foreach (var propertyGetter in propertyGetters)
            {
                if (!GetString(item, propertyGetter, convertValueToString, out var value))
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
                        if (!DoesStartOfAlphanumericalWordValueMatchFilter(word, filterWord, ignoreCase))
                            continue;

                        if (matchingFilterAndWords.Any(x => string.Equals(x.ItemWord, word, StringComparison.Ordinal)
                                                            && x.FilterWord != filterWord
                                                            && StartsWithNonAlphaNumericalLetters(x.FilterWord)))
                            continue;

                        matchingFilterAndWords.Add((word, filterWord, propertyGetter));
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

        private static bool GetString(object source, PropertyGetter propertyGetter, bool convertValueToString, out string value)
        {
            value = null;

            var valueAsString = propertyGetter.GetValue(source) as string;

            if (valueAsString == null && convertValueToString)
                valueAsString = propertyGetter.GetValue(source)?.ToString();

            if (valueAsString == null)
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

        private static bool StartsWithNonAlphaNumericalLetters(string word)
        {
            if (word.Length == 0)
                return false;

            return !char.IsLetterOrDigit(word[0]);
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

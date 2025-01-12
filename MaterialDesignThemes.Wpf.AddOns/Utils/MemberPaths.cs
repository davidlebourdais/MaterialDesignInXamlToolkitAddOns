using System;
using System.Collections.Generic;
using System.Linq;

namespace MaterialDesignThemes.Wpf.AddOns.Utils
{
    /// <summary>
    /// Provides utility methods to manipulate property member paths.
    /// </summary>
    public static class MemberPaths
    {
        /// <summary>
        /// Extracts a list of items from a string or collection of strings separated by a character.
        /// </summary>
        /// <param name="input">A collection of strings or a character separated list of items in a string.</param>
        /// <param name="separator">The separator to be considered when the input is a string.</param>
        /// <returns>A collection of strings after extraction.</returns>
        public static string[] ExtractFromCollectionOrCharacterSeparatedInput(object input, char separator = ',')
        {
            IEnumerable<string> result = null;

            switch (input)
            {
                case IEnumerable<string> asCollection:
                    result = asCollection.ToArray();
                    break;
                case string asString:
                    result = asString.Split(separator).ToArray();
                    break;
            }

            result = result?.Where(x => !string.IsNullOrWhiteSpace(x)).Select(x => x.Trim()).ToArray();

            if (result?.Any(x => x.Contains(separator)) == true)
                throw new ArgumentException("Member path must be a collection of strings or set of comma separated strings or a .");

            return result?.ToArray() ?? Array.Empty<string>();
        }
    }
}

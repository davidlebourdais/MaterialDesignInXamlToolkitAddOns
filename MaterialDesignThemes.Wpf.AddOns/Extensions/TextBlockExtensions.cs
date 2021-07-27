using System.Windows.Controls;
using System.Windows.Documents;

namespace MaterialDesignThemes.Wpf.AddOns.Extensions
{
    /// <summary>
    /// Extensions for <see cref="TextBlock"/> elements.
    /// </summary>
    public static class TextBlockExtensions
    {
        /// <summary>
        /// Breaks a <see cref="TextBlock"/> content down into <see cref="TextElement"/> objects.
        /// </summary>
        /// <param name="textBlock">Source text block.</param>
        public static void DecomposeIntoTextElements(this TextBlock textBlock)
        {
            var span = new Span();
            while (textBlock.Inlines.Count > 0)
            {
                span.Inlines.Add(textBlock.Inlines.FirstInline);
            }

            textBlock.Inlines.Add(span);
        }
    }
}

using System.Windows.Controls;

namespace MaterialDesignThemes.Wpf.AddOns.Utils.Caching
{
    /// <summary>
    /// A helper class to store <see cref="TextBox"/> information
    /// for later reload.
    /// </summary>
    public class TextBoxCache
    {
        private bool _isCached;
        private string _text;
        private int _caretIndex;
        private int _selectionStart;
        private int _selectionLength;
        
        /// <summary>
        /// Store information about a given <see cref="TextBox"/>
        /// in the cache.
        /// </summary>
        /// <param name="textBox">The <see cref="TextBox"/> to store information from.</param>
        public void Cache(TextBox textBox)
        {
            _text = textBox.Text;
            _caretIndex = textBox.CaretIndex;
            _selectionStart = textBox.SelectionStart;
            _selectionLength = textBox.SelectionLength;
            _isCached = true;
        }
        
        /// <summary>
        /// Updates a given <see cref="TextBox"/> with cached information.
        /// </summary>
        /// <param name="textBox">The <see cref="TextBox"/> to be updated.</param>
        public void SetFromCache(TextBox textBox)
        {
            if (!_isCached)
                return;
            
            textBox.Text = _text;
            textBox.CaretIndex = _caretIndex;
            textBox.SelectionStart = _selectionStart;
            textBox.SelectionLength = _selectionLength;
        }

        /// <summary>
        /// Clears the current cached information.
        /// </summary>
        public void ClearCache()
        {
            _isCached = false;
        }
    }
}

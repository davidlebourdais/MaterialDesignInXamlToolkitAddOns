using System;
using System.ComponentModel;

namespace EMA.MaterialDesignInXAMLExtender.Utils
{
    /// <summary>
    /// Represents an object that can be checked.
    /// </summary>
    public class CheckMark : INotifyPropertyChanged, IComparable
    {
        private static readonly CheckedStateChangedEventArgs toTrueArgs = new CheckedStateChangedEventArgs(true);  // stores "created once" args to inform true value was selected.
        private static readonly CheckedStateChangedEventArgs toFalseArgs = new CheckedStateChangedEventArgs(false);  // stores "created once" args to inform false value was selected.
        private static readonly PropertyChangedEventArgs propertyChangedArgs = new PropertyChangedEventArgs(nameof(IsChecked)); // stores property changed event args.

        private bool _is_checked = true;   // stores the checked value.

        /// <summary>
        /// Triggered with <see cref="IsChecked"/> state changes.
        /// </summary>
        public event CheckedStateChangedEventHandler IsCheckedChanged;

        /// <summary>
        /// Triggered with <see cref="IsChecked"/> state changes, stores less 
        /// info than <see cref="IsCheckedChanged"/> but enables UI Databinding.
        /// </summary>
        public event PropertyChangedEventHandler PropertyChanged;

        /// <summary>
        /// Gets or sets the checked state.
        /// </summary>
        public bool IsChecked
        {
            get => _is_checked;
            set
            {
                if (_is_checked != value)
                {
                    _is_checked = value;
                    IsCheckedChanged?.Invoke(this, value? toTrueArgs : toFalseArgs);
                    PropertyChanged?.Invoke(this, propertyChangedArgs);
                }
            }
        }

        /// <summary>
        /// Returns a string representation of item.
        /// </summary>
        /// <returns>The current <see cref="IsChecked"/> value.</returns>
        public override string ToString() => IsChecked.ToString();

        /// <summary>
        /// Compares this object with another one, returning -1 if current is less than
        /// comparison object, +1 is more and 0 is equal.
        /// Comparison rule is: IsChecked = true > false or null.
        /// </summary>
        /// <param name="obj">A <see cref="CheckMark"/> to be compared.</param>
        /// <returns>Comparison result.</returns>
        public int CompareTo(object obj)
        {
            if (!(obj is CheckMark check))
                return 1;
            else if (IsChecked == check.IsChecked)
                return 0;
            else return IsChecked ? 1 : -1;
        }
    }
}

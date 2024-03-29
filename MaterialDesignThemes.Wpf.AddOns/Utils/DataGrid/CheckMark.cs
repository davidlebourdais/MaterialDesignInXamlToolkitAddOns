﻿using System;
using System.ComponentModel;

namespace MaterialDesignThemes.Wpf.AddOns.Utils.DataGrid
{
    /// <summary>
    /// Represents an object that can be checked.
    /// </summary>
    public class CheckMark : INotifyPropertyChanged, IComparable
    {
        private static readonly CheckedStateChangedEventArgs _toTrueArgs = new CheckedStateChangedEventArgs(true);  // stores "created once" args to inform true value was selected.
        private static readonly CheckedStateChangedEventArgs _toFalseArgs = new CheckedStateChangedEventArgs(false);  // stores "created once" args to inform false value was selected.
        private static readonly PropertyChangedEventArgs _propertyChangedArgs = new PropertyChangedEventArgs(nameof(IsChecked));  // stores property changed event args.

        private bool _isChecked = true;   // stores the checked value.

        /// <summary>
        /// Triggered with <see cref="IsChecked"/> state changes.
        /// </summary>
        public event CheckedStateChangedEventHandler IsCheckedChanged;

        /// <summary>
        /// Triggered with <see cref="IsChecked"/> state changes, stores less 
        /// info than <see cref="IsCheckedChanged"/> but enables UI DataBinding.
        /// </summary>
        public event PropertyChangedEventHandler PropertyChanged;

        /// <summary>
        /// Gets or sets the checked state.
        /// </summary>
        public bool IsChecked
        {
            get => _isChecked;
            set
            {
                if (_isChecked == value)
                    return;
                
                _isChecked = value;
                IsCheckedChanged?.Invoke(this, value? _toTrueArgs : _toFalseArgs);
                PropertyChanged?.Invoke(this, _propertyChangedArgs);
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
            if (IsChecked == check.IsChecked)
                return 0;
            return IsChecked ? 1 : -1;
        }
    }
}

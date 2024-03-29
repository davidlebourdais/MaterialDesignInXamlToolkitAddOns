﻿using System;

namespace MaterialDesignThemes.Wpf.AddOns.Utils.DataGrid
{
    /// <summary>
    /// A delegate to be used to notify a check state change.
    /// </summary>
    /// <param name="sender">The object that sent the event.</param>
    /// <param name="e">The event parameters.</param>
    public delegate void CheckedStateChangedEventHandler(object sender, CheckedStateChangedEventArgs e);

    /// <summary>
    /// A class to store checked changed event information.
    /// </summary>
    public class CheckedStateChangedEventArgs : EventArgs
    {
        /// <summary>
        /// Gets the new boolean checked state of the object that sent the event.
        /// </summary>
        public bool? NewCheckedState { get; }

        /// <summary>
        /// Initiates a new instance of <see cref="CheckedStateChangedEventArgs"/>.
        /// </summary>
        /// <param name="newState">A new checked state to be stored.</param>
        public CheckedStateChangedEventArgs(bool? newState) : base()
        {
            NewCheckedState = newState;
        }
    }
}

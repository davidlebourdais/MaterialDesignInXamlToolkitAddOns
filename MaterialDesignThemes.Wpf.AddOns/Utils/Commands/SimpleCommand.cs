using System;
using System.Windows.Input;

namespace MaterialDesignThemes.Wpf.AddOns.Utils
{
    /// <summary>
    /// Provides a simple implementation of <see cref="ICommand"/> for internal usage.
    /// </summary>
    /// <remarks>Has no execution parameters.</remarks>
    internal class SimpleCommand : SimpleParameterizedCommand
    {
        /// <summary>
        /// Initiates a new instance of <see cref="SimpleCommand"/>.
        /// </summary>
        /// <param name="execute">A function the command should execute.</param>
        /// <param name="canExecute">Optional function to check if command can execute (always true if null).</param>
        public SimpleCommand(Action execute, Func<bool> canExecute = null) 
            : base ((execute == null ? (Action<object>)null : (param) => execute()), canExecute)
        {   }
    }
}
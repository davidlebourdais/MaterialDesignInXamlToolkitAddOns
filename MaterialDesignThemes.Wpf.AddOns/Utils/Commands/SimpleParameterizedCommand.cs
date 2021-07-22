using System;
using System.Windows.Input;

namespace MaterialDesignThemes.Wpf.AddOns.Utils
{
    /// <summary>
    /// Provides a simple implementation of <see cref="ICommand"/> for internal usage.
    /// </summary>
    /// <remarks>Supports parameter.</remarks>
    internal class SimpleParameterizedCommand : ICommand
    {
        /// <summary>
        /// Stores the function to be executed by the command.
        /// </summary>
        protected readonly Func<bool> _canExecute;

        /// <summary>
        /// Stores the external function to be called to check if command can be executed.
        /// </summary>
        protected readonly Action<object> _execute;

        /// <summary>
        /// Initiates a new instance of <see cref="SimpleParameterizedCommand"/>.
        /// </summary>
        /// <param name="execute">A function the command should execute.</param>
        /// <param name="canExecute">Optional function to check if command can execute (always true if null).</param>
        public SimpleParameterizedCommand(Action<object> execute, Func<bool> canExecute = null)
        {
            _execute = execute;
            _canExecute = canExecute;
        }

        /// <inheritdoc />
        public bool CanExecute(object parameter)
        {
            return _canExecute == null || _canExecute();
        }

        /// <inheritdoc />
        public void Execute(object parameter)
        {
            _execute(parameter);
        }

        /// <inheritdoc />
        public event EventHandler CanExecuteChanged
        {
            add => CommandManager.RequerySuggested += value;
            remove => CommandManager.RequerySuggested -= value;
        }

        /// <summary>
        /// Causes <see cref="CanExecute"/> invocation.
        /// </summary>
        public void RaiseCanExecuteChanged()
        {
            CommandManager.InvalidateRequerySuggested();
        }
    }
}
using System;
using System.Windows.Input;

namespace EMA.MaterialDesignInXAMLExtender.Utils
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
        protected readonly Func<bool> can_execute;

        /// <summary>
        /// Stores the external function to be called to check if command can be executed.
        /// </summary>
        protected readonly Action<object> execute;

        /// <summary>
        /// Initiates a new instance of <see cref="SimpleParameterizedCommand"/>.
        /// </summary>
        /// <param name="execute">A function the command should execute.</param>
        /// <param name="can_execute">Optionnal function to check if command can execute (always true if null).</param>
        public SimpleParameterizedCommand(Action<object> execute, Func<bool> can_execute = null)
        {
            this.execute = execute;
            this.can_execute = can_execute;
        }

        /// <inheritdoc />
        public bool CanExecute(object parameter)
        {
            return can_execute == null || can_execute();
        }

        /// <inheritdoc />
        public void Execute(object parameter)
        {
            execute(parameter);
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
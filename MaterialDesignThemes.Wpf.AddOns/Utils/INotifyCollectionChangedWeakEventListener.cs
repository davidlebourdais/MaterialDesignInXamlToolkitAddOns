using System;
using System.Collections;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Windows;
using System.Windows.Threading;

namespace MaterialDesignThemes.Wpf.AddOns.Utils
{
    /// <summary>
    /// A listener for <see cref="INotifyCollectionChanged"/> event that can be paused and disposed.
    /// </summary>
    public class CollectionChangedWeakEventListener : IWeakEventListener, IDisposable
    {
        private IEnumerable _collection;
        private readonly Action<IEnumerable> _onChange;
        private bool _reentrancyDisabled;
        private bool _isPaused;

        /// <summary>
        /// Constructs a new <see cref="CollectionChangedWeakEventListener"/> on a  <see cref="INotifyCollectionChanged"/> object.
        /// </summary>
        /// <param name="collection">The collection to listen for changed events.</param>
        /// <param name="onChange">A method that will be called whenever the collection changes.</param>
        public CollectionChangedWeakEventListener(IEnumerable collection, Action<IEnumerable> onChange)
        {
            _collection = collection;
            _onChange = onChange;
            
            if (collection is INotifyCollectionChanged asINotifyPropertyChanged)
                CollectionChangedEventManager.AddListener(asINotifyPropertyChanged, this);
        }
        
        /// <summary>
        /// Disables the current listener.
        /// </summary>
        /// <remarks>Reactivation must be performed through <see cref="Resume"/> method.</remarks>
        public void Pause() => _isPaused = true;
        
        /// <summary>
        /// Reactivates the current listener after a call to <see cref="Pause"/> method.
        /// </summary>
        public void Resume() => _isPaused = false;

        /// <summary>
        /// Disposes the currency listener.
        /// </summary>
        public void Dispose()
        {
            if (_collection is INotifyCollectionChanged asINotifyPropertyChanged)
                CollectionChangedEventManager.RemoveListener(asINotifyPropertyChanged, this);
            _collection = null;
        }

        /// <summary>
        /// Called if source is notifiable and its collection or a property changed.
        /// </summary>
        /// <param name="managerType">Type of the manager we subscribed to.</param>
        /// <param name="sender">The object that sent the event.</param>
        /// <param name="e">Information about the event.</param>
        /// <returns>True if was able to perform the required operation.</returns>
        /// <remarks><see cref="IWeakEventListener"/> implementation.</remarks>
        public bool ReceiveWeakEvent(Type managerType, object sender, EventArgs e)
        {
            if (_isPaused)
                return true;
            
            if (managerType == typeof(PropertyChangedEventManager) || managerType == typeof(CollectionChangedEventManager))
            {
                if (_collection != null && !_reentrancyDisabled)
                {
                    _reentrancyDisabled = true;
                    
                    var dispatcher = Application.Current?.Dispatcher ?? Dispatcher.CurrentDispatcher;
                    dispatcher.BeginInvoke(DispatcherPriority.Background, (Action) (() =>
                    {
                        try
                        {
                            if (!_isPaused)
                                _onChange?.Invoke(_collection);
                        }
                        finally
                        {
                            _reentrancyDisabled = false;
                        }
                    }));
                }
                else if (_collection == null && sender is INotifyCollectionChanged collection)
                    CollectionChangedEventManager.RemoveListener(collection, this);
            }

            return true; // must be true otherwise will generate an exception.
        }
    }
}

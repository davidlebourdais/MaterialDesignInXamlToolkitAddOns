using System;
using System.Collections.Specialized;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace EMA.MaterialDesignInXAMLExtender
{
    /// <summary>
    /// Implements helpers for <see cref="ScrollViewer"/> controls.
    /// </summary>
    public static class ScrollViewerAssist
    {
        #region Scroll to end when item is added/removed from within internal items control
        /// <summary>
        /// To be set to true to activate this helper.
        /// </summary>
        public static readonly DependencyProperty ScrollsToEndProperty
            = DependencyProperty.RegisterAttached("ScrollsToEnd", typeof(bool), typeof(ScrollViewerAssist), new FrameworkPropertyMetadata(default(bool), OnScrollsToEndChanged));

        /// <summary>
        /// Gets the value of the <see cref="ScrollsToEndProperty"/> dependency property.
        /// </summary>
        /// <param name="obj">The object that the dependency property is attached to.</param>
        /// <returns>Returns true if scrollviewer must scroll to end automaticaly, false otherwise.</returns>
        [AttachedPropertyBrowsableForType(typeof(ScrollViewer))]
        public static bool GetScrollsToEnd(DependencyObject obj)
        {
            return (bool)obj.GetValue(ScrollsToEndProperty);
        }

        /// <summary>
        /// Sets the value of the <see cref="ScrollsToEndProperty"/> dependency property.
        /// </summary>
        /// <param name="obj">The object that the dependency property is attached to.</param>
        /// <param name="value">The new value to be set.</param>
        [AttachedPropertyBrowsableForType(typeof(ScrollViewer))]
        public static void SetScrollsToEnd(DependencyObject obj, bool value)
        {
            obj.SetValue(ScrollsToEndProperty, value);
        }

        /// <summary>
        /// Called whenever the related attached property changes.
        /// </summary>
        /// <param name="sender">The object whose subscribed to the attached property.</param>
        /// <param name="args">Information about property change.</param>
        private static void OnScrollsToEndChanged(object sender, DependencyPropertyChangedEventArgs args)
        {
            if (sender is ScrollViewer scrollViewer)
            {
                if (!scrollViewer.IsLoaded)
                {
                    scrollViewer.Loaded += ScrollViewer_Loaded;
                    return;
                }

                // Find any items control within:
                if (FindChild<ItemsControl>(scrollViewer) is ItemsControl itemscontrol)
                {
                    // Create a weak ref to scroll viewer and affect it to collection change event,
                    // so we scroll this peculiar scrollviewer anytime the inner collection changes:
                    var weakSender = new WeakReference(scrollViewer);

                    // Build related event:
                    NotifyCollectionChangedEventHandler oncollectionchanged = (s, e) =>
                    {
                        if (weakSender.IsAlive)
                        {
                            (weakSender.Target as ScrollViewer).ScrollToRightEnd();
                            (weakSender.Target as ScrollViewer).ScrollToBottom();
                        }
                    };

                    // Subscribe on current items:
                    if (itemscontrol.Items != null && itemscontrol.Items is INotifyCollectionChanged inotify1)
                    {
                        inotify1.CollectionChanged += oncollectionchanged;

                        // Invoke now for init:
                        oncollectionchanged.Invoke(itemscontrol, new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Reset));
                    }

                    // And subscribe on future items:
                    itemscontrol.DataContextChanged +=
                        (s1, e1) =>
                        {
                            if (itemscontrol.Items != null && itemscontrol.Items is INotifyCollectionChanged inotify2)
                                inotify2.CollectionChanged += oncollectionchanged;
                        };
                }
            }
        }

        /// <summary>
        /// Called whenever the related scrollviewer is loaded. 
        /// Allows to recall the <see cref="OnScrollsToEndChanged"/> method in a more proper context.
        /// </summary>
        /// <param name="sender">The scrollviewer that loaded.</param>
        /// <param name="e">Information about property change.</param>
        private static void ScrollViewer_Loaded(object sender, RoutedEventArgs e)
        {
            if (sender is ScrollViewer casted)
            {
                casted.Loaded -= ScrollViewer_Loaded;
                OnScrollsToEndChanged(casted, new DependencyPropertyChangedEventArgs());
            }
        }

        /// <summary>
        /// Finds a child of in the visual tree using its type and (optionnaly) its name.
        /// </summary>
        /// <typeparam name="T">The type of the queried item.</typeparam>
        /// <param name="startNode">The node where to start looking from.</param>
        /// <param name="child_name">Name of the child to find.</param>
        /// <returns>A matching child, or null if none existing.</returns>
        /// <remarks>Adapted from https://stackoverflow.com/questions/636383/how-can-i-find-wpf-controls-by-name-or-type </remarks>
        private static T FindChild<T>(DependencyObject startNode, string child_name = null)
        {
            if (startNode == null) return default;

            var childrenCount = VisualTreeHelper.GetChildrenCount(startNode);
            for (int i = 0; i < childrenCount; i++)
            {
                var child = VisualTreeHelper.GetChild(startNode, i);

                // If the child is not of the requested type:
                if (!(child is T typedChild))
                {
                    // Continue to explore:
                    var foundChild = FindChild<T>(child, child_name);

                    // If the child is found, just return:
                    if (foundChild != null) return foundChild;
                }
                // Child of correct type, now check name if required:
                else if (!string.IsNullOrEmpty(child_name))
                {
                    // If the child's name is correct, return result:
                    if (child is FrameworkElement frameworkElement && frameworkElement.Name == child_name)
                        return typedChild;
                    else
                    {
                        // Continue to explore:
                        var foundChild = FindChild<T>(child, child_name);

                        // If the child is found, just return:
                        if (foundChild != null) return foundChild;
                    }
                }
                else // if no name required and type is correct:
                {
                    return typedChild;  // just return child result.
                }
            }

            return default;  // been at the end and nothing interesting came back.
        }
        #endregion
    }
}
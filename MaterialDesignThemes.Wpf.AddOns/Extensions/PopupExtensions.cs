﻿using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Threading;
using EMA.ExtendedWPFVisualTreeHelper;

namespace MaterialDesignThemes.Wpf.AddOns.Extensions
{
    /// <summary>
    /// Extensions for <see cref="Popup"/> classes.
    /// </summary>
    public static class PopupExtensions
    {
        /// <summary>
        /// Makes a popup close after one of its children inner button
        /// is clicked.
        /// </summary>
        /// <param name="popup">The popup to customize.</param>
        /// <param name="isActiveChecker">An optional method to check if this behavior is allowed to run or not.</param>
        /// <param name="rootToDiscard">Optional base element for which children will be discarded from this behavior.</param>
        public static void CloseOnInnerButtonClicks(this Popup popup, Func<bool> isActiveChecker = null, DependencyObject rootToDiscard = null)
        {
            var weaklyTiedPopup = new WeakReference<Popup>(popup);
            var weaklyTiedRootToDiscard = rootToDiscard != null ? new WeakReference<DependencyObject>(rootToDiscard) : null;
            var closePopupOnDelayedClick = new Action<object, EventArgs>((timer, _) => ClosePopup(timer, weaklyTiedPopup));
            var closePopupWithDelayTimer = new DispatcherTimer(TimeSpan.FromMilliseconds(350), DispatcherPriority.Background, closePopupOnDelayedClick.Invoke, Dispatcher.CurrentDispatcher);
            
            popup.AddHandler(ButtonBase.ClickEvent, new RoutedEventHandler((sender, args) => InnerButtonClicked(sender, args, weaklyTiedPopup, isActiveChecker, closePopupWithDelayTimer, weaklyTiedRootToDiscard)));
        }
        
        private static void InnerButtonClicked(object sender, RoutedEventArgs e, WeakReference<Popup> weaklyTiedPopup, Func<bool> isActive, DispatcherTimer timer, WeakReference<DependencyObject> weaklyTiedRootToDiscard)
        {
            if (!weaklyTiedPopup.TryGetTarget(out var popup))
                return;

            if (weaklyTiedRootToDiscard != null && weaklyTiedRootToDiscard.TryGetTarget(out var rootToDiscard) 
                                                && rootToDiscard.FindAllChildrenByType(e.OriginalSource?.GetType()).Contains(e.OriginalSource))
                return;
            
            if (isActive?.Invoke() != true)
                return;
            
            if (sender == null || sender == e.OriginalSource || !(e.OriginalSource is Button))
                return;

            var children = popup.Child?.FindAllChildrenByType(e.OriginalSource.GetType());
            if (children?.Contains(e.OriginalSource) != true)
                return;

            timer.Start();
            
            e.Handled = true;
        }

        private static void ClosePopup(object sender, WeakReference<Popup> weaklyTiedPopup)
        {
            if (sender is DispatcherTimer timer)
                timer.Stop();
            
            if (!weaklyTiedPopup.TryGetTarget(out var popup))
                return;
            
            popup.IsOpen = false;
        }
    }
}

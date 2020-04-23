using System;
using System.Windows;
using System.Windows.Controls.Primitives;

namespace EMA.MaterialDesignInXAMLExtender.Utils
{
    /// <summary>
    /// Provides additional functionality to <see cref="ToggleButton"/> based controls.
    /// </summary>
    public static class ToggleButtonAssist
    {
        /// <summary>
        /// Used to modify the <see cref="ToggleButton.IsChecked"/> state order of <see cref="ToggleButton"/> in <see cref="ToggleButton.IsThreeState"/> mode.
        /// </summary>
        public static readonly DependencyProperty InvertedThreeStatesOrderProperty
            = DependencyProperty.RegisterAttached("InvertedThreeStatesOrder", typeof(bool), typeof(ToggleButton), new PropertyMetadata(default(bool), InvertedThreeStatesOrderChanged));

        /// <summary>
        /// Called whenever the <see cref="InvertedThreeStatesOrderProperty"/> value changes.
        /// </summary>
        /// <param name="sender">The object for which the property changed.</param>
        /// <param name="args">Information about the property change.</param>
        public static void InvertedThreeStatesOrderChanged(DependencyObject sender, DependencyPropertyChangedEventArgs args)
        {
            if (sender is ToggleButton toggle)
            {
                if (args.OldValue != null)
                {
                    toggle.Checked -= OnCheckedStateChanged;
                    toggle.Unchecked -= OnCheckedStateChanged;
                    toggle.Indeterminate -= OnCheckedStateChanged;
                }

                if (args.NewValue != null)
                {
                    toggle.Checked += OnCheckedStateChanged;
                    toggle.Unchecked += OnCheckedStateChanged;
                    toggle.Indeterminate += OnCheckedStateChanged;
                }
            }
        }

        /// <summary>
        /// Called whenever the <see cref="ToggleButton.IsChecked"/> property changes.
        /// </summary>
        /// <param name="sender">Should be toggle button for which the property changed.</param>
        /// <param name="e">Event information.</param>
        private static void OnCheckedStateChanged(object sender, RoutedEventArgs e)
        {
            if (sender is ToggleButton toggle && toggle.IsThreeState)  // only if three stated.
            {
                // Only if should inverse states:
                var should_invert = (bool?)toggle.GetValue(InvertedThreeStatesOrderProperty);
                if (should_invert == true)
                {
                    // Disable connexions so we are not coming here at next step:
                    toggle.Checked -= OnCheckedStateChanged;
                    toggle.Unchecked -= OnCheckedStateChanged;
                    toggle.Indeterminate -= OnCheckedStateChanged;

                    // Invert current state:
                    try
                    {
                        if (toggle.IsChecked == true)
                            toggle.IsChecked = false;
                        else if (toggle.IsChecked == false)
                            toggle.IsChecked = null;
                        else
                            toggle.IsChecked = true;
                    }
                    catch (Exception except)
                    {
                        throw except;
                    }
                    finally
                    {
                        // Reenable connexions to state changes:
                        toggle.Checked += OnCheckedStateChanged;
                        toggle.Unchecked += OnCheckedStateChanged;
                        toggle.Indeterminate += OnCheckedStateChanged;
                    }
                }
            }
        }

        /// <summary>
        /// Used to force mouse over state of the <see cref="ToggleButton"/>.
        /// </summary>
        [AttachedPropertyBrowsableForType(typeof(ToggleButton))]
        public static void SetInvertedThreeStatesOrder(DependencyObject element, bool value)
        {
            element.SetValue(InvertedThreeStatesOrderProperty, value);
        }

        /// <summary>
        /// Used to force mouse over state of the <see cref="ToggleButton"/>.
        /// </summary>
        [AttachedPropertyBrowsableForType(typeof(ToggleButton))]
        public static bool GetInvertedThreeStatesOrder(DependencyObject element)
        {
            return (bool)element.GetValue(InvertedThreeStatesOrderProperty);
        }
    }
}
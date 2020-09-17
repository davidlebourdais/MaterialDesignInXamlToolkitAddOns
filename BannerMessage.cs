﻿using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.ComponentModel;
using MaterialDesignThemes.Wpf;
using System.Windows.Controls.Primitives;

namespace EMA.MaterialDesignInXAMLExtender
{
    /// <summary>
    /// This control are messages to be included in a <see cref="Banner"/>.
    /// </summary>
    [TemplatePart(Name = ActionButtonPartName, Type = typeof(ButtonBase))]
    [TemplatePart(Name = SecondaryActionButtonPartName, Type = typeof(ButtonBase))]
    public class BannerMessage : SnackbarMessage
    {
        /// <summary>
        /// Name to be used for the secondary action button in <see cref="BannerMessage"/> control templates.
        /// </summary>
        public const string SecondaryActionButtonPartName = "PART_SecondaryActionButton";

        private Action _templateCleanupAction = () => { };  // base override.

        /// <summary>
        /// Static constructor for <see cref="BannerMessage"/>.
        /// </summary>
        static BannerMessage()
        {
            DefaultStyleKeyProperty.OverrideMetadata(typeof(BannerMessage), new FrameworkPropertyMetadata(typeof(BannerMessage)));
        }

        /// <summary>
        /// Called at template loading. 
        /// </summary>
        public override void OnApplyTemplate()
        {
            _templateCleanupAction();

            if (GetTemplateChild(ActionButtonPartName) is ButtonBase buttonBase)
            {
                buttonBase.Click += ButtonBaseOnClick;

                if (GetTemplateChild(SecondaryActionButtonPartName) is ButtonBase secondaryButtonBase)
                {
                    secondaryButtonBase.Click += SecondaryButtonBaseOnClick;

                    _templateCleanupAction = () =>
                    {
                        secondaryButtonBase.Click -= SecondaryButtonBaseOnClick;
                        buttonBase.Click -= ButtonBaseOnClick;
                    };  // normal and secondary buttons
                }
                else _templateCleanupAction = () => buttonBase.Click -= ButtonBaseOnClick;  // normal button only
            }
            else
            {
                if (GetTemplateChild(SecondaryActionButtonPartName) is ButtonBase secondaryButtonBase)
                {
                    secondaryButtonBase.Click += SecondaryButtonBaseOnClick;

                    _templateCleanupAction = () => secondaryButtonBase.Click -= SecondaryButtonBaseOnClick;  // cancel button only
                }
                else _templateCleanupAction = () => { };
            }

            base.OnApplyTemplate();
        }

        /// <summary>
        /// Occurs whenever the primary button is clicked.
        /// </summary>
        /// <param name="sender">The secondary button.</param>
        /// <param name="args">Event information.</param>
        private void ButtonBaseOnClick(object sender, RoutedEventArgs args)
        {
            OnActionClick();
        }

        /// <summary>
        /// Occurs whenever the secondary button is clicked.
        /// </summary>
        /// <param name="sender">The secondary button.</param>
        /// <param name="args">Event information.</param>
        private void SecondaryButtonBaseOnClick(object sender, RoutedEventArgs args)
        {
            RaiseEvent(new RoutedEventArgs(SecondaryActionClickEvent, this));
        }

        #region Dependency properties

        #region Illustration
        /// <summary>
        /// Gets or sets the illustration icon.
        /// </summary>
        public object IllustrationContent
        {
            get { return GetValue(IllustrationContentProperty); }
            set { SetValue(IllustrationContentProperty, value); }
        }
        /// <summary>
        /// Registers <see cref="IllustrationContent"/> as a dependency property.
        /// </summary>
        public static readonly DependencyProperty IllustrationContentProperty = DependencyProperty.Register(
            nameof(IllustrationContent), typeof(object), typeof(BannerMessage), new PropertyMetadata(default(object)));

        /// <summary>
        /// Gets or sets the template for illustration.
        /// </summary>
        public DataTemplate IllustrationContentTemplate
        {
            get { return (DataTemplate)GetValue(IllustrationContentTemplateProperty); }
            set { SetValue(IllustrationContentTemplateProperty, value); }
        }
        /// <summary>
        /// Registers <see cref="IllustrationContentTemplate"/> as a dependency property.
        /// </summary>
        public static readonly DependencyProperty IllustrationContentTemplateProperty = DependencyProperty.Register(
            nameof(IllustrationContentTemplate), typeof(DataTemplate), typeof(BannerMessage), new PropertyMetadata(default(DataTemplate)));

        /// <summary>
        /// Gets or sets the template for illustration.
        /// </summary>
        public DataTemplateSelector IllustrationContentTemplateSelector
        {
            get { return (DataTemplateSelector)GetValue(IllustrationContentTemplateSelectorProperty); }
            set { SetValue(IllustrationContentTemplateSelectorProperty, value); }
        }
        /// <summary>
        /// Registers <see cref="IllustrationContentTemplateSelector"/> as a dependency property.
        /// </summary>
        public static readonly DependencyProperty IllustrationContentTemplateSelectorProperty = DependencyProperty.Register(
            nameof(IllustrationContentTemplateSelector), typeof(DataTemplateSelector), typeof(BannerMessage), new PropertyMetadata(default(DataTemplateSelector)));
        #endregion

        #region Secondary action button
        /// <summary>
        /// Gets or sets the command associated to the secondary button.
        /// </summary>
        public ICommand SecondaryActionCommand
        {
            get { return (ICommand)GetValue(SecondaryActionCommandProperty); }
            set { SetValue(SecondaryActionCommandProperty, value); }
        }
        /// <summary>
        /// Registers <see cref="SecondaryActionCommand"/> as a dependency property.
        /// </summary>
        public static readonly DependencyProperty SecondaryActionCommandProperty = DependencyProperty.Register(
            nameof(SecondaryActionCommand), typeof(ICommand), typeof(BannerMessage), new PropertyMetadata(default(ICommand)));

        /// <summary>
        /// Gets or sets the command parameter associated to the secondary button.
        /// </summary>
        public object SecondaryActionCommandParameter
        {
            get { return (object)GetValue(SecondaryActionCommandParameterProperty); }
            set { SetValue(SecondaryActionCommandParameterProperty, value); }
        }
        /// <summary>
        /// Registers <see cref="SecondaryActionCommandParameter"/> as a dependency property.
        /// </summary>
        public static readonly DependencyProperty SecondaryActionCommandParameterProperty = DependencyProperty.Register(
            nameof(SecondaryActionCommandParameter), typeof(object), typeof(BannerMessage), new PropertyMetadata(default(object)));

        /// <summary>
        /// Gets or sets the content of the secondary action button.
        /// </summary>
        public object SecondaryActionContent
        {
            get { return (object)GetValue(SecondaryActionContentProperty); }
            set { SetValue(SecondaryActionContentProperty, value); }
        }
        /// <summary>
        /// Registers <see cref="SecondaryActionContent"/> as a dependency property.
        /// </summary>
        public static readonly DependencyProperty SecondaryActionContentProperty = DependencyProperty.Register(
            nameof(SecondaryActionContent), typeof(object), typeof(BannerMessage), new PropertyMetadata(default(object)));

        /// <summary>
        /// Gets or sets the content template of the secondary action button.
        /// </summary>
        public DataTemplate SecondaryActionContentTemplate
        {
            get { return (DataTemplate)GetValue(SecondaryActionContentTemplateProperty); }
            set { SetValue(SecondaryActionContentTemplateProperty, value); }
        }
        /// <summary>
        /// Registers <see cref="SecondaryActionCommandParameter"/> as a dependency property.
        /// </summary>
        public static readonly DependencyProperty SecondaryActionContentTemplateProperty = DependencyProperty.Register(
            nameof(SecondaryActionContentTemplate), typeof(DataTemplate), typeof(BannerMessage), new PropertyMetadata(default(DataTemplate)));

        /// <summary>
        /// Gets or sets the content string format of the secondary action button.
        /// </summary>
        public string SecondaryActionContentStringFormat
        {
            get { return (string)GetValue(SecondaryActionContentStringFormatProperty); }
            set { SetValue(SecondaryActionContentStringFormatProperty, value); }
        }
        /// <summary>
        /// Registers <see cref="SecondaryActionContentStringFormat"/> as a dependency property.
        /// </summary>
        public static readonly DependencyProperty SecondaryActionContentStringFormatProperty = DependencyProperty.Register(
            nameof(SecondaryActionContentStringFormat), typeof(string), typeof(BannerMessage), new PropertyMetadata(default(string)));

        /// <summary>
        /// Gets or sets a template selector for the secondary action button.
        /// </summary>
        public DataTemplateSelector SecondaryActionContentTemplateSelector
        {
            get { return (DataTemplateSelector)GetValue(SecondaryActionContentTemplateSelectorProperty); }
            set { SetValue(SecondaryActionContentTemplateSelectorProperty, value); }
        }
        /// <summary>
        /// Registers <see cref="SecondaryActionContentTemplateSelector"/> as a dependency property.
        /// </summary>
        public static readonly DependencyProperty SecondaryActionContentTemplateSelectorProperty = DependencyProperty.Register(
            nameof(SecondaryActionContentTemplateSelector), typeof(DataTemplateSelector), typeof(BannerMessage), new PropertyMetadata(default(DataTemplateSelector)));
        #endregion

        #endregion

        #region Routed events
        /// <summary>
        /// Adds or removes secondary action button click event handlers.
        /// </summary>
        [Category("Behavior")]
        public event RoutedEventHandler SecondaryActionClick { add { AddHandler(SecondaryActionClickEvent, value); } remove { RemoveHandler(SecondaryActionClickEvent, value); } }

        /// <summary>
        /// Registers <see cref="SecondaryActionClick"/> as a routed event.
        /// </summary>
        public static readonly RoutedEvent SecondaryActionClickEvent = EventManager.RegisterRoutedEvent(
            nameof(SecondaryActionClick), RoutingStrategy.Bubble, typeof(RoutedEventHandler), typeof(BannerMessage));
        #endregion of routed events
    }
}
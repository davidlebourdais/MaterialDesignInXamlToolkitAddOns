using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using EMA.ExtendedWPFVisualTreeHelper;

namespace MaterialDesignThemes.Wpf.AddOns
{
    /// <summary>
    /// A popup to be used within a <see cref="SingleSelectBox"/>.
    /// </summary>
    [TemplatePart(Name = "PART_PopupFilterTextBox", Type = typeof(TextBox))]
    public class SelectBoxPopup : ComboBoxPopup
    {
        /// <summary>
        /// Gets the current filtering TextBox within this popup is any.
        /// </summary>
        public TextBox CurrentPopupFilterTextBox { get; private set; }

        /// <summary>
        /// Static constructor for <see cref="SelectBoxPopup"/> type.
        /// Override some base dependency properties.
        /// </summary>
        static SelectBoxPopup()
        {
            DefaultStyleKeyProperty.OverrideMetadata(typeof(SelectBoxPopup), new FrameworkPropertyMetadata(typeof(SelectBoxPopup)));
        }

        private static readonly DependencyProperty[] _dependencyPropertiesThatChangeFilterTextBox =
        {
            ClassicContentTemplateProperty,
            DownContentTemplateProperty,
            UpContentTemplateProperty,
            PopupPlacementProperty,
            ChildProperty,
            IsOpenProperty
        };

        /// <summary>
        /// Occurs whenever a dependency property changes.
        /// </summary>
        /// <param name="e">Information about property value change.</param>
        protected override void OnPropertyChanged(DependencyPropertyChangedEventArgs e)
        {
            base.OnPropertyChanged(e);

            if (_dependencyPropertiesThatChangeFilterTextBox.Contains(e.Property))
            {
                SetCurrentPopupFilterTextBox(e.Property == IsOpenProperty);
            }
        }

        private void SetCurrentPopupFilterTextBox(bool fromIsOpenProperty)
        {
            var previous = CurrentPopupFilterTextBox;

            RefreshCurrentPopupFilterTextBox(fromIsOpenProperty);

            if (previous != CurrentPopupFilterTextBox)
                RaiseEvent(new RoutedEventArgs(_filterTextBoxChangedEvent, this));
        }

        private void RefreshCurrentPopupFilterTextBox(bool fromIsOpenProperty = false)
        {
            CurrentPopupFilterTextBox = null;

            if (!(Child is ContentControl child) || child.Template == null)
                return;

            if (!child.IsLoaded)
            {
                child.Loaded += ChildOnLoaded;
                return;
            }

            CurrentPopupFilterTextBox = child.FindChild<TextBox>("PART_PopupFilterTextBox");

            if (CurrentPopupFilterTextBox != null)
                return;

            if (!fromIsOpenProperty)
                return;

            if (child.Template == DownContentTemplate)
                throw new Exception(nameof(DownContentTemplate) + " must contain a TextBox named 'PART_PopupFilterTextBox'.");
            if (child.Template == UpContentTemplate)
                throw new Exception(nameof(UpContentTemplate) + " must contain a TextBox named 'PART_PopupFilterTextBox'.");
        }

        private void ChildOnLoaded(object sender, RoutedEventArgs e)
        {
            RefreshCurrentPopupFilterTextBox();
        }

        /// <summary>
        /// Adds or removes event handlers for template change event.
        /// </summary>
        public event RoutedEventHandler FilterTextBoxChanged
        {
            add => AddHandler(_filterTextBoxChangedEvent, value);
            remove => RemoveHandler(_filterTextBoxChangedEvent, value);
        }

        /// <summary>
        /// Registers <see cref="FilterTextBoxChanged"/> as a routed event.
        /// </summary>
        private static readonly RoutedEvent _filterTextBoxChangedEvent = EventManager.RegisterRoutedEvent(
            nameof(FilterTextBoxChanged), RoutingStrategy.Direct, typeof(RoutedEventHandler), typeof(SelectBoxPopup));
    }
}

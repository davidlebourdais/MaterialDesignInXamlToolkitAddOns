using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Markup;
using System.Windows.Media;
    
namespace EMA.MaterialDesignInXAMLExtender
{   
    #region ComboBoxItem template selectors to discriminate dropdown items template from select item template
    /// <summary>
    /// A <see cref="MarkupExtension"/> to define a <see cref="ComboBoxTemplateSelector"/>
    /// in XAML code.
    /// </summary>
    /// <remarks>From: https://stackoverflow.com/questions/4672867/can-i-use-a-different-template-for-the-selected-item-in-a-wpf-combobox-than-for</remarks>
    public class ComboBoxTemplateSelectorExtension : MarkupExtension
    {
        /// <summary>
        /// Gets or sets the item template to be used for the selected item.
        /// </summary>
        public DataTemplate SelectedItemTemplate { get; set; }

        /// <summary>
        /// Gets or sets the item template selector to be used for template selector of the selected item.
        /// </summary>
        public DataTemplateSelector SelectedItemTemplateSelector { get; set; }

        /// <summary>   
        /// Gets or sets the item template to be used for the items displayed in the dropdown menu of the combobox.
        /// </summary>
        public DataTemplate DropdownItemsTemplate { get; set; }

        /// <summary>
        /// Gets or sets the item template selector to be used for template selection of
        /// the items displayed in the dropdown menu of the combobox.
        /// </summary>
        public DataTemplateSelector DropdownItemsTemplateSelector { get; set; }

        /// <summary>
        /// Provides a preset <see cref="ComboBoxTemplateSelector"/>.
        /// </summary>
        /// <param name="serviceProvider">Unused.</param>
        /// <returns>A new <see cref="ComboBoxTemplateSelector"/>.</returns>
        public override object ProvideValue(IServiceProvider serviceProvider)
        {
            return new ComboBoxTemplateSelector() {
                SelectedItemTemplate = SelectedItemTemplate,
                SelectedItemTemplateSelector = SelectedItemTemplateSelector,
                DropdownItemsTemplate = DropdownItemsTemplate,
                DropdownItemsTemplateSelector = DropdownItemsTemplateSelector
            };
        }
    }

    /// <summary>
    /// A <see cref="DataTemplateSelector"/> that discriminates selected
    /// item from item in dropdown menu for comboboxes.
    /// </summary>
    /// <remarks>From: https://stackoverflow.com/questions/4672867/can-i-use-a-different-template-for-the-selected-item-in-a-wpf-combobox-than-for</remarks>
    public class ComboBoxTemplateSelector : DataTemplateSelector
    {
        /// <summary>
        /// Gets or sets the item template to be used for the selected item.
        /// </summary>
        public DataTemplate SelectedItemTemplate { get; set; }

        /// <summary>
        /// Gets or sets the item template selector to be used for template selector of the selected item.
        /// </summary>
        public DataTemplateSelector SelectedItemTemplateSelector { get; set; }

        /// <summary>
        /// Gets or sets the item template to be used for the items displayed in the dropdown menu of the combobox.
        /// </summary>
        public DataTemplate DropdownItemsTemplate { get; set; }

        /// <summary>
        /// Gets or sets the item template selector to be used for template selection of
        /// the items displayed in the dropdown menu of the combobox.
        /// </summary>
        public DataTemplateSelector DropdownItemsTemplateSelector { get; set; }

        /// <summary>
        /// Selects the template that best matches the passed item.
        /// </summary>
        /// <param name="item">A selected or 'displayed in dropdown' item.</param>
        /// <param name="container">The item's container (will travel to find any <see cref="ComboBox"/> 
        /// or <see cref="ComboBoxItem"/> parent from this reference).</param>
        /// <returns></returns>
        public override DataTemplate SelectTemplate(object item, DependencyObject container)
        {
            var itemToCheck = container;

            // Search up the visual tree, stopping at either a ComboBox or
            // a ComboBoxItem (or null). This will determine which template to use
            while (itemToCheck != null && !(itemToCheck is ComboBoxItem) && !(itemToCheck is ComboBox))
                itemToCheck = VisualTreeHelper.GetParent(itemToCheck);

            // If you stopped at a ComboBoxItem, you're in the dropdown
            var inDropDown = (itemToCheck is ComboBoxItem);

            return inDropDown
                ? DropdownItemsTemplate ?? DropdownItemsTemplateSelector?.SelectTemplate(item, container)
                : SelectedItemTemplate ?? SelectedItemTemplateSelector?.SelectTemplate(item, container);
        }
    }
    #endregion
}
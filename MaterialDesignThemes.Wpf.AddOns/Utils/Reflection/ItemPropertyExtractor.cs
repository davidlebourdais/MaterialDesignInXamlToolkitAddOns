using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using MaterialDesignThemes.Wpf.AddOns.Extensions;

namespace MaterialDesignThemes.Wpf.AddOns.Utils.Reflection
{
    public static class ItemPropertyExtractor 
    {
        public static PropertyGetter[] BuildPropertyGetters(object source)
        {
            PropertyDescriptorCollection propertyDescriptors;

            if (source == null)
                return Array.Empty<PropertyGetter>();

            if (source is ITypedList typedList)
            {
                propertyDescriptors = typedList.GetItemProperties(null);
            }
            else
            {
                var type = (source as IEnumerable)?.GetGenericType() ?? source.GetType();
                propertyDescriptors = TypeDescriptor.GetProperties(type, new Attribute[] { new PropertyFilterAttribute(PropertyFilterOptions.All) });
            }

            var propertyGetters = new List<PropertyGetter>();
            foreach (var propertyDescriptor in propertyDescriptors.Cast<PropertyDescriptor>())
            {
                propertyGetters.Add(new PropertyGetter(propertyDescriptor.Name));
            }

            return propertyGetters.ToArray();
        }
    }
}

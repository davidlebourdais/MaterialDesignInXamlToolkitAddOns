using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Dynamic;
using System.Linq;
using System.Linq.Expressions;
using MaterialDesignThemes.Wpf.AddOns.Extensions;

namespace MaterialDesignThemes.Wpf.AddOns.Utils.Reflection
{
    /// <summary>
    /// A helper class to extract properties from an object.
    /// </summary>
    public static class ItemPropertyExtractor 
    {
        /// <summary>
        /// Gets getters that will ensure the retrieval of object values through reflection.
        /// </summary>
        /// <param name="source">The object source on which the properties will be retrieved.</param>
        /// <returns>Property getters for static or dynamic properties.</returns>
        public static PropertyGetter[] BuildPropertyGetters(object source)
        {
            var propertyGetters = new List<PropertyGetter>();

            if (source == null)
                return propertyGetters.ToArray();

            var type = (source as IEnumerable)?.GetGenericType() ?? source.GetType();
            var propertyDescriptors = TypeDescriptor.GetProperties(type, new Attribute[] { new PropertyFilterAttribute(PropertyFilterOptions.All) });
            foreach (var property in propertyDescriptors.Cast<PropertyDescriptor>())
            {
                propertyGetters.Add(new PropertyGetter(property.Name));
            }

            if (source is ITypedList typedList)
            {
                foreach (var property in typedList.GetItemProperties(null).Cast<PropertyDescriptor>())
                {
                    propertyGetters.Add(new PropertyGetter(property.Name));
                }
            }

            if (source is IDynamicMetaObjectProvider dynamicMetaObjectProvider)
            {
                var dynamicProperties = dynamicMetaObjectProvider.GetMetaObject(Expression.Constant(dynamicMetaObjectProvider)).GetDynamicMemberNames().ToArray();
                propertyGetters.AddRange(dynamicProperties.Select(x => new PropertyGetter(x)));
            }

            return propertyGetters.ToArray();
        }
    }
}

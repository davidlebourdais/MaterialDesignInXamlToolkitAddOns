using System;
using System.ComponentModel;
using System.Dynamic;
using System.Linq.Expressions;
using System.Runtime.CompilerServices;

namespace MaterialDesignThemes.Wpf.AddOns.Utils.Reflection
{
    /// <summary>
    /// A class specialized in getting property values from objects.
    /// </summary>
    public class PropertyGetter
    {
        private Func<object, object> _staticGetter;
        private CallSite<Func<CallSite, object, object>> _dynamicGetter;

        /// <summary>
        /// The name of the property that can be retrieved through the getter.
        /// </summary>
        public string PropertyName { get; }

        /// <summary>
        /// Initiates a new instance of <see cref="PropertyGetter"/>.
        /// </summary>
        /// <param name="propertyName"></param>
        public PropertyGetter(string propertyName)
        {
            PropertyName = propertyName;
        }

        /// <summary>
        /// Retrieves the property value of the getter.
        /// </summary>
        /// <param name="source">The source object on which the property must be read.</param>
        /// <returns>The value of the property.</returns>
        public object GetValue(object source)
        {
            if (_staticGetter == null && _dynamicGetter == null)
                InitializeGetter(source);

            if (_staticGetter != null)
                return _staticGetter(source);

            return _dynamicGetter?.Target(_dynamicGetter, source);
        }

        private void InitializeGetter(object source)
        {
            if (source is IDynamicMetaObjectProvider)
            {
                var binder = new GetMemberBinderForDynamicObject(PropertyName);
                _dynamicGetter = CallSite<Func<CallSite, object, object>>.Create(binder);
            }
            else
            {
                var descriptor = TypeDescriptor.GetProperties(source).Find(PropertyName, true);
                if (descriptor != null)
                    _staticGetter = descriptor.GetValue;
            }
        }
        
        private class GetMemberBinderForDynamicObject : GetMemberBinder
        {
            public GetMemberBinderForDynamicObject(string propertyName)
                : base(propertyName, false)
            {  }

            public override DynamicMetaObject FallbackGetMember(DynamicMetaObject target, DynamicMetaObject errorSuggestion)
                => errorSuggestion ??
                   new DynamicMetaObject(
                       Expression.Throw(
                           Expression.New(
                               typeof(InvalidOperationException).GetConstructor(new[] { typeof(string) }) ?? throw new InvalidOperationException(),
                               Expression.Constant("Property not found")),
                           typeof(object)),
                       BindingRestrictions.Empty);
        }
    }
}

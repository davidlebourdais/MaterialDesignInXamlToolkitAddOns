using System;
using System.Collections.Generic;
using System.Linq;

namespace MaterialDesignThemes.Wpf.AddOns.Utils.Filtering
{
    /// <summary>
    /// Provides a way to combine multiple filters into a single one.
    /// </summary>
    /// <remarks>To be used when a controlled collection is externally filtered, as it may be with <see cref="FilterTextBox"/>.</remarks>
    public class GroupFilter
    {
        private readonly List<Predicate<object>> _filters = new List<Predicate<object>>();
        private readonly Predicate<object> _filter;

        private GroupFilter(params Predicate<object>[] predicates)
        {
            _filters.AddRange(predicates);
            _filter = item => _filters.All(filter => filter(item));
        }

        /// <summary>
        /// Converts a <see cref="GroupFilter"/> to a <see cref="Predicate{T}"/>.
        /// </summary>
        /// <param name="filter">The <see cref="GroupFilter"/> to be converted.</param>
        /// <returns>A single <see cref="Predicate{T}"/> with the logic of the <see cref="GroupFilter"/>.</returns>
        public static implicit operator Predicate<object>(GroupFilter filter) => filter?._filter;

        /// <summary>
        /// Converts a <see cref="Predicate{T}"/> to a <see cref="GroupFilter"/>.
        /// </summary>
        /// <param name="filter">The filter to be converted.</param>
        /// <returns>A <see cref="GroupFilter"/> which can execute the passed filter.</returns>
        public static implicit operator GroupFilter(Predicate<object> filter)
        {
            if (filter == null)
                return new GroupFilter();

            if (filter.GetInvocationList().Length > 0)
                return new GroupFilter(filter.GetInvocationList().Cast<Predicate<object>>().ToArray());

            return new GroupFilter(filter);
        }

        /// <summary>
        /// Adds a filter to a group.
        /// </summary>
        /// <param name="groupFilter">A reference group of filters.</param>
        /// <param name="filter">Filter to be added.</param>
        /// <returns>The reference group of filters with the new filter included.</returns>
        public static GroupFilter operator+(GroupFilter groupFilter, Predicate<object> filter)
        {
            if (filter != null)
                groupFilter._filters.Add(filter);

            return groupFilter;
        }

        /// <summary>
        /// Removes a filter from a group.
        /// </summary>
        /// <param name="groupFilter">A reference group of filters.</param>
        /// <param name="filter">Filter to be removed.</param>
        /// <returns>The reference group of filters without the passed filter.</returns>
        public static GroupFilter operator-(GroupFilter groupFilter, Predicate<object> filter)
        {
            if (groupFilter?._filters.Contains(filter) == true)
                groupFilter._filters.Remove(filter);

            return groupFilter;
        }
    }
}

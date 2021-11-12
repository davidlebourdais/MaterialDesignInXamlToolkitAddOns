using System.Windows;
using System.Windows.Controls;

namespace MaterialDesignThemes.Wpf.AddOns
{
    /// <summary>
    /// Item for <see cref="SingleSelectBox"/> control.
    /// </summary>
    [TemplatePart(Name = "GridWrapper", Type = typeof(Grid))]
    public class SelectBoxItem : FilterBoxItem
    {
        private bool _isPreselected;

        #region Constructors
        /// <summary>
        /// Creates a new instance of <see cref="SelectBoxItem"/>.
        /// </summary>
        /// <param name="initialFilter">Initial filter to be applied on the item.</param>
        /// <param name="highlightPerFilterWord">Indicates if highlight must occur on a per filter word basis or on the whole filter string.</param>
        /// <param name="isPreselected">Initial preselection state.</param>
        /// <param name="isSelectedSourceMemberPath">Member path to the <see cref="FilterBoxItem.IsSelected"/> property.</param>
        public SelectBoxItem(string initialFilter, bool highlightPerFilterWord, bool isPreselected, string isSelectedSourceMemberPath = null) 
            : base(initialFilter, highlightPerFilterWord, isSelectedSourceMemberPath)
        {
            Loaded += (_, unused) => Initialize(isPreselected);
        }

        /// <summary>
        /// Static constructor for <see cref="SelectBoxItem"/> type.
        /// </summary>
        static SelectBoxItem()
        {
            DefaultStyleKeyProperty.OverrideMetadata(typeof(SelectBoxItem), new FrameworkPropertyMetadata(typeof(SelectBoxItem)));
        }
        
        private void Initialize(bool isPreselected)
        {
            _isPreselected = isPreselected;
            UpdateVisualState();
        }
        #endregion
        
        #region Preselection
        /// <summary>
        /// Sets the item in the preselected mode.
        /// </summary>
        public void SetAsPreselected()
        {
            _isPreselected = true;
            UpdateVisualState();
        }

        /// <summary>
        /// Unsets preselection mode.
        /// </summary>
        public void SetAsNotPreselected()
        {
            _isPreselected = false;
            UpdateVisualState();
        }
        #endregion

        #region Selection and visual state management
        /// <summary>
        /// Updates the current item visual state.
        /// </summary>
        protected override void UpdateVisualState()
        {
            if (_gridWrapper == null)
                return;

            if (!_isPreselected)
                VisualStateManager.GoToElementState(_gridWrapper, IsSelected ? "Selected" : "Unselected", true);
            VisualStateManager.GoToElementState(_gridWrapper, _isPreselected ? "Preselected" : "NotPreselected", true);
            VisualStateManager.GoToElementState(_gridWrapper, IsMouseOver ? "MouseOver" : "Normal", true);
            VisualStateManager.GoToElementState(_gridWrapper, IsFocused ? "Focused" : "Unfocused", true);
        }
        #endregion
    }
}

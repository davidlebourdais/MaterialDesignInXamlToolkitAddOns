using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;
using EMA.ExtendedWPFVisualTreeHelper;
using MaterialDesignThemes.Wpf.AddOns.Extensions;

namespace MaterialDesignThemes.Wpf.AddOns
{
    /// <summary>
    /// Represents placement of the icon for a <see cref="ButtonWithIcon"/>.
    /// </summary>
    public enum ButtonIconPlacement
    {
        /// <summary>
        /// Icon is placed on the left of the content.
        /// </summary>
        Lead,
        
        /// <summary>
        /// Icon is placed on the right of the content.
        /// </summary>
        Trail
    }
    
    /// <summary>
    /// A button allowing to insert an icon 
    /// </summary>
    public class ButtonWithIcon : Button
    {
        private DataTemplate _originalDataTemplate;
        private bool _templateIsBeingInternallyGenerated;
        
        static ButtonWithIcon()
        {
            DefaultStyleKeyProperty.OverrideMetadata(typeof(ButtonWithIcon), new FrameworkPropertyMetadata(typeof(ButtonWithIcon)));
            ContentTemplateProperty.OverrideMetadata(typeof(ButtonWithIcon), new FrameworkPropertyMetadata(default(DataTemplate), ContentTemplatePropertyChanged));
            ContentProperty.OverrideMetadata(typeof(ButtonWithIcon), new FrameworkPropertyMetadata(default, IconRelatedPropertyChanged));
            ForegroundProperty.OverrideMetadata(typeof(ButtonWithIcon), new FrameworkPropertyMetadata(default(Brush), IconRelatedPropertyChanged));
            FontWeightProperty.OverrideMetadata(typeof(ButtonWithIcon), new FrameworkPropertyMetadata(default(FontWeight), IconRelatedPropertyChanged));
            HorizontalContentAlignmentProperty.OverrideMetadata(typeof(ButtonWithIcon), new FrameworkPropertyMetadata(default(HorizontalAlignment), IconRelatedPropertyChanged));
            VerticalContentAlignmentProperty.OverrideMetadata(typeof(ButtonWithIcon), new FrameworkPropertyMetadata(VerticalAlignment.Center, IconRelatedPropertyChanged));
        }
        
        /// <summary>
        /// Called whenever the <see cref="ContentControl.ContentTemplate"/> property changes.
        /// </summary>
        /// <param name="sender">The object whose property changed.</param>
        /// <param name="args">Information about the property change.</param>
        private static void ContentTemplatePropertyChanged(DependencyObject sender, DependencyPropertyChangedEventArgs args)
        {
            if (!(sender is ButtonWithIcon button))
                return;

            if (button._templateIsBeingInternallyGenerated)
                return;
            
            button._originalDataTemplate = args.NewValue as DataTemplate;
            button.ReconstructContentTemplate();
        }

        private void ReconstructContentTemplate()
        {
            _templateIsBeingInternallyGenerated = true;
            
            if (Icon == null && IconKind == null)
                ContentTemplate = _originalDataTemplate;
            else
            {
                var modifiedContentTemplate = new DataTemplate() { VisualTree = ConstructContentWithIcon() };
                modifiedContentTemplate.Seal();

                ContentTemplate = modifiedContentTemplate;
            }

            _templateIsBeingInternallyGenerated = false;
        }

        private FrameworkElementFactory ConstructContentWithIcon()
        {
            var grid = CreateGrid();

            var icon = CreatePackIcon();
            icon.SetValue(Grid.ColumnProperty, IconPlacement == ButtonIconPlacement.Lead ? 0 : 1);
            grid.AppendChild(icon);
            
            var contentControl = CreateContentControl();
            contentControl.SetValue(Grid.ColumnProperty, IconPlacement == ButtonIconPlacement.Lead ? 1 : 0);
            grid.AppendChild(contentControl);

            return grid;
        }

        private static FrameworkElementFactory CreateGrid()
        {
            var grid = new FrameworkElementFactory(typeof(Grid));
            grid.SetValue(HorizontalAlignmentProperty, new TemplateBindingExtension(HorizontalContentAlignmentProperty));
            grid.SetValue(VerticalAlignmentProperty, new TemplateBindingExtension(VerticalContentAlignmentProperty));

            var column1 = new FrameworkElementFactory(typeof(ColumnDefinition));
            column1.SetValue(ColumnDefinition.WidthProperty, new GridLength(1, GridUnitType.Auto));
            grid.AppendChild(column1);
                
            var column2 = new FrameworkElementFactory(typeof(ColumnDefinition));
            column2.SetValue(ColumnDefinition.WidthProperty, new GridLength(1, GridUnitType.Auto));
            grid.AppendChild(column2);

            return grid;
        }
        
        private FrameworkElementFactory CreatePackIcon()
        {
            var icon = new FrameworkElementFactory(typeof(PackIcon));
            
            if (Icon == null && IconKind == null)
                return icon;
            
            var sourceIcon = Icon ?? new PackIcon() { Kind = IconKind.Value, Height = IconSize, Width = IconSize };
            
            RestoreIconVisualIfHasNone(sourceIcon);
            
            foreach (var property in sourceIcon.GetNonReadOnlyDependencyProperties())
            {
                icon.SetValue(property, sourceIcon.GetValue(property));
            }
            
            icon.SetValue(ForegroundProperty, Foreground);
            icon.SetValue(FontWeightProperty, FontWeight);

            icon.SetValue(MarginProperty, 
                          IconPlacement == ButtonIconPlacement.Lead ? new Thickness(0, 0, Content != null ? IconSpacing : 0, 0) : 
                                                                      new Thickness(Content != null ? IconSpacing : 0, 0, 0, 0));
            icon.SetValue(IsTabStopProperty, false);

            if (Icon != null)
                return icon;
            
            icon.SetValue(HeightProperty, IconSize);
            icon.SetValue(WidthProperty, IconSize);
            icon.SetValue(HorizontalAlignmentProperty, HorizontalAlignment.Center);
            icon.SetValue(VerticalAlignmentProperty, 
                          new Binding(VerticalContentAlignmentProperty.Name)  {  RelativeSource = new RelativeSource(RelativeSourceMode.FindAncestor) { AncestorType = typeof(ButtonWithIcon)}} );
            return icon;
        }
        
        private FrameworkElementFactory CreateContentControl()
        {
            var contentControl = new FrameworkElementFactory(typeof(ContentControl));
            contentControl.SetValue(ContentProperty, new TemplateBindingExtension(ContentProperty));
            contentControl.SetValue(ContentTemplateProperty, _originalDataTemplate);
            contentControl.SetValue(ContentStringFormatProperty, new TemplateBindingExtension(ContentStringFormatProperty));
            contentControl.SetValue(HorizontalAlignmentProperty, 
                                    new Binding(HorizontalContentAlignmentProperty.Name)  {  RelativeSource = new RelativeSource(RelativeSourceMode.FindAncestor) { AncestorType = typeof(ButtonWithIcon)}} );
            contentControl.SetValue(VerticalAlignmentProperty, 
                                    new Binding(VerticalContentAlignmentProperty.Name)  {  RelativeSource = new RelativeSource(RelativeSourceMode.FindAncestor) { AncestorType = typeof(ButtonWithIcon)}} );
            contentControl.SetValue(MarginProperty, 
                                    new Binding(ContentMarginProperty.Name)  {  RelativeSource = new RelativeSource(RelativeSourceMode.FindAncestor) { AncestorType = typeof(ButtonWithIcon)}} );
            contentControl.SetValue(IsTabStopProperty, false);

            return contentControl;
        }
        
        private void RestoreIconVisualIfHasNone(PackIcon icon)
        {
            ApplyIconStyleIfHasNone(icon);
            ApplyIconControlTemplateIfHasNone(icon);
        }

        private void ApplyIconStyleIfHasNone(PackIcon icon)
        {
            if (icon.Style != null)
                return;
            
            var style = FindResource(typeof(PackIcon)) as Style;
            icon.SetValue(StyleProperty, style);
        }

        private void ApplyIconControlTemplateIfHasNone(PackIcon icon)
        {
            if (icon.Template != null)
                return;

            FrameworkElement parent = this;
            ControlTemplate template;
            do
            {
                var style = parent.FindResource(typeof(PackIcon)) as Style;
                template = style?.Setters.Cast<Setter>().SingleOrDefault(x => x.Property == TemplateProperty)?.Value as ControlTemplate;
            } while(template == null && (parent = this.FindParent<FrameworkElement>()) != null);
           
            if (template != null)
                icon.SetValue(TemplateProperty, template);
        }
        
        /// <summary>
        /// Gets or sets the icon to be displayed next to button content.
        /// </summary>
        /// <remarks>Setting this property will override <see cref="IconKind"/> and <see cref="IconSize"/>.</remarks>
        public PackIcon Icon
        {
            get => (PackIcon)GetValue(IconProperty);
            set => SetCurrentValue(IconProperty, value);
        }
        /// <summary>
        /// Registers the <see cref="Icon"/> as a dependency property.
        /// </summary>
        public static readonly DependencyProperty IconProperty =
            DependencyProperty.Register(nameof(Icon), typeof(PackIcon), typeof(ButtonWithIcon), new FrameworkPropertyMetadata(null, IconPropertyChanged));
        
        /// <summary>
        /// Called whenever the <see cref="Icon"/> property changes.
        /// </summary>
        /// <param name="sender">The object whose property changed.</param>
        /// <param name="args">Information about the property change.</param>
        private static void IconPropertyChanged(DependencyObject sender, DependencyPropertyChangedEventArgs args)
        {
            if (!(sender is ButtonWithIcon button))
                return;

            if (button.Icon?.IsLoaded == false)
                button.Loaded += (_, unused) => button.ReconstructContentTemplate();
            else
                button.ReconstructContentTemplate();
        }
        
        /// <summary>
        /// Gets or sets the kind of the icon to be displayed next to button content.
        /// </summary>
        public PackIconKind? IconKind
        {
            get => (PackIconKind?)GetValue(IconKindProperty);
            set => SetCurrentValue(IconKindProperty, value);
        }
        /// <summary>
        /// Registers the <see cref="IconKind"/> as a dependency property.
        /// </summary>
        public static readonly DependencyProperty IconKindProperty =
            DependencyProperty.Register(nameof(IconKind), typeof(PackIconKind?), typeof(ButtonWithIcon), new FrameworkPropertyMetadata(default(PackIconKind?), IconRelatedPropertyChanged));
        
        /// <summary>
        /// Gets or sets the icon to be displayed next to button content.
        /// </summary>
        public ButtonIconPlacement IconPlacement
        {
            get => (ButtonIconPlacement)GetValue(IconPlacementProperty);
            set => SetCurrentValue(IconPlacementProperty, value);
        }
        /// <summary>
        /// Registers the <see cref="IconPlacement"/> as a dependency property.
        /// </summary>
        public static readonly DependencyProperty IconPlacementProperty =
            DependencyProperty.Register(nameof(IconPlacement), typeof(ButtonIconPlacement), typeof(ButtonWithIcon), new FrameworkPropertyMetadata(default(ButtonIconPlacement), IconRelatedPropertyChanged));
        
        /// <summary>
        /// Gets or sets the margin of the icon towards button's content.
        /// </summary>
        public double IconSpacing
        {
            get => (double)GetValue(IconSpacingProperty);
            set => SetCurrentValue(IconSpacingProperty, value);
        }
        /// <summary>
        /// Registers the <see cref="IconSpacing"/> as a dependency property.
        /// </summary>
        public static readonly DependencyProperty IconSpacingProperty =
            DependencyProperty.Register(nameof(IconSpacing), typeof(double), typeof(ButtonWithIcon), new FrameworkPropertyMetadata(5d, IconRelatedPropertyChanged));
        
        /// <summary>
        /// Gets or sets the size of the icon.
        /// </summary>
        public double IconSize
        {
            get => (double)GetValue(IconSizeProperty);
            set => SetCurrentValue(IconSizeProperty, value);
        }
        /// <summary>
        /// Registers the <see cref="IconSize"/> as a dependency property.
        /// </summary>
        public static readonly DependencyProperty IconSizeProperty =
            DependencyProperty.Register(nameof(IconSize), typeof(double), typeof(ButtonWithIcon), new FrameworkPropertyMetadata(20d, IconRelatedPropertyChanged));
        
        /// <summary>
        /// Gets or sets the margin of the main button content.
        /// </summary>
        public Thickness ContentMargin
        {
            get => (Thickness)GetValue(ContentMarginProperty);
            set => SetCurrentValue(ContentMarginProperty, value);
        }
        /// <summary>
        /// Registers the <see cref="ContentMargin"/> as a dependency property.
        /// </summary>
        public static readonly DependencyProperty ContentMarginProperty =
            DependencyProperty.Register(nameof(ContentMargin), typeof(Thickness), typeof(ButtonWithIcon), 
                                        new FrameworkPropertyMetadata(new Thickness(0,0,0,1), IconRelatedPropertyChanged));
        
        /// <summary>
        /// Called whenever a dependency property related to the added icon is modified.
        /// </summary>
        /// <param name="sender">The object whose property changed.</param>
        /// <param name="args">Information about the property change.</param>
        private static void IconRelatedPropertyChanged(DependencyObject sender, DependencyPropertyChangedEventArgs args)
        {
            if (!(sender is ButtonWithIcon button))
                return;

            button.ReconstructContentTemplate();
        }
    }
}

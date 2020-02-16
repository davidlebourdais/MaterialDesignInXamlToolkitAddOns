using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using EMA.MaterialDesignInXAMLExtender.Utils;

namespace EMA.MaterialDesignInXAMLExtender
{
    /// <summary>
    /// A color picker that offers users a selection of predefined colors to be picked up,
    /// or to define its own color or select from a preset of custom colors or from a a preset
    /// of system colors. 
    /// </summary>
    public class CustomColorPicker : Control
    {
        private double original_last_item_opacity = 1.0d;  // stores a trace of the last selected custom item opacity
        private BrushItem previouslySelectedPredefinedNonGrayBrushItem;  // stores last selected 'colored' predefined value
        private BrushItem previouslySelectedPredefinedGrayBrushItem;  // stores last selected 'gray scaled' predefined value
        private bool no_reentrancy;  // disables reentrancy when selected color changes.

        /// <summary>
        /// Static constructor for <see cref="CustomColorPicker"/> type.
        /// </summary>
        static CustomColorPicker()
        {
            DefaultStyleKeyProperty.OverrideMetadata(typeof(CustomColorPicker), new FrameworkPropertyMetadata(typeof(CustomColorPicker)));
            UIElement.IsEnabledProperty.OverrideMetadata(typeof(CustomColorPicker), new FrameworkPropertyMetadata(true, IsOpenChanged));
        }

        /// <summary>
        /// Creates a new instance of <see cref="CustomColorPicker"/>.
        /// </summary>
        public CustomColorPicker()
        {
            resetPredefinedColors();
            if (CustomColorsFeed != null)
                saveSelectedIntoCustomColorFeed();  // save before startup if user had set any useful custom value
            loadCustomColorFeed();
            OpenCommand = new SimpleCommand(() => IsOpen = true);
            AddCustomColorCommand = new SimpleCommand(addCurrentlyPickedCustomColor, () =>
                (CustomColors == null || CustomColors.Count == 0 || CustomColors.Last().Color.ToString() != PreviewedCustomColor.ToString())
                || CanSetSelectedColorOpacity && CustomColors.Last().Opacity != original_last_item_opacity);
            ShowCustomColorPickingCommand = new SimpleCommand(() => IsCustomColorPickingShown = true);
            HideCustomColorPickingCommand = new SimpleCommand(() => IsCustomColorPickingShown = false);
            SelectColorCommand = new SimpleParameterizedCommand((rawBrushItem) =>
            {
                if (rawBrushItem is BrushItem brushItem)
                    SelectedBrushItem = brushItem;
            });
            if (SystemBrushItems != null)
                SelectedSystemBrushItem = SystemBrushItems.First();

            trySelectCorrespondingGroup();
        }

        #region Dependency properties related to options
        /// <summary>
        /// Gets or sets a value indicating if the opacity of the
        /// selected color can be set.
        /// </summary>
        public bool CanSetSelectedColorOpacity
        {
            get => (bool)GetValue(CanSetSelectedColorOpacityProperty);
            set => SetValue(CanSetSelectedColorOpacityProperty, value);
        }
        /// <summary>
        /// Registers <see cref="CanSetSelectedColorOpacity"/> as a dependency property.
        /// </summary>
        public static readonly DependencyProperty CanSetSelectedColorOpacityProperty
            = DependencyProperty.Register(nameof(CanSetSelectedColorOpacity), typeof(bool), typeof(CustomColorPicker), new FrameworkPropertyMetadata(default(bool), CanSetSelectedColorOpacityChanged));

        /// <summary>
        /// Called whenever the <see cref="CanSetSelectedColorOpacity"/> property changes.
        /// </summary>
        /// <param name="sender">The object whose property changed.</param>
        /// <param name="args">Information about the property change.</param>
        public static void CanSetSelectedColorOpacityChanged(DependencyObject sender, DependencyPropertyChangedEventArgs args)
        {
            if (sender is CustomColorPicker picker && args.NewValue is bool)
            {
                picker.resetSelectedColorOpacity();
                picker.resetSelectedColorAsText();
            }
        }

        /// <summary>
        /// Gets or sets a value indicating if custom colors
        /// can be picked by the user.
        /// </summary>
        public bool CanChooseCustomColor
        {
            get => (bool)GetValue(CanChooseCustomColorProperty);
            set => SetValue(CanChooseCustomColorProperty, value);
        }
        /// <summary>
        /// Registers <see cref="CanChooseCustomColor"/> as a dependency property.
        /// </summary>
        public static readonly DependencyProperty CanChooseCustomColorProperty
            = DependencyProperty.Register(nameof(CanChooseCustomColor), typeof(bool), typeof(CustomColorPicker), new FrameworkPropertyMetadata(default(bool), CanChooseCustomColorChanged));

        /// <summary>
        /// Called whenever the <see cref="CanChooseCustomColor"/> property changes.
        /// </summary>
        /// <param name="sender">The object whose property changed.</param>
        /// <param name="args">Information about the property change.</param>
        public static void CanChooseCustomColorChanged(DependencyObject sender, DependencyPropertyChangedEventArgs args)
        {
            if (sender is CustomColorPicker picker && args.NewValue is bool new_value)
                if (!new_value)
                {
                    picker.IsCustomColorPickingShown = false;
                    // If we were on a custom color, reset to get a predefined one:
                    if (!picker.PredefinedColors.Contains(picker.SelectedBrushItem))
                        picker.SelectedBrushItem = picker.PredefinedColors.First();
                }
        }

        /// <summary>
        /// Gets or sets a value indicating is user can pick custom colors from system brushes.
        /// </summary>
        public bool CanSelectFromSystemColors
        {
            get => (bool)GetValue(CanSelectFromSystemColorsProperty);
            set => SetValue(CanSelectFromSystemColorsProperty, value);
        }
        /// <summary>
        /// Registers <see cref="CanSelectFromSystemColors"/> as a dependency property.
        /// </summary>
        public static readonly DependencyProperty CanSelectFromSystemColorsProperty
            = DependencyProperty.Register(nameof(CanSelectFromSystemColors), typeof(bool), typeof(CustomColorPicker), new FrameworkPropertyMetadata(default(bool)));
        #endregion

        #region Open state related properties
        /// <summary>
        /// Gets or sets a value indicating if the control is open.
        /// </summary>
        public bool IsOpen
        {
            get => (bool)GetValue(IsOpenProperty);
            set => SetValue(IsOpenProperty, value);
        }
        /// <summary>
        /// Registers <see cref="IsOpen"/> as a dependency property.
        /// </summary>
        public static readonly DependencyProperty IsOpenProperty
            = DependencyProperty.Register(nameof(IsOpen), typeof(bool), typeof(CustomColorPicker), new FrameworkPropertyMetadata(default(bool), IsOpenChanged));

        /// <summary>
        /// Called whenever the <see cref="UIElement.IsEnabled"/> or <see cref="IsOpen"/> property changes.
        /// </summary>
        /// <param name="sender">The object whose property changed.</param>
        /// <param name="args">Information about the property change.</param>
        public static void IsOpenChanged(DependencyObject sender, DependencyPropertyChangedEventArgs args)
        {
            if (sender is CustomColorPicker picker && args.NewValue is bool new_value)
            {
                picker.IsCustomColorPickingShown = false;   // reset to current state whenever the control is activated/deactivated.
                if (new_value)  // fill custom colors with app wide custom ones
                    picker.loadCustomColorFeed();
                else  // save selected if it is a new custom color.
                    picker.saveSelectedIntoCustomColorFeed();
            }
        }

        /// <summary>
        /// Gets the command to 'open' the current item.
        /// </summary>
        public ICommand OpenCommand { get; }
        #endregion

        #region Color opacity part
        /// <summary>
        /// Gets or sets the opacity of the resulting color.
        /// </summary>
        public double SelectedColorOpacity
        {
            get => (double)GetValue(SelectedColorOpacityroperty);
            set => SetValue(SelectedColorOpacityroperty, value);
        }
        /// <summary>
        /// Registers <see cref="SelectedColorOpacity"/> as a dependency property.
        /// </summary>
        public static readonly DependencyProperty SelectedColorOpacityroperty
            = DependencyProperty.Register(nameof(SelectedColorOpacity), typeof(double), typeof(CustomColorPicker), new FrameworkPropertyMetadata(1.0d, SelectedColorOpacityChanged));

        /// <summary>
        /// Called whenever the <see cref="SelectedColorOpacity"/> property changes.
        /// </summary>
        /// <param name="sender">The object whose property changed.</param>
        /// <param name="args">Information about the property change.</param>
        public static void SelectedColorOpacityChanged(DependencyObject sender, DependencyPropertyChangedEventArgs args)
        {
            if (sender is CustomColorPicker picker && args.NewValue is double)
            {
                picker.resetSelectedColorOpacity();
                picker.resetCustomColor();
                picker.resetSelectedColorAsText();
                if (picker.AddCustomColorCommand is SimpleCommand addcommand)  // used to lock when last custom is already current color.
                    addcommand.RaiseCanExecuteChanged();
            }
        }

        /// <summary>
        /// Changes the opacity of the selected color.
        /// </summary>
        private void resetSelectedColorOpacity()
        {
            if (SelectedBrushItems.Count > 0)
                SelectedBrushItems.ForEach(x => x.setOpacity(CanSetSelectedColorOpacity ? SelectedColorOpacity : 1.0d));
        }

        /// <summary>
        /// Changes the selected color text.
        /// </summary>
        private void resetSelectedColorAsText()
        {
            no_reentrancy = true;
            var nullable_opacity = CanSetSelectedColorOpacity ? SelectedColorOpacity : (double?)null;
            SelectedColorAsText = ColorHelper.GetARGBStringFromColor(SelectedBrushItem.Color, nullable_opacity);
            no_reentrancy = false;
        }
        #endregion

        #region System brush selection
        /// <summary>
        /// Gets the list of system colors as <see cref="BrushItem"/> objects.
        /// </summary>
        public static List<BrushItem> SystemBrushItems { get; } = generateSystemBrushItemsFromBrushes();

        /// <summary>
        /// Generates the list of system colors.
        /// </summary>
        /// <returns>A list of system colors as <see cref="BrushItem"/> objects.</returns>
        private static List<BrushItem> generateSystemBrushItemsFromBrushes()
        {
            var systemColors = new List<BrushItem>();

            // Get WPF brushes static properties that contains standard colors and add them in our list:
            var properties = typeof(Brushes).GetProperties(BindingFlags.Static | BindingFlags.Public);
            foreach (var property in properties)
                systemColors.Add(new BrushItem((Brush)property.GetValue(null, null), property.Name));

            return systemColors;
        }

        /// <summary>
        /// Gets or sets the selected brush item from system colors.
        /// </summary>
        public BrushItem SelectedSystemBrushItem
        {
            get => (BrushItem)GetValue(SelectedSystemBrushItemProperty);
            set => SetValue(SelectedSystemBrushItemProperty, value);
        }
        /// <summary>
        /// Registers <see cref="SelectedSystemBrushItem"/> as a dependency property.
        /// </summary>
        public static readonly DependencyProperty SelectedSystemBrushItemProperty
            = DependencyProperty.Register(nameof(SelectedSystemBrushItem), typeof(BrushItem), typeof(CustomColorPicker), new FrameworkPropertyMetadata(default(BrushItem), SelectedSystemBrushItemChanged));

        /// <summary>
        /// Called whenever the <see cref="SelectedSystemBrushItem"/> property changes.
        /// </summary>
        /// <param name="sender">The object whose property changed.</param>
        /// <param name="args">Information about the property change.</param>
        public static void SelectedSystemBrushItemChanged(DependencyObject sender, DependencyPropertyChangedEventArgs args)
        {
            if (sender is CustomColorPicker picker && args.NewValue is BrushItem)
                picker.PreviewedCustomColor = picker.SelectedSystemBrushItem.Color;
        }
        #endregion

        #region Custom color selection
        /// <summary>
        /// Stores the list of all custom colors for the app domain.
        /// </summary>
        public static List<BrushItem> AppWideCustomColorsFeed { get; } = new List<BrushItem>();

        /// <summary>
        /// Gets or sets the list of recently created custom colors.
        /// </summary>
        public List<BrushItem> CustomColors
        {
            get { return (List<BrushItem>)GetValue(CustomColorsProperty); }
            protected set { SetValue(CustomColorsPropertyKey, value); }
        }
        private static readonly DependencyPropertyKey CustomColorsPropertyKey
            = DependencyProperty.RegisterReadOnly(nameof(CustomColors), typeof(List<BrushItem>), typeof(CustomColorPicker), new FrameworkPropertyMetadata(new List<BrushItem>(), CustomColorsRelatedChanged));
        /// <summary>
        /// Registers <see cref="CustomColors"/> as a readonly dependency property.
        /// </summary>
        public static readonly DependencyProperty CustomColorsProperty = CustomColorsPropertyKey.DependencyProperty;

        /// <summary>
        /// Gets or sets a feed of values to be set as custom colors.
        /// </summary>
        public List<BrushItem> CustomColorsFeed
        {
            get { return (List<BrushItem>)GetValue(CustomColorsFeedProperty); }
            set { SetValue(CustomColorsFeedProperty, value); }
        }
        /// <summary>
        /// Registers <see cref="CustomColorsFeed"/> as a dependency property.
        /// </summary>
        public static readonly DependencyProperty CustomColorsFeedProperty
            = DependencyProperty.Register(nameof(CustomColorsFeed), typeof(List<BrushItem>), typeof(CustomColorPicker), new FrameworkPropertyMetadata(default(List<BrushItem>), CustomColorsRelatedChanged));

        /// <summary>
        /// Gets or sets the maximum authorized count for custom colors.
        /// </summary>
        public uint MaxCustomColorCount
        {
            get { return (uint)GetValue(MaxCustomColorCountProperty); }
            set { SetValue(MaxCustomColorCountProperty, value); }
        }
        /// <summary>
        /// Registers <see cref="MaxCustomColorCount"/> as a dependency property.
        /// </summary>
        public static readonly DependencyProperty MaxCustomColorCountProperty
            = DependencyProperty.Register(nameof(MaxCustomColorCount), typeof(uint), typeof(CustomColorPicker), new FrameworkPropertyMetadata(uint.MaxValue, CustomColorsRelatedChanged));

        /// <summary>
        /// Called whenever the <see cref="CustomColors"/> or <see cref="MaxCustomColorCount"/> property changes.
        /// </summary>
        /// <param name="sender">The object whose property changed.</param>
        /// <param name="args">Information about the property change.</param>
        public static void CustomColorsRelatedChanged(DependencyObject sender, DependencyPropertyChangedEventArgs args)
        {
            if (sender is CustomColorPicker picker && (args.NewValue is List<BrushItem> || args.NewValue is uint))
            {
                if (picker.CustomColors.Count > picker.MaxCustomColorCount)
                {
                    var newList = picker.CustomColors;
                    while (newList.Count > picker.MaxCustomColorCount)
                        newList.RemoveAt(0);
                    picker.CustomColors = new List<BrushItem>(newList);
                }
            }
        }

        /// <summary>
        /// Gets the command to validate the addition of a custom color.
        /// </summary>
        public ICommand AddCustomColorCommand { get; }

        /// <summary>
        /// Adds the currently previewed color to the custom color list and selects it.
        /// </summary>
        private void addCurrentlyPickedCustomColor()
        {
            var customList = new List<BrushItem>(CustomColors);
            var opacity = CanSetSelectedColorOpacity ? SelectedColorOpacity : 1.0d;
            if (customList.Count > 0)
                customList.RemoveAll(x => x.Color.ToString() == PreviewedCustomColor.ToString());
            customList.Add(new BrushItem(PreviewedCustomColor, opacity));
            CustomColors = customList;
            if (CustomColors.Count > 0)
            {
                SelectedBrushItem = CustomColors.Last();
                IsCustomColorPickingShown = false;
            }
        }

        /// <summary>
        /// Gets the command to toggle view to custom color picking.
        /// </summary>
        public ICommand ShowCustomColorPickingCommand { get; }

        /// <summary>
        /// Gets the command to toggle view to predefined color picking
        /// without doing any custom color additions.
        /// </summary>
        public ICommand HideCustomColorPickingCommand { get; }

        /// <summary>
        /// Gets or sets a value indicating if the custom color picker is displayed.
        /// </summary>
        public bool IsCustomColorPickingShown
        {
            get => (bool)GetValue(IsCustomColorPickingShownProperty);
            set => SetValue(IsCustomColorPickingShownProperty, value);
        }
        /// <summary>
        /// Registers <see cref="IsCustomColorPickingShown"/> as a dependency property.
        /// </summary>
        public static readonly DependencyProperty IsCustomColorPickingShownProperty
            = DependencyProperty.Register(nameof(IsCustomColorPickingShown), typeof(bool), typeof(CustomColorPicker), new FrameworkPropertyMetadata(default(bool), IsCustomColorPickingShownChanged));

        /// <summary>
        /// Called whenever the <see cref="IsCustomColorPickingShown"/> property changes.
        /// </summary>
        /// <param name="sender">The object whose property changed.</param>
        /// <param name="args">Information about the property change.</param>
        public static void IsCustomColorPickingShownChanged(DependencyObject sender, DependencyPropertyChangedEventArgs args)
        {
            if (sender is CustomColorPicker picker && args.NewValue is bool)
                picker.original_last_item_opacity = picker.SelectedColorOpacity;  // store last saved opacity state, so we can update the "add" button state for a better user xp.
        }

        /// <summary>
        /// Gets or sets a custom color to be previewed.
        /// </summary>
        public Color PreviewedCustomColor
        {
            get => (Color)GetValue(PreviewedCustomColorProperty);
            set => SetValue(PreviewedCustomColorProperty, value);
        }
        /// <summary>
        /// Registers <see cref="PreviewedCustomColor"/> as a dependency property.
        /// </summary>
        public static readonly DependencyProperty PreviewedCustomColorProperty = DependencyProperty.Register(nameof(PreviewedCustomColor), typeof(Color), typeof(CustomColorPicker),
            new FrameworkPropertyMetadata(Colors.Transparent, PreviewedCustomColorChanged));   // will be init by constructor.

        /// <summary>
        /// Called whenever the <see cref="PreviewedCustomColor"/> property changes.
        /// </summary>
        /// <param name="sender">The object whose property changed.</param>
        /// <param name="args">Information about the property change.</param>
        public static void PreviewedCustomColorChanged(DependencyObject sender, DependencyPropertyChangedEventArgs args)
        {
            if (sender is CustomColorPicker picker && args.NewValue is Color)
            {
                if (picker.no_reentrancy) return;
                picker.no_reentrancy = true;
                picker.resetCustomColor();
                picker.PreviewedBrushAsText = picker.PreviewedCustomColor.ToString().Remove(0, 3);
                if (picker.AddCustomColorCommand is SimpleCommand addcommand)  // used to lock when last custom is already current color.
                    addcommand.RaiseCanExecuteChanged();
                picker.no_reentrancy = false;
            }
        }

        /// <summary>
        /// Gets <see cref="PreviewedCustomColor"/> as a <see cref="SolidColorBrush"/>.
        /// </summary>
        public SolidColorBrush PreviewedCustomSolidColorBrush
        {
            get { return (SolidColorBrush)GetValue(PreviewedCustomSolidColorBrushProperty); }
            protected set { SetValue(PreviewedCustomSolidColorBrushPropertyKey, value); }
        }
        private static readonly DependencyPropertyKey PreviewedCustomSolidColorBrushPropertyKey
            = DependencyProperty.RegisterReadOnly(nameof(PreviewedCustomSolidColorBrush), typeof(SolidColorBrush), typeof(CustomColorPicker), new FrameworkPropertyMetadata(default(SolidColorBrush)));
        /// <summary>
        /// Registers <see cref="PreviewedCustomSolidColorBrush"/> as a readonly dependency property.
        /// </summary>
        public static readonly DependencyProperty PreviewedCustomSolidColorBrushProperty = PreviewedCustomSolidColorBrushPropertyKey.DependencyProperty;

        /// <summary>
        /// Gets or sets the color that was selected, as a <see cref="string"/> in hexadecimal format.
        /// </summary>
        public string PreviewedBrushAsText
        {
            get => (string)GetValue(PreviewedBrushAsTextProperty);
            set => SetValue(PreviewedBrushAsTextProperty, value);
        }
        /// <summary>
        /// Registers <see cref="PreviewedBrushAsText"/> as a dependency property.
        /// </summary>
        public static readonly DependencyProperty PreviewedBrushAsTextProperty = DependencyProperty.Register(nameof(PreviewedBrushAsText), typeof(string), typeof(CustomColorPicker),
            new FrameworkPropertyMetadata(default(string), PreviewedBrushAsTextChanged));

        /// <summary>
        /// Called whenever the <see cref="PreviewedBrushAsText"/> property changes.
        /// </summary>
        /// <param name="sender">The object whose property changed.</param>
        /// <param name="args">Information about the property change.</param>
        public static void PreviewedBrushAsTextChanged(DependencyObject sender, DependencyPropertyChangedEventArgs args)
        {
            if (sender is CustomColorPicker picker && args.NewValue is string new_value)
            {
                if (picker.no_reentrancy) return;
                picker.no_reentrancy = true;
                try
                {
                    var newPreviewed = ColorHelper.GetNullableColorFromArgb(new_value);
                    if (newPreviewed != null)
                    {
                        picker.PreviewedCustomColor = Color.FromArgb((byte)255, newPreviewed.Value.R, newPreviewed.Value.G, newPreviewed.Value.B);  // reset alpha value.
                        picker.resetCustomColor();
                        if (picker.CanSetSelectedColorOpacity)
                            picker.SelectedColorOpacity = (double)(newPreviewed.Value.A) / (double)255;
                    }
                }
                catch
                {
                    throw new Exception("Invalid hexa color");
                }
                finally
                {
                    picker.no_reentrancy = false;
                }
            }
        }

        /// <summary>
        /// Resets the custom color preview to a new value.
        /// </summary>
        private void resetCustomColor()
        {
            var opacity = CanSetSelectedColorOpacity ? SelectedColorOpacity : 1.0d;
            PreviewedCustomSolidColorBrush = new SolidColorBrush(PreviewedCustomColor) { Opacity = opacity };
        }

        /// <summary>
        /// Loads custom color from a feed.
        /// </summary>
        private void loadCustomColorFeed()
        {
            // If a feed is given, load custom colors from it:
            if (CustomColorsFeed != null)
                CustomColors = new List<BrushItem>(CustomColorsFeed);
            // else used app wide feed:
            else CustomColors = new List<BrushItem>(AppWideCustomColorsFeed);
        }

        /// <summary>
        /// Save new selected custom color into the feed.
        /// </summary>
        private void saveSelectedIntoCustomColorFeed()
        {
            // Only if custom color is selected:
            if (CustomColors.Contains(SelectedBrushItem))
            {
                // If a feed is given, manage it, otherwise use the static app wide feed:
                if (CustomColorsFeed != null)
                {
                    if (!CustomColorsFeed.Any(x => SelectedBrushItem.Color.ToString() == x.Color.ToString()))
                        CustomColorsFeed.Add(SelectedBrushItem);
                }
                else if (!AppWideCustomColorsFeed.Any(x => SelectedBrushItem.Color.ToString() == x.Color.ToString()))
                    AppWideCustomColorsFeed.Add(SelectedBrushItem);
            }
        }
        #endregion

        #region Predefined color part
        /// <summary>
        /// Gets or sets a value indicating if the light colors group is shown or not.
        /// </summary>
        public bool IsLightColorSelected
        {
            get => (bool)GetValue(IsLightColorSelectedProperty);
            set => SetValue(IsLightColorSelectedProperty, value);
        }
        /// <summary>
        /// Registers <see cref="IsLightColorSelected"/> as a dependency property.
        /// </summary>
        public static readonly DependencyProperty IsLightColorSelectedProperty
            = DependencyProperty.Register(nameof(IsLightColorSelected), typeof(bool), typeof(CustomColorPicker), new FrameworkPropertyMetadata(true, IsLightColorSelectedChanged));

        /// <summary>
        /// Gets or sets a value indicating if the middle luminosity colors group is shown or not.
        /// </summary>
        public bool IsMediumColorSelected
        {
            get => (bool)GetValue(IsMediumColorSelectedProperty);
            set => SetValue(IsMediumColorSelectedProperty, value);
        }
        /// <summary>
        /// Registers <see cref="IsMediumColorSelected"/> as a dependency property.
        /// </summary>
        public static readonly DependencyProperty IsMediumColorSelectedProperty
            = DependencyProperty.Register(nameof(IsMediumColorSelected), typeof(bool), typeof(CustomColorPicker), new FrameworkPropertyMetadata(default(bool), IsMediumColorSelectedChanged));

        /// <summary>
        /// Gets or sets a value indicating if the dark colors group is shown or not.
        /// </summary>
        public bool IsDarkColorSelected
        {
            get => (bool)GetValue(IsDarkColorSelectedProperty);
            set => SetValue(IsDarkColorSelectedProperty, value);
        }
        /// <summary>
        /// Registers <see cref="IsDarkColorSelected"/> as a dependency property.
        /// </summary>
        public static readonly DependencyProperty IsDarkColorSelectedProperty
            = DependencyProperty.Register(nameof(IsDarkColorSelected), typeof(bool), typeof(CustomColorPicker), new FrameworkPropertyMetadata(default(bool), IsDarkColorSelectedChanged));

        /// <summary>
        /// Gets or sets a value indicating if the gray scale colors group is shown or not.
        /// </summary>
        public bool IsGrayColorSelected
        {
            get => (bool)GetValue(IsGrayColorSelectedProperty);
            set => SetValue(IsGrayColorSelectedProperty, value);
        }
        /// <summary>
        /// Registers <see cref="IsGrayColorSelected"/> as a dependency property.
        /// </summary>
        public static readonly DependencyProperty IsGrayColorSelectedProperty
            = DependencyProperty.Register(nameof(IsGrayColorSelected), typeof(bool), typeof(CustomColorPicker), new FrameworkPropertyMetadata(default(bool), IsGrayColorSelectedChanged));

        /// <summary>
        /// Gets the list of light color brush items.
        /// </summary>
        protected List<BrushItem> LightColorItems { get; set; }

        /// <summary>
        /// Gets the list of medium color brush items.
        /// </summary>
        protected List<BrushItem> MediumColorItems { get; set; }

        /// <summary>
        /// Gets the list of dark color items.
        /// </summary>
        protected List<BrushItem> DarkColorItems { get; set; }

        /// <summary>
        /// Gets the list of gray scale color brush items.
        /// </summary>
        protected List<BrushItem> GrayColorItems { get; set; }

        /// <summary>
        /// Determines the number of Hue/saturation level we should display.
        /// </summary>
        public uint PredefinedColorsCount
        {
            get => (uint)GetValue(PredefinedColorsCountProperty);
            set => SetValue(PredefinedColorsCountProperty, value);
        }
        /// <summary>
        /// Registers <see cref="PredefinedColorsCount"/> as a dependency property.
        /// </summary>
        public static readonly DependencyProperty PredefinedColorsCountProperty
            = DependencyProperty.Register(nameof(PredefinedColorsCount), typeof(uint), typeof(CustomColorPicker), new FrameworkPropertyMetadata((uint)20, PredefinedColorsCountChanged));

        /// <summary>
        /// Gets a list of predefined colors, whose values matches the currently
        /// selected 'lightness' of color group.
        /// </summary>
        public List<BrushItem> PredefinedColors
        {
            get { return (List<BrushItem>)GetValue(PredefinedColorsProperty); }
            protected set { SetValue(PredefinedColorsPropertyKey, value); }
        }
        private static readonly DependencyPropertyKey PredefinedColorsPropertyKey
            = DependencyProperty.RegisterReadOnly(nameof(PredefinedColors), typeof(List<BrushItem>), typeof(CustomColorPicker), new FrameworkPropertyMetadata(new List<BrushItem>()));
        /// <summary>
        /// Registers <see cref="PredefinedColors"/> as a readonly dependency property.
        /// </summary>
        public static readonly DependencyProperty PredefinedColorsProperty = PredefinedColorsPropertyKey.DependencyProperty;

        /// <summary>
        /// Called whenever the <see cref="PredefinedColorsCount"/> property changes.
        /// </summary>
        /// <param name="sender">The object whose property changed.</param>
        /// <param name="args">Information about the property change.</param>
        public static void PredefinedColorsCountChanged(DependencyObject sender, DependencyPropertyChangedEventArgs args)
        {
            if (sender is CustomColorPicker picker && args.NewValue is uint)
            {
                picker.resetPredefinedColors();
                if (picker.IsLightColorSelected)
                    picker.PredefinedColors = picker.LightColorItems;
                else if (picker.IsMediumColorSelected)
                    picker.PredefinedColors = picker.MediumColorItems;
                else if (picker.IsDarkColorSelected)
                    picker.PredefinedColors = picker.DarkColorItems;
                else if (picker.IsGrayColorSelected)
                    picker.PredefinedColors = picker.GrayColorItems;
                picker.SelectedBrushItem = new BrushItem(picker.SelectedBrushItem.Color);
            }
        }

        /// <summary>
        /// Called whenever the <see cref="IsLightColorSelected"/> property changes.
        /// </summary>
        /// <param name="sender">The object whose property changed.</param>
        /// <param name="args">Information about the property change.</param>
        public static void IsLightColorSelectedChanged(DependencyObject sender, DependencyPropertyChangedEventArgs args)
        {
            if (sender is CustomColorPicker picker && args.NewValue is bool new_value)
                if (new_value)
                    picker.setPredefinedColorGroup(picker.LightColorItems);
        }

        /// <summary>
        /// Called whenever the <see cref="IsMediumColorSelected"/> property changes.
        /// </summary>
        /// <param name="sender">The object whose property changed.</param>
        /// <param name="args">Information about the property change.</param>
        public static void IsMediumColorSelectedChanged(DependencyObject sender, DependencyPropertyChangedEventArgs args)
        {
            if (sender is CustomColorPicker picker && args.NewValue is bool new_value)
                if (new_value)
                    picker.setPredefinedColorGroup(picker.MediumColorItems);
        }

        /// <summary>
        /// Called whenever the <see cref="IsDarkColorSelected"/> property changes.
        /// </summary>
        /// <param name="sender">The object whose property changed.</param>
        /// <param name="args">Information about the property change.</param>
        public static void IsDarkColorSelectedChanged(DependencyObject sender, DependencyPropertyChangedEventArgs args)
        {
            if (sender is CustomColorPicker picker && args.NewValue is bool new_value)
                if (new_value)
                    picker.setPredefinedColorGroup(picker.DarkColorItems);
        }

        /// <summary>
        /// Called whenever the <see cref="IsGrayColorSelected"/> property changes.
        /// </summary>
        /// <param name="sender">The object whose property changed.</param>
        /// <param name="args">Information about the property change.</param>
        public static void IsGrayColorSelectedChanged(DependencyObject sender, DependencyPropertyChangedEventArgs args)
        {
            if (sender is CustomColorPicker picker && args.NewValue is bool new_value)
                if (new_value)
                    picker.setPredefinedColorGroup(picker.GrayColorItems);
        }

        /// <summary>
        /// Sets the main color group with the passed new selection.
        /// </summary>
        /// <param name="newSelection">The new predefined color group to be displayed.</param>
        private void setPredefinedColorGroup(List<BrushItem> newSelection)
        {
            if (newSelection != PredefinedColors)
            {
                var had_predefined_item_selected = SelectedBrushItem != null && PredefinedColors.Contains(SelectedBrushItem);
                var previousSelection = PredefinedColors;
                PredefinedColors = newSelection;
                if (had_predefined_item_selected && !PredefinedColors.Contains(SelectedBrushItem))
                {
                    // If going to gray scale, select first or previously saved gray color:
                    if (previousSelection != GrayColorItems && newSelection == GrayColorItems)
                        SelectedBrushItem = previouslySelectedPredefinedGrayBrushItem ?? PredefinedColors.First();
                    else  // going to 'colored' so find matching hue or select first.
                    {
                        // Must do the equivalent finding in any cases, but we get the previously selected color in case we went from gray scale selection:
                        var selectedItem = previousSelection == GrayColorItems && newSelection != GrayColorItems ? previouslySelectedPredefinedNonGrayBrushItem ?? SelectedBrushItem : SelectedBrushItem;
                        var selectedAsDrawingColorHue = System.Drawing.Color.FromArgb(selectedItem.Color.R, selectedItem.Color.G, selectedItem.Color.B).GetHue();
                        var equivalent = PredefinedColors.FirstOrDefault(x => (int)System.Drawing.Color.FromArgb(x.Color.R, x.Color.G, x.Color.B).GetHue() == (int)selectedAsDrawingColorHue);
                        SelectedBrushItem = equivalent ?? PredefinedColors.First();
                    }
                    resetSelectedColorOpacity();
                }
            }
        }

        /// <summary>
        /// Resets items of the various predefined groups. 
        /// </summary>
        private void resetPredefinedColors()
        {
            LightColorItems = generateColorGroup(PredefinedColorsCount, 160);
            MediumColorItems = generateColorGroup(PredefinedColorsCount, 120);
            DarkColorItems = generateColorGroup(PredefinedColorsCount, 80);
            GrayColorItems = generateGrayColorGroup(PredefinedColorsCount);
        }

        /// <summary>
        /// Generates a specific set of colors based on specific passed parameters.
        /// </summary>
        /// <param name="count">Number of items to be generated.</param>
        /// <param name="luminosity_level">The luminosity level to be applied to all colors in the generated group.</param>
        /// <param name="saturation_level">The staturation level colors should have.</param>
        /// <returns>A list of predefined brush items.</returns>
        private static List<BrushItem> generateColorGroup(uint count, int luminosity_level, int saturation_level = 240)
        {
            var newGroup = new List<BrushItem>();
            if (count == 0) return newGroup;

            // Build colors based on hues at regular intervals:
            var step = 240 / count;
            for (uint i = 0; i < 240; i += step)
                newGroup.Add(new BrushItem(ColorHelper.GetColorFromHSLParameters((double)i, saturation_level, luminosity_level)));

            return newGroup;
        }

        /// <summary>
        /// Generates a set a gray scale colors in a group.
        /// </summary>
        /// <param name="count">Number of items to be generated.</param>
        /// <returns>A list of predefined brush items.</returns>
        private static List<BrushItem> generateGrayColorGroup(uint count)
        {
            var newGroup = new List<BrushItem>();
            if (count == 0) return newGroup;

            // Build gray scale colors at regular intervals:
            var step = 255 / (count - 1);
            var max = step * (count - 1);
            for (uint i = 0; i < max; i += step)
                newGroup.Add(new BrushItem(Color.FromArgb(255, (byte)i, (byte)i, (byte)i)));
            newGroup.Add(new BrushItem(Color.FromArgb(255, (byte)255, (byte)255, (byte)255)));

            return newGroup;
        }
        #endregion

        #region Selected color + selection management
        /// <summary>
        /// Gets the command for color selection.
        /// </summary>
        public ICommand SelectColorCommand { get; }

        /// <summary>
        /// Stores the list of selected brush items, when they are both available as custom, predfined, etc.
        /// </summary>
        private List<BrushItem> SelectedBrushItems { get; set; } = new List<BrushItem>();

        /// <summary>
        /// Gets the selected brush item.
        /// </summary>
        public BrushItem SelectedBrushItem
        {
            get => (BrushItem)GetValue(SelectedBrushItemProperty);
            set => SetValue(SelectedBrushItemProperty, value);
        }
        /// <summary>
        /// Registers <see cref="SelectedBrushItem"/> as a dependency property.
        /// </summary>
        public static readonly DependencyProperty SelectedBrushItemProperty
            = DependencyProperty.Register(nameof(SelectedBrushItem), typeof(BrushItem), typeof(CustomColorPicker), new FrameworkPropertyMetadata(default(BrushItem), SelectedBrushItemChanged));

        /// <summary>
        /// Called whenever the <see cref="SelectedBrushItem"/> property changes.
        /// </summary>
        /// <param name="sender">The object whose property changed.</param>
        /// <param name="args">Information about the property change.</param>
        public static void SelectedBrushItemChanged(DependencyObject sender, DependencyPropertyChangedEventArgs args)
        {
            if (sender is CustomColorPicker picker && args.NewValue is BrushItem)
            {
                if (picker.no_reentrancy) return;
                picker.no_reentrancy = true;
                picker.onSelectionChanged();
                var opacity = picker.CanSetSelectedColorOpacity ? picker.SelectedColorOpacity : 1.0d;
                picker.SelectedBrushItem.Opacity = opacity;
                picker.SelectedColor = picker.SelectedBrushItem.Color;
                picker.SelectedSolidColorBrush = picker.SelectedBrushItem.AsSolidColorBrush;
                var nullable_opacity = picker.CanSetSelectedColorOpacity ? picker.SelectedColorOpacity : (double?)null;
                picker.SelectedColorAsText = ColorHelper.GetARGBStringFromColor(picker.SelectedBrushItem.Color, nullable_opacity);
                picker.no_reentrancy = false;
            }
        }

        /// <summary>
        /// Gets or sets the selected color item.
        /// </summary>
        public Color SelectedColor
        {
            get => (Color)GetValue(SelectedColorProperty);
            set => SetValue(SelectedColorProperty, value);
        }
        /// <summary>
        /// Registers <see cref="SelectedColor"/> as a dependency property.
        /// </summary>
        public static readonly DependencyProperty SelectedColorProperty
            = DependencyProperty.Register(nameof(SelectedColor), typeof(Color), typeof(CustomColorPicker), new FrameworkPropertyMetadata(default(Color), SelectedColorChanged));

        /// <summary>
        /// Called whenever the <see cref="SelectedColor"/> property changes.
        /// </summary>
        /// <param name="sender">The object whose property changed.</param>
        /// <param name="args">Information about the property change.</param>
        public static void SelectedColorChanged(DependencyObject sender, DependencyPropertyChangedEventArgs args)
        {
            if (sender is CustomColorPicker picker && args.NewValue is Color new_value)
            {
                if (picker.no_reentrancy) return;
                picker.no_reentrancy = true;
                picker.SelectedBrushItem = new BrushItem(new_value);
                var opacity = picker.CanSetSelectedColorOpacity ? picker.SelectedColorOpacity : 1.0d;
                picker.SelectedBrushItem.Opacity = opacity;
                picker.SelectedSolidColorBrush = new SolidColorBrush(new_value) { Opacity = opacity };
                var nullable_opacity = picker.CanSetSelectedColorOpacity ? picker.SelectedColorOpacity : (double?)null;
                picker.SelectedColorAsText = ColorHelper.GetARGBStringFromColor(new_value, nullable_opacity);
                picker.no_reentrancy = false;
            }
        }

        /// <summary>
        /// Gets or sets the color that was selected, as a <see cref="SolidColorBrush"/>.
        /// </summary>
        public SolidColorBrush SelectedSolidColorBrush
        {
            get => (SolidColorBrush)GetValue(SelectedSolidColorBrushProperty);
            set => SetValue(SelectedSolidColorBrushProperty, value);
        }
        /// <summary>
        /// Registers <see cref="SelectedSolidColorBrush"/> as a dependency property.
        /// </summary>
        public static readonly DependencyProperty SelectedSolidColorBrushProperty = DependencyProperty.Register(nameof(SelectedSolidColorBrush), typeof(SolidColorBrush), typeof(CustomColorPicker),
            new FrameworkPropertyMetadata(default(SolidColorBrush), SelectedSolidColorBrushChanged));

        /// <summary>
        /// Called whenever the <see cref="SelectedSolidColorBrush"/> property changes.
        /// </summary>
        /// <param name="sender">The object whose property changed.</param>
        /// <param name="args">Information about the property change.</param>
        public static void SelectedSolidColorBrushChanged(DependencyObject sender, DependencyPropertyChangedEventArgs args)
        {
            if (sender is CustomColorPicker picker && args.NewValue is SolidColorBrush new_value)
            {
                if (picker.no_reentrancy) return;
                picker.no_reentrancy = true;
                picker.SelectedBrushItem = new BrushItem(new_value.Color);
                picker.SelectedBrushItem.Opacity = picker.CanSetSelectedColorOpacity ? new_value.Opacity : 1.0d;
                picker.SelectedColor = picker.SelectedBrushItem.Color;
                picker.SelectedColorOpacity = picker.SelectedBrushItem.Opacity;
                var nullable_opacity = picker.CanSetSelectedColorOpacity ? picker.SelectedColorOpacity : (double?)null;
                picker.SelectedColorAsText = ColorHelper.GetARGBStringFromColor(picker.SelectedBrushItem.Color, nullable_opacity);
                picker.no_reentrancy = false;
            }
        }

        /// <summary>
        /// Gets or sets the color that was selected, as a <see cref="string"/>.
        /// </summary>
        public string SelectedColorAsText
        {
            get => (string)GetValue(SelectedColorAsTextProperty);
            set => SetValue(SelectedColorAsTextProperty, value);
        }
        /// <summary>
        /// Registers <see cref="SelectedColorAsText"/> as a dependency property.
        /// </summary>
        public static readonly DependencyProperty SelectedColorAsTextProperty = DependencyProperty.Register(nameof(SelectedColorAsText), typeof(string), typeof(CustomColorPicker),
            new FrameworkPropertyMetadata(default(string), SelectedColorAsTextChanged));

        /// <summary>
        /// Called whenever the <see cref="SelectedColorAsText"/> property changes.
        /// </summary>
        /// <param name="sender">The object whose property changed.</param>
        /// <param name="args">Information about the property change.</param>
        public static void SelectedColorAsTextChanged(DependencyObject sender, DependencyPropertyChangedEventArgs args)
        {
            if (sender is CustomColorPicker picker && args.NewValue is string new_value)
            {
                if (picker.no_reentrancy) return;
                picker.no_reentrancy = true;
                picker.SelectedBrushItem = new BrushItem(ColorHelper.GetColorFromArgb(new_value));
                picker.SelectedBrushItem.Opacity = picker.CanSetSelectedColorOpacity ? ColorHelper.GetSolidColorBrushFromArg(new_value).Opacity : 1.0d;
                picker.SelectedColorOpacity = picker.SelectedBrushItem.Opacity;
                picker.SelectedSolidColorBrush = new SolidColorBrush(ColorHelper.GetColorFromArgb(new_value)) { Opacity = picker.SelectedColorOpacity };
                picker.SelectedColor = picker.SelectedBrushItem.Color;
                picker.no_reentrancy = false;
            }
        }

        #region Subregion: Unselection part
        /// <summary>
        /// Sets a specific <see cref="BrushItem"/> as unselected.
        /// </summary>
        /// <param name="brushItem">The brush item to deselect.</param>
        private void unselectBrushItem(BrushItem brushItem)
        {
            if (brushItem == null) return;

            if (brushItem.IsSelected)
            {
                brushItem.IsSelected = false;
                brushItem.setOpacity(1.0d);
                if (SelectedBrushItems.Contains(brushItem))
                    SelectedBrushItems.Remove(brushItem);
                if (GrayColorItems.Contains(brushItem))
                    previouslySelectedPredefinedGrayBrushItem = brushItem;
            }
        }

        /// <summary>
        /// Unselect any predefined item.
        /// </summary>
        private void unselectAllPredefined()
        {
            LightColorItems.ForEach(x => unselectBrushItem(x));
            MediumColorItems.ForEach(x => unselectBrushItem(x));
            DarkColorItems.ForEach(x => unselectBrushItem(x));
            GrayColorItems.ForEach(x => unselectBrushItem(x));
        }

        /// <summary>
        /// Unselect any custom item.
        /// </summary>
        private void unselectAllCustom()
        {
            CustomColors.ForEach(x => unselectBrushItem(x));
        }
        #endregion

        #region Subregion: Selection part
        /// <summary>
        /// Selects a specific brush item.
        /// </summary>
        /// <param name="brushItem">The brush item to be selected.</param>
        private void selectBrushItem(BrushItem brushItem)
        {
            if (brushItem == null) return;

            if (!brushItem.IsSelected)
            {
                if (CanSetSelectedColorOpacity)
                    brushItem.setOpacity(SelectedColorOpacity);
                brushItem.IsSelected = true;
                if (!SelectedBrushItems.Contains(brushItem))
                    SelectedBrushItems.Add(brushItem);
            }
        }

        /// <summary>
        /// Tries to find the most suitable color group regarding
        /// to current state.
        /// </summary>
        private void trySelectCorrespondingGroup()
        {
            if (SelectedBrushItem == null)
            {
                IsLightColorSelected = true;
                SelectedBrushItem = LightColorItems.First();
            }
            else
            {
                var selected_as_string = SelectedBrushItem.Color.ToString();
                if (LightColorItems.Any(x => x.ToString() == selected_as_string))
                    IsLightColorSelected = true;
                else if (MediumColorItems.Any(x => x.ToString() == selected_as_string))
                    IsMediumColorSelected = true;
                else if (DarkColorItems.Any(x => x.ToString() == selected_as_string))
                    IsDarkColorSelected = true;
                else if (GrayColorItems.Any(x => x.ToString() == selected_as_string))
                    IsGrayColorSelected = true;
            }
        }

        /// <summary>
        /// Selects all brush items that would match the currently selected brush item.
        /// </summary>
        /// <returns>True if at least an item matched in the predefined lists.</returns>
        private bool selectAllMatchingPredefined()
        {
            if (SelectedBrushItem == null) return false;

            var result = false;
            var selected_as_string = SelectedBrushItem.Color.ToString();
            var matchingGroup = (List<BrushItem>)null;
            if (LightColorItems.Any(x => x.Color.ToString() == selected_as_string))
            {
                IsLightColorSelected = true;
                matchingGroup = LightColorItems;
            }
            else if (MediumColorItems.Any(x => x.Color.ToString() == selected_as_string))
            {
                IsMediumColorSelected = true;
                matchingGroup = MediumColorItems;
            }
            else if (DarkColorItems.Any(x => x.ToString() == selected_as_string))
            {
                IsDarkColorSelected = true;
                matchingGroup = DarkColorItems;
            }
            else if (GrayColorItems.Any(x => x.ToString() == selected_as_string))
            {
                IsGrayColorSelected = true;
                matchingGroup = GrayColorItems;
            }

            if (matchingGroup != null)
            {
                var matches = matchingGroup.FindAll(x => x.ToString() == selected_as_string);
                SelectedBrushItems.AddRange(matches);
                matches.ForEach(x => selectBrushItem(x));
                result = matches.Count > 0;
                PredefinedColors = matchingGroup;
                if (result)
                {
                    // Set previously selected items:
                    if (matchingGroup == GrayColorItems)
                        previouslySelectedPredefinedGrayBrushItem = matches.First();
                    else
                        previouslySelectedPredefinedNonGrayBrushItem = matches.First();
                }
            }

            return result;
        }

        /// <summary>
        /// Selects any custom colors that would match the current selected item brush.
        /// </summary>
        /// <returns>True if any matched in custom colors.</returns>
        private bool selectInCustom()
        {
            if (SelectedBrushItem == null) return false;

            var selected_as_string = SelectedBrushItem.Color.ToString();
            var matching = CustomColors.FindAll(x => x.ToString() == selected_as_string);
            matching.ForEach(x => selectBrushItem(x));
            return matching.Count > 0;

        }
        #endregion

        /// <summary>
        /// Manages a color selection change by the user.
        /// </summary>
        private void onSelectionChanged()
        {
            // First of all, unselect all previous items:
            unselectAllPredefined();
            unselectAllCustom();

            // Then update current selected state:
            if (SelectedBrushItem != null)
            {
                //selectBrushItem(SelectedBrushItem);
                var found_a_match = selectAllMatchingPredefined();
                found_a_match |= selectInCustom();
                if (!found_a_match)
                {
                    var newList = new List<BrushItem>(CustomColors);
                    newList.Add(SelectedBrushItem);
                    CustomColors = newList;
                    selectInCustom();
                }

                // Check if new selection is a system color:
                if (CanSelectFromSystemColors && SelectedBrushItem != null)
                {
                    var sysmatch = SystemBrushItems.FirstOrDefault(x => x.Color.ToString() == SelectedBrushItem.Color.ToString());
                    if (sysmatch != null && !string.IsNullOrEmpty(sysmatch.BrushName))
                        SelectedBrushItem.setBrushName(sysmatch.BrushName);

                }
            }
        }
        #endregion
    }
}
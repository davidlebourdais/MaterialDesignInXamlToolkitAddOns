using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using MaterialDesignThemes.Wpf.AddOns.Utils;
using MaterialDesignThemes.Wpf.AddOns.Utils.Colors;
using MaterialDesignThemes.Wpf.AddOns.Utils.Commands;

namespace MaterialDesignThemes.Wpf.AddOns
{
    /// <summary>
    /// A color picker that offers users a selection of predefined colors to be picked up,
    /// or to define its own color or select from a preset of custom colors or from a a preset
    /// of system colors. 
    /// </summary>
    public class CustomColorPicker : Control
    {
        private double _originalLastItemOpacity = 1.0d;  // stores a trace of the last selected custom item opacity
        private BrushItem _previouslySelectedPredefinedNonGrayBrushItem;  // stores last selected 'colored' predefined value
        private BrushItem _previouslySelectedPredefinedGrayBrushItem;  // stores last selected 'gray scaled' predefined value
        private bool _noReentrancy;  // disables reentrancy when selected color changes.

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
            ResetPredefinedColors();
            if (CustomColorsFeed != null)
                SaveSelectedIntoCustomColorFeed();  // save before startup if user had set any useful custom value
            LoadCustomColorFeed();

            OpenCommand = new SimpleCommand(() => IsOpen = true);
            AddCustomColorCommand = new SimpleCommand(AddCurrentlyPickedCustomColor, () =>
                (CustomColors == null || CustomColors.Count == 0 || CustomColors.Last().Color.ToString() != PreviewedCustomColor.ToString())
                || CanSetSelectedColorOpacity && CustomColors.Last().BrushOpacity != _originalLastItemOpacity);
            ShowCustomColorPickingCommand = new SimpleCommand(() => IsCustomColorPickingShown = true);
            HideCustomColorPickingCommand = new SimpleCommand(() => IsCustomColorPickingShown = false);
            SelectColorCommand = new SimpleParameterizedCommand((rawBrushItem) =>
            {
                if (rawBrushItem is BrushItem brushItem)
                    SelectedBrushItem = brushItem;
            });

            if (SystemBrushItems != null)
                SelectedSystemBrushItem = SystemBrushItems.First();
           
            Loaded += CustomColorPicker_Loaded;
        }

        /// <summary>
        /// Occurs when the control is loaded and bindings are resolved. End some initialization here.
        /// </summary>
        /// <param name="sender">Unused. The object that triggered the loaded event.</param>
        /// <param name="e">unused.</param>
        private void CustomColorPicker_Loaded(object sender, RoutedEventArgs e)
        {
            Loaded -= CustomColorPicker_Loaded;

            // Select predefined group to be displayed at start-up:
            if (SelectedBrushItem == null)
            {
                IsLightColorSelected = true;
                SelectedBrushItem = LightColorItems.First();
            }
            else
            {
                var selectedAsString = SelectedBrushItem.Color.ToString();
                if (LightColorItems.Any(x => x.ToString() == selectedAsString))
                    IsLightColorSelected = true;
                else if (MediumColorItems.Any(x => x.ToString() == selectedAsString))
                    IsMediumColorSelected = true;
                else if (DarkColorItems.Any(x => x.ToString() == selectedAsString))
                    IsDarkColorSelected = true;
                else if (GrayColorItems.Any(x => x.ToString() == selectedAsString))
                    IsGrayColorSelected = true;
                else
                    IsLightColorSelected = true;
            }

            // Set custom color if not existing:
            if (!SystemBrushItems.Contains(SelectedBrushItem) && !CustomColors.Contains(SelectedBrushItem))
            {
                var customList = new List<BrushItem>(CustomColors)
                {
                    new BrushItem(SelectedBrushItem.Color, SelectedBrushItem.BrushOpacity)
                };
                CustomColors = customList;
                if (CustomColors.Count > 0)
                    SelectedBrushItem = CustomColors.Last();
                SelectBrushItem(SelectedBrushItem);
                SaveSelectedIntoCustomColorFeed();
            }
        }

        #region Dependency properties related to options
        /// <summary>
        /// Gets or sets a value indicating if the opacity of the
        /// selected color can be set.
        /// </summary>
        public bool CanSetSelectedColorOpacity
        {
            get => (bool)GetValue(CanSetSelectedColorOpacityProperty);
            set => SetCurrentValue(CanSetSelectedColorOpacityProperty, value);
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
        private static void CanSetSelectedColorOpacityChanged(DependencyObject sender, DependencyPropertyChangedEventArgs args)
        {
            if (sender is CustomColorPicker picker && args.NewValue is bool)
            {
                picker.ResetSelectedColorOpacity();
            }
        }

        /// <summary>
        /// Gets or sets a value indicating if custom colors
        /// can be picked by the user.
        /// </summary>
        public bool CanChooseCustomColor
        {
            get => (bool)GetValue(CanChooseCustomColorProperty);
            set => SetCurrentValue(CanChooseCustomColorProperty, value);
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
        private static void CanChooseCustomColorChanged(DependencyObject sender, DependencyPropertyChangedEventArgs args)
        {
            if (sender is CustomColorPicker picker && args.NewValue is bool newValue)
            {
                if (!newValue)
                {
                    picker.IsCustomColorPickingShown = false;
                    // If we were on a custom color, reset to get a predefined one:
                    if (!picker.PredefinedColors.Contains(picker.SelectedBrushItem))
                        picker.SelectedBrushItem = picker.PredefinedColors.First();
                }
                else // Set custom color as currently selected color:
                {
                    if (picker.CanChooseCustomColor)
                        picker.PreviewedCustomColor = picker.SelectedColor;
                }
            }
        }

        /// <summary>
        /// Gets or sets a value indicating is user can pick custom colors from system brushes.
        /// </summary>
        public bool CanSelectFromSystemColors
        {
            get => (bool)GetValue(CanSelectFromSystemColorsProperty);
            set => SetCurrentValue(CanSelectFromSystemColorsProperty, value);
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
            set => SetCurrentValue(IsOpenProperty, value);
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
        private static void IsOpenChanged(DependencyObject sender, DependencyPropertyChangedEventArgs args)
        {
            if (!(sender is CustomColorPicker picker) || !(args.NewValue is bool newValue))
                return;
            
            picker.IsCustomColorPickingShown = false;   // reset to current state whenever the control is activated/deactivated.
            if (newValue)  // fill custom colors with app wide custom ones
                picker.LoadCustomColorFeed();
            else  // save selected if it is a new custom color.
                picker.SaveSelectedIntoCustomColorFeed();
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
            get => (double)GetValue(SelectedColorOpacityProperty);
            set => SetCurrentValue(SelectedColorOpacityProperty, value);
        }
        /// <summary>
        /// Registers <see cref="SelectedColorOpacity"/> as a dependency property.
        /// </summary>
        public static readonly DependencyProperty SelectedColorOpacityProperty
            = DependencyProperty.Register(nameof(SelectedColorOpacity), typeof(double), typeof(CustomColorPicker), new FrameworkPropertyMetadata(1.0d, SelectedColorOpacityChanged));

        /// <summary>
        /// Called whenever the <see cref="SelectedColorOpacity"/> property changes.
        /// </summary>
        /// <param name="sender">The object whose property changed.</param>
        /// <param name="args">Information about the property change.</param>
        private static void SelectedColorOpacityChanged(DependencyObject sender, DependencyPropertyChangedEventArgs args)
        {
            if (!(sender is CustomColorPicker picker) || !(args.NewValue is double))
                return;
            
            picker.ResetSelectedColorOpacity();
            picker.ResetCustomColor();
            if (picker.AddCustomColorCommand is SimpleCommand addCommand)  // used to lock when last custom is already current color.
                addCommand.RaiseCanExecuteChanged();
        }

        /// <summary>
        /// Changes the opacity of the selected color.
        /// </summary>
        private void ResetSelectedColorOpacity()
        {
            if (SelectedBrushItems.Count > 0)
                SelectedBrushItems.ForEach(x => x.SetBrushOpacity(CanSetSelectedColorOpacity ? SelectedColorOpacity : 1.0d));
        }
        #endregion

        #region System brush selection
        /// <summary>
        /// Gets the list of system colors as <see cref="BrushItem"/> objects.
        /// </summary>
        public static List<BrushItem> SystemBrushItems { get; } = GenerateSystemBrushItemsFromBrushes();

        /// <summary>
        /// Generates the list of system colors.
        /// </summary>
        /// <returns>A list of system colors as <see cref="BrushItem"/> objects.</returns>
        private static List<BrushItem> GenerateSystemBrushItemsFromBrushes()
        {
            // Get WPF brushes static properties that contains standard colors and add them in our list:
            var properties = typeof(Brushes).GetProperties(BindingFlags.Static | BindingFlags.Public);

            return properties.Select(property => new BrushItem((Brush)property.GetValue(null, null), property.Name)).ToList();
        }

        /// <summary>
        /// Gets or sets the selected brush item from system colors.
        /// </summary>
        public BrushItem SelectedSystemBrushItem
        {
            get => (BrushItem)GetValue(SelectedSystemBrushItemProperty);
            set => SetCurrentValue(SelectedSystemBrushItemProperty, value);
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
        private static void SelectedSystemBrushItemChanged(DependencyObject sender, DependencyPropertyChangedEventArgs args)
        {
            if (sender is CustomColorPicker picker && args.NewValue is BrushItem && picker.IsLoaded)
                picker.PreviewedCustomColor = picker.SelectedSystemBrushItem.Color;
        }
        #endregion

        #region Custom color selection
        /// <summary>
        /// Stores the list of all custom colors for the app domain.
        /// </summary>
        private static List<BrushItem> AppWideCustomColorsFeed { get; } = new List<BrushItem>();

        /// <summary>
        /// Gets or sets the list of recently created custom colors.
        /// </summary>
        public List<BrushItem> CustomColors
        {
            get => (List<BrushItem>)GetValue(CustomColorsProperty);
            protected set => SetValue(_customColorsPropertyKey, value);
        }
        private static readonly DependencyPropertyKey _customColorsPropertyKey
            = DependencyProperty.RegisterReadOnly(nameof(CustomColors), typeof(List<BrushItem>), typeof(CustomColorPicker), new FrameworkPropertyMetadata(new List<BrushItem>(), CustomColorsRelatedChanged));
        /// <summary>
        /// Registers <see cref="CustomColors"/> as a readonly dependency property.
        /// </summary>
        public static readonly DependencyProperty CustomColorsProperty = _customColorsPropertyKey.DependencyProperty;

        /// <summary>
        /// Gets or sets a feed of values to be set as custom colors.
        /// </summary>
        public List<BrushItem> CustomColorsFeed
        {
            get => (List<BrushItem>)GetValue(CustomColorsFeedProperty);
            set => SetCurrentValue(CustomColorsFeedProperty, value);
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
            get => (uint)GetValue(MaxCustomColorCountProperty);
            set => SetCurrentValue(MaxCustomColorCountProperty, value);
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
        private static void CustomColorsRelatedChanged(DependencyObject sender, DependencyPropertyChangedEventArgs args)
        {
            if (!(sender is CustomColorPicker picker) || (!(args.NewValue is List<BrushItem>) && !(args.NewValue is uint)))
                return;

            if (picker.CustomColors.Count <= picker.MaxCustomColorCount)
                return;
            
            var newList = picker.CustomColors;
            while (newList.Count > picker.MaxCustomColorCount)
                newList.RemoveAt(0);
            picker.CustomColors = new List<BrushItem>(newList);
        }

        /// <summary>
        /// Gets the command to validate the addition of a custom color.
        /// </summary>
        public ICommand AddCustomColorCommand { get; }

        /// <summary>
        /// Adds the currently previewed color to the custom color list and selects it.
        /// </summary>
        private void AddCurrentlyPickedCustomColor()
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
            set => SetCurrentValue(IsCustomColorPickingShownProperty, value);
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
        private static void IsCustomColorPickingShownChanged(DependencyObject sender, DependencyPropertyChangedEventArgs args)
        {
            if (sender is CustomColorPicker picker && args.NewValue is bool)
                picker._originalLastItemOpacity = picker.SelectedColorOpacity;  // store last saved opacity state, so we can update the "add" button state for a better user xp.
        }

        /// <summary>
        /// Gets or sets a custom color to be previewed.
        /// </summary>
        public Color PreviewedCustomColor
        {
            get => (Color)GetValue(PreviewedCustomColorProperty);
            set => SetCurrentValue(PreviewedCustomColorProperty, value);
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
        private static void PreviewedCustomColorChanged(DependencyObject sender, DependencyPropertyChangedEventArgs args)
        {
            if (!(sender is CustomColorPicker picker) || !(args.NewValue is Color))
                return;
            
            if (picker._noReentrancy)
                return;
            
            picker._noReentrancy = true;
            picker.ResetCustomColor();
            picker.PreviewedBrushAsText = picker.PreviewedCustomColor.ToString().Remove(0, 3);
            
            if (picker.AddCustomColorCommand is SimpleCommand addCommand)  // used to lock when last custom is already current color.
                addCommand.RaiseCanExecuteChanged();
            
            picker._noReentrancy = false;
        }

        /// <summary>
        /// Gets <see cref="PreviewedCustomColor"/> as a <see cref="SolidColorBrush"/>.
        /// </summary>
        public SolidColorBrush PreviewedCustomSolidColorBrush
        {
            get => (SolidColorBrush)GetValue(PreviewedCustomSolidColorBrushProperty);
            protected set => SetValue(_previewedCustomSolidColorBrushPropertyKey, value);
        }
        private static readonly DependencyPropertyKey _previewedCustomSolidColorBrushPropertyKey
            = DependencyProperty.RegisterReadOnly(nameof(PreviewedCustomSolidColorBrush), typeof(SolidColorBrush), typeof(CustomColorPicker), new FrameworkPropertyMetadata(default(SolidColorBrush)));
        /// <summary>
        /// Registers <see cref="PreviewedCustomSolidColorBrush"/> as a readonly dependency property.
        /// </summary>
        public static readonly DependencyProperty PreviewedCustomSolidColorBrushProperty = _previewedCustomSolidColorBrushPropertyKey.DependencyProperty;

        /// <summary>
        /// Gets or sets the color that was selected, as a <see cref="string"/> in hexadecimal format.
        /// </summary>
        public string PreviewedBrushAsText
        {
            get => (string)GetValue(PreviewedBrushAsTextProperty);
            set => SetCurrentValue(PreviewedBrushAsTextProperty, value);
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
        private static void PreviewedBrushAsTextChanged(DependencyObject sender, DependencyPropertyChangedEventArgs args)
        {
            if (!(sender is CustomColorPicker picker) || !(args.NewValue is string newValue))
                return;
            
            if (picker._noReentrancy) return;
            picker._noReentrancy = true;
            try
            {
                var newPreviewed = ColorHelper.GetNullableColorFromArgb(newValue);
                if (newPreviewed == null)
                    return;
                picker.PreviewedCustomColor = Color.FromArgb(255, newPreviewed.Value.R, newPreviewed.Value.G, newPreviewed.Value.B);  // reset alpha value.
                picker.ResetCustomColor();
                if (picker.CanSetSelectedColorOpacity)
                    picker.SelectedColorOpacity = newPreviewed.Value.A / 255.0d;
            }
            catch
            {
                throw new Exception("Invalid hexadecimal color");
            }
            finally
            {
                picker._noReentrancy = false;
            }
        }

        /// <summary>
        /// Resets the custom color preview to a new value.
        /// </summary>
        private void ResetCustomColor()
        {
            var opacity = CanSetSelectedColorOpacity ? SelectedColorOpacity : 1.0d;
            PreviewedCustomSolidColorBrush = new SolidColorBrush(PreviewedCustomColor) { Opacity = opacity };
        }

        /// <summary>
        /// Loads custom color from a feed.
        /// </summary>
        private void LoadCustomColorFeed()
        {
            // If a feed is given, load custom colors from it or use app wide feed:
            CustomColors = CustomColorsFeed != null ? new List<BrushItem>(CustomColorsFeed) : new List<BrushItem>(AppWideCustomColorsFeed);
        }

        /// <summary>
        /// Save new selected custom color into the feed.
        /// </summary>
        private void SaveSelectedIntoCustomColorFeed()
        {
            // Only if custom color is selected:
            if (!CustomColors.Contains(SelectedBrushItem))
                return;
            
            // If a feed is given, manage it, otherwise use the static app wide feed:
            if (CustomColorsFeed != null)
            {
                if (CustomColorsFeed.All(x => SelectedBrushItem.Color.ToString() != x.Color.ToString()))
                    CustomColorsFeed.Add(SelectedBrushItem);
            }
            else if (AppWideCustomColorsFeed.All(x => SelectedBrushItem.Color.ToString() != x.Color.ToString()))
                AppWideCustomColorsFeed.Add(SelectedBrushItem);
        }
        #endregion

        #region Predefined color part
        /// <summary>
        /// Gets or sets a value indicating if the light colors group is shown or not.
        /// </summary>
        public bool IsLightColorSelected
        {
            get => (bool)GetValue(IsLightColorSelectedProperty);
            set => SetCurrentValue(IsLightColorSelectedProperty, value);
        }
        /// <summary>
        /// Registers <see cref="IsLightColorSelected"/> as a dependency property.
        /// </summary>
        public static readonly DependencyProperty IsLightColorSelectedProperty
            = DependencyProperty.Register(nameof(IsLightColorSelected), typeof(bool), typeof(CustomColorPicker), new FrameworkPropertyMetadata(false, IsLightColorSelectedChanged));

        /// <summary>
        /// Gets or sets a value indicating if the middle luminosity colors group is shown or not.
        /// </summary>
        public bool IsMediumColorSelected
        {
            get => (bool)GetValue(IsMediumColorSelectedProperty);
            set => SetCurrentValue(IsMediumColorSelectedProperty, value);
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
            set => SetCurrentValue(IsDarkColorSelectedProperty, value);
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
            set => SetCurrentValue(IsGrayColorSelectedProperty, value);
        }
        /// <summary>
        /// Registers <see cref="IsGrayColorSelected"/> as a dependency property.
        /// </summary>
        public static readonly DependencyProperty IsGrayColorSelectedProperty
            = DependencyProperty.Register(nameof(IsGrayColorSelected), typeof(bool), typeof(CustomColorPicker), new FrameworkPropertyMetadata(default(bool), IsGrayColorSelectedChanged));

        /// <summary>
        /// Gets the list of light color brush items.
        /// </summary>
        private List<BrushItem> LightColorItems { get; set; }

        /// <summary>
        /// Gets the list of medium color brush items.
        /// </summary>
        private List<BrushItem> MediumColorItems { get; set; }

        /// <summary>
        /// Gets the list of dark color items.
        /// </summary>
        private List<BrushItem> DarkColorItems { get; set; }

        /// <summary>
        /// Gets the list of gray scale color brush items.
        /// </summary>
        private List<BrushItem> GrayColorItems { get; set; }

        /// <summary>
        /// Determines the number of Hue/saturation level we should display.
        /// </summary>
        public uint PredefinedColorsCount
        {
            get => (uint)GetValue(PredefinedColorsCountProperty);
            set => SetCurrentValue(PredefinedColorsCountProperty, value);
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
            get => (List<BrushItem>)GetValue(PredefinedColorsProperty);
            protected set => SetValue(_predefinedColorsPropertyKey, value);
        }
        private static readonly DependencyPropertyKey _predefinedColorsPropertyKey
            = DependencyProperty.RegisterReadOnly(nameof(PredefinedColors), typeof(List<BrushItem>), typeof(CustomColorPicker), new FrameworkPropertyMetadata(new List<BrushItem>()));
        /// <summary>
        /// Registers <see cref="PredefinedColors"/> as a readonly dependency property.
        /// </summary>
        public static readonly DependencyProperty PredefinedColorsProperty = _predefinedColorsPropertyKey.DependencyProperty;

        /// <summary>
        /// Called whenever the <see cref="PredefinedColorsCount"/> property changes.
        /// </summary>
        /// <param name="sender">The object whose property changed.</param>
        /// <param name="args">Information about the property change.</param>
        private static void PredefinedColorsCountChanged(DependencyObject sender, DependencyPropertyChangedEventArgs args)
        {
            if (!(sender is CustomColorPicker picker) || !(args.NewValue is uint))
                return;
            
            picker.ResetPredefinedColors();
            
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

        /// <summary>
        /// Called whenever the <see cref="IsLightColorSelected"/> property changes.
        /// </summary>
        /// <param name="sender">The object whose property changed.</param>
        /// <param name="args">Information about the property change.</param>
        private static void IsLightColorSelectedChanged(DependencyObject sender, DependencyPropertyChangedEventArgs args)
        {
            if (!(sender is CustomColorPicker picker) || !(args.NewValue is bool newValue))
                return;
            
            if (newValue)
                picker.SetPredefinedColorGroup(picker.LightColorItems);
        }

        /// <summary>
        /// Called whenever the <see cref="IsMediumColorSelected"/> property changes.
        /// </summary>
        /// <param name="sender">The object whose property changed.</param>
        /// <param name="args">Information about the property change.</param>
        private static void IsMediumColorSelectedChanged(DependencyObject sender, DependencyPropertyChangedEventArgs args)
        {
            if (!(sender is CustomColorPicker picker) || !(args.NewValue is bool newValue))
                return;
            
            if (newValue)
                picker.SetPredefinedColorGroup(picker.MediumColorItems);
        }

        /// <summary>
        /// Called whenever the <see cref="IsDarkColorSelected"/> property changes.
        /// </summary>
        /// <param name="sender">The object whose property changed.</param>
        /// <param name="args">Information about the property change.</param>
        private static void IsDarkColorSelectedChanged(DependencyObject sender, DependencyPropertyChangedEventArgs args)
        {
            if (!(sender is CustomColorPicker picker) || !(args.NewValue is bool newValue))
                return;
            
            if (newValue)
                picker.SetPredefinedColorGroup(picker.DarkColorItems);
        }

        /// <summary>
        /// Called whenever the <see cref="IsGrayColorSelected"/> property changes.
        /// </summary>
        /// <param name="sender">The object whose property changed.</param>
        /// <param name="args">Information about the property change.</param>
        private static void IsGrayColorSelectedChanged(DependencyObject sender, DependencyPropertyChangedEventArgs args)
        {
            if (!(sender is CustomColorPicker picker) || !(args.NewValue is bool newValue))
                return;
            
            if (newValue)
                picker.SetPredefinedColorGroup(picker.GrayColorItems);
        }

        /// <summary>
        /// Sets the main color group with the passed new selection.
        /// </summary>
        /// <param name="newSelection">The new predefined color group to be displayed.</param>
        private void SetPredefinedColorGroup(List<BrushItem> newSelection)
        {
            if (newSelection == PredefinedColors)
                return;
            
            var hadPredefinedItemSelected = SelectedBrushItem != null && PredefinedColors.Contains(SelectedBrushItem);
            var previousSelection = PredefinedColors;
            PredefinedColors = newSelection;
                
            if (!hadPredefinedItemSelected || PredefinedColors.Contains(SelectedBrushItem))
                return;
                
            // If going to gray scale, select first or previously saved gray color:
            if (previousSelection != GrayColorItems && newSelection == GrayColorItems)
                SelectedBrushItem = _previouslySelectedPredefinedGrayBrushItem ?? PredefinedColors.First();
            else  // going to 'colored' so find matching hue or select first.
            {
                // Must do the equivalent finding in any cases, but we get the previously selected color in case we went from gray scale selection:
                var selectedItem = previousSelection == GrayColorItems && newSelection != GrayColorItems ? _previouslySelectedPredefinedNonGrayBrushItem ?? SelectedBrushItem : SelectedBrushItem;
                var selectedAsDrawingColorHue = System.Drawing.Color.FromArgb(selectedItem.Color.R, selectedItem.Color.G, selectedItem.Color.B).GetHue();
                var equivalent = PredefinedColors.FirstOrDefault(x => (int)System.Drawing.Color.FromArgb(x.Color.R, x.Color.G, x.Color.B).GetHue() == (int)selectedAsDrawingColorHue);
                SelectedBrushItem = equivalent ?? PredefinedColors.First();
            }
            ResetSelectedColorOpacity();
        }

        /// <summary>
        /// Resets items of the various predefined groups. 
        /// </summary>
        private void ResetPredefinedColors()
        {
            LightColorItems = GenerateColorGroup(PredefinedColorsCount, 160);
            MediumColorItems = GenerateColorGroup(PredefinedColorsCount, 120);
            DarkColorItems = GenerateColorGroup(PredefinedColorsCount, 80);
            GrayColorItems = GenerateGrayColorGroup(PredefinedColorsCount);
        }

        /// <summary>
        /// Generates a specific set of colors based on specific passed parameters.
        /// </summary>
        /// <param name="count">Number of items to be generated.</param>
        /// <param name="luminosityLevel">The luminosity level to be applied to all colors in the generated group.</param>
        /// <param name="saturationLevel">The saturation level colors should have.</param>
        /// <returns>A list of predefined brush items.</returns>
        private static List<BrushItem> GenerateColorGroup(uint count, int luminosityLevel, int saturationLevel = 240)
        {
            var newGroup = new List<BrushItem>();
            if (count == 0) return newGroup;

            // Build colors based on hues at regular intervals:
            var step = 240 / count;
            for (uint i = 0; i < 240; i += step)
                newGroup.Add(new BrushItem(ColorHelper.GetColorFromHslParameters(i, saturationLevel, luminosityLevel)));

            return newGroup;
        }

        /// <summary>
        /// Generates a set a gray scale colors in a group.
        /// </summary>
        /// <param name="count">Number of items to be generated.</param>
        /// <returns>A list of predefined brush items.</returns>
        private static List<BrushItem> GenerateGrayColorGroup(uint count)
        {
            var newGroup = new List<BrushItem>();
            if (count == 0) return newGroup;

            // Build gray scale colors at regular intervals:
            var step = 255 / (count - 1);
            var max = step * (count - 1);
            for (uint i = 0; i < max; i += step)
                newGroup.Add(new BrushItem(Color.FromArgb(255, (byte)i, (byte)i, (byte)i)));
            newGroup.Add(new BrushItem(Color.FromArgb(255, 255, 255, 255)));

            return newGroup;
        }
        #endregion

        #region Selected color + selection management
        /// <summary>
        /// Gets the command for color selection.
        /// </summary>
        public ICommand SelectColorCommand { get; }

        /// <summary>
        /// Stores the list of selected brush items, when they are both available as custom, predefined, etc.
        /// </summary>
        private List<BrushItem> SelectedBrushItems { get; } = new List<BrushItem>();

        /// <summary>
        /// Gets the selected brush item.
        /// </summary>
        public BrushItem SelectedBrushItem
        {
            get => (BrushItem)GetValue(SelectedBrushItemProperty);
            set => SetCurrentValue(SelectedBrushItemProperty, value);
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
        private static void SelectedBrushItemChanged(DependencyObject sender, DependencyPropertyChangedEventArgs args)
        {
            if (!(sender is CustomColorPicker picker) || !(args.NewValue is BrushItem))
                return;
            
            if (picker._noReentrancy) return;
            picker._noReentrancy = true;
            picker.OnSelectionChanged();
            var opacity = picker.CanSetSelectedColorOpacity ? picker.SelectedColorOpacity : 1.0d;
            picker.SelectedBrushItem.SetBrushOpacity(opacity);
            picker.SelectedColor = picker.SelectedBrushItem.Color;
            picker.SelectedSolidColorBrush = picker.SelectedBrushItem.AsSolidColorBrush;
            picker.SelectedColorAsText = ColorHelper.GetRgbStringFromColor(picker.SelectedBrushItem.Color);
            picker._noReentrancy = false;
            if (picker.CanChooseCustomColor)
                picker.PreviewedCustomColor = picker.SelectedColor;
        }

        /// <summary>
        /// Gets or sets the selected color item.
        /// </summary>
        public Color SelectedColor
        {
            get => (Color)GetValue(SelectedColorProperty);
            set => SetCurrentValue(SelectedColorProperty, value);
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
        private static void SelectedColorChanged(DependencyObject sender, DependencyPropertyChangedEventArgs args)
        {
            if (!(sender is CustomColorPicker picker) || !(args.NewValue is Color newValue))
                return;
            
            if (picker._noReentrancy) return;
            picker._noReentrancy = true;
            picker.SelectedBrushItem = new BrushItem(newValue);
            var opacity = picker.CanSetSelectedColorOpacity ? picker.SelectedColorOpacity : 1.0d;
            picker.SelectedBrushItem.SetBrushOpacity(opacity);
            picker.SelectedSolidColorBrush = new SolidColorBrush(newValue) { Opacity = opacity };
            picker.SelectedColorAsText = ColorHelper.GetRgbStringFromColor(newValue);
            picker._noReentrancy = false;
            
            if (picker.CanChooseCustomColor)
                picker.PreviewedCustomColor = picker.SelectedColor;
        }

        /// <summary>
        /// Gets or sets the color that was selected, as a <see cref="SolidColorBrush"/>.
        /// </summary>
        public SolidColorBrush SelectedSolidColorBrush
        {
            get => (SolidColorBrush)GetValue(SelectedSolidColorBrushProperty);
            set => SetCurrentValue(SelectedSolidColorBrushProperty, value);
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
        private static void SelectedSolidColorBrushChanged(DependencyObject sender, DependencyPropertyChangedEventArgs args)
        {
            if (!(sender is CustomColorPicker picker) || !(args.NewValue is SolidColorBrush newValue))
                return;
            
            if (picker._noReentrancy) 
                return;
            
            picker._noReentrancy = true;
            picker.SelectedBrushItem = new BrushItem(newValue.Color);
            picker.SelectedBrushItem.SetBrushOpacity(picker.CanSetSelectedColorOpacity ? newValue.Opacity : 1.0d);
            picker.SelectedColor = picker.SelectedBrushItem.Color;
            picker.SelectedColorOpacity = picker.SelectedBrushItem.BrushOpacity;
            picker.SelectedColorAsText = ColorHelper.GetRgbStringFromColor(picker.SelectedBrushItem.Color);
            picker._noReentrancy = false;
            
            if (picker.CanChooseCustomColor)
                picker.PreviewedCustomColor = picker.SelectedColor;
        }

        /// <summary>
        /// Gets or sets the color that was selected, as a <see cref="string"/>.
        /// </summary>
        public string SelectedColorAsText
        {
            get => (string)GetValue(SelectedColorAsTextProperty);
            set => SetCurrentValue(SelectedColorAsTextProperty, value);
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
        private static void SelectedColorAsTextChanged(DependencyObject sender, DependencyPropertyChangedEventArgs args)
        {
            if (!(sender is CustomColorPicker picker) || !(args.NewValue is string newValue))
                return;
            if (picker._noReentrancy) 
                return;
                
            picker._noReentrancy = true;
            picker.SelectedBrushItem = new BrushItem(ColorHelper.GetColorFromArgb(newValue));
            var opacity = ColorHelper.GetOpacityFromArgb(newValue);
            if (opacity != 1.0d)
            { 
                picker.SelectedBrushItem.SetBrushOpacity(picker.CanSetSelectedColorOpacity ? opacity : 1.0d);
                picker.SelectedColorOpacity = picker.SelectedBrushItem.BrushOpacity;

                if (picker.CanSetSelectedColorOpacity)
                    picker.SelectedColorAsText = ColorHelper.GetArgbStringFromColor(ColorHelper.GetColorFromArgb(newValue, 1.0d)); // reset color alpha channel to max so we decouple opacity setting from color.
            }
            picker.SelectedSolidColorBrush = new SolidColorBrush(ColorHelper.GetColorFromArgb(newValue)) { Opacity = picker.SelectedColorOpacity };
            picker.SelectedColor = picker.SelectedBrushItem.Color;
            picker._noReentrancy = false;
            
            if (picker.CanChooseCustomColor)
                picker.PreviewedCustomColor = picker.SelectedColor;
        }

        #region Subregion: Unselection part
        /// <summary>
        /// Sets a specific <see cref="BrushItem"/> as unselected.
        /// </summary>
        /// <param name="brushItem">The brush item to deselect.</param>
        private void UnselectBrushItem(BrushItem brushItem)
        {
            if (brushItem == null || !brushItem.IsSelected) 
                return;

            brushItem.IsSelected = false;
            brushItem.SetBrushOpacity(1.0d);
            if (SelectedBrushItems.Contains(brushItem))
                SelectedBrushItems.Remove(brushItem);
            if (GrayColorItems.Contains(brushItem))
                _previouslySelectedPredefinedGrayBrushItem = brushItem;
        }

        /// <summary>
        /// Unselect any predefined item.
        /// </summary>
        private void UnselectAllPredefined()
        {
            LightColorItems.ForEach(UnselectBrushItem);
            MediumColorItems.ForEach(UnselectBrushItem);
            DarkColorItems.ForEach(UnselectBrushItem);
            GrayColorItems.ForEach(UnselectBrushItem);
        }

        /// <summary>
        /// Unselect any custom item.
        /// </summary>
        private void UnselectAllCustom()
        {
            CustomColors.ForEach(UnselectBrushItem);
        }
        #endregion

        #region Subregion: Selection part
        /// <summary>
        /// Selects a specific brush item.
        /// </summary>
        /// <param name="brushItem">The brush item to be selected.</param>
        private void SelectBrushItem(BrushItem brushItem)
        {
            if (brushItem == null) return;

            if (brushItem.IsSelected)
                return;
            
            if (CanSetSelectedColorOpacity)
                brushItem.SetBrushOpacity(SelectedColorOpacity);
            
            brushItem.IsSelected = true;
            
            if (!SelectedBrushItems.Contains(brushItem))
                SelectedBrushItems.Add(brushItem);
        }

        ///// <summary>
        ///// Tries to find the most suitable color group regarding
        ///// to current state.
        ///// </summary>
        //private void TrySelectCorrespondingGroup()
        //{
        //    if (SelectedBrushItem == null)
        //    {
        //        IsLightColorSelected = true;
        //        SelectedBrushItem = LightColorItems.First();
        //    }
        //    else
        //    {
        //        var selected_as_string = SelectedBrushItem.Color.ToString();
        //        if (LightColorItems.Any(x => x.ToString() == selected_as_string))
        //            IsLightColorSelected = true;
        //        else if (MediumColorItems.Any(x => x.ToString() == selected_as_string))
        //            IsMediumColorSelected = true;
        //        else if (DarkColorItems.Any(x => x.ToString() == selected_as_string))
        //            IsDarkColorSelected = true;
        //        else if (GrayColorItems.Any(x => x.ToString() == selected_as_string))
        //            IsGrayColorSelected = true;
        //        else
        //            IsLightColorSelected = true;
        //    }
        //}

        /// <summary>
        /// Selects all brush items that would match the currently selected brush item.
        /// </summary>
        /// <returns>True if at least an item matched in the predefined lists.</returns>
        private bool SelectAllMatchingPredefined()
        {
            if (SelectedBrushItem == null) return false;

            var selectedAsString = SelectedBrushItem.Color.ToString();
            var matchingGroup = (List<BrushItem>)null;
            if (LightColorItems.Any(x => x.Color.ToString() == selectedAsString))
            {
                IsLightColorSelected = true;
                matchingGroup = LightColorItems;
            }
            else if (MediumColorItems.Any(x => x.Color.ToString() == selectedAsString))
            {
                IsMediumColorSelected = true;
                matchingGroup = MediumColorItems;
            }
            else if (DarkColorItems.Any(x => x.ToString() == selectedAsString))
            {
                IsDarkColorSelected = true;
                matchingGroup = DarkColorItems;
            }
            else if (GrayColorItems.Any(x => x.ToString() == selectedAsString))
            {
                IsGrayColorSelected = true;
                matchingGroup = GrayColorItems;
            }

            if (matchingGroup == null)
                return false;

            var matches = matchingGroup.FindAll(x => x.ToString() == selectedAsString);
            SelectedBrushItems.AddRange(matches);
            matches.ForEach(SelectBrushItem);
            
            PredefinedColors = matchingGroup;
            
            if (!(matches.Count > 0))
                return false;
            
            // Set previously selected items:
            if (matchingGroup == GrayColorItems)
                _previouslySelectedPredefinedGrayBrushItem = matches.First();
            else
                _previouslySelectedPredefinedNonGrayBrushItem = matches.First();

            return true;
        }

        /// <summary>
        /// Selects any custom colors that would match the current selected item brush.
        /// </summary>
        /// <returns>True if any matched in custom colors.</returns>
        private bool SelectInCustom()
        {
            if (SelectedBrushItem == null) return false;

            var selectedAsString = SelectedBrushItem.Color.ToString();
            var matching = CustomColors.FindAll(x => x.ToString() == selectedAsString);
            matching.ForEach(SelectBrushItem);
            return matching.Count > 0;

        }
        #endregion

        /// <summary>
        /// Manages a color selection change by the user.
        /// </summary>
        private void OnSelectionChanged()
        {
            // First of all, unselect all previous items:
            UnselectAllPredefined();
            UnselectAllCustom();

            // Then update current selected state:
            if (SelectedBrushItem == null)
                return;
            
            var foundAMatch = SelectAllMatchingPredefined();
            foundAMatch |= SelectInCustom();
            if (!foundAMatch)
            {
                var newList = new List<BrushItem>(CustomColors)
                {
                    SelectedBrushItem
                };
                CustomColors = newList;
                SelectInCustom();
            }

            // Check if new selection is a system color:
            if (!CanSelectFromSystemColors || SelectedBrushItem == null)
                return;
            
            var systemMatch = SystemBrushItems.FirstOrDefault(x => x.Color.ToString() == SelectedBrushItem.Color.ToString());
            if (!string.IsNullOrEmpty(systemMatch?.BrushName))
                SelectedBrushItem.SetBrushName(systemMatch.BrushName);
        }
        #endregion
    }
}
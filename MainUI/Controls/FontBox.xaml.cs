using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using Avalonia.Media;
using System;
using System.Linq;

namespace MainUI.Controls
{
    public class FontBox : UserControl
    {
        public static readonly AvaloniaProperty<FontFamily> SelectedFontFamilyProperty
            = AvaloniaProperty.Register<FontBox, FontFamily>(nameof(SelectedFontFamily), FontFamily.Default);
        public FontFamily SelectedFontFamily
        {
            get => GetValue(SelectedFontFamilyProperty);
            set
            {
                if (value == null) { throw new ArgumentNullException(nameof(value)); }
                if (value.Equals(SelectedFontFamily)) { return; }
                //value of this property will be set down in the SeectionChanged event
                cbxFonts.SelectedItem = cbxFonts.Items.Cast<FontFamily>().FirstOrDefault(f => f.Equals(value)) ??
                    throw new ArgumentOutOfRangeException(nameof(value), value, $"Unsupported font family, please select a value from {nameof(SupportedFonts)} .");
            }
        }
        public static FontFamily[] SupportedFonts { get; } = FontFamily.SystemFontFamilies
            .Where(f => SixLabors.Fonts.SystemFonts.TryFind(f.Name, out var _))
            .ToArray();
        private readonly ComboBox cbxFonts;
        public FontBox()
        {
            this.InitializeComponent();
            cbxFonts = this.FindControl<ComboBox>(nameof(cbxFonts));
            cbxFonts.SelectionChanged += CbxFonts_SelectionChanged;
            cbxFonts.Items = SupportedFonts;
            cbxFonts.SelectedIndex = 0;
        }

        private void CbxFonts_SelectionChanged(object? sender, SelectionChangedEventArgs e)
        {
            //We cleared the selection to force rerender, FML
            if (/*We cleared the selection*/e.AddedItems.Count == 0 || /*We reselected the item after a clear*/e.RemovedItems.Count == 0) { return; }
            cbxFonts.SelectedItem = null;
            cbxFonts.SelectedItem = e.AddedItems[0];
            SetValue(SelectedFontFamilyProperty, e.AddedItems[0]);
        }

        private void InitializeComponent() => AvaloniaXamlLoader.Load(this);
    }
}

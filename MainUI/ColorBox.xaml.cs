using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using System.Linq;
using System.Reflection;
using System;
using System.Numerics;
using System.Diagnostics;

namespace MainUI.Controls
{
    public class ColorBox : UserControl
    {
        public class ColorBoxItem
        {
            public IBitmap Sample { get; }
            public string Name { get; }
            public Color Color { get; }
            public ColorBoxItem(Color color, string name)
            {
                static uint ToRgba(Color color)
                {
                    unsafe
                    {
                        uint rgba = 98;
                        //endianess
                        byte* rgbaP = (byte*)&rgba;
                        rgbaP[0] = color.R;
                        rgbaP[1] = color.G;
                        rgbaP[2] = color.B;
                        rgbaP[3] = color.A;
                        return rgba;
                    }
                }
                Color = color;
                Name = name;
                var sample = new WriteableBitmap(new PixelSize(20, 20), default, Avalonia.Platform.PixelFormat.Rgba8888);
                uint colorRgba = ToRgba(Color);
                using (var sampleBuffer = sample.Lock())
                {
                    Span<uint> sampleBufferSpan;
                    unsafe
                    {
                        sampleBufferSpan = new Span<uint>(sampleBuffer.Address.ToPointer(), (sampleBuffer.Size.Height * sampleBuffer.RowBytes) / sizeof(uint));
                    }
                    for (int i = 0; i < sampleBufferSpan.Length; i++)
                    {
                        sampleBufferSpan[i] = colorRgba;
                    }
                }
                Sample = sample;
            }
        }
        public static ColorBoxItem[] Colors { get; } = typeof(Colors).GetProperties(BindingFlags.Public | BindingFlags.Static)
            .Select(p => new ColorBoxItem((Color)p.GetValue(null)!, p.Name))
            .ToArray();

        public static readonly StyledProperty<Color> SelectedColorProperty = AvaloniaProperty.Register<ColorBox, Color>(nameof(SelectedColor));

        public Color SelectedColor
        {
            get => GetValue(SelectedColorProperty);
            set
            {
                if (value.Equals(SelectedColor)) { return; }
                cbxColors.SelectedItem = Colors.First(i => i.Color.Equals(value));
            }
        }

        public static readonly StyledProperty<PixelSize> ColorSampleSizeProperty = AvaloniaProperty.Register<ColorBox, PixelSize>(nameof(ColorSampleSize), new PixelSize(5, 5));

        public PixelSize ColorSampleSize
        {
            get => GetValue(ColorSampleSizeProperty);
            set => SetValue(ColorSampleSizeProperty, value);
        }
        private readonly ComboBox cbxColors;
        public ColorBox()
        {
            this.InitializeComponent();
            cbxColors = this.FindControl<ComboBox>(nameof(cbxColors));
            cbxColors.Items = Colors;
            cbxColors.SelectedIndex = 0;
            cbxColors.SelectionChanged += ColorBox_SelectionChanged;
        }
        private void ColorBox_SelectionChanged(object? sender, SelectionChangedEventArgs e)
        {
            //We cleared the selection to force rerender, FML
            if (/*We cleared the selection*/e.AddedItems.Count == 0 || /*We reselected the item after a clear*/e.RemovedItems.Count == 0) { return; }
            cbxColors.SelectedItem = null;
            cbxColors.SelectedItem = e.AddedItems[0];
            SetValue(SelectedColorProperty, (e.AddedItems[0] as ColorBoxItem)!.Color);
        }

        private void InitializeComponent() => AvaloniaXamlLoader.Load(this);
    }
}

using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Markup.Xaml;
using Avalonia.Media.Imaging;
using RepoImageMan;
using System;
using System.Collections.Generic;
using System.IO;
using System.Reactive.Linq;
using SizeF = SixLabors.Primitives.SizeF;
using Size = SixLabors.Primitives.Size;
using TPixel = SixLabors.ImageSharp.PixelFormats.Rgba32;
using System.Diagnostics;
using SixLabors.ImageSharp.Advanced;
using System.Linq;
using Avalonia.Interactivity;
using System.ComponentModel;
using MainUI.Controls;
using Avalonia.Threading;

namespace MainUI
{
    //AvaloniUI doesn't support generic window classes
    public class DesigningWindow/*<TPixel>*/ : Window /*where TPixel : unmanaged, SixLabors.ImageSharp.PixelFormats.IPixel<TPixel>*/
    {
        private readonly List<IDisposable> _eventsSubscriptions = new List<IDisposable>();
        private readonly Image imgPlayground;
        private readonly MenuItem miDeleteSelectedCommodity, miReloadSelectedCommodity;
        private readonly ContextMenu imgPlaygroundCTXMenu;
        private readonly DesignCImage<TPixel> _image;
        private readonly ColorBox cbLabelColor;
        private readonly FontBox fbLabelFont;
        private bool _isSelectedCommodityHooked = false;
        private SizeF _toImageMappingScale, _fromImageMappingScale;
        private readonly Settings _settings;
        private DesignImageCommodity<TPixel>? _selectedCommodity;
        private readonly NumericUpDown nudImageContrast, nudImageBrightness, nudLabelSize;

        private string GetCommdoityShortName(DesignImageCommodity<TPixel> com)
        {
            var name = com.Commodity.Name;
            return name.Length <= 10 ? name : $"{name.Substring(0, 7)}...";
        }

        private IDisposable[] _selectedCommodityNotificationSubs;
        private DesignImageCommodity<TPixel>? SelectedCommodity
        {
            get => _selectedCommodity;
            set
            {
                if (_selectedCommodity == value) { return; }
                if (_selectedCommodity != null)
                {
                    _selectedCommodity.Commodity.Deleting -= SelectedCommodity_Deleting;
                    _selectedCommodity.IsSurrounded = false;
                    foreach (var sub in _selectedCommodityNotificationSubs)
                    {
                        sub.Dispose();
                    }
                }
                _selectedCommodity = value;
                if (_selectedCommodity != null)
                {
                    _selectedCommodity.Commodity.Deleting += SelectedCommodity_Deleting;
                    _selectedCommodity.IsSurrounded = true;
                    _selectedCommodityNotificationSubs = new IDisposable[]
                    {
                        _selectedCommodity.Commodity
                        .Where(pn => pn == nameof(ImageCommodity.LabelColor))
                        .Subscribe(_ => cbLabelColor.SelectedColor = _selectedCommodity.Commodity.LabelColor.ToAvalonia()),
                        _selectedCommodity.Commodity
                        .Where(pn => pn == nameof(ImageCommodity.Font))
                        .Subscribe(_ =>
                        {
                            fbLabelFont.SelectedFontFamily = new Avalonia.Media.FontFamily(_selectedCommodity.Commodity.Font.Name);
                            nudLabelSize.Value = _selectedCommodity.Commodity.Font.Size;
                        })
                    };
                    nudLabelSize.Value = _selectedCommodity.Commodity.Font.Size;
                    cbLabelColor.SelectedColor = _selectedCommodity.Commodity.LabelColor.ToAvalonia();
                    fbLabelFont.SelectedFontFamily = new Avalonia.Media.FontFamily(_selectedCommodity.Commodity.Font.Name);
                }
            }
        }

        private void SelectedCommodity_Deleting(Commodity _) => _selectedCommodity = null;

        public DesigningWindow() : this(null) { }
        /// <summary>
        /// Would resize the image as specified by <see cref="Settings.DesigningWindowResizingScale"/>
        /// </summary>
        private void ResizeImage()
        {
            if (_settings.DesigningWindowResizingScale.Width != 0.0f && _settings.DesigningWindowResizingScale.Height != 0.0f)
            {
                if (_settings.IsDesigningWindowResizingScaleDynamic)
                {
                    _image.InstanceSize = new Size
                    {
                        Width = (int)(imgPlayground.Width * _settings.DesigningWindowResizingScale.Width),
                        Height = (int)(imgPlayground.Height * _settings.DesigningWindowResizingScale.Height)
                    };
                }
                else
                {
                    _image.InstanceSize = _settings.DesigningWindowResizingScale.ToSize();
                }
            }
            _toImageMappingScale = _image.GetToInstanceMappingScale(new SizeF { Width = (int)imgPlayground.Width, Height = (int)imgPlayground.Height });
            _fromImageMappingScale = _image.GetFromInstanceMappingScale(new SizeF { Width = (int)imgPlayground.Width, Height = (int)imgPlayground.Height });
        }
        public DesigningWindow(DesignCImage<TPixel> image)
        {
            InitializeComponent();
#if DEBUG
            this.AttachDevTools();
#endif
            _settings = Settings.Load().Result;

            imgPlayground = this.FindControl<Image>(nameof(imgPlayground));
            miDeleteSelectedCommodity = this.FindControl<MenuItem>(nameof(miDeleteSelectedCommodity));
            miReloadSelectedCommodity = this.FindControl<MenuItem>(nameof(miReloadSelectedCommodity));
            imgPlaygroundCTXMenu = this.FindControl<ContextMenu>(nameof(imgPlaygroundCTXMenu));
            cbLabelColor = this.FindControl<ColorBox>(nameof(cbLabelColor));
            fbLabelFont = this.FindControl<FontBox>(nameof(fbLabelFont));
            nudLabelSize = this.FindControl<NumericUpDown>(nameof(nudLabelSize));
            nudImageContrast = this.FindControl<NumericUpDown>(nameof(nudImageContrast));
            nudImageBrightness = this.FindControl<NumericUpDown>(nameof(nudImageBrightness));


            _image = image ?? throw new ArgumentNullException(nameof(image));
            _image.ImageUpdated += Image_ImageUpdated;
            imgPlayground.PointerPressed += ImgPlayground_PointerPressed;
            imgPlayground.PointerReleased += ImgPlayground_PointerReleased;
            imgPlayground.PointerMoved += ImgPlayground_PointerMoved;


            _eventsSubscriptions.Add(this.GetObservable(Window.ClientSizeProperty).Subscribe(sz =>
            {
                imgPlayground.Height = sz.Height - 100;
                imgPlayground.Width = sz.Width - 10;
            }));


            _eventsSubscriptions.Add(
                imgPlayground.GetObservable(Image.WidthProperty)
                .Merge(imgPlayground.GetObservable(Image.HeightProperty))
                .Subscribe(_ => ResizeImage()));
            _eventsSubscriptions.Add(cbLabelColor.GetObservable(ColorBox.SelectedColorProperty).Subscribe(c =>
            {
                var selectedCom = SelectedCommodity;
                if (selectedCom == null) { return; }
                selectedCom.Commodity.LabelColor = c.ToSixLabors();
            }));
            _eventsSubscriptions.Add(fbLabelFont.GetObservable(FontBox.SelectedFontFamilyProperty).Subscribe(f =>
            {
                var selectedCom = SelectedCommodity?.Commodity;
                if (selectedCom == null) { return; }
                selectedCom.Font = SixLabors.Fonts.SystemFonts.CreateFont(f.Name, selectedCom.Font.Size);
            }));
            nudLabelSize.ValueChanged += NudLabelSize_ValueChanged;
            nudImageContrast.ValueChanged += NudImageContrast_ValueChanged;
            nudImageBrightness.ValueChanged += NudImageBrightness_ValueChanged;

            ReloadPlayground();

            imgPlaygroundCTXMenu.ContextMenuOpening += ImgPlaygroundCTXMenu_ContextMenuOpening;
            miDeleteSelectedCommodity.Click += MiDeleteSelectedCommodity_Click;
            miReloadSelectedCommodity.Click += MiReloadSelectedCommodity_Click;

        }

        private void NudImageBrightness_ValueChanged(object? sender, NumericUpDownValueChangedEventArgs e) => _image.Image.Brightness = (float)e.NewValue;

        private void NudImageContrast_ValueChanged(object? sender, NumericUpDownValueChangedEventArgs e) => _image.Image.Contrast = (float)e.NewValue;

        private void NudLabelSize_ValueChanged(object? sender, NumericUpDownValueChangedEventArgs e)
        {
            var selectedCom = SelectedCommodity?.Commodity;
            if (selectedCom == null) { return; }
            selectedCom.Font = new SixLabors.Fonts.Font(selectedCom.Font, (float)e.NewValue);
        }

        private async void MiReloadSelectedCommodity_Click(object? sender, RoutedEventArgs e) => await SelectedCommodity!.Commodity.Reload();

        private async void MiDeleteSelectedCommodity_Click(object? sender, RoutedEventArgs e) =>
            await SelectedCommodity!.Commodity.Delete();

        private void ImgPlaygroundCTXMenu_ContextMenuOpening(object sender, CancelEventArgs e)
        {
            var selectedCommodity = SelectedCommodity;
            miDeleteSelectedCommodity.IsVisible = miReloadSelectedCommodity.IsVisible = selectedCommodity != null;
            if (miDeleteSelectedCommodity.IsVisible)
            {
                miDeleteSelectedCommodity.Header = $"Delete(DEL) {GetCommdoityShortName(selectedCommodity!)}";
            }
            if (miReloadSelectedCommodity.IsVisible)
            {
                miReloadSelectedCommodity.Header = $"Reload {GetCommdoityShortName(selectedCommodity!)}";
            }
        }

        private void ImgPlayground_PointerReleased(object? sender, PointerReleasedEventArgs e)
        {
            _isSelectedCommodityHooked = false;
        }

        private void ReloadPlayground()
        {
            WriteableBitmap? bmp = null;
            if (Dispatcher.UIThread.CheckAccess()) { bmp = imgPlayground.Source as WriteableBitmap; }
            else { Dispatcher.UIThread.Post(() => bmp = imgPlayground.Source as WriteableBitmap); }
            if (bmp == null || (int)bmp.PixelSize.Width != _image.RenderedImage.Width || (int)bmp.PixelSize.Height != _image.RenderedImage.Height)
            {
                bmp?.Dispose();
                bmp = new WriteableBitmap(new PixelSize(_image.RenderedImage.Width, _image.RenderedImage.Height), new Vector(), Avalonia.Platform.PixelFormat.Rgba8888);
                Trace.WriteLine("CREATED NEW BMP");
            }
            using (var bmpBuffer = bmp.Lock())
            {
                Trace.WriteLine("Wrting new image data");
                var renderedImagePixels = System.Runtime.InteropServices.MemoryMarshal.AsBytes(_image.RenderedImage.GetPixelSpan());
                Span<byte> bmpBufferSpan;
                unsafe
                {
                    bmpBufferSpan = new Span<byte>(bmpBuffer.Address.ToPointer(), bmpBuffer.Size.Height * bmpBuffer.RowBytes);
                }

                renderedImagePixels.CopyTo(bmpBufferSpan);
                Trace.WriteLine("Wrote new image data");
            }
            if (Dispatcher.UIThread.CheckAccess())
            {
                imgPlayground.Source = null;
                imgPlayground.Source = bmp;
            }
            else
            {
                Dispatcher.UIThread.Post(() =>
                {
                    imgPlayground.Source = null;
                    imgPlayground.Source = bmp;
                });
            }
            Trace.WriteLine("Updated image box");
        }
        private void Image_ImageUpdated(DesignCImage<TPixel> sender) => ReloadPlayground();

        private void ImgPlayground_PointerMoved(object? sender, PointerEventArgs e)
        {
            if (_isSelectedCommodityHooked == false) { return; }
            var point = e.GetCurrentPoint(imgPlayground);
            SelectedCommodity.Location = point.Position.ToSixLabors().Scale(_toImageMappingScale);
        }
        private void ImgPlayground_PointerPressed(object? sender, PointerPressedEventArgs e)
        {
            var point = e.GetCurrentPoint(imgPlayground);
            var pointerPosition = point.Position.ToSixLabors().Scale(_toImageMappingScale);
            if (SelectedCommodity?.IsInHandle(pointerPosition) ?? false)
            {
                _isSelectedCommodityHooked = true;
            }
            else
            {
                SelectedCommodity = _image.FirstOnPoint(pointerPosition);
            }
        }

        private void InitializeComponent() => AvaloniaXamlLoader.Load(this);
    }
}

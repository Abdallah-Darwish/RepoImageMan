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
using PixelFormat = SixLabors.ImageSharp.PixelFormats.Rgba32;
using SizeF = SixLabors.Primitives.SizeF;
namespace MainUI
{
    public class DesigningWindow : Window
    {
        private readonly List<IDisposable> _eventsSubscriptions = new List<IDisposable>();
        private readonly Image imgPlayground;
        private readonly DesignCImage<PixelFormat> _image;
        private readonly MemoryStream _imageBuffer;
        private readonly SixLabors.ImageSharp.Formats.Jpeg.JpegEncoder _jpegEncoder = new SixLabors.ImageSharp.Formats.Jpeg.JpegEncoder() { Quality = 100, Subsample = SixLabors.ImageSharp.Formats.Jpeg.JpegSubsample.Ratio444 };
        private DesignImageCommodity<PixelFormat>? _selectedCommodity;
        private bool _isSelectedCommodityHooked = false;
        private SizeF _toImageMappingScale, _fromImageMappingScale;
        public DesigningWindow() : this(null) { }
        public DesigningWindow(DesignCImage<PixelFormat> image)
        {
            InitializeComponent();
#if DEBUG
            this.AttachDevTools();
#endif

            imgPlayground = this.FindControl<Image>(nameof(imgPlayground));
            _image = image;
            _imageBuffer = new MemoryStream(_image.Image.Size.Width * _image.Image.Size.Height * (_image.RenderedImage.PixelType.BitsPerPixel / 8) + 100);
            _image.ImageUpdated += Image_ImageUpdated;
            imgPlayground.PointerPressed += ImgPlayground_PointerPressed;
            imgPlayground.PointerReleased += ImgPlayground_PointerReleased;
            imgPlayground.PointerMoved += ImgPlayground_PointerMoved;


            _eventsSubscriptions.Add(this.GetObservable(Window.ClientSizeProperty).Do(sz =>
            {
                imgPlayground.Height = sz.Height - 100;
                imgPlayground.Width = sz.Width - 10;
            }).Subscribe());


            _eventsSubscriptions.Add(imgPlayground.GetObservable(Image.WidthProperty).Do(w =>
            {
                _image.InstanceSize = new SixLabors.Primitives.Size { Width = (int)w - 20, Height = _image.InstanceSize.Height };
            }).Subscribe());
            _eventsSubscriptions.Add(imgPlayground.GetObservable(Image.HeightProperty).Do(h =>
            {
                _image.InstanceSize = new SixLabors.Primitives.Size { Width = _image.InstanceSize.Width, Height = (int)h - 20 };
            }).Subscribe());


            ReloadPlayground();

        }

        private void ImgPlayground_PointerReleased(object? sender, PointerReleasedEventArgs e)
        {
            _isSelectedCommodityHooked = false;
        }

        private void ReloadPlayground()
        {
            _imageBuffer.Position = 0;
            _image.RenderedImage.Save(_imageBuffer, _jpegEncoder);
            _imageBuffer.SetLength(_imageBuffer.Position);
            _imageBuffer.Position = 0;
            (imgPlayground.Source as IBitmap)?.Dispose();
            imgPlayground.Source = new Bitmap(_imageBuffer);
        }
        private void Image_ImageUpdated(DesignCImage<PixelFormat> sender) => ReloadPlayground();

        private void ImgPlayground_PointerMoved(object? sender, PointerEventArgs e)
        {
            if (_isSelectedCommodityHooked == false) { return; }
            var point = e.GetCurrentPoint(imgPlayground);
            _selectedCommodity.Location = point.Position.ToSixLabors();
        }
        private void ResetSelectedCommodity()
        {
            _selectedCommodity = null;
            _isSelectedCommodityHooked = false;
        }
        private void ImgPlayground_PointerPressed(object? sender, PointerPressedEventArgs e)
        {
            var point = e.GetCurrentPoint(imgPlayground);
            if (point.Properties.IsLeftButtonPressed == false) { return; }
            if (_selectedCommodity != null && _selectedCommodity.IsInHandle(point.Position.ToSixLabors()))
            {
                _isSelectedCommodityHooked = true;
            }
            else
            {
                if (_selectedCommodity != null) { _selectedCommodity.IsSurrounded = false; }
                _selectedCommodity = _image.FirstOnPoint(point.Position.ToSixLabors());
                if (_selectedCommodity != null) { _selectedCommodity.IsSurrounded = true; }

            }
        }

        private void InitializeComponent()
        {
            AvaloniaXamlLoader.Load(this);
        }
    }
}

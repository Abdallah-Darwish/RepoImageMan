using System;
using System.Collections.Generic;
using System.Linq;
using System.Reactive.Linq;
using Avalonia.Controls;
using Avalonia;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Input;
using Avalonia.Themes.Default;

namespace RepoImageMan.Controls
{
    /// <summary>
    /// A specialization of <see cref="CImage"/> that is used for editing the image using UI and still provide fast operations.
    /// YOU CAN'T MODIFY IMAGE STREAM WHILE DESIGNING.
    /// </summary>
    public sealed partial class DesignCImage : Control, IDisposable
    {
        private Size InstanceSize => Bounds.Size;
        public Size ToDesignMappingScale => new Size(InstanceSize.Width / Image.Size.Width, InstanceSize.Height / Image.Size.Height);
        public Size ToOriginalMappingScale => new Size(Image.Size.Width / InstanceSize.Width, Image.Size.Height / InstanceSize.Height);


        protected override Size MeasureOverride(Size availableSize) => availableSize;

        public override void Render(DrawingContext ctx)
        {
            if (_bmp == null) { return; }
            if (_resizedBmp == null) { return; }
            base.Render(ctx!);
            if (_resizedBmp.PixelSize.ToSize(1.0) != InstanceSize)
            {
                ResizeBmp();
            }
            ctx!.DrawImage(_resizedBmp, 1.0,
                new Rect(0, 0, _resizedBmp.PixelSize.Width, _resizedBmp.PixelSize.Height),
                new Rect(new Point(0, 0), _resizedBmp.PixelSize.ToSize(1.0)), Avalonia.Visuals.Media.Imaging.BitmapInterpolationMode.LowQuality);
            foreach (var com in _coms)
            {
                ctx.DrawText(com.RenderingBrush, com.Location, com.Text);
            }
            var sc = GetDesignImageCommodity(SelectedCommodity);
            if (sc != null)
            {
                ctx.DrawRectangle(sc.RenderingPen, sc.Box);
                ctx.FillRectangle(Brushes.Blue, sc.HandleBox);
            }
        }

        public static readonly StyledProperty<ImageCommodity?> SelectedCommodityProperty = AvaloniaProperty
            .Register<DesignCImage, ImageCommodity?>(nameof(SelectedCommodity));
        public ImageCommodity? SelectedCommodity
        {
            get => GetValue(SelectedCommodityProperty);
            set => SetValue(SelectedCommodityProperty, value);
        }
        private void AddCommodity(CImage _, ImageCommodity com)
        {
            var dcom = new DesignImageCommodity(com, this);
            _coms.Add(dcom);
            InvalidateVisual();
        }
        private void RemoveCommodity(CImage _, ImageCommodity com)
        {
            if (com == SelectedCommodity) { SelectedCommodity = null; }
            var dcom = GetDesignImageCommodity(com);
            _coms.Remove(dcom);
            dcom.Dispose();
            InvalidateVisual();
        }
        private DesignImageCommodity? GetDesignImageCommodity(ImageCommodity? c) => _coms.FirstOrDefault(dc => dc.Commodity == c);
        private bool _isSelectedCommodityHooked = false;

        protected override void OnPointerPressed(PointerPressedEventArgs e)
        {
            base.OnPointerPressed(e);
            var p = e.GetCurrentPoint(this).Position;
            var dsc = GetDesignImageCommodity(SelectedCommodity);
            if (dsc?.IsInHandle(p) ?? false) { _isSelectedCommodityHooked = true; }
            else
            {
                SelectedCommodity = _coms.FirstOrDefault(c => c.IsIn(p))?.Commodity;
            }
        }
        protected override void OnPointerMoved(PointerEventArgs e)
        {
            base.OnPointerMoved(e);
            if (_isSelectedCommodityHooked == false) { return; }
            var p = e.GetCurrentPoint(this).Position;
            GetDesignImageCommodity(SelectedCommodity)!.Location = p;
        }
        protected override void OnPointerReleased(PointerReleasedEventArgs e)
        {
            base.OnPointerReleased(e);
            _isSelectedCommodityHooked = false;
        }
        private List<DesignImageCommodity> _coms = new List<DesignImageCommodity>();

        public CImage Image { get; private set; }
        private IDisposable[] _subs;
        public void Init(CImage img)
        {
            ClipToBounds = true;

            Image = img;
            foreach (var com in img.Commodities)
            {
                AddCommodity(null, com);
            }
            Image.CommodityAdded += AddCommodity;
            Image.CommodityRemoved += RemoveCommodity;
            Image.Deleting += HandleImageDeleteing; ;
            Image.FileUpdated += UpdateBmp;
            _subs = new IDisposable[]
            {
                Image.AsObservable()
                    .Where(pn => pn == nameof(CImage.Contrast) || pn == nameof(CImage.Brightness))
                    .Subscribe(_ =>
                    {
                        ApplyContrastBrightness();
                        ResizeBmp();
                    }),
                this.GetObservable(SelectedCommodityProperty)
                    .Subscribe(_ => InvalidateVisual())
            };

            UpdateBmp(null);
        }

        private void HandleImageDeleteing(CImage sender) => throw new InvalidOperationException("YOU CAN'T DELETE AN IMAGE WHILE ITS BEING DESIGNED");

        #region IDisposable Support
        private bool disposedValue = false; // To detect redundant calls

        void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                if (disposing)
                {
                    Image = null;
                }
                foreach (var sub in _subs)
                {
                    sub.Dispose();
                }
                foreach (var com in _coms)
                {
                    com.Dispose();
                }
                Image.Deleting -= HandleImageDeleteing;
                Image.CommodityAdded -= AddCommodity;
                Image.CommodityRemoved -= RemoveCommodity;
                Image.FileUpdated -= UpdateBmp;
                _coms.Clear();
                _coms = null;
                _subs = null;
                _bmp.Dispose();
                _resizedBmp.Dispose();
                _modedBmp.Dispose();
                _bmp = null;
                _resizedBmp = null;
                _modedBmp = null;
                disposedValue = true;
            }
        }

        ~DesignCImage()
        {
            // Do not change this code. Put cleanup code in Dispose(bool disposing) above.
            Dispose(false);
        }

        // This code added to correctly implement the disposable pattern.
        public void Dispose()
        {
            // Do not change this code. Put cleanup code in Dispose(bool disposing) above.
            Dispose(true);
            GC.SuppressFinalize(this);
        }
        #endregion
    }
}
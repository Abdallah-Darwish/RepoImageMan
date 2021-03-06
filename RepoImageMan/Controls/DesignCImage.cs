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
using System.Threading.Tasks;
using Avalonia.Threading;

namespace RepoImageMan.Controls
{
    /// <summary>
    /// A specialization of <see cref="CImage"/> that is used for editing the image using UI and still provide fast operations.
    /// </summary>
    public sealed partial class DesignCImage : Control, IDisposable
    {
        internal void SafeInvalidate() => Dispatcher.UIThread.Invoke(InvalidateVisual);
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
            ctx!.DrawImage(_resizedBmp,
                new Rect(0, 0, _resizedBmp.PixelSize.Width, _resizedBmp.PixelSize.Height),
                new Rect(new Point(0, 0), _resizedBmp.PixelSize.ToSize(1.0)));
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
            Dispatcher.UIThread.Invoke(() =>
            {
                if (com == SelectedCommodity) { SelectedCommodity = null; }
            });
            var dcom = GetDesignImageCommodity(com);
            _coms.Remove(dcom);
            dcom.Dispose();
            SafeInvalidate();
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
            try
            {
                GetDesignImageCommodity(SelectedCommodity)!.Location = p;
            }
            catch (ArgumentOutOfRangeException ex) when (ex.TargetSite?.Name == $"set_{nameof(ImageCommodity.Location)}")
            {
                _isSelectedCommodityHooked = false;
            }
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
            Focusable = true;
            if (img.TryEnterDesign() == false)
            {
                throw new InvalidOperationException("Image is currently opened for design in another window.");
            }
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
                        SafeInvalidate();
                    }),
                this.GetObservable(SelectedCommodityProperty)
                    .Subscribe(_ => SafeInvalidate())
            };

            UpdateBmp(null);
        }

        private void HandleImageDeleteing(CImage sender) => throw new InvalidOperationException("YOU CAN'T DELETE AN IMAGE WHILE ITS BEING DESIGNED");
        protected async override void OnKeyDown(KeyEventArgs e)
        {
            base.OnKeyDown(e);
            switch (e.Key)
            {
                case Key.Delete:
                    await (SelectedCommodity?.Delete() ?? Task.CompletedTask);
                    break;
                case Key.R:
                    if (e.KeyModifiers == KeyModifiers.Control)
                    {
                        await (SelectedCommodity?.Reload() ?? Task.CompletedTask);
                    }
                    break;
                case Key.S:
                    if (e.KeyModifiers == KeyModifiers.Control)
                    {
                        await (SelectedCommodity?.Save() ?? Task.CompletedTask);
                    }
                    break;
            }
        }
        #region IDisposable Support
        private bool disposedValue = false; // To detect redundant calls

        void Dispose(bool disposing)
        {
            if (Image == null) { disposedValue = true; }

            if (disposedValue) { return; }

            Image.ExitDesign();
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
            _bmp.Dispose();
            _resizedBmp.Dispose();
            _modedBmp.Dispose();
            _coms.Clear();
            Image = null;
            _coms = null;
            _subs = null;
            _bmp = null;
            _resizedBmp = null;
            _modedBmp = null;
            disposedValue = true;

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
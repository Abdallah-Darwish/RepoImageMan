using System;
using System.Collections.Generic;
using System.Linq;
using System.Reactive.Linq;
using Avalonia.Controls;
using Avalonia;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using System.Runtime.CompilerServices;
using Avalonia.Input;

namespace RepoImageMan.Controls
{
    /// <summary>
    /// A specialization of <see cref="CImage"/> that is used for editing the image using UI and still provide fast operations.
    /// YOU CAN'T MODIFY IMAGE STREAM WHILE DESIGNING.
    /// </summary>
    internal sealed class DesignCImage : Control, IDisposable
    {
        /// <summary>
        /// The original image that this instance is acting upon.
        /// </summary>
        public DesignCImagePanel Panel { get; private set; }

        /// <summary>
        /// Contains the original image stored in <see cref="Image"/> stream with no mods at all.
        /// </summary>
        private IBitmap _bmp;
        //TODO: fix me
        protected override Size MeasureOverride(Size availableSize) => availableSize;
        
        public override void Render(DrawingContext ctx)
        {
            base.Render(ctx!);
            ctx!.DrawImage(_bmp, 1.0,
                new Rect(0, 0, _bmp.PixelSize.Width, _bmp.PixelSize.Height),
                new Rect(new Point(0, 0), DesiredSize), Avalonia.Visuals.Media.Imaging.BitmapInterpolationMode.HighQuality);
            //TODO: get contrats and brightness from SixLabors and push it here
        }


        private readonly List<IDisposable> _notificationsSubscription = new List<IDisposable>();
        public void Init(DesignCImagePanel panel)
        {
            Panel = panel;
           
            using (var imgStream = Panel.Image.OpenStream())
            {
                _bmp = new Bitmap(imgStream);
            }
            Panel.Image.FileUpdated += Image_FileUpdated;
            _notificationsSubscription.Add(Panel.Image
                 .Where(pn => pn == nameof(CImage.Contrast) || pn == nameof(CImage.Brightness))
                 .Subscribe(pn => InvalidateVisual()));
        }
        private void Image_FileUpdated(CImage image)
        {
            using (var imgStream = image.OpenStream())
            {
                _bmp = new Bitmap(imgStream);
            }
            InvalidateVisual();
        }

        protected override void OnPointerPressed(PointerPressedEventArgs e)
        {
            base.OnPointerPressed(e);
            Panel.SelectedCommodity = null;
        }
        #region IDisposable Support

        private bool _disposedValue = false; // To detect redundant calls
        public void Dispose()
        {
            if (!_disposedValue)
            {
                //_disposedValue = true;
                //foreach (var sub in _notificationsSubscription)
                //{
                //    sub.Dispose();
                //}
                //_notificationsSubscription.Clear();
                //foreach (var com in _commodities)
                //{
                //    com.Dispose();
                //}

                //_commodities.Clear();
                //_bmp.Dispose();
                //DesignImageDisposed?.Invoke(this);
                //DesignImageDisposed = null;
            }
        }

        #endregion
    }
}
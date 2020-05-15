using Avalonia;
using Avalonia.Media;
using Avalonia.Threading;
using System;
using System.Collections.Generic;
using System.Reactive.Linq;
using System.Text;

namespace RepoImageMan.Controls
{
    class DesignImageCommodity : IDisposable
    {
        public FormattedText Text { get; private set; }
        public IBrush RenderingBrush { get; private set; }
        public IPen RenderingPen { get; private set; }
        public float SurroundingBoxThickness => (float)Text.Bounds.Size.Average() / 15f;
        private void UpdateFont() => _font = Commodity.Font.Scale((float)_img.ToDesignMappingScale.Average()).ToSixLabors();
        private void UpdateText()
        {
            Text = new FormattedText
            {
                Text = "000",
                TextAlignment = TextAlignment.Left,
                Typeface = Commodity.Font.Scale((float)_img.ToDesignMappingScale.Average()).ToTypeFace(),
                Wrapping = TextWrapping.NoWrap
            };
        }
        private void UpdateBrush() => RenderingBrush = new SolidColorBrush(Commodity.LabelColor);
        private void UpdatePen() => RenderingPen = new Pen(Colors.Red.ToUint32(), SurroundingBoxThickness);

        public Point Location
        {
            get => Commodity.Location.Scale(_img.ToDesignMappingScale) - new Point(0, MN * 3);
            set => Commodity.Location = (value + new Point(0, MN * 3)).Scale(_img.ToOriginalMappingScale);
        }
        //MAGIC NUMBER 
        private float MN => _font.Size * (_font.EmSize - _font.Ascender + _font.Descender) / _font.EmSize;

        public Rect Box => new Rect(Location, Text.Bounds.Size - new Size(0, MN * 2));
        public bool IsIn(in Point p) => Box.Contains(p);
        public Rect HandleBox => new Rect(
            Location - new Point(SurroundingBoxThickness / 2, SurroundingBoxThickness / 2),
            new Size(SurroundingBoxThickness, SurroundingBoxThickness));
        public bool IsInHandle(in Point p) => HandleBox.Contains(p);
        public ImageCommodity Commodity { get; private set; }
        private DesignCImage _img;
        private IDisposable[] _subs;
        SixLabors.Fonts.Font _font;
        public DesignImageCommodity(ImageCommodity com, DesignCImage img)
        {
            _img = img;
            Commodity = com;
            _subs = new IDisposable[]
            {
                    Commodity.AsObservable()
                        .Where(pn => pn == nameof(ImageCommodity.Font))
                        .Subscribe(_ =>
                        {
                            UpdateFont();
                            UpdateText();
                            UpdatePen();
                        }),
                    Commodity.AsObservable()
                        .Where(pn => pn == nameof(ImageCommodity.LabelColor))
                        .Subscribe(_ => UpdateBrush()),
                    Commodity.AsObservable()
                        .Where(pn => pn == nameof(ImageCommodity.Location))
                        .Subscribe(_ => _img.InvalidateVisual()),
                    Commodity.Where(pn => pn == nameof(ImageCommodity.Font) || pn == nameof(ImageCommodity.LabelColor) || pn == nameof(ImageCommodity.Location))
                        .Subscribe(_ => _img.InvalidateVisual()),
                    _img.GetObservable(DesignCImage.BoundsProperty)
                        .Subscribe(_ =>
                        {
                            UpdateFont();
                            UpdateText();
                            UpdatePen();
                        })
            };
            UpdateFont();
            UpdateText();
            UpdateBrush();
            UpdatePen();
        }

        #region IDisposable Support
        private bool disposedValue = false; // To detect redundant calls

        protected virtual void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                if (disposing)
                {
                    foreach (var sub in _subs)
                    {
                        sub.Dispose();
                    }
                    RenderingBrush = null;
                    RenderingPen = null;
                    Text = null;
                    _img = null;
                    _subs = null;
                    Commodity = null;
                    _font = null;
                }
                disposedValue = true;
            }
        }

        public void Dispose() => Dispose(true);
        #endregion
    }
}

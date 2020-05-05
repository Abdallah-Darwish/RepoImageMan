using SixLabors.Fonts;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.Primitives;
using System;
using System.ComponentModel;
using System.Reactive.Linq;
namespace RepoImageMan
{
    //Can be moved only using top left corner(easier Life mah nigga)
    public sealed class DesignImageCommodity<TPixel> : IDisposable where TPixel : unmanaged, IPixel<TPixel>
    {
        //Can't be variable by commodity or else the caching will be useless
        internal const string LabelText = "000";

        public delegate void CommodityUpdatedEventHandler(DesignImageCommodity<TPixel> sender);
        /// <summary>
        /// Will be raised when this instance is updated and the update will affect the final image
        /// </summary>
        public event CommodityUpdatedEventHandler? Updated;


        public PointF Location
        {
            get => Commodity.Location.Scale(Image.ToDesignMappingScale);
            set => Commodity.Location = value.Scale(Image.ToOriginalMappingScale);
        }
        /// <summary>
        /// The original <see cref="ImageCommodity"/> that this instance acts upon.
        /// </summary>
        public ImageCommodity Commodity { get; }
        private bool _isSurrounded = false;

        /// <summary>
        /// Specifies whether to draw a box with a handle on the top left around the commodity label.
        /// </summary>
        public bool IsSurrounded
        {
            get => _isSurrounded;
            set
            {
                if (value == _isSurrounded) { return; }
                _isSurrounded = value;
                UpdateMe();
            }
        }
        public DesignCImage<TPixel> Image { get; }

        public RectangleF GetLabelBounds()
        {
            var renderOptions = new RendererOptions(Font, Location)
            {
                HorizontalAlignment = HorizontalAlignment.Left,
                VerticalAlignment = VerticalAlignment.Top
            };
            return TextMeasurer.MeasureBounds(LabelText, renderOptions);
        }
        /// <summary>
        /// Checks whether <paramref name="p"/> is inside this commodity box.
        /// </summary>
        /// <remarks>Used to know if the user is trying to activate or select commodity.</remarks>
        public bool IsInside(PointF p) => GetLabelBounds().Contains(p);

        /// <summary>
        /// The size of the "arrow" or whatever that is used as a handle to allow the user to move the label around.
        /// </summary>
        public Size HandleSize => new Size((int)SurroundingBoxThickness, (int)SurroundingBoxThickness);

        /// <summary>
        /// Checks whether <paramref name="p"/> is inside this commodity HANDLE box.
        /// </summary>
        /// <remarks>Used to know if the user is trying to move selected commodity.</remarks>
        public bool IsInHandle(PointF p)
        {
            var handleBounds = new RectangleF(HandleLocation, HandleSize);
            return handleBounds.Contains(p);
        }
        /// <summary>
        /// Location of the handle.
        /// I know I know <see cref="HandleLocation"/> should be the same as <see cref="Location"/> buuut its not cause we move the surrounding box a little bit to not over-write the label.
        /// </summary>
        public Point HandleLocation
        {
            get
            {
                var boxBounds = GetLabelBounds();
                return new Point((int)(boxBounds.Left - SurroundingBoxThickness), (int)(boxBounds.Top - SurroundingBoxThickness));
            }
        }
        public PointF[] GetSurroundingBox()
        {
            //How much we should move the lines of the surrounding box to not overwrite the commodity text.
            float x = SurroundingBoxThickness / 1.75f/*Magic number*/;
            var boxBounds = GetLabelBounds();

            //Clockwise
            var boxCorners = new PointF[] {
                new PointF(boxBounds.Left - x,  boxBounds.Top - x),
                new PointF(boxBounds.Right + x, boxBounds.Top - x),
                new PointF(boxBounds.Right + x, boxBounds.Bottom + x),
                new PointF(boxBounds.Left - x, boxBounds.Bottom + x),
                new PointF(boxBounds.Left - x,  boxBounds.Top - x),
            };
            return boxCorners;
        }

        public float SurroundingBoxThickness => Math.Max(1f, Font.Size / 10f);
        private Color _surroundingBoxColor = Color.Red;

        public Color SurroundingBoxColor
        {
            get => _surroundingBoxColor;
            set
            {
                _surroundingBoxColor = value;
                UpdateMe();
            }
        }
        private void UpdateMe() => Updated?.Invoke(this);
        private Font _font;
        public Font Font
        {
            get => _font;
            private set
            {
                if (_font.Equals(value)) { return; }
                _font = value;
                Commodity.Font = new Font(_font, _font.Size * Image.ToOriginalMappingScale.Average());
            }
        }
        private void UpdateFont()
        {
            _font = new Font(Commodity.Font, /*skip unnecessary fractions*/(int)(Commodity.Font.Size * Image.ToDesignMappingScale.Average()));
        }
        private IDisposable[] _notificationsSubscriptions;
        public DesignImageCommodity(ImageCommodity com, DesignCImage<TPixel> image)
        {
            Commodity = com;
            Image = image;

            _notificationsSubscriptions = new IDisposable[]
            {
                //order is impoertant here, so we would update the font before rendering
                Commodity
                .Where(pn => pn == nameof(ImageCommodity.Font))
                .Subscribe(pn => UpdateFont()),
                Commodity
                .Where(pn =>
                pn == nameof(ImageCommodity.Font) ||
                pn == nameof(ImageCommodity.Location) ||
                pn == nameof(ImageCommodity.LabelColor))
                .Subscribe(pn => UpdateMe()),
            };

            UpdateAfterImageResize();
        }


        internal void UpdateAfterImageResize() => UpdateFont();

        #region IDisposable Support
        private bool _disposedValue = false; // To detect redundant calls

        /// <summary>
        /// You shouldn't call this explecitly, instead call <see cref="CommodityPackage.Dispose"/>.
        /// </summary>
        public void Dispose()
        {
            if (!_disposedValue)
            {
                _disposedValue = true;
                Updated = null;
                foreach (var sub in _notificationsSubscriptions) { sub.Dispose(); }
                _notificationsSubscriptions = null;
            }
        }
        #endregion
    }
}

using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using System;
using System.ComponentModel;
using System.Reactive.Linq;
using System.Reflection.Metadata;

namespace RepoImageMan.Controls
{
    //Can be moved only using top left corner(easier Life mah nigga)
    internal sealed class DesignImageCommodity : Control, IDisposable
    {
        public static readonly StyledProperty<FormattedText> TextProperty = AvaloniaProperty.Register<DesignImageCommodity, FormattedText>(nameof(Text));
        public FormattedText Text
        {
            get => GetValue(TextProperty);
            private set => SetValue(TextProperty, value);
        }
        public static readonly StyledProperty<IBrush> RenderingBrushProperty = AvaloniaProperty.Register<DesignImageCommodity, IBrush>(nameof(RenderingBrush));
        public IBrush RenderingBrush
        {
            get => GetValue(RenderingBrushProperty);
            private set => SetValue(RenderingBrushProperty, value);
        }
        public static readonly StyledProperty<IPen> RenderingPenProperty = AvaloniaProperty.Register<DesignImageCommodity, IPen>(nameof(RenderingPen));
        public IPen RenderingPen
        {
            get => GetValue(RenderingPenProperty);
            private set => SetValue(RenderingPenProperty, value);
        }

        private const string LabelText = "000";

        public static readonly StyledProperty<bool> IsSurrondedProperty = AvaloniaProperty.Register<DesignImageCommodity, bool>(nameof(IsSurronded));
        public bool IsSurronded
        {
            get => GetValue(IsSurrondedProperty);
            set => SetValue(IsSurrondedProperty, value);
        }
        static DesignImageCommodity()
        {
            AffectsMeasure<DesignImageCommodity>(TextProperty);
            AffectsRender<DesignImageCommodity>(TextProperty, RenderingBrushProperty, RenderingPenProperty, IsSurrondedProperty);
        }
        /// <summary>
        /// The original <see cref="ImageCommodity"/> that this instance acts upon.
        /// </summary>
        public ImageCommodity Commodity { get; private set; }

        public DesignCImagePanel Panel { get; private set; }

        /// <summary>
        /// The size of the "arrow" or whatever that is used as a handle to allow the user to move the label around.
        /// </summary>
        public PixelSize HandleSize => new PixelSize((int)SurroundingBoxThickness, (int)SurroundingBoxThickness);

        /// <summary>
        /// Checks whether <paramref name="p"/> is inside this commodity HANDLE box.
        /// </summary>
        /// <remarks>Used to know if the user is trying to move selected commodity.</remarks>
        private bool IsInHandle(in Point p)
        {
            var handleBounds = new Rect(new Point(0, 0), HandleSize.ToSize(1.0));
            return handleBounds.Contains(p);
        }

        public float SurroundingBoxThickness => (float)Math.Max(1.0, Text.Bounds.Size.Average() / 75.0);
        /// <summary>
        /// How much we should move the lines of the surrounding box to not overwrite the commodity text.
        /// </summary>
        private float SurroundingBoxOffset => SurroundingBoxThickness / 1.75f/*Magic number*/;
        private void UpdateText()
        {
            Text = new FormattedText
            {
                Text = LabelText,
                TextAlignment = TextAlignment.Left,
                Wrapping = TextWrapping.NoWrap,
                Typeface = new Font(Commodity.Font.FamilyName, (float)(Math.Max(1.0, Commodity.Font.Size * Panel.ToDesignMappingScale.Average())), Commodity.Font.Style).ToTypeFace()
            };
            RenderingPen = new Pen(Colors.Red.ToUint32(), SurroundingBoxThickness);
        }
        private void UpdateRenderingBrush() => RenderingBrush = new SolidColorBrush(Commodity.LabelColor);
        private void UpdateMargin()
        {
            var loc = Commodity.Location.Scale(Panel.ToDesignMappingScale);
            Margin = new Thickness(loc.X, loc.Y, 0, 0);
        }
        private IDisposable[] _notificationsSubscriptions;
        public void Init(ImageCommodity com, DesignCImagePanel panel)
        {
            ZIndex = 1;
            Opacity = 1.0;
            ClipToBounds = true;
            Focusable = true;
            HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Left;
            VerticalAlignment = Avalonia.Layout.VerticalAlignment.Top;
            Commodity = com;
            Panel = panel;
            UpdateText();
            UpdateRenderingBrush();
            UpdateMargin();
            _notificationsSubscriptions = new IDisposable[]
            {
                //order is important here, so we would update the font before rendering
                Commodity
                .Where(pn => pn == nameof(ImageCommodity.Font))
                .Subscribe(_ => UpdateText()),
                Commodity
                .Where(pn => pn == nameof(ImageCommodity.Location))
                .Subscribe(_ => UpdateMargin()),
                Commodity
                .Where(pn => pn == nameof(ImageCommodity.LabelColor))
                .Subscribe(_ => UpdateRenderingBrush()),
            };
        }

        internal void UpdateAfterImageResize() => UpdateText();
        public override void Render(DrawingContext ctx)
        {
            base.Render(ctx);
            if (DesiredSize == default) { return; }
            ctx.DrawText(RenderingBrush, new Point(0, 0), Text);
            if (IsSurronded)
            {
                ctx.DrawRectangle(RenderingPen, new Rect(new Point(3, 3), DesiredSize - new Size(3, 3)));
                //TODO: draw handle
            }
        }
        private bool _isHooked = false;
        protected override void OnGotFocus(GotFocusEventArgs e)
        {
            base.OnGotFocus(e);
            IsSurronded = true;
        }

        protected override Size MeasureOverride(Size availableSize)
        {
            return Text?.Bounds.Inflate(SurroundingBoxOffset).Size ?? new Size();
        }
        protected override void OnPointerPressed(PointerPressedEventArgs e)
        {
            base.OnPointerPressed(e);
            var pointPos = e!.GetCurrentPoint(this).Position;
            _isHooked = IsInHandle(pointPos);
        }
        protected override void OnPointerReleased(PointerReleasedEventArgs e)
        {
            base.OnPointerReleased(e);
            _isHooked = false;
        }
        protected override void OnPointerMoved(PointerEventArgs e)
        {
            base.OnPointerMoved(e);
            if (_isHooked == false) { return; }
            var pointPos = e!.GetCurrentPoint(Panel).Position.Scale(Panel.ToOriginalMappingScale);
            Commodity.Location = pointPos;
        }
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
                foreach (var sub in _notificationsSubscriptions) { sub.Dispose(); }
                _notificationsSubscriptions = null;
            }
        }
        #endregion
    }
}

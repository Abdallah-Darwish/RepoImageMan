using SixLabors.Fonts;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Advanced;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using SixLabors.Primitives;
using System;
using System.Collections.Concurrent;

namespace RepoImageMan
{
    internal class LabelRenderingOptions : IEquatable<LabelRenderingOptions>
    {
        public string Text { get; set; }
        public Font Font { get; set; }
        public Color Color { get; set; }

        public bool Equals(LabelRenderingOptions? o) => o != null && Text.Equals(o.Text, StringComparison.InvariantCulture) && Font.Equals(o.Font) && Color.Equals(o.Color);

        public override bool Equals(object? obj) => Equals(obj as LabelRenderingOptions);

        public override int GetHashCode() => HashCode.Combine(Text, Font, Color);

        public static implicit operator RendererOptions?(LabelRenderingOptions? options)
        {
            if (options == null) { return null; }
            return new RendererOptions(options.Font) { HorizontalAlignment = HorizontalAlignment.Left, VerticalAlignment = VerticalAlignment.Top };
        }
    }
    internal class ImageCommodityLabelCache<TPixel> where TPixel : unmanaged, IPixel<TPixel>
    {

        private readonly ConcurrentDictionary<LabelRenderingOptions, TPixel[][]> _labels = new ConcurrentDictionary<LabelRenderingOptions, TPixel[][]>();
        /// <summary>
        /// Estimated size of <see cref="_labels"/> in bytes.
        /// </summary>
        private int _labelsSize = 0;
        private int _clearingThreshold;

        private TPixel[][] RenderLabel(LabelRenderingOptions options)
        {
            Size expectedSize = (Size)TextMeasurer.Measure(options.Text, options);
            expectedSize.Height += 10;
            expectedSize.Width += 10;

            TPixel backgroundColor = (options.Color == Color.DarkBlue ? Color.DarkCyan : Color.DarkBlue).ToPixel<TPixel>();

            using var playground = new Image<TPixel>(new Configuration(), expectedSize.Width, expectedSize.Height, backgroundColor);
            playground.Mutate(c => c.DrawText(options.Text, options.Font, options.Color, new Point(0, 0)));
            int height = 0, width = 0;
            for (int r = playground.Height - 1; r >= 0; r--)
            {
                var plyRowSpan = playground.GetPixelRowSpan(r);

                for (int c = playground.Width - 1; c >= 0; c--)
                {
                    if (plyRowSpan[c].Equals(backgroundColor) == false)
                    {
                        if (height == 0) { height = r + 1; }
                        width = Math.Max(width, c + 1);
                        break;
                    }
                }
            }
            TPixel[][] label = new TPixel[height][];
            for (int r = height - 1; r >= 0; r--)
            {
                var plyRowSpan = playground.GetPixelRowSpan(r);
                label[r] = new TPixel[width];
                var labelRowSpan = label[r].AsSpan();
                for (int c = width - 1; c >= 0; c--)
                {
                    if (plyRowSpan[c].Equals(backgroundColor) == false)
                    {
                        labelRowSpan[c] = plyRowSpan[c];
                    }
                }
            }
            return label;
        }
        void TryClearCache()
        {
            if (_labelsSize >= ClearingThreshold) { _labels.Clear(); }
        }
        /// <summary>
        /// If the Pixel is zeroed this means that it shouldn't be copied.
        /// </summary>
        public ReadOnlyMemory<TPixel[]> GetLabel(LabelRenderingOptions options)
        {
            TryClearCache();
            return _labels.GetOrAdd(options, op =>
            {
                var label = RenderLabel(op);
                _labelsSize += label[0].Length * label.Length;
                return label;
            }).AsMemory();
        }
        /// <summary>
        /// In bytes.
        /// </summary>
        public int ClearingThreshold
        {
            get => _clearingThreshold;
            set
            {
                _clearingThreshold = value;
                TryClearCache();

            }
        }
    }
}

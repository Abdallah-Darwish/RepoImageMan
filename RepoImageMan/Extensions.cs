namespace RepoImageMan
{
    public static class Extensions
    {
        public static System.Drawing.Size ToDrawingSize(this SixLabors.Primitives.Size sz) => new System.Drawing.Size(sz.Width, sz.Height);
        public static SixLabors.Primitives.PointF ToSixLaborsPointF(this System.Drawing.Point p) => new SixLabors.Primitives.PointF(p.X, p.Y);

        public static SixLabors.Primitives.Size ToSize(this SixLabors.Primitives.SizeF sz) => (SixLabors.Primitives.Size)sz;
        public static SixLabors.Primitives.PointF Scale(this SixLabors.Primitives.PointF p, SixLabors.Primitives.SizeF scale)
            => new SixLabors.Primitives.PointF(p.X * scale.Width, p.Y * scale.Height);

        public static SixLabors.Primitives.Point Scale(this SixLabors.Primitives.Point p, SixLabors.Primitives.SizeF scale)
            => new SixLabors.Primitives.Point((int)(p.X * scale.Width), (int)(p.Y * scale.Height));

        public static float Average(this SixLabors.Primitives.SizeF sz) => (sz.Height + sz.Width) / 2f;
    }
}

using Avalonia.Media;
using Microsoft.VisualBasic.CompilerServices;
using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Text;

namespace RepoImageMan
{
    [Flags]
    public enum FontStyle
    {
        Regular = 0,
        Bold = 0b1,
        Italic = 0b10,
    }
    public class Font : IEquatable<Font>
    {
        public Font(string familyName, float size, FontStyle style)
        {
            if (!float.IsNormal(size) || size <= 0.0)
            {
                throw new ArgumentOutOfRangeException(nameof(size), "size must be > 0.");
            }
            FamilyName = familyName ?? throw new ArgumentNullException(nameof(familyName));
            Size = size;
            Style = style;
        }

        public string FamilyName { get; }
        public float Size { get; }
        public FontStyle Style { get; }

        public bool Equals(Font? other)
        {
            if (other == null) { return false; }
            return FamilyName.Equals(other.FamilyName, StringComparison.OrdinalIgnoreCase) && Size == other.Size && Style == other.Style;
        }

        public override bool Equals(object? obj) => Equals(obj as Font);

        public override int GetHashCode() => HashCode.Combine(FamilyName, Size, Style);

        public static explicit operator Typeface?(Font? f) => f?.ToTypeFace();
        public Typeface ToTypeFace() => new Typeface(FamilyName, Size,
                Style.HasFlag(FontStyle.Italic) ? Avalonia.Media.FontStyle.Italic : Avalonia.Media.FontStyle.Normal,
                Style.HasFlag(FontStyle.Bold) ? FontWeight.Bold : FontWeight.Regular);

        public static explicit operator FontFamily?(Font? f) => f?.ToFontFamily();
        public FontFamily ToFontFamily() => new FontFamily(FamilyName);

    }
}

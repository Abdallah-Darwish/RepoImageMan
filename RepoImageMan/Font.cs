﻿using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Text;
using Avalonia.Media;
using Microsoft.VisualBasic.CompilerServices;
using SkiaSharp;

namespace RepoImageMan
{
    /// <remarks>
    /// Binary values shouldn't be changed because the match with skia
    /// </remarks>
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
            if (float.IsNaN(size) || size < 0)
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
            if (other is null) { return false; }
            return FamilyName.Equals(other.FamilyName, StringComparison.OrdinalIgnoreCase) && Size == other.Size && Style == other.Style;
        }

        public override bool Equals(object? obj) => Equals(obj as Font);

        public override int GetHashCode() => HashCode.Combine(FamilyName, Size, Style);

        public static explicit operator Typeface?(Font? f) => f?.ToTypeFace();
        public Typeface ToTypeFace() => new Typeface(FamilyName,
                Style.HasFlag(FontStyle.Italic) ? Avalonia.Media.FontStyle.Italic : Avalonia.Media.FontStyle.Normal,
                Style.HasFlag(FontStyle.Bold) ? FontWeight.Bold : FontWeight.Regular);

        public static explicit operator FontFamily?(Font? f) => f?.ToFontFamily();
        public FontFamily ToFontFamily() => new FontFamily(FamilyName);
        public SixLabors.Fonts.Font ToSixLabors() => SixLabors.Fonts.SystemFonts.CreateFont(FamilyName, Size,
          Style switch
          {
              FontStyle.Bold | FontStyle.Italic => SixLabors.Fonts.FontStyle.BoldItalic,
              FontStyle.Bold => SixLabors.Fonts.FontStyle.Bold,
              FontStyle.Italic => SixLabors.Fonts.FontStyle.Italic,
              _ => SixLabors.Fonts.FontStyle.Regular
          });
        public SKPaint ToSKPaint() => new()
        {
            Typeface = SKTypeface.FromFamilyName(FamilyName, Style.ToSK()),
            TextSize = Size,
            TextAlign = SKTextAlign.Left,
            IsAntialias = true,
            IsLinearText = true,
            IsStroke = false,
            SubpixelText = true,
            FilterQuality = SKFilterQuality.High,
            TextEncoding = SKTextEncoding.Utf16,
            LcdRenderText = true,
        };
    }
}

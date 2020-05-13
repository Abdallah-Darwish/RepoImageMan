using Avalonia;
using Avalonia.Media.Imaging;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Processing;
using System;
using System.Collections.Concurrent;

namespace RepoImageMan
{
    public sealed partial class CommodityPackage
    {
        /// <summary>
        /// In case you don't want to design just supply a 1 x 1 image
        /// </summary>
        private readonly Image _handleImage;

        private readonly ConcurrentDictionary<PixelSize, IBitmap> _handlesCache = new ConcurrentDictionary<PixelSize, IBitmap>();


        internal IBitmap GetHandle(in PixelSize handleSize) => _handlesCache.GetOrAdd(handleSize, sz =>
        {
            var sixImg = _handleImage.Clone(c => c.Resize(sz.ToSixLabors()));
            return sixImg.ToAvalonia();
        });
    }
}
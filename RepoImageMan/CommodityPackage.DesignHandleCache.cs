using System;
using System.Collections.Concurrent;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using SixLabors.Primitives;

namespace RepoImageMan
{
    public sealed partial class CommodityPackage
    {
        private readonly struct HandleImageInstanceInfo  :IEquatable<HandleImageInstanceInfo>
        {
            public readonly Size Size;
            public readonly Type PixelType;

            public HandleImageInstanceInfo(Size size, Type pixelType)
            {
                Size = size;
                PixelType = pixelType ?? throw new ArgumentNullException(nameof(pixelType));
            }

            public bool Equals(HandleImageInstanceInfo other) => Size.Equals(other.Size) && PixelType.Equals(other!.PixelType);

            public override bool Equals(object? obj) => obj is HandleImageInstanceInfo other && Equals(other);

            public override int GetHashCode() => HashCode.Combine(Size, PixelType);
        }
/// <summary>
/// In case you don't want to design just supply a 1 x 1 image
/// </summary>
        private readonly Image _handleImage;
        
        private readonly ConcurrentDictionary<HandleImageInstanceInfo, Image> _handlesCache = new ConcurrentDictionary<HandleImageInstanceInfo, Image>();

        internal Image<TPixel> GetHandle<TPixel>(Size handleSize) where  TPixel : struct, IPixel<TPixel> =>
            _handlesCache.GetOrAdd(new HandleImageInstanceInfo(handleSize, typeof(TPixel)),
                inf => _handleImage.Clone(c => c.Resize(handleSize)).CloneAs<TPixel>()) as Image<TPixel>;
    }
}
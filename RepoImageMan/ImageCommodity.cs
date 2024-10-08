﻿using System;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Media;
using Dapper;

namespace RepoImageMan
{
    public class ImageCommodity : Commodity
    {
        protected ImageCommodity(int id, CommodityPackage package, CImage image) : base(id, package)
        {
            Image = image;
        }
        /// <summary>
        /// Loads the <see cref="ImageCommodity"/> from <see cref="CommodityPackage"/>.
        /// </summary>
        /// <param name="id">Id of the <see cref="ImageCommodity"/> to load.</param>
        /// <param name="package">The <see cref="CommodityPackage"/> which this <see cref="ImageCommodity"/> belongs to.</param>
        /// <param name="image">The <see cref="CImage"/> which this <see cref="ImageCommodity"/> belongs to.</param>
        internal static async Task<ImageCommodity> Load(int id, CommodityPackage package, CImage image)
        {
            var res = new ImageCommodity(id, package, image);
            await res.Reload().ConfigureAwait(false);
            return res;
        }
        /// <summary>
        /// The <see cref="CImage"/> which this <see cref="ImageCommodity"/> belongs to and will be drawn on.
        /// </summary>
        public CImage Image { get; }

        private Color _labelColor;
        public Color LabelColor
        {
            get => _labelColor;
            set
            {
                if (value.Equals(_labelColor)) { return; }
                _labelColor = value;
                OnPropertyChanged();
            }
        }

        private Font _font;
        /// <summary>
        /// The font to use in the drawing of this <see cref="ImageCommodity"/> label on <see cref="Image"/>.
        /// </summary>
        public Font Font
        {
            get => _font;
            set
            {
                if (value is null) { throw new ArgumentNullException(nameof(value)); }
                if (value.Equals(/*_font maybe null on initialization*/_font)) { return; }
                _font = value;
                OnPropertyChanged();
            }
        }
        private Point _location;
        /// <summary>
        /// The location of the TOP-LEFT corner where this <see cref="ImageCommodity"/> label will be drawn on <see cref="Image"/>.
        /// </summary>
        public Point Location
        {
            get => _location;

            set
            {
                if (value == _location) { return; }
                if (value.X < 0 || value.Y < 0 ||
                    value.X > Image.Size.Width || value.Y > Image.Size.Height)
                {
                    throw new ArgumentOutOfRangeException(nameof(value), value, $"ImageCommodity(Id: {Id}, {nameof(Location)}: {value}) must be in [(0, 0), ({Image.Size.Width}, {Image.Size.Height})].");
                }
                _location = value;
                OnPropertyChanged();
            }
        }
        /// <summary>
        /// Saves all the properties of the <see cref="ImageCommodity"/> to the <see cref="CommodityPackage"/>.
        /// </summary>
        public override async Task Save()
        {
            await base.Save().ConfigureAwait(false);
            await Package.DbConnection.ExecuteAsync("UPDATE ImageCommodity SET fontFamilyName = @fontFamilyName, fontStyle = @fontStyle, fontSize = @fontSize, locationX = @locationX, locationY = @locationY, labelColor = @labelColor WHERE id = @Id",
                  new
                  {
                      fontFamilyName = Font.FamilyName,
                      fontStyle = (int)Font.Style,
                      fontSize = Font.Size,
                      locationX = Location.X,
                      locationY = Location.Y,
                      labelColor = LabelColor.ToString(),
                      Id
                  })
                .ConfigureAwait(false);
        }
        /// <summary>
        /// Re-reads all <see cref="ImageCommodity"/> properities from the <see cref="CommodityPackage"/>.
        /// Will raise <see cref="Commodity.PropertyChanged"/>.
        /// </summary>
        public override async Task Reload()
        {
            await base.Reload().ConfigureAwait(false);
            var fields = await Package.DbConnection.QueryFirstAsync("SELECT * FROM ImageCommodity WHERE id = @Id", new { Id }).ConfigureAwait(false);
            Font = new Font(fields.FontFamilyName, (float)fields.FontSize, (FontStyle)(int)fields.FontStyle);
            Location = new Point(fields.LocationX, fields.LocationY);
            LabelColor = Color.Parse(fields.LabelColor);
        }
        /// <summary>
        /// <inheritdoc/>
        /// Also will raise <see cref="CImage.CommodityRemoved"/>.
        /// </summary>
        public override async Task Delete()
        {
            await Package.DbConnection.ExecuteAsync("DELETE FROM ImageCommodity WHERE id = @Id", new { Id }).ConfigureAwait(false);
            await base.Delete().ConfigureAwait(false);
            await Image.RemoveCommodity(this).ConfigureAwait(false);
        }
    }
}

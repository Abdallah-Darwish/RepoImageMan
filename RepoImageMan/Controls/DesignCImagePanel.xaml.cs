using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using System;
using System.Collections.Generic;
using System.Linq;

namespace RepoImageMan.Controls
{
    public class DesignCImagePanel : UserControl
    {
        internal delegate void ImageDisposedEventHandler(DesignCImagePanel sender);

        /// <remarks>
        /// Used to tell the original image that this instance is being disposed so another instance can be created.
        /// </remarks>
        internal event ImageDisposedEventHandler? DesignImagePanelDisposed;

        /// <summary>
        /// The scale that is used to map points from <see cref="CImage"/> to this resized <see cref="DesignCImage"/>.
        /// </summary>
        public Size ToOriginalMappingScale => new Size(Image.Size.Width / img.DesiredSize.Width, Image.Size.Height / img.DesiredSize.Height);

        /// <summary>
        /// The scale that is used to map points from this instance to the original <see cref="CImage"/>.
        /// </summary>
        public Size ToDesignMappingScale => new Size(img.DesiredSize.Width / Image.Size.Width, img.DesiredSize.Height / Image.Size.Height);
        public CImage Image { get; private set; }
        private readonly List<DesignImageCommodity> _commodities = new List<DesignImageCommodity>();

        public static readonly StyledProperty<ImageCommodity?> SelectedCommodityProperty =
            AvaloniaProperty.Register<DesignCImage, ImageCommodity?>(nameof(SelectedCommodity));
        public ImageCommodity? SelectedCommodity
        {
            get => GetValue(SelectedCommodityProperty);
            internal set => SetValue(SelectedCommodityProperty, value);
        }
        private readonly Panel pnl;
        private readonly DesignCImage img;
        private readonly List<IDisposable> _subs = new List<IDisposable>();
        public DesignCImagePanel()
        {
            this.InitializeComponent();
            pnl = this.FindControl<Panel>(nameof(pnl));
            img = this.FindControl<DesignCImage>(nameof(img));
            _subs.Add(this.GetObservable(SelectedCommodityProperty).Subscribe(c =>
            {
                foreach (var com in _commodities)
                {
                    if (com.Commodity != c) { com.IsSurronded = false; }
                }
            }));
        }
        public void Init(CImage image)
        {
            Image = image!;
            img.Init(this);
            foreach (var com in Image.Commodities) { AddCommodity(com); }
            Image.CommodityAdded += CommodityAdded;
            Image.CommodityRemoved += CommodityRemoved;
        }
        private void InitializeComponent() => AvaloniaXamlLoader.Load(this);

        private void AddCommodity(ImageCommodity com)
        {
            var dCom = new DesignImageCommodity();
            dCom.Init(com, this);
            _commodities.Add(dCom);
            pnl.Children.Add(dCom);
            
            dCom.Focus();
        }
        private void CommodityAdded(CImage _, ImageCommodity com) => AddCommodity(com);

        private void CommodityRemoved(CImage _, ImageCommodity com)
        {
            if (com == SelectedCommodity) { SelectedCommodity = null; }

            var dCom = _commodities.First(c => c.Commodity.Id == com.Id);
            _commodities.Remove(dCom);
            dCom.Dispose();
        }

    }
}

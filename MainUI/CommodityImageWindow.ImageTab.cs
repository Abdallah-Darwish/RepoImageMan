using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media.Imaging;
using JetBrains.Annotations;
using MainUI.ImageTabModels;
using RepoImageMan;
using System.Reactive.Linq;
namespace MainUI
{
    namespace ImageTabModels
    {
        sealed class TvImagesImageModel : INotifyPropertyChanged, IDisposable
        {
            private static readonly Size ThumbnailSize = new Size(400, 400);
            public ObservableCollection<TvImagesCommodityModel> Commodities { get; }

            private IBitmap _imageSource;
            public IBitmap ImageSource => _imageSource;

            private void ImageOnFileUpdated(CImage _)
            {
                if (Image.TryOpenStream(out var imageStream) == false)
                {
                    return;
                }

                using (imageStream)
                {
                    _imageSource = imageStream.LoadResizedBitmap(ThumbnailSize);
                }

                OnPropertyChanged(nameof(ImageSource));
            }

            public CImage Image { get; }

            public TvImagesImageModel(CImage image)
            {
                Image = image;
                Commodities =
                    new ObservableCollection<TvImagesCommodityModel>(
                        Image.Commodities.Select(c => new TvImagesCommodityModel(c, this)));
                Image.FileUpdated += ImageOnFileUpdated;
                ImageOnFileUpdated(Image);
            }

            public event PropertyChangedEventHandler PropertyChanged;

            [NotifyPropertyChangedInvocator]
            private void OnPropertyChanged([CallerMemberName] string propertyName = null) =>
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));

            public void Dispose()
            {
                _imageSource.Dispose();
                Image.FileUpdated -= ImageOnFileUpdated;
                foreach (var com in Commodities)
                {
                    com.Dispose();
                }

                Commodities.Clear();
            }
        }

        sealed class TvImagesCommodityModel : INotifyPropertyChanged, IDisposable
        {
            public TvImagesImageModel Image { get; }
            public Commodity Commodity { get; }
            public string Name => Commodity.Name;

            public TvImagesCommodityModel(Commodity commodity, TvImagesImageModel image)
            {
                Commodity = commodity;
                Image = image;
                Commodity.PropertyNotificationManager.Subscribe(nameof(RepoImageMan.Commodity.Name),
                    CommodityOnNameChanged);
            }

            private void CommodityOnNameChanged(object sender, PropertyChangedEventArgs e) =>
                OnPropertyChanged(e.PropertyName);

            public event PropertyChangedEventHandler PropertyChanged;

            [NotifyPropertyChangedInvocator]
            private void OnPropertyChanged([CallerMemberName] string propertyName = null) =>
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));

            public void Dispose()
            {
                Commodity.PropertyNotificationManager.Unsubscribe(nameof(RepoImageMan.Commodity.Name),
                    CommodityOnNameChanged);
            }
        }

        sealed class CbxMoveImagesModel : INotifyPropertyChanged, IDisposable
        {
            private static readonly Size ThumbnailSize = new Size(30, 50);
            private IBitmap _imageSource;
            public IBitmap ImageSource => _imageSource;

            private void ImageOnFileUpdated(CImage _)
            {
                if (Image.TryOpenStream(out var imageStream) == false)
                {
                    return;
                }

                using (imageStream)
                {
                    _imageSource = imageStream.LoadResizedBitmap(ThumbnailSize);
                }

                OnPropertyChanged(nameof(ImageSource));
            }

            public CImage Image { get; }

            public CbxMoveImagesModel(CImage image)
            {
                Image = image;
                image.FileUpdated += ImageOnFileUpdated;
            }

            public event PropertyChangedEventHandler PropertyChanged;

            [NotifyPropertyChangedInvocator]
            private void OnPropertyChanged([CallerMemberName] string propertyName = null) =>
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));

            public void Dispose()
            {
                _imageSource.Dispose();
                Image.FileUpdated -= ImageOnFileUpdated;
            }
        }
    }

    public partial class CommodityImageWindow
    {
        private class ImageTab
        {
            private readonly CommodityImageWindow _hostingWindow;
            private readonly ComboBox cbxMoveImages;
            private readonly Button btnMoveSelectedImage;
            private readonly TreeView tvImages;
            private readonly ObservableCollection<TvImagesImageModel> _tvImagesItems;
            private readonly ObservableCollection<CbxMoveImagesModel> _cbxMoveImagesItems;
            private readonly Dictionary<int, TvImagesImageModel> _tvImagesModels;
            private readonly Dictionary<int, CbxMoveImagesModel> _cbxMoveImagesModels;
            private readonly List<IDisposable> _eventsSubscriptions = new List<IDisposable>();

            public ImageTab(CommodityImageWindow hostingWindow)
            {
                _hostingWindow = hostingWindow;

                tvImages = _hostingWindow.Get<TreeView>(nameof(tvImages));
                cbxMoveImages = _hostingWindow.Get<ComboBox>(nameof(cbxMoveImages));
                btnMoveSelectedImage = _hostingWindow.Get<Button>(nameof(btnMoveSelectedImage));

                _tvImagesModels =
                    _hostingWindow._package.Images.ToDictionary(i => i.Id, i => new TvImagesImageModel(i));
                _tvImagesItems = new ObservableCollection<TvImagesImageModel>(_tvImagesModels.Values);
                tvImages.Items = _tvImagesItems;

                _cbxMoveImagesModels =
                    _hostingWindow._package.Images.ToDictionary(i => i.Id, i => new CbxMoveImagesModel(i));
                _cbxMoveImagesItems = new ObservableCollection<CbxMoveImagesModel>(_cbxMoveImagesModels.Values);
                cbxMoveImages.Items = _tvImagesItems;


                tvImages.SelectionChanged += TvImagesOnSelectionChanged;
                
                _eventsSubscriptions.Add(_hostingWindow.GetObservable(Window.ClientSizeProperty)
                    .Do(sz =>
                    {
                        tvImages.Height = sz.Height - 200;
                    }).Subscribe());
            }

            private void TvImagesOnSelectionChanged(object? sender, SelectionChangedEventArgs e)
            {
                //throw new NotImplementedException();
            }
        }
    }
}
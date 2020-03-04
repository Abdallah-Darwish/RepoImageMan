using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media.Imaging;
using JetBrains.Annotations;
using MainUI.ImageTabModels;
using RepoImageMan;
using MessageBox.Avalonia;
using MessageBox.Avalonia.DTO;
using MessageBox.Avalonia.Enums;

namespace MainUI
{
    namespace ImageTabModels
    {
        sealed class TvImagesImageModel : INotifyPropertyChanged, IDisposable
        {
            private static readonly Size ThumbnailSize = new Size(400, 400);

            private readonly CommodityImageWindow.ImageTab _hostingTab;

            //Wont create a dictionary for commodities models and instead I will use Commodities collection cause it will be pretty small
            public ObservableCollection<TvImagesCommodityModel> Commodities { get; }

            public TvImagesCommodityModel GetCommodityModel(ImageCommodity com) =>
                Commodities.First(c => c.Commodity.Id == com.Id);

            private int _position;

            public int Position
            {
                get => _position;
                private set
                {
                    if (value == _position) { return; }

                    _position = value;
                    RePositionInTvItems();
                }
            }

            public string ShortName
            {
                get
                {
                    var name = Commodities.FirstOrDefault()?.Name ?? "---";
                    return name.Length <= 10 ? name : $"{name.Substring(0, 7)}...";
                }
            }

            private bool _export;

            public bool Export
            {
                get => _export;
                set
                {
                    if (value == _export) { return; }

                    _export = value;
                    OnPropertyChanged();
                }
            }

            private IBitmap _imageSource;
            public  IBitmap ImageSource => _imageSource;

            private void ImageOnFileUpdated(CImage _)
            {
                if (Image.TryOpenStream(out var imageStream) == false) { return; }

                using (imageStream) { _imageSource = imageStream.LoadResizedBitmap(ThumbnailSize); }

                OnPropertyChanged(nameof(ImageSource));
            }

            public CImage Image { get; }

            public TvImagesImageModel(CImage image, CommodityImageWindow.ImageTab hostingTab)
            {
                _hostingTab = hostingTab;
                Image       = image;
                UpdatePosition();
                Commodities =
                    new ObservableCollection<TvImagesCommodityModel>(Image.Commodities.Select(c =>
                                                                                                  new
                                                                                                      TvImagesCommodityModel(c,
                                                                                                                             this)));
                //TODO: unsubscribe from events
                Image.FileUpdated      += ImageOnFileUpdated;
                Image.CommodityAdded   += ImageOnCommodityAdded;
                Image.CommodityRemoved += ImageOnCommodityRemoved;

                foreach (var com in Commodities)
                {
                    com.Commodity.PropertyNotificationManager.Subscribe(nameof(Commodity.Position),
                                                                        CommodityOnPositionChanged);
                }

                ImageOnFileUpdated(Image);
            }

            public async Task SetPosition(int newPosition)
            {
                foreach (var com in Commodities.Reverse()) { await com.Commodity.SetPosition(newPosition); }
            }

            private void UpdatePosition() =>
                Position = Image.Commodities.DefaultIfEmpty().Min(c => c?.Position ?? int.MaxValue - 100);

            private void CommodityOnPositionChanged(object sender, PropertyChangedEventArgs _)
            {
                var com      = sender as ImageCommodity;
                var comModel = GetCommodityModel(com);
                Commodities.Remove(comModel);
                AddToCommodities(new[] {comModel});
                UpdatePosition();
            }

            private void ImageOnCommodityRemoved(CImage sender, ImageCommodity commodity)
            {
                Commodities.Remove(GetCommodityModel(commodity));
                if (Commodities.Count == 0 && _hostingTab._imageToMove == this) { _hostingTab.ResetImageToMove(); }

                UpdatePosition();
            }

            private void AddToCommodities(IEnumerable<TvImagesCommodityModel> coms)
            {
                coms = coms.OrderBy(c => c.Commodity.Position);

                int i = 0, comPos;
                foreach (var com in coms)
                {
                    comPos = Commodities.Count;
                    for (; i < Commodities.Count; i++)
                    {
                        if (Commodities[i].Commodity.Position > com.Commodity.Position)
                        {
                            comPos = i;
                            break;
                        }
                    }

                    Commodities.Insert(comPos, com);
                }
            }

            private void ImageOnCommodityAdded(CImage sender, ImageCommodity commodity)
            {
                var comModel = new TvImagesCommodityModel(commodity, this);
                commodity.PropertyNotificationManager.Subscribe(nameof(Position), CommodityOnPositionChanged);
                AddToCommodities(new[] {comModel});
                UpdatePosition();
            }

            /// <summary>
            /// Adds the item if it fits the search pattern and removes it otherwise.
            /// If item is added its added in the correct position.
            /// </summary>
            public void RePositionInTvItems()
            {
                _hostingTab._tvImagesItems.Remove(this);
                int i = 0;
                for (; i < _hostingTab._tvImagesItems.Count; i++)
                {
                    if (_hostingTab._tvImagesItems[i].Position > Position) { break; }
                }

                _hostingTab._tvImagesItems.Insert(i, this);
            }

            public event PropertyChangedEventHandler PropertyChanged;

            [NotifyPropertyChangedInvocator]
            private void OnPropertyChanged([CallerMemberName] string propertyName = null) =>
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));

            public void Dispose()
            {
                _imageSource.Dispose();
                Image.FileUpdated -= ImageOnFileUpdated;
                foreach (var com in Commodities) { com.Dispose(); }

                Commodities.Clear();
            }
        }

        sealed class TvImagesCommodityModel : INotifyPropertyChanged, IDisposable
        {
            public TvImagesImageModel Image     { get; }
            public Commodity          Commodity { get; }
            public string             Name      => Commodity.Name;

            public TvImagesCommodityModel(Commodity commodity, TvImagesImageModel image)
            {
                Commodity = commodity;
                Image     = image;
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
    }

    public partial class CommodityImageWindow
    {
        internal class ImageTab
        {
            private readonly CommodityImageWindow _hostingWindow;
            private readonly TreeView             tvImages;

            internal readonly ObservableCollection<TvImagesImageModel> _tvImagesItems =
                new ObservableCollection<TvImagesImageModel>();

            private readonly List<TvImagesImageModel> _tvImagesModels;
            private readonly ContextMenu              tvImagesCTXMenu;

            private readonly MenuItem miExportImages,
                                      miExportSelectedImages,
                                      miExportAllImages,
                                      miUnExportAllImages,
                                      miUnExportSelectedImages,
                                      miCreateImage,
                                      miDeleteImage,
                                      miMoveImage,
                                      miMoveSelectedImage,
                                      miMoveBeforeSelectedImage,
                                      miMoveAfterSelectedImage,
                                      miGoToCommodity;

            private readonly TabItem tabImages;

            internal         TvImagesImageModel? _imageToMove;
            private          DateTime            _imageToMoveSelectionTime = DateTime.MinValue;
            private readonly TimeSpan            ImageMovingWindow         = TimeSpan.FromMinutes(3);

            public ImageTab(CommodityImageWindow hostingWindow)
            {
                _hostingWindow = hostingWindow;

                tabImages                 = _hostingWindow.Get<TabItem>(nameof(tabImages));
                tvImages                  = _hostingWindow.Get<TreeView>(nameof(tvImages));
                tvImagesCTXMenu           = _hostingWindow.Get<ContextMenu>(nameof(tvImagesCTXMenu));
                miCreateImage             = _hostingWindow.Get<MenuItem>(nameof(miCreateImage));
                miExportImages            = _hostingWindow.FindControl<MenuItem>(nameof(miExportImages));
                miExportSelectedImages    = _hostingWindow.FindControl<MenuItem>(nameof(miExportSelectedImages));
                miUnExportSelectedImages  = _hostingWindow.FindControl<MenuItem>(nameof(miUnExportSelectedImages));
                miExportAllImages         = _hostingWindow.Get<MenuItem>(nameof(miExportAllImages));
                miUnExportAllImages       = _hostingWindow.Get<MenuItem>(nameof(miUnExportAllImages));
                miDeleteImage             = _hostingWindow.Get<MenuItem>(nameof(miDeleteImage));
                miMoveImage               = _hostingWindow.Get<MenuItem>(nameof(miMoveImage));
                miMoveSelectedImage       = _hostingWindow.Get<MenuItem>(nameof(miMoveSelectedImage));
                miMoveBeforeSelectedImage = _hostingWindow.Get<MenuItem>(nameof(miMoveBeforeSelectedImage));
                miMoveAfterSelectedImage  = _hostingWindow.Get<MenuItem>(nameof(miMoveAfterSelectedImage));
                miGoToCommodity           = _hostingWindow.FindControl<MenuItem>(nameof(miGoToCommodity));

                _tvImagesModels =
                    _hostingWindow._package.Images.Select(i => new TvImagesImageModel(i, this)).ToList();
                tvImages.Items = _tvImagesItems;


                tvImagesCTXMenu.ContextMenuOpening += TvImagesCTXMenuOnContextMenuOpening;
                miExportAllImages.Click            += MiExportAllImagesOnClick;
                miUnExportAllImages.Click          += MiUnExportAllImagesOnClick;
                miExportSelectedImages.Click       += MiExportSelectedImagesOnClick;
                miUnExportSelectedImages.Click     += MiUnExportSelectedImagesOnClick;
                miGoToCommodity.Click              += MiGoToCommodityOnClick;
                miMoveSelectedImage.Click          += MiMoveSelectedImageOnClick;
                miMoveBeforeSelectedImage.Click    += MiMoveBeforeSelectedImageOnClick;
                miMoveAfterSelectedImage.Click     += MiMoveAfterSelectedImageOnClick;
            }

            private void MiMoveAfterSelectedImageOnClick(object? sender, RoutedEventArgs e)
            {
                var selectedImage = GetSelectedImage();
                if (selectedImage.Commodities.Count == 0)
                {
                    MessageBoxManager.GetMessageBoxStandardWindow(new MessageBoxStandardParams
                    {
                        Icon                  = MessageBox.Avalonia.Enums.Icon.Forbidden,
                        ButtonDefinitions     = ButtonEnum.Ok,
                        CanResize             = false,
                        ShowInCenter          = true,
                        WindowStartupLocation = WindowStartupLocation.CenterScreen,
                        ContentTitle          = "Invalid Operation",
                        ContentMessage =
                            "You can't use this image as a reference point because doesn't have any commodities assigned to it."
                    }).ShowDialog(_hostingWindow);
                    return;
                }

                if (_imageToMove == null || _imageToMove == selectedImage) { return; }

                int selectedImageMaxComPos = selectedImage.Commodities.Max(c => c.Commodity.Position);
                int newPos = _imageToMove.Position < selectedImage.Position
                                 ? selectedImageMaxComPos
                                 : selectedImageMaxComPos + 1;

                _imageToMove.SetPosition(newPos);
                ResetImageToMove();
            }

            private void MiMoveBeforeSelectedImageOnClick(object? sender, RoutedEventArgs e)
            {
                var selectedImage = GetSelectedImage();
                if (selectedImage.Commodities.Count == 0)
                {
                    MessageBoxManager.GetMessageBoxStandardWindow(new MessageBoxStandardParams()
                    {
                        Icon                  = MessageBox.Avalonia.Enums.Icon.Forbidden,
                        ButtonDefinitions     = ButtonEnum.Ok,
                        CanResize             = false,
                        ShowInCenter          = true,
                        WindowStartupLocation = WindowStartupLocation.CenterScreen,
                        ContentTitle          = "Invalid Operation.",
                        ContentMessage =
                            "You can't use this image as a reference point because doesn't have any commodities assigned to it."
                    }).ShowDialog(_hostingWindow);
                    return;
                }

                if (_imageToMove == null || _imageToMove == selectedImage) { return; }

                int newPos = _imageToMove.Position > selectedImage.Position
                                 ? selectedImage.Position
                                 : selectedImage.Position - 1;

                _imageToMove.SetPosition(newPos);
                ResetImageToMove();
            }

            private void MiMoveSelectedImageOnClick(object? sender, RoutedEventArgs e)
            {
                _imageToMove = GetSelectedImage();
                if (_imageToMove.Commodities.Count == 0)
                {
                    MessageBoxManager.GetMessageBoxStandardWindow(new MessageBoxStandardParams
                    {
                        Icon                  = MessageBox.Avalonia.Enums.Icon.Forbidden,
                        ButtonDefinitions     = ButtonEnum.Ok,
                        CanResize             = false,
                        ShowInCenter          = true,
                        WindowStartupLocation = WindowStartupLocation.CenterScreen,
                        ContentTitle          = "Invalid Operation.",
                        ContentMessage =
                            "You can't move an image that doesn't have any commodities assigned to it."
                    }).ShowDialog(_hostingWindow);
                    ResetImageToMove();
                    return;
                }

                _imageToMoveSelectionTime = DateTime.UtcNow;
            }

            internal void ResetImageToMove()
            {
                _imageToMove              = null;
                _imageToMoveSelectionTime = DateTime.UtcNow - (ImageMovingWindow * 2);
            }

            private void MiGoToCommodityOnClick(object? sender, RoutedEventArgs e)
            {
                if (tvImages.SelectedItems.Count != 1 || !(tvImages.SelectedItems[0] is TvImagesCommodityModel com))
                {
                    return;
                }

                _hostingWindow._commodityTab.GoToCommodity(com.Commodity);
            }

            public void GoToCommodity(ImageCommodity com)
            {
                tabImages.IsSelected = true;
                tvImages.SelectedItems.Clear();
                tvImages.SelectedItems.Add(_tvImagesModels.First(c => c.Image.Id ==
                                                                      com.Image.Id) /*.GetCommodityModel(com)*/);
            }

            private void MiUnExportSelectedImagesOnClick(object? sender, RoutedEventArgs e)
            {
                foreach (var img in tvImages.SelectedItems.OfType<TvImagesImageModel>()) { img.Export = false; }
            }

            private void MiExportSelectedImagesOnClick(object? sender, RoutedEventArgs e)
            {
                foreach (var img in tvImages.SelectedItems.OfType<TvImagesImageModel>()) { img.Export = true; }
            }

            private void MiUnExportAllImagesOnClick(object? sender, RoutedEventArgs e)
            {
                foreach (var img in _tvImagesItems) { img.Export = false; }
            }

            private void MiExportAllImagesOnClick(object? sender, RoutedEventArgs e)
            {
                foreach (var img in _tvImagesItems) { img.Export = true; }
            }

            private void TvImagesCTXMenuOnContextMenuOpening(object sender, CancelEventArgs e)
            {
                if (DateTime.UtcNow - _imageToMoveSelectionTime > ImageMovingWindow) { ResetImageToMove(); }

                miExportImages.IsVisible = _tvImagesItems.Count > 0;
                miExportSelectedImages.IsVisible =
                    miUnExportSelectedImages.IsVisible = tvImages.SelectedItems.Count > 0;

                var selectedImage = GetSelectedImage();
                miDeleteImage.IsVisible = selectedImage != null;
                miMoveImage.IsVisible   = tvImages.SelectedItems.Count == 1 && selectedImage != null;
                miMoveAfterSelectedImage.IsVisible = miMoveBeforeSelectedImage.IsVisible =
                                                         _imageToMove != null && selectedImage != _imageToMove;

                if (miDeleteImage.IsVisible) { miDeleteImage.Header = $"Delete(DEL) {selectedImage.ShortName}"; }

                if (miMoveBeforeSelectedImage.IsVisible)
                {
                    miMoveBeforeSelectedImage.Header =
                        $"Move {_imageToMove.ShortName} Before {selectedImage.ShortName}";
                }

                if (miMoveAfterSelectedImage.IsVisible)
                {
                    miMoveAfterSelectedImage.Header = $"Move {_imageToMove.ShortName} After {selectedImage.ShortName}";
                }

                if (miMoveImage.IsVisible) { miMoveSelectedImage.Header = $"Move {selectedImage.ShortName}"; }
            }

            private TvImagesImageModel? GetSelectedImage() => tvImages.SelectedItems.Count == 0
                                                                  ? null
                                                                  : tvImages.SelectedItems[0] as TvImagesImageModel;

            //Will be called from hosting window
            public void TvImages_ImageRightClicked(object? sender, PointerPressedEventArgs e)
            {
                if (!((sender as IDataContextProvider)?.DataContext is TvImagesImageModel clickedImage)) return;
                if (tvImages.SelectedItems.Contains(clickedImage) == false)
                {
                    tvImages.SelectedItems.Add(clickedImage);
                }
            }
        }
    }
}
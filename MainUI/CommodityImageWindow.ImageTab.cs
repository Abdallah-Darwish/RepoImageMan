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
using MainUI.ImageTabModels;
using RepoImageMan;
using MessageBox.Avalonia;
using MessageBox.Avalonia.DTO;
using MessageBox.Avalonia.Enums;
using System.IO;
using Avalonia.Threading;
using SixLabors.ImageSharp.Processing;

namespace MainUI
{
    namespace ImageTabModels
    {
        sealed class TvImagesImageModel : INotifyPropertyChanged, IDisposable
        {
            private static readonly Size ThumbnailSize = new Size(400, 400);
            private static readonly IBitmap DefaultThumbnail;
            static TvImagesImageModel()
            {
                var boxCorners = new[] { (0, 0), (ThumbnailSize.Width, 0), (ThumbnailSize.Width, ThumbnailSize.Height), (0, ThumbnailSize.Height) }.Select(p => new SixLabors.Primitives.PointF((float)p.Width, (float)p.Height)).ToArray();
                var boxColor = SixLabors.ImageSharp.Color.Red;
                var boxThickness = 4.0f;
                using var defaultImage = new SixLabors.ImageSharp.Image<SixLabors.ImageSharp.PixelFormats.Rgba32>((int)ThumbnailSize.Width, (int)ThumbnailSize.Height);

                using var defaultImageStream = new MemoryStream();

                defaultImage.Mutate(c =>
                c.Fill(SixLabors.ImageSharp.Color.Black)
                .DrawPolygon(boxColor, boxThickness * 2, boxCorners)
                .DrawLines(boxColor, boxThickness, new[] { boxCorners[0], boxCorners[2] })
                .DrawLines(boxColor, boxThickness, new[] { boxCorners[1], boxCorners[3] })
                );
                defaultImage.Save(defaultImageStream, new SixLabors.ImageSharp.Formats.Jpeg.JpegEncoder());
                defaultImageStream.Position = 0;
                DefaultThumbnail = defaultImageStream.LoadResizedBitmap(ThumbnailSize);
            }

            private readonly CommodityImageWindow.ImageTab _hostingTab;

            //Wont create a dictionary for commodities models and instead I will use Commodities collection cause it will be pretty small
            public ObservableCollection<TvImagesCommodityModel> Commodities { get; }

            public TvImagesCommodityModel GetCommodityModel(ImageCommodity com) => Commodities.First(c => c.Commodity.Id == com.Id);

            private int _position;

            public int Position
            {
                get => _position;
                private set
                {
                    if (value == _position) { return; }

                    _position = value;
                    RePositionInTvItems();
                    OnPropertyChanged();
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
            public IBitmap ImageSource
            {
                get => _imageSource;
                private set
                {
                    if (_imageSource == value) { return; }

                    _imageSource = value;
                    OnPropertyChanged();
                }
            }
            /// <summary>
            /// Forces a reload of ImageSource.
            /// Will be mainly used to load thumbnails concurrently when loading this tab, its done this way because most of this class operations must be done in STA mode because its UI operations but the thumbnail loading will be done concurrently to make the ui loading faster.
            /// </summary>
            public void ReloadImageSource() => ImageOnFileUpdated(Image);
            private void ImageOnFileUpdated(CImage _)
            {
                using var imageStream = Image.OpenStream();
                var oldImageSource = ImageSource;
                ImageSource = imageStream.Length > 0 ? imageStream.LoadResizedBitmap(ThumbnailSize) : DefaultThumbnail;
                if (oldImageSource != null && oldImageSource != DefaultThumbnail) { oldImageSource.Dispose(); }
            }

            public CImage Image { get; }
            /// <summary>
            /// <see cref="ImageSource"/> won't be initiazlized and you must do it seperatly, its safe to call it from different threads ON DIFFERENT INSTANCES.
            /// </summary>
            /// <param name="image"></param>
            /// <param name="hostingTab"></param>
            public TvImagesImageModel(CImage image, CommodityImageWindow.ImageTab hostingTab)
            {
                _hostingTab = hostingTab;
                _hostingTab._tvImagesModels.Add(this);
                Image = image;
                UpdatePosition();
                Commodities = new ObservableCollection<TvImagesCommodityModel>(Image.Commodities.Select(c => new TvImagesCommodityModel(c, this)));
                //TODO: unsubscribe from events
                Image.FileUpdated += ImageOnFileUpdated;
                Image.CommodityAdded += ImageOnCommodityAdded;
                Image.CommodityRemoved += ImageOnCommodityRemoved;

                foreach (var com in Commodities)
                {
                    com.Commodity.PropertyNotificationManager.Subscribe(nameof(Commodity.Position), CommodityOnPositionChanged);
                }
                Image.Deleting += Image_Deleting;
            }

            private void Image_Deleting(CImage _) => Dispatcher.UIThread.Post(Dispose);

            public async Task SetPosition(int newPosition)
            {
                foreach (var com in Commodities.Reverse()) { await com.Commodity.SetPosition(newPosition); }
            }

            private void UpdatePosition() => Position = Image.Commodities.DefaultIfEmpty().Min(c => c?.Position ?? int.MaxValue - 100);

            private void CommodityOnPositionChanged(object sender, PropertyChangedEventArgs _)
            {
                var com = sender as ImageCommodity;
                var comModel = GetCommodityModel(com);
                Commodities.Remove(comModel);
                AddToCommodities(new[] { comModel });
                UpdatePosition();
            }

            private void ImageOnCommodityRemoved(CImage sender, ImageCommodity commodity)
            {
                void Work()
                {
                    Commodities.Remove(GetCommodityModel(commodity));
                    if (Commodities.Count == 0 && _hostingTab._imageToMove == this) { _hostingTab.ResetImageToMove(); }

                    UpdatePosition();
                }
                if (Dispatcher.UIThread.CheckAccess() == false) { Dispatcher.UIThread.Post(Work); }
                else { Work(); }
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
                AddToCommodities(new[] { comModel });
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

            private void OnPropertyChanged([CallerMemberName] string propertyName = null)
            {
                if (Dispatcher.UIThread.CheckAccess()) { PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName)); }
                else { Dispatcher.UIThread.Post(() => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName))); }
            }
            public void Dispose()
            {
                _hostingTab._tvImagesItems.Remove(this);
                _hostingTab._tvImagesModels.Remove(this);
                if (ImageSource != DefaultThumbnail) { ImageSource.Dispose(); }
                //ImageSource = null;
                Image.FileUpdated -= ImageOnFileUpdated;
                Image.CommodityAdded -= ImageOnCommodityAdded;
                Image.CommodityRemoved -= ImageOnCommodityRemoved;
                while (Commodities.Count > 0) { Commodities[0].Dispose(); }
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
                Commodity.PropertyNotificationManager.Subscribe(nameof(RepoImageMan.Commodity.Name), CommodityOnNameChanged);
            }

            private void CommodityOnNameChanged(object sender, PropertyChangedEventArgs e) => OnPropertyChanged(e.PropertyName);

            public event PropertyChangedEventHandler PropertyChanged;

            private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
            {
                if (PropertyChanged == null) { return; }
                if (Dispatcher.UIThread.CheckAccess()) { PropertyChanged.Invoke(this, new PropertyChangedEventArgs(propertyName)); }
                else { Dispatcher.UIThread.Post(() => PropertyChanged.Invoke(this, new PropertyChangedEventArgs(propertyName))); }

            }

            public void Dispose()
            {
                Image.Commodities.Remove(this);
                Commodity.PropertyNotificationManager.Unsubscribe(nameof(RepoImageMan.Commodity.Name), CommodityOnNameChanged);
            }
        }
    }

    public partial class CommodityImageWindow
    {
        internal class ImageTab
        {
            private readonly CommodityImageWindow _hostingWindow;
            private readonly TreeView tvImages;

            internal readonly ObservableCollection<TvImagesImageModel> _tvImagesItems =
                new ObservableCollection<TvImagesImageModel>();

            internal readonly List<TvImagesImageModel> _tvImagesModels = new List<TvImagesImageModel>();
            private readonly ContextMenu tvImagesCTXMenu;

            private readonly MenuItem miExportImages,
                miExportSelectedImages,
                miExportAllImages,
                miUnExportAllImages,
                miUnExportSelectedImages,
                miCreateImage,
                miDeleteSelectedImagesAndCommdoities,
                miMoveImage,
                miMoveSelectedImage,
                miMoveBeforeSelectedImage,
                miMoveAfterSelectedImage,
                miGoToCommodity,
                miReplaceImageFile;

            private readonly TabItem tabImages;

            internal TvImagesImageModel? _imageToMove;
            private DateTime _imageToMoveSelectionTime = DateTime.MinValue;
            private readonly TimeSpan ImageMovingWindow = TimeSpan.FromMinutes(3);

            public ImageTab(CommodityImageWindow hostingWindow)
            {
                _hostingWindow = hostingWindow;

                tabImages = _hostingWindow.Get<TabItem>(nameof(tabImages));
                tvImages = _hostingWindow.Get<TreeView>(nameof(tvImages));
                tvImagesCTXMenu = _hostingWindow.Get<ContextMenu>(nameof(tvImagesCTXMenu));
                miCreateImage = _hostingWindow.Get<MenuItem>(nameof(miCreateImage));
                miExportImages = _hostingWindow.FindControl<MenuItem>(nameof(miExportImages));
                miExportSelectedImages = _hostingWindow.FindControl<MenuItem>(nameof(miExportSelectedImages));
                miUnExportSelectedImages = _hostingWindow.FindControl<MenuItem>(nameof(miUnExportSelectedImages));
                miExportAllImages = _hostingWindow.Get<MenuItem>(nameof(miExportAllImages));
                miUnExportAllImages = _hostingWindow.Get<MenuItem>(nameof(miUnExportAllImages));
                miDeleteSelectedImagesAndCommdoities = _hostingWindow.Get<MenuItem>(nameof(miDeleteSelectedImagesAndCommdoities));
                miMoveImage = _hostingWindow.Get<MenuItem>(nameof(miMoveImage));
                miMoveSelectedImage = _hostingWindow.Get<MenuItem>(nameof(miMoveSelectedImage));
                miMoveBeforeSelectedImage = _hostingWindow.Get<MenuItem>(nameof(miMoveBeforeSelectedImage));
                miMoveAfterSelectedImage = _hostingWindow.Get<MenuItem>(nameof(miMoveAfterSelectedImage));
                miGoToCommodity = _hostingWindow.FindControl<MenuItem>(nameof(miGoToCommodity));
                miReplaceImageFile = _hostingWindow.FindControl<MenuItem>(nameof(miReplaceImageFile));

                _hostingWindow._package.ImageAdded += Package_ImageAdded;
                foreach (var img in _hostingWindow._package.Images)
                {
                    new TvImagesImageModel(img, this);
                }
                Parallel.ForEach(_tvImagesModels, model => model.ReloadImageSource());
                GC.Collect();
                tvImages.Items = _tvImagesItems;

                tvImages.KeyDown += TvImages_KeyDown;

                tvImagesCTXMenu.ContextMenuOpening += TvImagesCTXMenuOnContextMenuOpening;
                miExportAllImages.Click += MiExportAllImagesOnClick;
                miUnExportAllImages.Click += MiUnExportAllImagesOnClick;
                miExportSelectedImages.Click += MiExportSelectedImagesOnClick;
                miUnExportSelectedImages.Click += MiUnExportSelectedImagesOnClick;
                miGoToCommodity.Click += MiGoToCommodityOnClick;
                miMoveSelectedImage.Click += MiMoveSelectedImageOnClick;
                miMoveBeforeSelectedImage.Click += MiMoveBeforeSelectedImageOnClick;
                miMoveAfterSelectedImage.Click += MiMoveAfterSelectedImageOnClick;
                miReplaceImageFile.Click += MiReplaceImageFile_Click;
                miDeleteSelectedImagesAndCommdoities.Click += MiDeleteSelectedImagesAndCommdoities_Click;
                miCreateImage.Click += MiCreateImage_Click;
            }

            private void Package_ImageAdded(CommodityPackage _, CImage image)
            {
                var imageModel = new TvImagesImageModel(image, this);
                imageModel.ReloadImageSource();
            }
            private async ValueTask CreateNewImage()
            {
                var ofd = new OpenFileDialog
                {
                    Title = $"New Image.",
                    AllowMultiple = false,
                };
                var newImagePath = (await ofd.ShowAsync(_hostingWindow))?.FirstOrDefault();
                if (string.IsNullOrWhiteSpace(newImagePath))
                {
                    await MessageBoxManager.GetMessageBoxStandardWindow("No image", "You didn't select any image to replace the old one.", ButtonEnum.Ok, MessageBox.Avalonia.Enums.Icon.Warning).ShowDialog(_hostingWindow);
                    return;
                }
                await using var imgStream = new FileStream(newImagePath, FileMode.Open, FileAccess.Read, FileShare.Read);
                if (IsValidImage(imgStream) == false)
                {
                    await MessageBoxManager.GetMessageBoxStandardWindow("Invalid Image", "The selected file doesn't represent a valid image.", ButtonEnum.Ok, MessageBox.Avalonia.Enums.Icon.Error).ShowDialog(_hostingWindow);
                    return;
                }
                imgStream.Position = 0;
                var newImage = await _hostingWindow._package.AddImage();
                await newImage.ReplaceFile(imgStream);
            }
            private async void MiCreateImage_Click(object? sender, RoutedEventArgs e) => await CreateNewImage();

            private async Task DeleteSelectedImagesAndCommodities()
            {
                foreach (var selectedImage in tvImages.SelectedItems.OfType<TvImagesImageModel>().ToArray())
                {
                    await selectedImage.Image.Delete();
                }

                foreach (var selectedCommodity in tvImages.SelectedItems.OfType<TvImagesCommodityModel>().ToArray())
                {
                    await selectedCommodity.Commodity.Delete();
                }
            }
            private async void MiDeleteSelectedImagesAndCommdoities_Click(object? sender, RoutedEventArgs e) => await DeleteSelectedImagesAndCommodities();

            private bool IsValidImage(Stream imgStream)
            {
                try
                {
                    if (SixLabors.ImageSharp.Image.Identify(imgStream).Height > 0) { return true; }
                }
                catch { }
                return false;
            }
            private async void MiReplaceImageFile_Click(object? sender, RoutedEventArgs e)
            {
                var selectedImage = GetSelectedImage()!;
                var ofd = new OpenFileDialog
                {
                    Title = $"New {selectedImage.ShortName} Image.",
                    AllowMultiple = false,
                };
                var newImagePath = (await ofd.ShowAsync(_hostingWindow))?.FirstOrDefault();
                if (string.IsNullOrWhiteSpace(newImagePath))
                {
                    await MessageBoxManager.GetMessageBoxStandardWindow("No image", "You didn't select any image to replace the old one.", ButtonEnum.Ok, MessageBox.Avalonia.Enums.Icon.Warning).ShowDialog(_hostingWindow);
                    return;
                }
                await using var imgStream = new FileStream(newImagePath, FileMode.Open, FileAccess.Read, FileShare.Read);
                if (IsValidImage(imgStream) == false)
                {
                    await MessageBoxManager.GetMessageBoxStandardWindow("Invalid Image", "The selected file doesn't represent a valid image.", ButtonEnum.Ok, MessageBox.Avalonia.Enums.Icon.Error).ShowDialog(_hostingWindow);
                    return;
                }
                imgStream.Position = 0;
                await selectedImage.Image.ReplaceFile(imgStream);
            }

            private async void MiMoveAfterSelectedImageOnClick(object? sender, RoutedEventArgs e)
            {
                var selectedImage = GetSelectedImage()!;
                if (selectedImage.Commodities.Count == 0)
                {
                    await MessageBoxManager.GetMessageBoxStandardWindow(new MessageBoxStandardParams
                    {
                        Icon = MessageBox.Avalonia.Enums.Icon.Forbidden,
                        ButtonDefinitions = ButtonEnum.Ok,
                        CanResize = false,
                        ShowInCenter = true,
                        WindowStartupLocation = WindowStartupLocation.CenterScreen,
                        ContentTitle = "Invalid Operation",
                        ContentMessage = "You can't use this image as a reference point because doesn't have any commodities assigned to it."
                    }).ShowDialog(_hostingWindow);
                    return;
                }

                if (_imageToMove == null || _imageToMove == selectedImage) { return; }

                int selectedImageMaxComPos = selectedImage.Commodities.Max(c => c.Commodity.Position);
                int newPos = _imageToMove.Position < selectedImage.Position
                                 ? selectedImageMaxComPos
                                 : selectedImageMaxComPos + 1;

                await _imageToMove.SetPosition(newPos);
                ResetImageToMove();
            }

            private async void MiMoveBeforeSelectedImageOnClick(object? sender, RoutedEventArgs e)
            {
                var selectedImage = GetSelectedImage()!;
                if (selectedImage.Commodities.Count == 0)
                {
                    await MessageBoxManager.GetMessageBoxStandardWindow(new MessageBoxStandardParams()
                    {
                        Icon = MessageBox.Avalonia.Enums.Icon.Forbidden,
                        ButtonDefinitions = ButtonEnum.Ok,
                        CanResize = false,
                        ShowInCenter = true,
                        WindowStartupLocation = WindowStartupLocation.CenterScreen,
                        ContentTitle = "Invalid Operation.",
                        ContentMessage = "You can't use this image as a reference point because doesn't have any commodities assigned to it."
                    }).ShowDialog(_hostingWindow);
                    return;
                }

                if (_imageToMove == null || _imageToMove == selectedImage) { return; }

                int newPos = _imageToMove.Position > selectedImage.Position
                                 ? selectedImage.Position
                                 : selectedImage.Position - 1;

                await _imageToMove.SetPosition(newPos);
                ResetImageToMove();
            }

            private async void MiMoveSelectedImageOnClick(object? sender, RoutedEventArgs e)
            {
                _imageToMove = GetSelectedImage()!;
                if (_imageToMove.Commodities.Count == 0)
                {
                    await MessageBoxManager.GetMessageBoxStandardWindow(new MessageBoxStandardParams
                    {
                        Icon = MessageBox.Avalonia.Enums.Icon.Forbidden,
                        ButtonDefinitions = ButtonEnum.Ok,
                        CanResize = false,
                        ShowInCenter = true,
                        WindowStartupLocation = WindowStartupLocation.CenterScreen,
                        ContentTitle = "Invalid Operation.",
                        ContentMessage = "You can't move an image that doesn't have any commodities assigned to it."
                    }).ShowDialog(_hostingWindow);
                    ResetImageToMove();
                    return;
                }

                _imageToMoveSelectionTime = DateTime.UtcNow;
            }

            internal void ResetImageToMove()
            {
                _imageToMove = null;
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
                tvImages.SelectedItems.Add(_tvImagesModels.First(c => c.Image.Id == com.Image.Id) /*.GetCommodityModel(com)*/);
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
                miExportSelectedImages.IsVisible = miUnExportSelectedImages.IsVisible = miDeleteSelectedImagesAndCommdoities.IsVisible = tvImages.SelectedItems.Count > 0;

                var selectedImage = GetSelectedImage();
                miMoveImage.IsVisible = miReplaceImageFile.IsVisible = tvImages.SelectedItems.Count == 1 && selectedImage != null;
                miMoveAfterSelectedImage.IsVisible = miMoveBeforeSelectedImage.IsVisible =
                                                         _imageToMove != null && selectedImage != _imageToMove;
                miGoToCommodity.IsVisible = tvImages.SelectedItems.Count == 1 && tvImages.SelectedItems[0] is TvImagesCommodityModel;

                if (miReplaceImageFile.IsVisible) { miReplaceImageFile.Header = $"Replace {selectedImage!.ShortName} Image"; }

                if (miMoveBeforeSelectedImage.IsVisible)
                {
                    miMoveBeforeSelectedImage.Header = $"Move {_imageToMove!.ShortName} Before {selectedImage!.ShortName}";
                }

                if (miMoveAfterSelectedImage.IsVisible)
                {
                    miMoveAfterSelectedImage.Header = $"Move {_imageToMove!.ShortName} After {selectedImage!.ShortName}";
                }

                if (miMoveImage.IsVisible) { miMoveSelectedImage.Header = $"Move {selectedImage!.ShortName}"; }
            }

            private async void TvImages_KeyDown(object? sender, KeyEventArgs e)
            {
                if (e.KeyModifiers != KeyModifiers.None) { return; }

                switch (e.Key)
                {
                    case Key.Insert:
                        await CreateNewImage();
                        break;
                    case Key.Delete:
                        await DeleteSelectedImagesAndCommodities();
                        break;
                }
            }

            private TvImagesImageModel? GetSelectedImage() => tvImages.SelectedItems.Count == 0 ? null : tvImages.SelectedItems[0] as TvImagesImageModel;

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
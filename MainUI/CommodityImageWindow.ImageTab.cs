using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Reactive.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Collections;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using MainUI.ImageTabModels;
using MessageBox.Avalonia;
using MessageBox.Avalonia.DTO;
using MessageBox.Avalonia.Enums;
using RepoImageMan;
using RepoImageMan.Controls;
namespace MainUI
{
    namespace ImageTabModels
    {
        public sealed class TvImagesImageModel : INotifyPropertyChanged, IDisposable
        {
            private static readonly PixelSize ThumbnailSize = new PixelSize(400, 400);
            private static readonly IBitmap DefaultThumbnail;
            static TvImagesImageModel()
            {
                var p = new Pen(Colors.Red.ToUint32(), 4.0f);

                using var defaultThumb = new RenderTargetBitmap(ThumbnailSize);

                using (var ctx = defaultThumb.CreateDrawingContext(null))
                {
                    ctx.DrawRectangle(p.Brush, p, new Rect(default, ThumbnailSize.ToSize(1.0)));
                    ctx.DrawLine(p, new Point(0, ThumbnailSize.Height), new Point(0, ThumbnailSize.Width));
                }
                using var ms = new MemoryStream();
                defaultThumb.Save(ms);
                ms.Position = 0;

                DefaultThumbnail = new Bitmap(ms);
            }

            private readonly CommodityImageWindow.ImageTab _hostingTab;

            //Wont create a dictionary for commodities models and instead I will use Commodities collection cause it will be pretty small
            public AvaloniaList<TvImagesCommodityModel> Commodities { get; }

            public TvImagesCommodityModel GetCommodityModel(ImageCommodity com) => Commodities.First(c => c.Commodity.Id == com.Id);
            /// <summary>
            /// Set to -1 to force even the first commodity to place it self and invoke <see cref="RePositionInTvItems"/>
            /// </summary>
            private int _position = -1;

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
                    var name = Commodities.FirstOrDefault()?.Name ?? $"Image{Image.Id}";
                    return name.Length <= 10 ? name : $"{name.Substring(0, 7)}...";
                }
            }
            public bool IsExported { get => Image.IsExported; set => Image.IsExported = value; }
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

            private List<IDisposable> _eventsSubscriptions;
            /// <summary>
            /// <see cref="ImageSource"/> won't be initiazlized and you must do it seperatly, its safe to call it from different threads ON DIFFERENT INSTANCES.
            /// </summary>
            public TvImagesImageModel(CImage image, CommodityImageWindow.ImageTab hostingTab)
            {
                _hostingTab = hostingTab;
                _hostingTab._tvImagesModels.Add(this);
                Image = image;
                UpdatePosition();
                Commodities = new AvaloniaList<TvImagesCommodityModel>(Image.Commodities.Select(c => new TvImagesCommodityModel(c, this)));
                Image.FileUpdated += ImageOnFileUpdated;
                Image.CommodityAdded += ImageOnCommodityAdded;
                Image.CommodityRemoved += ImageOnCommodityRemoved;

                _eventsSubscriptions = Commodities
                    .Select(com => com.Commodity.Where(pn => pn == nameof(Commodity.Position))
                        .Select(pn => com)
                        .Subscribe(CommodityOnPositionChanged))
                    .ToList();
                _eventsSubscriptions.Add(Image
                    .Where(pn => pn == nameof(CImage.IsExported))
                    .Subscribe(pn => OnPropertyChanged(pn)));
                Image.Deleting += Image_Deleting;
            }

            private void Image_Deleting(CImage _) => Dispatcher.UIThread.Post(Dispose);

            public async Task SetPosition(int newPosition)
            {
                foreach (var com in Commodities.Reverse()) { await com.Commodity.SetPosition(newPosition); }
            }

            private void UpdatePosition() => Position = Image.Commodities.DefaultIfEmpty().Min(c => c?.Position ?? int.MaxValue - 100);

            private void CommodityOnPositionChanged(TvImagesCommodityModel comModel)
            {
                Commodities.Remove(comModel);
                AddToCommodities(new[] { comModel });
                UpdatePosition();
            }

            private void ImageOnCommodityRemoved(CImage sender, ImageCommodity commodity)
            {
                //we should remove the PositionChanged event subscription from _eventsSubscriptions buf fuck it
                void Work()
                {
                    Commodities.Remove(GetCommodityModel(commodity));
                    if (Commodities.Count == 0 && _hostingTab._imageToMove == this) { _hostingTab.ResetImageToMove(); }

                    UpdatePosition();
                }
                Dispatcher.UIThread.Invoke(Work);
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

            private void ImageOnCommodityAdded(CImage _, ImageCommodity commodity)
            {
                var comModel = new TvImagesCommodityModel(commodity, this);
                _eventsSubscriptions
                    .Add(commodity
                    .Where(pn => pn == nameof(Commodity.Position))
                    .Select(pn => comModel)
                    .Subscribe(CommodityOnPositionChanged));
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
                if (Dispatcher.UIThread.CheckAccess())
                {
                    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
                }
                else
                {
                    Dispatcher.UIThread.Post(() => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName)));
                }
            }
            public void Dispose()
            {
                _hostingTab._tvImagesItems.Remove(this);
                _hostingTab._tvImagesModels.Remove(this);
                if (ImageSource != DefaultThumbnail) { ImageSource.Dispose(); }
                Image.FileUpdated -= ImageOnFileUpdated;
                Image.CommodityAdded -= ImageOnCommodityAdded;
                Image.CommodityRemoved -= ImageOnCommodityRemoved;
                foreach (var sub in _eventsSubscriptions) { sub.Dispose(); }
                _eventsSubscriptions.Clear();
                _eventsSubscriptions = null;
                while (Commodities.Count > 0) { Commodities[0].Dispose(); }
                Commodities.Clear();
                Image.Deleting -= Image_Deleting;
            }
        }

        public sealed class TvImagesCommodityModel : INotifyPropertyChanged, IDisposable
        {
            public TvImagesImageModel Image { get; }
            public Commodity Commodity { get; }
            public string Name => Commodity.Name;

            public bool IsExported
            {
                get => Commodity.IsExported;
                set => Commodity.IsExported = value;
            }

            private readonly IDisposable _commodityNotificationsSubscription;
            public TvImagesCommodityModel(Commodity commodity, TvImagesImageModel image)
            {
                Commodity = commodity;
                Image = image;
                _commodityNotificationsSubscription = Commodity
                    .Where(pn => pn == nameof(Commodity.Name) || pn == nameof(Commodity.IsExported))
                    .Subscribe(pn => OnPropertyChanged(pn));
            }

            public event PropertyChangedEventHandler PropertyChanged;

            private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
            {
                if (PropertyChanged is null) { return; }
                if (Dispatcher.UIThread.CheckAccess()) { PropertyChanged.Invoke(this, new PropertyChangedEventArgs(propertyName)); }
                else { Dispatcher.UIThread.Post(() => PropertyChanged.Invoke(this, new PropertyChangedEventArgs(propertyName))); }

            }

            public void Dispose()
            {
                Image.Commodities.Remove(this);
                _commodityNotificationsSubscription.Dispose();
            }
        }
    }

    public partial class CommodityImageWindow
    {
        public class ImageTab
        {
            private readonly CommodityImageWindow _hostingWindow;
            private readonly TreeView tvImages;

            internal readonly ObservableCollection<TvImagesImageModel> _tvImagesItems = new ObservableCollection<TvImagesImageModel>();

            internal readonly List<TvImagesImageModel> _tvImagesModels = new List<TvImagesImageModel>();
            private readonly ContextMenu tvImagesCTXMenu;

            private readonly MenuItem miExportImages,
                miExportSelectedImages,
                miExportAllImages,
                miUnExportAllImages,
                miUnExportSelectedImages,
                miCreateImage,
                miCreateImageCommodity,
                miDeleteSelectedImagesAndCommdoities,
                miMoveImage,
                miMoveSelectedImage,
                miMoveBeforeSelectedImage,
                miMoveAfterSelectedImage,
                miGoToCommodity,
                miReplaceImageFile,
                miSaveAllImagesAndCommoditiesToDb,
                miSaveSelectedImagesAndCommoditiesToDb,
                miReloadAllImagesAndCommoditiesFromDb,
                miReloadSelectedImagesAndCommoditiesToDb;

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
                miCreateImageCommodity = _hostingWindow.Get<MenuItem>(nameof(miCreateImageCommodity));
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
                miSaveAllImagesAndCommoditiesToDb = _hostingWindow.FindControl<MenuItem>(nameof(miSaveAllImagesAndCommoditiesToDb));
                miSaveSelectedImagesAndCommoditiesToDb = _hostingWindow.FindControl<MenuItem>(nameof(miSaveSelectedImagesAndCommoditiesToDb));
                miReloadAllImagesAndCommoditiesFromDb = _hostingWindow.FindControl<MenuItem>(nameof(miReloadAllImagesAndCommoditiesFromDb));
                miReloadSelectedImagesAndCommoditiesToDb = _hostingWindow.FindControl<MenuItem>(nameof(miReloadSelectedImagesAndCommoditiesToDb));


                _hostingWindow._package.ImageAdded += Package_ImageAdded;
                foreach (var img in _hostingWindow._package.Images)
                {
                    new TvImagesImageModel(img, this);
                }
                Parallel.ForEach(_tvImagesModels, model => model.ReloadImageSource());
                GC.Collect();
                tvImages.Items = _tvImagesItems;

                tvImages.KeyDown += TvImages_KeyDown;

                tvImagesCTXMenu.ContextMenuOpening += TvImagesCTXMenu_ContextMenuOpening;
                miExportAllImages.Click += MiExportAllImages_Click;
                miUnExportAllImages.Click += MiUnExportAll_Click;
                miExportSelectedImages.Click += MiExportSelected_Click;
                miUnExportSelectedImages.Click += MiUnExportSelected_Click;
                miGoToCommodity.Click += MiGoToCommodity_Click;
                miMoveSelectedImage.Click += MiMoveSelectedImage_Click;
                miMoveBeforeSelectedImage.Click += MiMoveBeforeSelectedImage_Click;
                miMoveAfterSelectedImage.Click += MiMoveAfterSelectedImage_Click;
                miReplaceImageFile.Click += MiReplaceImageFile_Click;
                miDeleteSelectedImagesAndCommdoities.Click += MiDeleteSelectedImagesAndCommdoities_Click;
                miCreateImage.Click += MiCreateImage_Click;
                miCreateImageCommodity.Click += MiCreateImageCommodity_Click;
                miSaveAllImagesAndCommoditiesToDb.Click += MiSaveAllImagesAndCommoditiesToDb_Click;
                miSaveSelectedImagesAndCommoditiesToDb.Click += MiSaveSelectedImagesAndCommoditiesToDb_Click;
                miReloadAllImagesAndCommoditiesFromDb.Click += MiReloadAllImagesAndCommoditiesFromDb_Click;
                miReloadSelectedImagesAndCommoditiesToDb.Click += MiReloadSelectedImagesAndCommoditiesToDb_Click;
            }

            private async void MiReloadSelectedImagesAndCommoditiesToDb_Click(object? sender, RoutedEventArgs e)
            {
                await tvImages.SelectedItems.OfType<TvImagesImageModel>().ForEachAsync(img => img.Image.Reload());
                await tvImages.SelectedItems.OfType<TvImagesCommodityModel>().ForEachAsync(com => com.Commodity.Reload());
            }

            private async void MiReloadAllImagesAndCommoditiesFromDb_Click(object? sender, RoutedEventArgs e)
            {
                await _tvImagesModels.ForEachAsync(async img =>
                {
                    await img.Image.Reload();
                    foreach (var com in img.Image.Commodities)
                    {
                        await com.Reload();
                    }
                });
            }

            private async void MiSaveSelectedImagesAndCommoditiesToDb_Click(object? sender, RoutedEventArgs e)
            {
                await tvImages.SelectedItems.OfType<TvImagesImageModel>().ForEachAsync(img => img.Image.Save());
                await tvImages.SelectedItems.OfType<TvImagesCommodityModel>().ForEachAsync(com => com.Commodity.Save());
            }

            private async void MiSaveAllImagesAndCommoditiesToDb_Click(object? sender, RoutedEventArgs e)
            {
                await _tvImagesModels.ForEachAsync(async img =>
                {
                    await img.Image.Save();
                    foreach (var com in img.Image.Commodities)
                    {
                        await com.Save();
                    }
                });
            }

            private async void MiCreateImageCommodity_Click(object? sender, RoutedEventArgs e)
            {
                var selectedImage = GetSelectedImage()!;
                await selectedImage.Image.AddCommodity();
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
                if (!IsValidImage(imgStream))
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
                var selectedComs = tvImages.SelectedItems.OfType<TvImagesCommodityModel>().ToArray();
                var selectedImages = tvImages.SelectedItems.OfType<TvImagesImageModel>().ToArray();
                if (selectedImages.Length == 0 && selectedComs.Length == 0) { return; }
                string confMessage = "Are you sure you want to delete ";
                if (selectedComs.Length > 0)
                {
                    confMessage += $"commodities:\n{string.Join("\n", selectedComs.Select((c, i) => $"    {i + 1}- {c.Name}."))}";
                }
                if (selectedImages.Length > 0)
                {
                    confMessage += selectedComs.Length > 0 ? "\n\nAnd images:\n" : "images:\n";
                    confMessage += $"{string.Join("\n", selectedImages.Select((img, i) => $"    {i + 1}- {img.ShortName}."))}";
                }
                var confRes = await MessageBoxManager.GetMessageBoxStandardWindow("Confirmation",
                    confMessage,
                    ButtonEnum.YesNo,
                    MessageBox.Avalonia.Enums.Icon.Warning)
                    .ShowDialog(_hostingWindow);
                if (confRes != ButtonResult.Yes) { return; }
                foreach (var com in selectedComs)
                {

                    await com.Commodity.Delete();
                }
                foreach (var selectedImage in selectedImages)
                {
                    await selectedImage.Image.Delete();
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
                if (!IsValidImage(imgStream))
                {
                    await MessageBoxManager.GetMessageBoxStandardWindow("Invalid Image", "The selected file doesn't represent a valid image.", ButtonEnum.Ok, MessageBox.Avalonia.Enums.Icon.Error).ShowDialog(_hostingWindow);
                    return;
                }
                imgStream.Position = 0;
                await selectedImage.Image.ReplaceFile(imgStream);
            }

            private async void MiMoveAfterSelectedImage_Click(object? sender, RoutedEventArgs e)
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

                if (_imageToMove is null || _imageToMove == selectedImage) { return; }

                int newPos = _imageToMove.Position < selectedImage.Position
                                 ? selectedImage.Position
                                 : selectedImage.Position + 1;

                await _imageToMove.SetPosition(newPos);
                ResetImageToMove();
            }

            private async void MiMoveBeforeSelectedImage_Click(object? sender, RoutedEventArgs e)
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

                if (_imageToMove is null || _imageToMove == selectedImage) { return; }

                int newPos = _imageToMove.Position > selectedImage.Position
                                 ? selectedImage.Position
                                 : selectedImage.Position - 1;

                await _imageToMove.SetPosition(newPos);
                ResetImageToMove();
            }

            private async void MiMoveSelectedImage_Click(object? sender, RoutedEventArgs e)
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

            private void MiGoToCommodity_Click(object? sender, RoutedEventArgs e)
            {
                if (tvImages.SelectedItems.Count != 1 || !(tvImages.SelectedItems[0] is TvImagesCommodityModel com))
                {
                    return;
                }

                _hostingWindow._commodityTab.GoToCommodity(com.Commodity);
            }

            public void GoToCommodity(ImageCommodity com)
            {
                _hostingWindow.Activate();
                tabImages.IsSelected = true;
                tvImages.SelectedItems.Clear();
                tvImages.SelectedItems.Add(_tvImagesModels.First(c => c.Image.Id == com.Image.Id) /*.GetCommodityModel(com)*/);
            }
            public void GoToImage(CImage img)
            {
                _hostingWindow.Activate();
                tabImages.IsSelected = true;
                tvImages.SelectedItems.Clear();
                tvImages.SelectedItems.Add(_tvImagesModels.First(c => c.Image == img));
            }
            private void MiUnExportSelected_Click(object? sender, RoutedEventArgs e)
            {
                foreach (var img in tvImages.SelectedItems.OfType<TvImagesImageModel>()) { img.IsExported = false; }
                foreach (var com in tvImages.SelectedItems.OfType<TvImagesCommodityModel>()) { com.IsExported = false; }
            }

            private void MiExportSelected_Click(object? sender, RoutedEventArgs e)
            {
                foreach (var img in tvImages.SelectedItems.OfType<TvImagesImageModel>()) { img.IsExported = true; }
                foreach (var com in tvImages.SelectedItems.OfType<TvImagesCommodityModel>()) { com.IsExported = true; }
            }

            private void MiUnExportAll_Click(object? sender, RoutedEventArgs e)
            {
                foreach (var img in _tvImagesItems)
                {
                    img.IsExported = false;
                    foreach (var com in img.Commodities) { com.IsExported = false; }
                }
            }

            private void MiExportAllImages_Click(object? sender, RoutedEventArgs e)
            {
                foreach (var img in _tvImagesItems)
                {
                    img.IsExported = true;
                    foreach (var com in img.Commodities) { com.IsExported = true; }
                }
            }

            private void TvImagesCTXMenu_ContextMenuOpening(object sender, CancelEventArgs e)
            {
                if (DateTime.UtcNow - _imageToMoveSelectionTime > ImageMovingWindow) { ResetImageToMove(); }

                miExportImages.IsVisible = _tvImagesItems.Count > 0;
                miExportSelectedImages.IsVisible = miUnExportSelectedImages.IsVisible = miDeleteSelectedImagesAndCommdoities.IsVisible = tvImages.SelectedItems.Count > 0;

                var selectedImage = GetSelectedImage();
                miCreateImageCommodity.IsVisible = miMoveImage.IsVisible = miReplaceImageFile.IsVisible = tvImages.SelectedItems.Count == 1 && selectedImage != null;
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
            private async void DesignImage(TvImagesImageModel img)
            {
                if (img is null) { return; }
                var dWin = new DesigningWindow(img.Image, this, _hostingWindow._commodityTab);
                try
                {
                    dWin.Show();
                }
                catch (InvalidOperationException ex) when (ex.TargetSite?.Name == nameof(DesignCImage.Init))
                {
                    dWin.Close();
                    await MessageBoxManager.GetMessageBoxStandardWindow(new MessageBoxStandardParams
                    {
                        WindowStartupLocation = WindowStartupLocation.CenterOwner,
                        CanResize = false,
                        ContentTitle = "Error",
                        ContentHeader = "Invalid Operation",
                        ContentMessage = $"{img.ShortName} is currently being designed in another window.",
                        ButtonDefinitions = ButtonEnum.Ok,
                        Icon = MessageBox.Avalonia.Enums.Icon.Error

                    }).ShowDialog(_hostingWindow);
                }
            }
            private async void TvImages_KeyDown(object? sender, KeyEventArgs e)
            {
                switch (e.Key)
                {
                    case Key.Insert:
                        await CreateNewImage();
                        break;
                    case Key.Delete:
                        await DeleteSelectedImagesAndCommodities();
                        break;
                    case Key.Enter:
                        if (tvImages.SelectedItems.Count == 1)
                        {
                            DesignImage(GetSelectedImage()!);
                        }
                        break;
                    case Key.S:
                        if (e.KeyModifiers == KeyModifiers.Control)
                        {
                            await tvImages.SelectedItems.OfType<TvImagesImageModel>().ForEachAsync(img => img.Image.Save());
                            await tvImages.SelectedItems.OfType<TvImagesCommodityModel>().ForEachAsync(com => com.Commodity.Save());
                        }
                        break;
                    case Key.R:
                        if (e.KeyModifiers == KeyModifiers.Control)
                        {
                            await tvImages.SelectedItems.OfType<TvImagesImageModel>().ForEachAsync(img => img.Image.Reload());
                            await tvImages.SelectedItems.OfType<TvImagesCommodityModel>().ForEachAsync(com => com.Commodity.Reload());
                        }
                        break;

                }
            }

            private TvImagesImageModel? GetSelectedImage() => tvImages.SelectedItems.Count == 0 ? null : tvImages.SelectedItems[0] as TvImagesImageModel;

            //Will be called from hosting window
            public void TvImages_ImageRightClicked(object? sender, PointerPressedEventArgs e)
            {
                if (!((sender as IDataContextProvider)?.DataContext is TvImagesImageModel clickedImage)) return;
                if (!tvImages.SelectedItems.Contains(clickedImage))
                {
                    tvImages.SelectedItems.Add(clickedImage);
                }
            }
            public void TvImages_ImageDoubleClicked(object? sender, PointerPressedEventArgs e)
            {
                if (!((sender as IDataContextProvider)?.DataContext is TvImagesImageModel clickedImage)) return;
                DesignImage(clickedImage);
            }
        }
    }
}
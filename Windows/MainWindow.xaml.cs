using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.Windows.Storage.Pickers;
using QStickerManager.Pages;
using QStickerManager.Stickers;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Windows.ApplicationModel.DataTransfer;
using Windows.Storage;


// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace QStickerManager.Windows
{
    /// <summary>
    /// An empty window that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
            string basePath =
                Path.Combine(ApplicationData.Current.LocalFolder.Path, "QStickerManager");
            stickerRepository = new(basePath);
            stickerRepository.LoadMetaFile();
            RefreshStickers();
        }

        private readonly StickerRepository stickerRepository;
        private readonly StickerArchiveService stickerArchiveService = new();
        private bool isDraggingFromGrid;

        private async void ImportFiles_Click(object sender, RoutedEventArgs e)
        {
            FileOpenPicker picker = new(AppWindow.Id);
            foreach (string ext in StickerFileTypes.ImageExtensions)
            {
                picker.FileTypeFilter.Add(ext);
            }
            picker.ViewMode = PickerViewMode.Thumbnail;
            IReadOnlyList<PickFileResult> files = await picker.PickMultipleFilesAsync();

            await ImportStickers(files.Select(f => f.Path));
        }

        private async void ImportZip_Click(object sender, RoutedEventArgs e)
        {
            FileOpenPicker picker = new(AppWindow.Id);
            picker.FileTypeFilter.Add(StickerFileTypes.ZipExtension);
            IReadOnlyList<PickFileResult> files = await picker.PickMultipleFilesAsync();

            await ImportStickers(files.Select(f => f.Path));
        }

        private async void ImportFolders_Click(object sender, RoutedEventArgs e)
        {
            FolderPicker picker = new(AppWindow.Id);
            IReadOnlyList<PickFolderResult> folders = await picker.PickMultipleFoldersAsync();

            IEnumerable<string> files = folders
                .SelectMany(folder => Directory.GetFiles(folder.Path))
                .Where(StickerFileTypes.IsImageFile);
            await ImportStickers(files);
        }

        private async void ImportQQ_Click(object sender, RoutedEventArgs e)
        {
            string qqDataPath = Path.Combine(
                        Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
                        "Tencent Files");
            if (!Directory.Exists(qqDataPath))
            {
                await MessageBox("Files not found", "Did not found QQ stickers. Might be moved?");
                return;
            }

            string[] users = Directory.GetDirectories(qqDataPath)
                .Select(p => Path.GetFileName(p) ?? "").Except(["nt_qq", ""]).ToArray() ?? [];
            if (users.Length > 1)
            {
                SelectQQUsersContentDialogContent dialogContent = new(users);
                ContentDialog dialog = new()
                {
                    Title = "Sticker Already Exists",
                    Content = dialogContent,
                    PrimaryButtonText = "OK",
                    CloseButtonText = "Cancel",
                    XamlRoot = Content.XamlRoot
                };

                ContentDialogResult result = await dialog.ShowAsync();
                if (result == ContentDialogResult.None) return;
                users = dialogContent.Selections;
            }

            List<string> stickerPaths = [];
            foreach (string user in users)
            {
                string path = Path.Combine(qqDataPath, user, "nt_qq", "nt_data", "Emoji", "personal_emoji", "Ori");
                if (Path.Exists(path))
                {
                    stickerPaths.AddRange(
                        Directory.GetFiles(path)
                        .Where(StickerFileTypes.IsImageFile));
                }
            }

            await ImportStickers(stickerPaths);
        }

        private async Task ImportStickers(IEnumerable<string> filesPath)
        {
            await ImportStickers(await GetImportCandidatesAsync(filesPath));
        }

        private async Task ImportStickers(IEnumerable<StickerImportSource> stickersToImport)
        {
            bool skipExisting = false;
            foreach (StickerImportSource? stickerToImport in stickersToImport)
            {
                if (stickerToImport is null) continue;

                try
                {
                    var importResult = await Task.Run(() =>
                    {
                        var success = stickerRepository.TryPrepareImport(
                            stickerToImport.Path,
                            stickerToImport.Description,
                            stickerToImport.Keywords,
                            out string hash,
                            out Sticker? sticker);
                        return (success, hash, sticker);
                    });

                    if (importResult.success)
                        stickerRepository.AddImportedSticker(importResult.sticker);

                    // if not repeated, continue. otherwise pop up a message box
                    if (importResult.success || skipExisting) continue;

                    StickerFoundContentDialogContent dialogContent = new(stickerToImport.Path, importResult.hash);
                    ContentDialog dialog = new()
                    {
                        Title = "Sticker Already Exists",
                        Content = dialogContent,
                        PrimaryButtonText = "Skip",
                        SecondaryButtonText = "Skip for all",
                        CloseButtonText = "Abort",
                        XamlRoot = Content.XamlRoot
                    };

                    ContentDialogResult result = await dialog.ShowAsync();
                    switch (result)
                    {
                        case ContentDialogResult.Primary:
                            continue;
                        case ContentDialogResult.Secondary:
                            skipExisting = true;
                            break;
                        case ContentDialogResult.None:
                            return;
                    }
                }
                finally
                {
                    stickerToImport.Cleanup();
                }
            }
            stickerRepository.UpdateMetaFile();
            RefreshStickers();
        }

        private async Task<List<StickerImportSource>> GetImportCandidatesAsync(IEnumerable<string> filesPath)
        {
            List<StickerImportSource> candidates = [];
            foreach (string? filePath in filesPath)
            {
                if (string.IsNullOrWhiteSpace(filePath)) continue;

                if (StickerFileTypes.IsZipFile(filePath))
                {
                    candidates.AddRange(await Task.Run(() => stickerArchiveService.ReadArchive(filePath)));
                    continue;
                }

                if (StickerFileTypes.IsImageFile(filePath))
                    candidates.Add(new(filePath, "", []));
            }

            return candidates;
        }

        private void RefreshStickers()
        {
            int count = stickerRepository.Stickers.Count;
            StatusTextRight2.Text = $"{count} sticker{(count == 1 ? "" : "s")} loaded";
        }

        private async Task MessageBox(string title, string message)
        {
            ContentDialog dialog = new()
            {
                Title = title,
                Content = message,
                CloseButtonText = "OK",
                XamlRoot = Content.XamlRoot
            };

            await dialog.ShowAsync();
        }

        //private Flyout? _previewFlyout;

        private void Sticker_PointerEntered(object sender, Microsoft.UI.Xaml.Input.PointerRoutedEventArgs e)
        {
            if (((FrameworkElement)sender).DataContext is Sticker sticker)
            {
                //_previewFlyout ??= new Flyout()
                //{
                //    Content = new Image()
                //    {
                //        Source = new BitmapImage(new Uri(sticker.Path))
                //    }
                //};
                //_previewFlyout.ShowAt(sender as FrameworkElement);

                //var name = string.IsNullOrEmpty(sticker.Description) ? sticker.Hash : sticker.Description;
                if (isSelecting) return;

                StatusTextLeft.Text = sticker.Description;
                StatusTextRight1.Text = string.Join(", ", sticker.Keywords);
                StatusTextRight2.Text = Path.GetFileName(sticker.Path);
            }
        }

        private async void ExportZipFile_Click(object sender, RoutedEventArgs e)
        {
            List<Sticker> stickers = [.. StickersGridView.SelectedItems.Cast<Sticker>()];
            if (stickers.Count == 0)
            {
                await MessageBox("No stickers selected", "Select one or more stickers before exporting.");
                return;
            }

            FileSavePicker picker = new(AppWindow.Id)
            {
                SuggestedFileName = "stickers"
            };
            picker.FileTypeChoices.Add("Zip archive", [".zip"]);

            PickFileResult? file = await picker.PickSaveFileAsync();
            string? path = file?.Path;
            if (string.IsNullOrWhiteSpace(path)) return;

            await Task.Run(() =>
            {
                stickerArchiveService.WriteArchive(path, stickers);
            });

            StatusTextRight2.Text = $"Exported {stickers.Count} sticker{(stickers.Count == 1 ? "" : "s")} to zip";
        }

        private async void ExportFiles_Click(object sender, RoutedEventArgs e)
        {
            FolderPicker picker = new(AppWindow.Id);
            PickFolderResult folder = await picker.PickSingleFolderAsync();
            string? path = folder?.Path;
            if (path == null || !Directory.Exists(path)) return;

            foreach (Sticker sticker in StickersGridView.SelectedItems.Cast<Sticker>())
            {
                StickerRepository.CopyStickerFile(sticker, path);
            }
        }

        private async void StickersGridView_ItemClick(object sender, ItemClickEventArgs e)
        {
            if (isSelecting || e.ClickedItem is not Sticker sticker) return;

            await CopyStickersToClipboard([sticker]);
            StatusTextLeft.Text = sticker.Description;
            StatusTextRight1.Text = string.Join(", ", sticker.Keywords);
            StatusTextRight2.Text = $"{Path.GetFileName(sticker.Path)} copied to clipboard";
        }

        private async Task CopyStickersToClipboard(IEnumerable<Sticker> stickers)
        {
            StorageFile[] files = await Task.WhenAll(
                stickers.Select(sticker =>
                    StorageFile.GetFileFromPathAsync(stickerRepository.EnsureGif(sticker)).AsTask()));

            DataPackage package = new()
            {
                RequestedOperation = DataPackageOperation.Copy
            };
            package.SetStorageItems(files);
            Clipboard.SetContent(package);
        }

        private void Window_Closed(object sender, WindowEventArgs args)
        {
            stickerRepository.UpdateMetaFile();
        }

        private bool isSelecting = false;
        private void SelectButton_Click(object sender, RoutedEventArgs e)
        {
            isSelecting ^= true;
            SelectButton.Content = isSelecting ? "Cancel" : "Select";
            StickersGridView.SelectionMode = isSelecting ? ListViewSelectionMode.Multiple : ListViewSelectionMode.Single;
        }

        private void StickersGridView_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (!isSelecting) return;

            int selectedCount = StickersGridView.SelectedItems.Count;
            ActionButton.IsEnabled = selectedCount > 0;
            StatusTextRight2.Text = $"{selectedCount} sticker{((selectedCount == 1) ? "" : "s")} selected";
        }

        private async void RemoveSelected_Click(object sender, RoutedEventArgs e)
        {
            RemoveStickers(StickersGridView.SelectedItems.Cast<Sticker>());
            //ConfirmRemoveContentDialogContent content = new(StickersGridView.SelectedItems.Count);
            //ContentDialog dialog = new()
            //{
            //    Title = "Remove Sticker",
            //    Content = content,
            //    PrimaryButtonText = "Confirm",
            //    CloseButtonText = "Cancel",
            //    XamlRoot = Content.XamlRoot
            //};

            //if (await dialog.ShowAsync() == ContentDialogResult.None) return;
            //bool deleteFile = content.DoDeleteFile;

            //object[] selectedItems = StickersGridView.SelectedItems.ToArray();
            //foreach (object? item in selectedItems)
            //    if (item is Sticker sticker)
            //        stickerRepository.Remove(sticker, deleteFile);

            //stickerRepository.UpdateMetaFile();
        }

        private void SelectedMoveToFront_Click(object sender, RoutedEventArgs e)
        {
            List<Sticker> selected = [.. StickersGridView.SelectedItems.Reverse().Cast<Sticker>()];
            foreach (Sticker sticker in selected)
                stickerRepository.MoveToFront(sticker);
        }

        private async void RemoveStickers(IEnumerable<Sticker> stickers)
        {
            stickers = [.. stickers];
            ConfirmRemoveContentDialogContent content = new(stickers.Count());
            ContentDialog dialog = new()
            {
                Title = "Remove Sticker",
                Content = content,
                PrimaryButtonText = "Confirm",
                CloseButtonText = "Cancel",
                XamlRoot = Content.XamlRoot
            };

            if (await dialog.ShowAsync() == ContentDialogResult.None) return;
            bool deleteFile = content.DoDeleteFile;

            foreach (Sticker sticker in stickers)
                stickerRepository.Remove(sticker, deleteFile);

            stickerRepository.UpdateMetaFile();
        }

        private async void ExportSticker_Click(object sender, RoutedEventArgs e)
        {
            if (sender is not MenuFlyoutItem item || item.DataContext is not Sticker sticker)
                return;

            FileSavePicker picker = new(AppWindow.Id)
            {
                SuggestedFileName = Path.GetFileNameWithoutExtension(sticker.Path)
            };
            picker.FileTypeChoices.Add(
                $"{Path.GetExtension(sticker.Path).ToUpperInvariant()} file",
                [Path.GetExtension(sticker.Path)]);

            PickFileResult? file = await picker.PickSaveFileAsync();
            string? path = file?.Path;
            if (string.IsNullOrWhiteSpace(path)) return;

            File.Copy(sticker.Path, path, true);
            StatusTextRight2.Text = $"{Path.GetFileName(sticker.Path)} exported";
        }

        private async void CopySticker_Click(object sender, RoutedEventArgs e)
        {
            if (sender is not MenuFlyoutItem item || item.DataContext is not Sticker sticker)
                return;

            await CopyStickersToClipboard([sticker]);
            StatusTextRight2.Text = $"{Path.GetFileName(sticker.Path)} copied to clipboard";
        }

        private async void CopySelected_Click(object sender, RoutedEventArgs e)
        {
            List<Sticker> stickers = [.. StickersGridView.SelectedItems.Cast<Sticker>()];
            if (stickers.Count == 0)
            {
                await MessageBox("No stickers selected", "Select one or more stickers before copying.");
                return;
            }

            await CopyStickersToClipboard(stickers);
            StatusTextRight2.Text = $"Copied {stickers.Count} sticker{(stickers.Count == 1 ? "" : "s")} to clipboard";
        }

        private void RemoveSticker_Click(object sender, RoutedEventArgs e)
        {
            if (sender is MenuFlyoutItem grid && grid.DataContext is Sticker sticker)
                RemoveStickers([sticker]);
        }

        private void StickerMoveToFront_Click(object sender, RoutedEventArgs e)
        {
            if (sender is MenuFlyoutItem grid && grid.DataContext is Sticker sticker)
                stickerRepository.MoveToFront(sticker);
        }

        private void StickersGridView_DragOver(object sender, DragEventArgs e)
        {
            if (isDraggingFromGrid)
            {
                e.AcceptedOperation = DataPackageOperation.Move;
                return;
            }

            if (e.DataView.Contains(StandardDataFormats.StorageItems)
                /* || e.DataView.Contains(StandardDataFormats.Bitmap)*/)
            {
                e.DragUIOverride.Caption = "Import stickers";
                e.AcceptedOperation = DataPackageOperation.Copy;
            }
            //if (e.DataView.Contains(StandardDataFormats.Bitmap))
            //{

            //}
        }

        private async void StickersGridView_Drop(object sender, DragEventArgs e)
        {
            if (isDraggingFromGrid)
            {
                isDraggingFromGrid = false;
                return;
            }

            if (e.DataView.Contains(StandardDataFormats.StorageItems))
            {
                IReadOnlyList<IStorageItem> items = await e.DataView.GetStorageItemsAsync();

                IEnumerable<IStorageItem> notSupportedFiles = items.Where(f => !StickerFileTypes.IsSupportedImportFile(f.Path));
                if (notSupportedFiles.Any())
                {
                    await MessageBox("Some files couldn't be imported",
                        $"{notSupportedFiles.Count()} file{(notSupportedFiles.Count() == 1 ? " is" : "s are")} in unsupported formats");
                }

                await ImportStickers(items.Select(f => f.Path).Where(StickerFileTypes.IsSupportedImportFile));
            }

            //if (e.DataView.Contains(StandardDataFormats.Bitmap))
            //{
            //    RandomAccessStreamReference item = await e.DataView.GetBitmapAsync();

            //    /ODO: Implement
            //}
        }

        private async void StickersGridView_DragItemsStarting(object sender, DragItemsStartingEventArgs e)
        {
            isDraggingFromGrid = true;
            e.Data.SetStorageItems(
                await Task.WhenAll(e.Items.Cast<Sticker>()
                .Select(s => StorageFile.GetFileFromPathAsync(stickerRepository.EnsureGif(s)).AsTask()))
                );
        }

        private void StickersGridView_DragItemsCompleted(ListViewBase sender, DragItemsCompletedEventArgs args)
        {
            isDraggingFromGrid = false;
        }
    }
}

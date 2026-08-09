using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.Windows.Storage.Pickers;
using QStickerManager.Localization;
using QStickerManager.Pages;
using QStickerManager.Settings;
using QStickerManager.Stickers;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
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
            appSettings = new AppSettings();
            basePath = appSettings.BasePath;
            stickerRepository = new StickerRepository(basePath);
            stickerRepository.Stickers.CollectionChanged += RepositoryStickers_CollectionChanged;
            StickersGridView.ItemsSource = stickerRepository.Stickers;
            _ = ReloadStickerRepositoryAsync();
        }

        private readonly AppSettings appSettings;
        private string basePath;
        private StickerRepository stickerRepository = null!;
        private readonly StickerArchiveService stickerArchiveService = new();
        private bool isDraggingFromGrid;
        private Settings? settingsWindow;
        private readonly HashSet<string> activeKeywordFilters = new(StringComparer.OrdinalIgnoreCase);
        private readonly ObservableCollection<Sticker> filteredStickers = [];

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
                await MessageBox(
                    Localizer.Get("QQFilesNotFound_Title"),
                    Localizer.Get("QQFilesNotFound_Message"));
                return;
            }

            string[] users = Directory.GetDirectories(qqDataPath)
                .Select(p => Path.GetFileName(p) ?? "").Except(["nt_qq", ""]).ToArray() ?? [];
            if (users.Length > 1)
            {
                SelectQQUsersContentDialogContent dialogContent = new(users);
                ContentDialog dialog = new()
                {
                    Title = Localizer.Get("SelectQQUsers_Title"),
                    Content = dialogContent,
                    PrimaryButtonText = Localizer.Get("Button_OK"),
                    CloseButtonText = Localizer.Get("Button_Cancel"),
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
            StatusTextLeft.Text = Localizer.Get("Status_Importing");
            var candidates = await GetImportCandidatesAsync(filesPath);
            StatusTextLeft.Text = Localizer.FormatCount(
                candidates.Count,
                "Status_ImportingCountOne",
                "Status_ImportingCountMany");
            await ImportStickers(candidates);
            StatusTextLeft.Text = Localizer.Get("Status_ImportComplete");
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
                        Title = Localizer.Get("StickerExists_Title"),
                        Content = dialogContent,
                        PrimaryButtonText = Localizer.Get("Button_Skip"),
                        SecondaryButtonText = Localizer.Get("Button_SkipAll"),
                        CloseButtonText = Localizer.Get("Button_Abort"),
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
            StatusTextRight2.Text = Localizer.FormatCount(count, "Status_LoadedOne", "Status_LoadedMany");
            RefreshKeywordFilters();
            RefreshStickerGrid();
        }

        private void SearchBox_TextChanged(AutoSuggestBox sender, AutoSuggestBoxTextChangedEventArgs args)
        {
            RefreshStickerGrid();
        }

        private void RefreshKeywordFilters()
        {
            activeKeywordFilters.RemoveWhere(activeFilter =>
                !stickerRepository.Keywords.Any(keyword =>
                    string.Equals(keyword, activeFilter, StringComparison.OrdinalIgnoreCase)));

            while (KeywordFiltersPanel.Children.Count > 1)
                KeywordFiltersPanel.Children.RemoveAt(1);

            NoKeywordsText.Visibility = stickerRepository.Keywords.Count == 0
                ? Visibility.Visible
                : Visibility.Collapsed;

            foreach (string keyword in stickerRepository.Keywords)
            {
                ToggleButton filter = new()
                {
                    Content = keyword,
                    Tag = keyword,
                    IsChecked = activeKeywordFilters.Contains(keyword)
                };
                filter.Checked += KeywordFilter_Changed;
                filter.Unchecked += KeywordFilter_Changed;
                KeywordFiltersPanel.Children.Add(filter);
            }
        }

        private void KeywordFilter_Changed(object sender, RoutedEventArgs e)
        {
            if (sender is not ToggleButton filter || filter.Tag is not string keyword)
                return;

            if (filter.IsChecked == true)
                activeKeywordFilters.Add(keyword);
            else
                activeKeywordFilters.Remove(keyword);

            RefreshStickerGrid();
        }

        private void RefreshStickerGrid()
        {
            string query = SearchBox.Text.Trim();
            bool hasSearch = !string.IsNullOrWhiteSpace(query);
            bool hasKeywordFilter = activeKeywordFilters.Count > 0;

            if (!hasSearch && !hasKeywordFilter)
            {
                if (!ReferenceEquals(StickersGridView.ItemsSource, stickerRepository.Stickers))
                    StickersGridView.ItemsSource = stickerRepository.Stickers;
                return;
            }

            IEnumerable<Sticker> stickers = stickerRepository.Stickers;
            if (hasSearch)
                stickers = stickers.Where(sticker =>
                    sticker.Hash.Contains(query, StringComparison.OrdinalIgnoreCase)
                    || sticker.Description.Contains(query, StringComparison.OrdinalIgnoreCase)
                    || sticker.Keywords.Any(keyword =>
                        keyword.Contains(query, StringComparison.OrdinalIgnoreCase)));

            if (hasKeywordFilter)
                stickers = stickers.Where(sticker =>
                    activeKeywordFilters.All(filter =>
                        sticker.Keywords.Any(keyword =>
                            string.Equals(keyword, filter, StringComparison.OrdinalIgnoreCase))));

            filteredStickers.Clear();
            foreach (Sticker sticker in stickers)
                filteredStickers.Add(sticker);

            if (!ReferenceEquals(StickersGridView.ItemsSource, filteredStickers))
                StickersGridView.ItemsSource = filteredStickers;
        }

        private async Task MessageBox(string title, string message)
        {
            ContentDialog dialog = new()
            {
                Title = title,
                Content = message,
                CloseButtonText = Localizer.Get("Button_OK"),
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
                await MessageBox(
                    Localizer.Get("NoStickersSelected_Title"),
                    Localizer.Get("NoStickersSelected_Export"));
                return;
            }

            FileSavePicker picker = new(AppWindow.Id)
            {
                SuggestedFileName = Localizer.Get("SuggestedFileName_Stickers")
            };
            picker.FileTypeChoices.Add(Localizer.Get("FileType_ZipArchive"), [".zip"]);

            PickFileResult? file = await picker.PickSaveFileAsync();
            string? path = file?.Path;
            if (string.IsNullOrWhiteSpace(path)) return;

            await Task.Run(() =>
            {
                stickerArchiveService.WriteArchive(path, stickers);
            });

            StatusTextRight2.Text = Localizer.FormatCount(
                stickers.Count,
                "Status_ExportedZipOne",
                "Status_ExportedZipMany");
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
            StatusTextRight2.Text = Localizer.Format("Status_CopiedFile", Path.GetFileName(sticker.Path));
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

        private void SettingsButton_Click(object sender, RoutedEventArgs e)
        {
            if (settingsWindow is not null)
            {
                settingsWindow.Activate();
                return;
            }

            settingsWindow = new Settings(appSettings, stickerRepository);
            settingsWindow.StoragePathChanged += Settings_StoragePathChanged;
            settingsWindow.StickersChanged += Settings_StickersChanged;
            settingsWindow.Closed += (_, _) => settingsWindow = null;
            settingsWindow.Activate();
        }

        private async void Settings_StoragePathChanged(object? sender, EventArgs e)
        {
            await ReloadStickerRepositoryAsync();
        }

        private void Settings_StickersChanged(object? sender, EventArgs e)
        {
            RefreshStickerGrid();
        }

        private async Task ReloadStickerRepositoryAsync()
        {
            string requestedBasePath = appSettings.BasePath;
            StickerRepository loadedRepository = await Task.Run(() =>
            {
                StickerRepository repository = new(requestedBasePath);
                repository.LoadMetaFile();
                return repository;
            });

            basePath = requestedBasePath;
            stickerRepository.Stickers.CollectionChanged -= RepositoryStickers_CollectionChanged;
            stickerRepository = loadedRepository;
            stickerRepository.Stickers.CollectionChanged += RepositoryStickers_CollectionChanged;
            RefreshStickers();
        }

        private void RepositoryStickers_CollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
        {
            if (!ReferenceEquals(StickersGridView.ItemsSource, stickerRepository.Stickers))
                RefreshStickerGrid();
        }

        private bool isSelecting = false;
        private void SelectButton_Click(object sender, RoutedEventArgs e)
        {
            isSelecting ^= true;
            SelectButton.Content = Localizer.Get(isSelecting ? "Button_Cancel" : "Button_Select");
            StickersGridView.SelectionMode = isSelecting ? ListViewSelectionMode.Multiple : ListViewSelectionMode.Single;
        }

        private void StickersGridView_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (!isSelecting) return;

            int selectedCount = StickersGridView.SelectedItems.Count;
            ActionButton.IsEnabled = selectedCount > 0;
            StatusTextRight2.Text = Localizer.FormatCount(
                selectedCount,
                "Status_SelectedOne",
                "Status_SelectedMany");
        }

        private async void RemoveSelected_Click(object sender, RoutedEventArgs e)
        {
            RemoveStickers(StickersGridView.SelectedItems.Cast<Sticker>());
        }

        private void SelectedMoveToFront_Click(object sender, RoutedEventArgs e)
        {
            List<Sticker> selected = [.. StickersGridView.SelectedItems.Reverse().Cast<Sticker>()];
            foreach (Sticker sticker in selected)
                stickerRepository.MoveToFront(sticker);
            RefreshStickerGrid();
        }

        private async void RemoveStickers(IEnumerable<Sticker> stickers)
        {
            stickers = [.. stickers];
            ConfirmRemoveContentDialogContent content = new(stickers.Count());
            ContentDialog dialog = new()
            {
                Title = Localizer.Get("RemoveSticker_Title"),
                Content = content,
                PrimaryButtonText = Localizer.Get("Button_Confirm"),
                CloseButtonText = Localizer.Get("Button_Cancel"),
                XamlRoot = Content.XamlRoot
            };

            if (await dialog.ShowAsync() == ContentDialogResult.None) return;
            bool deleteFile = content.DoDeleteFile;

            foreach (Sticker sticker in stickers)
                stickerRepository.Remove(sticker, deleteFile);

            stickerRepository.UpdateMetaFile();
            RefreshStickers();
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
                Localizer.Format("FileType_File", Path.GetExtension(sticker.Path).ToUpperInvariant()),
                [Path.GetExtension(sticker.Path)]);

            PickFileResult? file = await picker.PickSaveFileAsync();
            string? path = file?.Path;
            if (string.IsNullOrWhiteSpace(path)) return;

            File.Copy(sticker.Path, path, true);
            StatusTextRight2.Text = Localizer.Format("Status_ExportedFile", Path.GetFileName(sticker.Path));
        }

        private async void CopySticker_Click(object sender, RoutedEventArgs e)
        {
            if (sender is not MenuFlyoutItem item || item.DataContext is not Sticker sticker)
                return;

            await CopyStickersToClipboard([sticker]);
            StatusTextRight2.Text = Localizer.Format("Status_CopiedFile", Path.GetFileName(sticker.Path));
        }

        private async void EditStickerDescription_Click(object sender, RoutedEventArgs e)
        {
            if (sender is not MenuFlyoutItem item || item.DataContext is not Sticker sticker)
                return;

            TextBox descriptionBox = new()
            {
                Text = sticker.Description,
                PlaceholderText = Localizer.Get("EditDescription_Placeholder"),
                AcceptsReturn = true,
                TextWrapping = TextWrapping.Wrap,
                MinWidth = 320,
                MaxWidth = 520
            };

            ContentDialog dialog = new()
            {
                Title = Localizer.Get("EditDescription_Title"),
                Content = descriptionBox,
                PrimaryButtonText = Localizer.Get("Button_Save"),
                CloseButtonText = Localizer.Get("Button_Cancel"),
                XamlRoot = Content.XamlRoot
            };

            if (await dialog.ShowAsync() != ContentDialogResult.Primary)
                return;

            sticker.Description = descriptionBox.Text.Trim();
            stickerRepository.UpdateMetaFile();
            RefreshStickerGrid();
            StatusTextLeft.Text = sticker.Description;
        }

        private async void EditStickerKeywords_Click(object sender, RoutedEventArgs e)
        {
            if (sender is not MenuFlyoutItem item || item.DataContext is not Sticker sticker)
                return;

            await EditKeywordsAsync([sticker]);
        }

        private async void EditSelectedKeywords_Click(object sender, RoutedEventArgs e)
        {
            List<Sticker> stickers = [.. StickersGridView.SelectedItems.Cast<Sticker>()];
            if (stickers.Count == 0)
            {
                await MessageBox(
                    Localizer.Get("NoStickersSelected_Title"),
                    Localizer.Get("NoStickersSelected_EditKeywords"));
                return;
            }

            await EditKeywordsAsync(stickers);
        }

        private async Task EditKeywordsAsync(IReadOnlyList<Sticker> stickers)
        {
            EditKeywordsContentDialogContent content = new(stickers, stickerRepository.Keywords);

            ContentDialog dialog = new()
            {
                Title = stickers.Count == 1
                    ? Localizer.Get("EditKeywords_Title")
                    : Localizer.Format("EditKeywords_ManyTitle", stickers.Count),
                Content = content,
                PrimaryButtonText = Localizer.Get("Button_Save"),
                CloseButtonText = Localizer.Get("Button_Cancel"),
                XamlRoot = Content.XamlRoot
            };

            if (await dialog.ShowAsync() != ContentDialogResult.Primary)
                return;

            foreach (Sticker sticker in stickers)
            {
                sticker.Keywords.RemoveAll(keyword => content.KeywordsToRemove.Contains(keyword));
                foreach (string keyword in content.KeywordsToAdd)
                {
                    if (!sticker.Keywords.Any(existing =>
                        string.Equals(existing, keyword, StringComparison.OrdinalIgnoreCase)))
                    {
                        sticker.Keywords.Add(keyword);
                    }
                }
            }

            stickerRepository.RegisterKeywords(content.KeywordsToAdd);
            stickerRepository.UpdateMetaFile();
            RefreshStickers();
        }

        private async void CopySelected_Click(object sender, RoutedEventArgs e)
        {
            List<Sticker> stickers = [.. StickersGridView.SelectedItems.Cast<Sticker>()];
            if (stickers.Count == 0)
            {
                await MessageBox(
                    Localizer.Get("NoStickersSelected_Title"),
                    Localizer.Get("NoStickersSelected_Copy"));
                return;
            }

            await CopyStickersToClipboard(stickers);
            StatusTextRight2.Text = Localizer.FormatCount(
                stickers.Count,
                "Status_CopiedOne",
                "Status_CopiedMany");
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
                e.DragUIOverride.Caption = Localizer.Get("Drag_ImportStickers");
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
                    int unsupportedCount = notSupportedFiles.Count();
                    await MessageBox(
                        Localizer.Get("UnsupportedFiles_Title"),
                        Localizer.FormatCount(
                            unsupportedCount,
                            "UnsupportedFiles_One",
                            "UnsupportedFiles_Many"));
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

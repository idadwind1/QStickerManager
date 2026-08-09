using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.Windows.Storage.Pickers;
using QStickerManager.Localization;
using QStickerManager.Settings;
using QStickerManager.Stickers;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Threading.Tasks;
using Windows.Foundation;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace QStickerManager.Windows
{
    /// <summary>
    /// An empty window that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class Settings : Window
    {
        private readonly StickerRepository stickerRepository;
        private readonly AppSettings appSettings;

        public event EventHandler? StoragePathChanged;
        public event EventHandler? StickersChanged;

        public Settings(AppSettings appSettings, StickerRepository stickerRepository)
        {
            this.appSettings = appSettings;
            this.stickerRepository = stickerRepository;
            InitializeComponent();
            Title = Localizer.Get("Settings_Title");
        }

        public string LibraryDirectory => stickerRepository.LibraryDirectory;

        public string VersionText
            => Localizer.Format(
                "Version_Format",
                Assembly.GetExecutingAssembly().GetName().Version?.ToString(3)
                    ?? Localizer.Get("Version_Unknown"));

        private async void ChangeStickerLocation_Click(object sender, RoutedEventArgs e)
        {
            FolderPicker picker = new(AppWindow.Id);
            PickFolderResult? folder = await picker.PickSingleFolderAsync();
            if (folder is null)
                return;

            await ChangeBasePathAsync(folder.Path, Localizer.Get("ChangeLibrary_Title"));
        }

        private async void ResetSettings_Click(object sender, RoutedEventArgs e)
        {
            await ChangeBasePathAsync(
                appSettings.DefaultBasePath,
                Localizer.Get("ResetSettings_Title"));
        }

        private async Task ChangeBasePathAsync(string newBasePath, string title)
        {
            string normalizedBasePath = Path.GetFullPath(newBasePath);
            if (string.Equals(
                normalizedBasePath,
                Path.GetFullPath(LibraryDirectory),
                StringComparison.OrdinalIgnoreCase))
            {
                await ShowMessageAsync(
                    Localizer.Get("SettingsAlreadyReset_Title"),
                    Localizer.Get("SettingsAlreadyReset_Message"));
                return;
            }

            ContentDialog dialog = new()
            {
                Title = title,
                Content = Localizer.Format("MoveLibrary_Message", LibraryDirectory, normalizedBasePath),
                PrimaryButtonText = Localizer.Get("Button_MoveFiles"),
                SecondaryButtonText = Localizer.Get("Button_UseNewFolder"),
                CloseButtonText = Localizer.Get("Button_Cancel"),
                XamlRoot = Content.XamlRoot
            };

            ContentDialogResult result = await dialog.ShowAsync();
            if (result == ContentDialogResult.None)
                return;

            try
            {
                bool overwriteExistingFiles = false;
                if (result == ContentDialogResult.Primary)
                {
                    List<string> conflicts = stickerRepository.GetBasePathMigrationConflicts(normalizedBasePath);
                    if (conflicts.Count > 0)
                    {
                        ContentDialog replaceDialog = new()
                        {
                            Title = Localizer.Get("ReplaceFiles_Title"),
                            Content = Localizer.FormatCount(
                                conflicts.Count,
                                "ReplaceFiles_One",
                                "ReplaceFiles_Many"),
                            PrimaryButtonText = Localizer.Get("Button_Replace"),
                            CloseButtonText = Localizer.Get("Button_Cancel"),
                            XamlRoot = Content.XamlRoot
                        };

                        if (await replaceDialog.ShowAsync() != ContentDialogResult.Primary)
                            return;

                        overwriteExistingFiles = true;
                    }
                }

                stickerRepository.ChangeBasePath(
                    normalizedBasePath,
                    result == ContentDialogResult.Primary,
                    overwriteExistingFiles);
                appSettings.SetBasePath(normalizedBasePath);
                StoragePathChanged?.Invoke(this, EventArgs.Empty);
                BasePathText.Text = LibraryDirectory;
            }
            catch (Exception exception)
            {
                await ShowMessageAsync(Localizer.Get("ChangeLocationFailed_Title"), exception.Message);
            }
        }

        private async Task ShowMessageAsync(string title, string message)
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

        private void OpenStickerFolder_Click(object sender, RoutedEventArgs e)
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = LibraryDirectory,
                UseShellExecute = true
            });
        }

        private async void ClearCache_Click(object sender, RoutedEventArgs e)
        {
            int deletedCount = stickerRepository.ClearGifCache();
            ContentDialog dialog = new()
            {
                Title = Localizer.Get("CacheCleared_Title"),
                Content = Localizer.FormatCount(
                    deletedCount,
                    "CacheCleared_One",
                    "CacheCleared_Many"),
                CloseButtonText = Localizer.Get("Button_OK"),
                XamlRoot = Content.XamlRoot
            };

            await dialog.ShowAsync();
        }

        private async void ShuffleStickers_Click(object sender, RoutedEventArgs e)
        {
            ContentDialog dialog = new()
            {
                Title = Localizer.Get("Shuffle_Title"),
                Content = Localizer.Get("Shuffle_Message"),
                PrimaryButtonText = Localizer.Get("Button_Shuffle"),
                CloseButtonText = Localizer.Get("Button_Cancel"),
                XamlRoot = Content.XamlRoot
            };

            if (await dialog.ShowAsync() != ContentDialogResult.Primary)
                return;

            stickerRepository.Shuffle();
            StickersChanged?.Invoke(this, EventArgs.Empty);
        }
    }
}

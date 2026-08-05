using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.Windows.Storage.Pickers;
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

        public Settings(AppSettings appSettings, StickerRepository stickerRepository)
        {
            this.appSettings = appSettings;
            this.stickerRepository = stickerRepository;
            InitializeComponent();
        }

        public string LibraryDirectory => stickerRepository.LibraryDirectory;

        public string VersionText
            => $"Version {Assembly.GetExecutingAssembly().GetName().Version?.ToString(3) ?? "Unknown"}";

        private async void ChangeStickerLocation_Click(object sender, RoutedEventArgs e)
        {
            FolderPicker picker = new(AppWindow.Id);
            PickFolderResult? folder = await picker.PickSingleFolderAsync();
            if (folder is null)
                return;

            await ChangeBasePathAsync(folder.Path, "Change library location?");
        }

        private async void ResetSettings_Click(object sender, RoutedEventArgs e)
        {
            await ChangeBasePathAsync(
                appSettings.DefaultBasePath,
                "Reset settings?");
        }

        private async Task ChangeBasePathAsync(string newBasePath, string title)
        {
            string normalizedBasePath = Path.GetFullPath(newBasePath);
            if (string.Equals(
                normalizedBasePath,
                Path.GetFullPath(LibraryDirectory),
                StringComparison.OrdinalIgnoreCase))
            {
                await ShowMessageAsync("Settings already reset", "The library is already using the default folder.");
                return;
            }

            ContentDialog dialog = new()
            {
                Title = title,
                Content = $"Move the library from:\n{LibraryDirectory}\n\nto:\n{normalizedBasePath}?",
                PrimaryButtonText = "Move files",
                SecondaryButtonText = "Use new folder",
                CloseButtonText = "Cancel",
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
                            Title = "Replace existing files?",
                            Content = $"{conflicts.Count} file{(conflicts.Count == 1 ? "" : "s")} already exist in the new folder. Replace them?",
                            PrimaryButtonText = "Replace",
                            CloseButtonText = "Cancel",
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
                await ShowMessageAsync("Could not change sticker location", exception.Message);
            }
        }

        private async Task ShowMessageAsync(string title, string message)
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
                Title = "Cache cleared",
                Content = $"{deletedCount} generated GIF file{(deletedCount == 1 ? "" : "s")} deleted.",
                CloseButtonText = "OK",
                XamlRoot = Content.XamlRoot
            };

            await dialog.ShowAsync();
        }
    }
}

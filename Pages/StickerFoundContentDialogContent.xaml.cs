using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media.Imaging;
using QStickerManager.Localization;
using System;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace QStickerManager.Pages
{
    /// <summary>
    /// An empty page that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class StickerFoundContentDialogContent : Page
    {
        public StickerFoundContentDialogContent(string stickerPath, string hash)
        {
            InitializeComponent();
            Sticker.Source = new BitmapImage(new Uri(stickerPath));
            Hash.Text = Localizer.Format("StickerHash_Format", hash);
        }
    }
}

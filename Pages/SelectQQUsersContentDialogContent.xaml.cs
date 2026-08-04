using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace QStickerManager.Pages
{
    /// <summary>
    /// An empty page that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class SelectQQUsersContentDialogContent : Page
    {
        public SelectQQUsersContentDialogContent(string[] users)
        {
            InitializeComponent();
            foreach (var user in users)
            {
                Options.Add(user, false);
            }
        }

        public Dictionary<string, bool> Options = new();

        public string[] Selections
        {
            get => Options.Where(pair => pair.Value).Select(pair => pair.Key).ToArray();
        }

        private void CheckBox_Checked(object sender, RoutedEventArgs e)
        {
            if (sender is not CheckBox checkbox || checkbox.DataContext is not string option)
                return;
            Options[option] = checkbox.IsChecked ?? false;
        }
    }
}

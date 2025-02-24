using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml;

namespace Yukari;

public sealed partial class InfoDialog : Page
{
    public InfoDialog()
    {
        InitializeComponent();
    }

    public static async Task Show(MainWindow mainWindow, string message, string title = "")
    {
        ContentDialog dialog = new ContentDialog();
        dialog.XamlRoot = mainWindow.Content.XamlRoot;
        dialog.Style = Application.Current.Resources["DefaultContentDialogStyle"] as Style;
        dialog.Title = title;
        dialog.PrimaryButtonText = "OK";
        dialog.DefaultButton = ContentDialogButton.Primary;
        dialog.Content = new ScrollViewer(){
            Content = new TextBlock(){
                Text = message
            }
        };
        await dialog.ShowAsync();
    }
}
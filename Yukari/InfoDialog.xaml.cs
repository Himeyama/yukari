using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml;
using System.Threading.Tasks;
using System;

namespace Yukari;

public sealed partial class InfoDialog : Page
{
    public InfoDialog()
    {
        InitializeComponent();
    }

    public static async Task Show(MainWindow mainWindow, string message, string title = "")
    {
        ContentDialog dialog = new()
        {
            XamlRoot = mainWindow.Content.XamlRoot,
            Style = Application.Current.Resources["DefaultContentDialogStyle"] as Style,
            Title = title,
            PrimaryButtonText = "OK",
            DefaultButton = ContentDialogButton.Primary,
            Content = new ScrollViewer()
            {
                Content = new TextBlock()
                {
                    Text = message
                }
            }
        };
        _ = await dialog.ShowAsync();
    }
}
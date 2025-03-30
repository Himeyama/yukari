using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml;

namespace Yukari;

public sealed partial class ErrorDialog : Page
{
    public ErrorDialog()
    {
        InitializeComponent();
    }

    public static async void Show(MainWindow mainWindow, string message)
    {
        // ダイアログで JSON を表示
        ContentDialog dialog = new()
        {
            Title = new ErrorDialog(),
            Content = new ScrollViewer
            {
                Content = new TextBlock
                {
                    Text = message,
                    TextWrapping = TextWrapping.Wrap
                }
            },
            XamlRoot = mainWindow.Content.XamlRoot,
            DefaultButton = ContentDialogButton.Primary,
            PrimaryButtonText = "OK"
        };

        await dialog.ShowAsync();
    }
}
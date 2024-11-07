using Microsoft.UI.Xaml.Controls;
using Microsoft.Web.WebView2.Core;

namespace Yukari;
public sealed partial class Client : Page
{
    MainWindow mainWindow = null;
    public TabViewItem tabViewItem = null;

    public Client(int? apiEnginePort, MainWindow mainWindow){
        InitializeComponent();

        if(apiEnginePort != null){
            WebUI.Source = new Uri($"http://127.0.0.1:{apiEnginePort}/index.html");

            this.mainWindow = mainWindow;

            try{
                WebUI.WebMessageReceived += CoreWebView2_WebMessageReceived;
            }catch(Exception ex){
                mainWindow?.ShowErrorDialog(ex.Message);
            }
        }
    }

    void CoreWebView2_WebMessageReceived(object sender, CoreWebView2WebMessageReceivedEventArgs e)
    {
        // JavaScript から送信されたメッセージを処理
        string message = e.TryGetWebMessageAsString();
        
        // mainWindow?.ShowErrorDialog(message);
        tabViewItem.Header = message;
    }
}
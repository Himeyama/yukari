using Microsoft.UI.Xaml.Controls;
using Microsoft.Web.WebView2.Core;

namespace Yukari;
public sealed partial class Client : Page
{
    MainWindow mainWindow = null;
    public TabViewItem tabViewItem = null;

    public Client(MainWindow mainWindow){
        InitializeComponent();


        // 環境変数からユーザーディレクトリを取得
        string userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);

        // ファイルパスを構築
        string webui = Path.Combine(userProfile, "AppData", "Local", "Yukari", "yukari-ui", "index.html");

        WebUI.Source = new Uri(webui);

        this.mainWindow = mainWindow;

        try{
            WebUI.WebMessageReceived += CoreWebView2_WebMessageReceived;
        }catch(Exception ex){
            mainWindow?.ShowErrorDialog(ex.Message);
        }
    }

    async void initWebView(){
        CoreWebView2Environment environment = await CoreWebView2Environment.CreateAsync();

        await WebUI.EnsureCoreWebView2Async(environment);
    }

    void CoreWebView2_WebMessageReceived(object sender, CoreWebView2WebMessageReceivedEventArgs e)
    {
        // JavaScript から送信されたメッセージを処理
        string message = e.TryGetWebMessageAsString();
        
        // mainWindow?.ShowErrorDialog(message);
        tabViewItem.Header = message;
    }
}
using System.Text.Json;
using Microsoft.UI.Xaml.Controls;
using Microsoft.Web.WebView2.Core;
using System.Text.Json.Serialization;
using System.Text;


namespace Yukari;


public class IPCData {
    [JsonPropertyName("type")]
    public string Type { get; set; }

    [JsonPropertyName("data")]
    public string Data { get; set; }
}

public class Base64Decoder
{
    public static string DecodeBase64UTF8(string encoded)
    {
        // Base64デコード
        byte[] bytes = Convert.FromBase64String(encoded);

        // UTF-8文字列にデコード
        string decodedString = Encoding.UTF8.GetString(bytes);

        return decodedString;
    }
}

public sealed partial class Client : Page
{
    MainWindow mainWindow = null;
    public TabViewItem tabViewItem = null;

    List<HistoryItem> historyItems = [];

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

    string GetLanguageModel()
    {
        LanguageModelItem languageModelItem;
        try
        {
            languageModelItem = mainWindow.SelectLanguageModel.SelectedItem as LanguageModelItem;
        }
        catch (Exception)
        {
            return "unset";
        }
        return languageModelItem.Name;
    }

    async void initWebView(){
        CoreWebView2Environment environment = await CoreWebView2Environment.CreateAsync();

        await WebUI.EnsureCoreWebView2Async(environment);
    }

    async void CoreWebView2_WebMessageReceived(object sender, CoreWebView2WebMessageReceivedEventArgs e)
    {
        // JavaScript から送信されたメッセージを処理
        string message = e.TryGetWebMessageAsString();
        IPCData iPCData = JsonSerializer.Deserialize<IPCData>(message);
        if (iPCData.Type == "tab-title")
        {
            tabViewItem.Header = Base64Decoder.DecodeBase64UTF8(iPCData.Data);
        }
        else if (iPCData.Type == "get" && iPCData.Data == "apiKey")
        {
            string apiKey = MainWindow.GetApiKey();
            if (apiKey == "")
                apiKey = "unset";
            _ = await WebUI.ExecuteScriptAsync($"window.setAPIKey(\"{apiKey}\")");
        }
        else if (iPCData.Type == "get" && iPCData.Data == "languageModel")
        {
            string model = GetLanguageModel();
            _ = await WebUI.ExecuteScriptAsync($"window.setLanguageModel(\"{model}\")");
        }
        else if (iPCData.Type == "history")
        {
            HistoryItem history = JsonSerializer.Deserialize<HistoryItem>(Base64Decoder.DecodeBase64UTF8(iPCData.Data));
            history.HeadUser = GetFirstLine(history.User);
            history.HeadAssistant = GetFirstLine(history.Assistant);
            historyItems.Add(history);
            mainWindow.ChatItems.Items.Insert(0, history);
        }
    }

    public void ApplyHistory()
    {
        mainWindow.ChatItems.Items.Clear();
        foreach (HistoryItem history in historyItems){
            mainWindow.ChatItems.Items.Insert(0, history);
        }
    }

    static string GetFirstLine(string text)
    {
        // 改行コードで分割し、最初の行を取得し、トリミング
        return text.Split(['\n', '\r'], StringSplitOptions.RemoveEmptyEntries)[0].Trim();
    }
}
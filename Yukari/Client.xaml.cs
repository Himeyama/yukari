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
    // UTF-8文字列をBase64エンコード
    public static string EncodeToBase64UTF8(string plainText)
    {
        // UTF-8文字列をバイト配列に変換
        byte[] bytes = Encoding.UTF8.GetBytes(plainText);

        // Base64エンコード
        string encodedString = Convert.ToBase64String(bytes);

        return encodedString;
    }

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

    public List<HistoryItem> historyItems = [];
    public int activeIdx = -1;

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
        string displayName = mainWindow.LanguageModel.Text;
        // LanguageModelItem languageModelItem = null;
        // foreach (KeyValuePair<string, string> model in mainWindow.languageModels){
        //     if(displayName == model.Value){
        //         languageModelItem = new(){
        //             Name = model.Key,
        //             DisplayName = model.Value
        //         };
        //         break;
        //     }
        // }
        // return languageModelItem.Name;
        return displayName;
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
        else if (iPCData.Type == "get" && iPCData.Data == "openAIApiKey")
        {
            string apiKey = MainWindow.GetOpenAIApiKey();
            if (apiKey == "")
                apiKey = "unset";
            _ = await WebUI.ExecuteScriptAsync($"window.setOpenAIAPIKey(\"{apiKey}\")");
        }
        else if (iPCData.Type == "get" && iPCData.Data == "grokApiKey")
        {
            string apiKey = MainWindow.GetGrokApiKey();
            if (apiKey == "")
                apiKey = "unset";
            _ = await WebUI.ExecuteScriptAsync($"window.setGrokAPIKey(\"{apiKey}\")");
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
            mainWindow.Save();
        }else if(iPCData.Type == "get-assistant"){
            string assistantEncoded = iPCData.Data;
            string assistant = Base64Decoder.DecodeBase64UTF8(assistantEncoded);

            /* VOICEVOX の処理 */
            // 分割文字を設定
            char[] separators = ['？', '！', '、', '。', '\n'];
            // 文字列を分割
            List<string> assistants = new(assistant.Split(separators, StringSplitOptions.RemoveEmptyEntries));
            foreach (string assis in assistants)
            {
                // 音声合成
                await VOICEVOX.GenerateVoice(assis);
                string id = $"{await VOICEVOX.GetSpeakerId() ?? 1}";
                string sha256Hash = VOICEVOX.ComputeSha256Hash(assis);
                string wavPath = Path.Combine(Path.GetTempPath(), $"{sha256Hash}-{id}.wav");
                await VOICEVOX.PlayWavAsync(wavPath);   
            }
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

    public async void Print(HistoryItem historyItem)
    {
        string userBase64 = Base64Decoder.EncodeToBase64UTF8(historyItem.User);
        string assistantBase64 = Base64Decoder.EncodeToBase64UTF8(historyItem.Assistant);
        _ = await WebUI.ExecuteScriptAsync($"window.setEditorBase64(\"{userBase64}\")");
        _ = await WebUI.ExecuteScriptAsync($"window.setOutputBase64(\"{assistantBase64}\")");
    }

    public async void VoicevoxReading()
    {
        await WebUI.ExecuteScriptAsync("window.getAssistant()");
    }
}
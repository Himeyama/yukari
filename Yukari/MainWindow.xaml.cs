#nullable enable
using System.Diagnostics;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using System.Text.Unicode;
using Microsoft.UI;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.Win32;
using OpenAI;
using System.ClientModel;
using OpenAI.Models;
using Microsoft.UI.Text;
using OpenAI.Chat;
using Microsoft.Web.WebView2.Core;
using System.Collections.Generic;
using System.Threading.Tasks;
using System;
using System.IO;
using System.Linq;

namespace Yukari;

public class HistoryItem
{
    [JsonPropertyName("user")]
    public string? User { get; set; }
    [JsonPropertyName("assistant")]
    public string? Assistant { get; set; }
    [JsonPropertyName("headuser")]
    public string? HeadUser { get; set; }
    [JsonPropertyName("headassistant")]
    public string? HeadAssistant { get; set; }

    [JsonPropertyName("uuid")]
    public string? Uuid { get; set; }
}

public class Record
{
    public List<HistoryItem>? Histories { get; set; }
    public int ActiveIdx { get; set; }
    public string? Header { get; set; }
}

public class ChatMessage
{
    [JsonPropertyName("role")]
    public string Role { get; set; } = string.Empty;

    [JsonPropertyName("content")]
    public string Content { get; set; } = string.Empty;
}

public class ChatRequest
{
    [JsonPropertyName("model")]
    public string Model { get; set; } = string.Empty;

    [JsonPropertyName("messages")]
    public List<ChatMessage> Messages { get; set; } = new();

    [JsonPropertyName("stream")]
    public bool Stream { get; set; } = false;
}

public sealed partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
        Title = "Yukari";

        // クロージングの処理
        AppWindow thisAppWin = GetCurrentAppWin();
        if (thisAppWin != null)
            thisAppWin.Closing += OnWindowClosing;

        ExtendsContentIntoTitleBar = true;
        SetTitleBar(AppTitleBar);

        string model = GetModel();
        if (model == string.Empty)
        {
            model = "GPT-4o mini";
        }
        SetModel(model);

        InitVoicevox();

        // Set the Editor's Uri to Assets\mini-editor\index.html
        InitPreviewer();
    }

    public async void InitPreviewer()
    {
        string exePath = System.Reflection.Assembly.GetExecutingAssembly().Location;
        string installDir = Path.GetDirectoryName(exePath) ?? "";

        string editorPath = Path.Combine(Directory.GetCurrentDirectory(), "Yukari", "Assets", "mini-editor", "index.html");
        string previewPath = Path.Combine(Directory.GetCurrentDirectory(), "Yukari", "Assets", "mini-editor", "markdown-preview.html");

        if (!File.Exists(editorPath) || !File.Exists(previewPath))
        {
            editorPath = Path.Combine(installDir, "Assets", "mini-editor", "index.html");
            previewPath = Path.Combine(installDir, "Assets", "mini-editor", "markdown-preview.html");
            if (!File.Exists(editorPath) || !File.Exists(previewPath))
            {
                AddMessage($"Editor or Preview HTML file not found. Please check the file paths: {installDir}");
                return;
            }
        }


        try
        {
            Editor.Source = new Uri(editorPath);
            Preview.Source = new Uri(previewPath);
            await Preview.EnsureCoreWebView2Async(null);
            await Editor.EnsureCoreWebView2Async(null);
            Editor.WebMessageReceived += Editor_WebMessageReceived;
        }
        catch (Exception ex)
        {
            ShowErrorDialog($"Failed to initialize WebView2: {ex.Message}");
        }
    }

    async void PrintMarkdownPreview(string? message)
    {
        if (message == null) return;

        while (Preview.CoreWebView2 == null)
        {
            await Task.Delay(100); // 0.1秒待機
        }

        // JSON形式でメッセージを送信
        var messageObject = new
        {
            message = message
        };
        string json = JsonSerializer.Serialize(messageObject);
        Preview.CoreWebView2.PostWebMessageAsJson(json);
    }

    // <summary>
    // エディタにメッセージを送信します。
    void Send(object sender, RoutedEventArgs e)
    {
        if (Editor.CoreWebView2 != null)
        {
            // エディタは内容を取得して、生成 AI 質問モードでアプリ側に文章を返す
            Editor.CoreWebView2.PostWebMessageAsJson("{\"send\": true}");
        }
    }

    // <summary>
    // エディタにメッセージを送信します。
    /// </summary>
    /// <param name="message">送信するメッセージ</param>
    void WriteEditor(string? message)
    {
        if (message == null) return;
        if (Editor.CoreWebView2 != null)
        {
            // JSON形式でメッセージを送信
            EditorMessage messageObject = new()
            {
                Send = false,
                Message = message
            };
            string json = JsonSerializer.Serialize(messageObject);
            Editor.CoreWebView2.PostWebMessageAsJson(json);
        }
    }

    // <summary>
    // エディタから情報を受け取る
    /// </summary> 
    void Editor_WebMessageReceived(object sender, CoreWebView2WebMessageReceivedEventArgs args)
    {
        // WebView2からのメッセージを受信
        string message = args.TryGetWebMessageAsString();
        if (message != null)
        {
            SendChatMessage(message);
        }
    }

    async void SendChatMessage(string userMessage)
    {
        // Ensure userMessage is treated as UTF-8
        byte[] utf8Bytes = Encoding.UTF8.GetBytes(userMessage);
        userMessage = Encoding.UTF8.GetString(utf8Bytes);

        AddMessage(userMessage);

        string apiKey = GetOpenAIApiKey();
        if (string.IsNullOrEmpty(apiKey))
        {
            ShowErrorDialog("API キーが設定されていません");
            return;
        }

        // ChatClientのインスタンス作成
        string model = GetModel();
        ChatClient chatClient;

        Client? activeClient = GetClient();
        chatClient = new ChatClient(model: model, credential: new ApiKeyCredential(GetApiKey()), new OpenAIClientOptions()
        {
            Endpoint = GetEndpoint()
        });

        // チャット履歴を保持
        List<OpenAI.Chat.ChatMessage> messages = [];

        if (activeClient?.historyItems != null)
        {
            foreach (HistoryItem? item in activeClient.historyItems)
            {
                messages.Add(new UserChatMessage(item.User));
                messages.Add(new AssistantChatMessage(item.Assistant));
            }
        }
        messages.Add(new UserChatMessage(userMessage));

        string responseText = string.Empty;
        try
        {
            // メッセージを送信してレスポンスを取得
            AsyncCollectionResult<StreamingChatCompletionUpdate> responseStream = chatClient.CompleteChatStreamingAsync(messages);

            // ストリームレスポンスの読み取り
            await foreach (StreamingChatCompletionUpdate completionUpdate in responseStream)
            {
                if (completionUpdate.ContentUpdate.Count > 0)
                {
                    responseText += completionUpdate.ContentUpdate[0].Text;
                    PrintMarkdownPreview(responseText);
                }
            }
        }
        catch (Exception ex)
        {
            // エラー時の詳細情報を表示
            string errorDetails = $"エラーが発生しました: {ex.Message}\n" +
                                $"スタックトレース: {ex.StackTrace}";
            if (ex.InnerException != null)
            {
                errorDetails += $"\n内部例外: {ex.InnerException.Message}";
            }

            ShowErrorDialog(errorDetails);
            return;
        }

        AddHistory(activeClient, userMessage, responseText);
    }

    void Debug(object message)
    {
        // メッセージを JSON に変換
        string messagesJson = JsonSerializer.Serialize(message, new JsonSerializerOptions
        {
            WriteIndented = true,
            Encoder = JavaScriptEncoder.Create(UnicodeRanges.All)
        });

        // ダイアログで JSON を表示
        ContentDialog dialog = new()
        {
            Title = "Debug",
            Content = new ScrollViewer
            {
                Content = new TextBlock
                {
                    Text = messagesJson,
                    TextWrapping = TextWrapping.Wrap
                }
            },
            CloseButtonText = "Close",
            XamlRoot = Content.XamlRoot
        };
        _ = dialog.ShowAsync();
    }


    // <summary>
    // チャット履歴を追加します。
    // </summary>
    /// <param name="client">クライアントインスタンス</param>
    /// <param name="userMessage">ユーザーメッセージ</param>
    /// <param name="assistantMessage">アシスタントメッセージ</param>
    /// <remarks>
    /// <param name="client"></param>
    /// <param name="userMessage"></param>
    /// <param name="assistantMessage"></param>//  
    void AddHistory(Client? client, string userMessage, string assistantMessage)
    {
        if (client == null)
            return;
        HistoryItem historyItem = new()
        {
            User = userMessage,
            Assistant = assistantMessage,
            HeadUser = GetFirstLine(userMessage),
            HeadAssistant = GetFirstLine(assistantMessage),
            Uuid = Guid.NewGuid().ToString()
        };
        client.historyItems.Add(historyItem);
        ChatItems.Items.Insert(0, historyItem);
        ChatItems.SelectedIndex = 0;
        client.activeIdx = 0;
    }

    string GetFirstLine(string userMessage)
    {
        // 改行コードで分割し、最初の行を取得し、トリミング
        return userMessage.Split(['\n', '\r'], StringSplitOptions.RemoveEmptyEntries)[0].Trim();
    }

    Client? GetClient()
    {
        TabViewItem? tabViewItem = Tabs.SelectedItem as TabViewItem;
        if (tabViewItem == null)
            return null;
        if (tabViewItem.Content is Client client)
        {
            return client;
        }
        return null;
    }

    /// <summary>
    /// 
    /// </summary>
    /// <returns></returns>
    public async void InitVoicevox()
    {
        VOICEVOX.mainWindow = this;
        string? voicevoxVersion = await VOICEVOX.GetVersion();
        VoicevoxButton.Visibility = Visibility.Visible;
        if (voicevoxVersion == null)
        {
            VoicevoxButtonText.Text = "VOICEVOX を起動する";
            return;
        }
        VoicevoxButtonText.Text = "VOICEVOX " + voicevoxVersion;
        VoicevoxReadingButton.Visibility = Visibility.Visible;

        string speaker = VOICEVOX.GetSpeaker();
        string style = VOICEVOX.GetStyle();
        if (speaker != string.Empty && style != string.Empty)
        {
            VoicevoxButtonText.Text = $"VOICEVOX: {speaker} ({style})";
        }
    }

    async void SelectVoicevox(object sender, RoutedEventArgs e)
    {
        string pattern1 = @"^VOICEVOX \d+\.";
        string pattern2 = @"^VOICEVOX: ";
        string version = VoicevoxButtonText.Text;
        if (Regex.IsMatch(version, pattern1) || Regex.IsMatch(version, pattern2))
        {
            string? ver = await VOICEVOX.GetVersion();
            if (ver == null)
            {
                VoicevoxButtonText.Text = "VOICEVOX を起動する";
                AddMessage("VOICEVOX が起動していません");
                return;
            }
            _ = await VOICEVOX.SelectSpeaker();
        }
        else
        {
            // 起動
            VOICEVOX.LaunchVoicevox();
        }
    }

    // <summary>
    // 現在のアプリケーション ウィンドウを取得します。
    // </summary>
    AppWindow GetCurrentAppWin()
    {
        IntPtr hwnd = WinRT.Interop.WindowNative.GetWindowHandle(this);
        WindowId winId = Win32Interop.GetWindowIdFromWindow(hwnd);
        return AppWindow.GetFromWindowId(winId);
    }

    // <summary>
    // ウィンドウが閉じられる際の処理を行います。
    // </summary>
    void OnWindowClosing(AppWindow sender, AppWindowClosingEventArgs e)
    {
    }

    // <summary>
    // ステータスバーにメッセージを設定
    // 基本的に AddMessage() を使用
    // </summary>
    void SetStatusBar(string message)
    {
        if (DispatcherQueue.HasThreadAccess)
        {
            StatusBar.Text = message;
        }
        else
        {
            DispatcherQueue.TryEnqueue(DispatcherQueuePriority.Normal, () =>
            {
                StatusBar.Text = message;
            });
        }
    }

    int messageIndex = 0;

    // <summary>
    // 指定されたメッセージをステータスバーに追加し、一定時間後に消去します。
    // </summary>
    public void AddMessage(string message, int delay = 3000)
    {
        Task.Run(async () =>
        {
            int messageIdx = messageIndex;
            SetStatusBar(message);
            await Task.Delay(delay);
            if (messageIdx == messageIndex)
            {
                SetStatusBar("");
            }
            messageIndex++;
        });
    }

    // <summary>
    // "About" ボタンがクリックされたときに、指定された URL を開きます。
    // </summary>
    void ClickAbout(object sender, RoutedEventArgs e)
    {
        string url = "https://github.com/himeyama/yukari"; // 開きたいURLを指定
        Process.Start(
            new ProcessStartInfo { FileName = url, UseShellExecute = true }
        );
    }

    // <summary>
    // TabView を読み込んだときに呼び出され、API エンジンのポートを探します。
    // </summary>
    void TabView_Loaded(object sender, RoutedEventArgs e)
    {
        Load();
        TabView? tabView = sender as TabView;
        if (tabView == null)
            return;
        if (tabView.TabItems.Count == 0)
        {
            TabViewItem tabViewItem = CreateNewTab();
            tabView?.TabItems.Add(tabViewItem);
            if (tabView != null)
                tabView.SelectedItem = tabViewItem;
        }
    }

    // <summary>
    // TabView に新しいタブを追加します。
    // </summary>
    void TabView_AddButtonClick(object sender, object args)
    {
        if (sender is TabView tabView)
        {
            TabViewItem tabViewItem = CreateNewTab();
            tabView.TabItems.Add(tabViewItem);
            tabView.SelectedItem = tabViewItem;
            WriteEditor("");
            PrintMarkdownPreview("");
        }
    }

    /// <summary>
    /// タブが閉じられるリクエストを処理します。
    /// </summary>
    void TabView_TabCloseRequested(object sender, TabViewTabCloseRequestedEventArgs e)
    {
        if (sender is TabView tabView)
        {
            TabViewItem tab = e.Tab;
            int tabIndex = GetTabIndex(tabView, tab);
            if (tabIndex < 0)
            {
                AddMessage("タブが見つかりませんでした。");
                return;
            }
            HandleTabClose(tabView, tab, tabIndex);
        }
    }

    int GetTabIndex(TabView tabView, TabViewItem tab)
    {
        for (int i = 0; i < tabView.TabItems.Count; i++)
            if (tabView.TabItems[i] as TabViewItem == tab)
                return i;
        return -1; // タブが見つからない場合のインデックス
    }

    void HandleTabClose(TabView tabView, TabViewItem tab, int tabIndex)
    {
        if (IsLastTab(tabView, tabIndex))
        {
            SaveReset();
            Close();
            return;
        }
        UpdateSelectedIndexIfLast(tabView, tabIndex);
        RemoveTab(tabView, tab);
        Save();
    }

    bool IsLastTab(TabView tabView, int tabIndex)
    {
        return tabIndex == 0 && tabView.TabItems.Count == 1;
    }

    void UpdateSelectedIndexIfLast(TabView tabView, int tabIndex)
    {
        if (tabIndex == tabView.TabItems.Count - 1)
            tabView.SelectedIndex = tabView.TabItems.Count - 2;
    }

    void RemoveTab(TabView tabView, TabViewItem tab)
    {
        try
        {
            tabView.TabItems.Remove(tab);
        }
        catch (Exception ex)
        {
            AddMessage(ex.Message);
        }
    }
    /* タブを閉じる処理: ここまで */

    // <summary>
    // 新しいタブを作成します。
    // </summary>
    TabViewItem CreateNewTab()
    {
        ChatItems.Items.Clear();
        Client client = new(this);
        TabViewItem newItem = new()
        {
            Header = NewTab.Text,
            IconSource = new SymbolIconSource()
            {
                Symbol = Symbol.Contact
            },
            Content = client
        };
        client.tabViewItem = newItem;
        return newItem;
    }

    // <summary>
    // API キーを設定します。
    // </summary>
    static void SetUpOpenAIAPIKey(string apiKey)
    {
        // apiKeyの値と保存先のパスを指定
        string subKeyPath = @"SOFTWARE\Yukari";

        // レジストリに書き込む
        using RegistryKey key = Registry.CurrentUser.CreateSubKey(subKeyPath);
        key?.SetValue("OPENAI_API_KEY", apiKey, RegistryValueKind.String);
    }

    // <summary>
    // API キーを設定します。
    // </summary>
    static void SetUpGrokAPIKey(string apiKey)
    {
        // apiKeyの値と保存先のパスを指定
        string subKeyPath = @"SOFTWARE\Yukari";

        // レジストリに書き込む
        using RegistryKey key = Registry.CurrentUser.CreateSubKey(subKeyPath);
        key?.SetValue("GROK_API_KEY", apiKey, RegistryValueKind.String);
    }

    public static string GetApiKey()
    {
        string model = GetModel();
        if (model.StartsWith("grok", StringComparison.OrdinalIgnoreCase))
            return GetGrokApiKey();
        return GetOpenAIApiKey();        
    }

    // <summary>
    // API キーを非同期に取得します。
    // </summary>
    public static string GetOpenAIApiKey()
    {
        // レジストリのパスを指定
        string subKeyPath = @"SOFTWARE\Yukari";

        // レジストリから値を読み込む
        using RegistryKey? key = Registry.CurrentUser.OpenSubKey(subKeyPath);
        return key?.GetValue("OPENAI_API_KEY") as string ?? string.Empty;
    }

    // <summary>
    // API キーを非同期に取得します。
    // </summary>
    public static string GetGrokApiKey()
    {
        // レジストリのパスを指定
        string subKeyPath = @"SOFTWARE\Yukari";

        // レジストリから値を読み込む
        using RegistryKey? key = Registry.CurrentUser.OpenSubKey(subKeyPath);
        return key?.GetValue("GROK_API_KEY") as string ?? string.Empty;
    }

    // <summary>
    // API キー設定用のダイアログを表示し、入力された API キーを設定します。
    // </summary>
    async void ClickSetOpenAIAPIKey(object sender, RoutedEventArgs e)
    {
        string apiKey = GetOpenAIApiKey();

        PasswordBox passwordBox = new()
        {
            Password = apiKey
        };

        ContentDialog dialog = new()
        {
            XamlRoot = Content.XamlRoot,
            Style = Application.Current.Resources["DefaultContentDialogStyle"] as Style,
            PrimaryButtonText = "OK",
            SecondaryButtonText = Cancel.Text,
            Title = SetOpenAIAPIKey.Text,
            DefaultButton = ContentDialogButton.Primary,
            Content = passwordBox
        };
        ContentDialogResult result = await dialog.ShowAsync();

        if (result == ContentDialogResult.Primary)
        {
            apiKey = passwordBox.Password;
            SetUpOpenAIAPIKey(apiKey);
        }
        else if (result == ContentDialogResult.Secondary)
        {
            // 何もしない
        }
    }

    // <summary>
    // API キー設定用のダイアログを表示し、入力された API キーを設定します。
    // </summary>
    async void ClickSetGrokAPIKey(object sender, RoutedEventArgs e)
    {
        string apiKey = GetGrokApiKey();

        PasswordBox passwordBox = new()
        {
            Password = apiKey
        };

        ContentDialog dialog = new()
        {
            XamlRoot = Content.XamlRoot,
            Style = Application.Current.Resources["DefaultContentDialogStyle"] as Style,
            PrimaryButtonText = "OK",
            SecondaryButtonText = Cancel.Text,
            Title = SetGrokAPIKey.Text,
            DefaultButton = ContentDialogButton.Primary,
            Content = passwordBox
        };
        ContentDialogResult result = await dialog.ShowAsync();

        if (result == ContentDialogResult.Primary)
        {
            apiKey = passwordBox.Password;
            SetUpGrokAPIKey(apiKey);
        }
        else if (result == ContentDialogResult.Secondary)
        {
            // 何もしない
        }
    }

    // <summary>
    // タブの選択が変更されたときの処理
    // </summary>
    /// <param name="sender">タブビュー</param>
    /// <param name="e">選択変更イベント</param>
    void Tabs_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (sender is not TabView tabs || tabs.SelectedItem is not TabViewItem tabViewItem)
        {
            return;
        }

        if (tabViewItem.Content is Client client)
        {
            client.ApplyHistory();
            ChatItems.SelectedIndex = client.activeIdx;
            if (ChatItems.SelectedItem == null)
            {
                WriteEditor("");
                PrintMarkdownPreview("");
            }
            Grid.SetRow(tabs, 0);
            EditorPreview.Visibility = Visibility.Visible;
            SidePanel.Visibility = Visibility.Visible;
        }
        else if (tabViewItem.Content is Automate automate)
        {
            Grid.SetRow(Tabs, 1);
            EditorPreview.Visibility = Visibility.Collapsed;
            SidePanel.Visibility = Visibility.Collapsed;
        }
    }

    // <summary>
    // 履歴をクリックしたときの処理
    // </summary>
    /// <param name="sender">クリックされた ListView</param>
    void ClickChatItems(object sender)
    {
        if (sender is ListView chatItems)
        {
            // 選択されたアイテムを取得
            if (chatItems.SelectedItem is HistoryItem historyItem)
            {
                TabViewItem? tabViewItem = Tabs.SelectedItem as TabViewItem;
                if (tabViewItem == null)
                    return;
                tabViewItem.Header = historyItem.HeadUser;
                if (tabViewItem.Content is Client client)
                {
                    client.activeIdx = chatItems.SelectedIndex;
                    WriteEditor(historyItem.User);
                    PrintMarkdownPreview(historyItem.Assistant);
                    Save();
                }
            }
        }
    }

    void ChatItems_Clicked(object sender, RoutedEventArgs e)
    {
        ClickChatItems(sender);
    }

    // 履歴をクリック
    void ChatItems_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        ClickChatItems(sender);
    }

    // <summary>
    // 指定されたメッセージを含むエラーダイアログを表示します。
    // </summary>
    public void ShowErrorDialog(string message)
    {
        _ = ErrorDialog.Show(this, message);
    }

    // <summary>
    // 指定されたメッセージを含む情報ダイアログを表示します。
    // </summary>
    public async Task ShowInfoDialog(string message)
    {
        await InfoDialog.Show(this, message);
    }

    // <summary>
    // アプリケーションを終了します。
    // </summary>
    void ClickExit(object sender, RoutedEventArgs e)
    {
        Close();
    }

    static string[] FilterModelsByRegex(string[] regexes, string[] models)
    {
        // 一致した要素を削除した結果を格納するリスト
        List<string> filteredModels = new();

        // 各モデルに対して正規表現の一致チェック
        foreach (string model in models)
        {
            bool matches = false;

            foreach (string ragex in regexes)
            {
                if (Regex.IsMatch(model, ragex))
                {
                    matches = true;
                    break; // 一致したらループを抜ける
                }
            }

            if (!matches)
            {
                filteredModels.Add(model); // 一致しなければ追加
            }
        }

        return filteredModels.ToArray(); // リストを配列に変換して返す
    }

    async Task<string[]> GetOpenAIModelsAsync()
    {
        string apiKey = GetOpenAIApiKey();
        if (apiKey == null)
        {
            ShowErrorDialog("API キーが設定されていません");
            return [];
        }
        OpenAIClient openAIClient = new(apiKey);

        // モデル一覧を取得
        OpenAIModelClient modelClient = openAIClient.GetOpenAIModelClient();
        ClientResult<OpenAIModelCollection> models = await modelClient.GetModelsAsync();

        List<string> allModel = [];

        // モデル一覧を表示
        foreach (OpenAIModel? model in models.Value)
        {
            allModel.Add(model.Id);
        }

        string[] regexes = ["^dall", ".+audio.+?", "^whisper", "^tts", "^text-embedding", "^babbage", "^davinci", "^omni-moderation", ".*realtime-preview", ".*instruct", "\\d{2}$"];
        string[] allModelFiltered = FilterModelsByRegex(regexes, [.. allModel]);
        Array.Sort(allModelFiltered);

        return allModelFiltered;
    }

    async Task<string[]> GetGrokModelsAsync()
    {
        string apiKey = GetGrokApiKey();
        if (apiKey == null)
        {
            ShowErrorDialog("API キーが設定されていません");
            return [];
        }
        OpenAIClientOptions options = new()
        {
            Endpoint = new Uri("https://api.x.ai/v1")
        };
        OpenAIClient openAIClient = new(new ApiKeyCredential(apiKey), options);

        // モデル一覧を取得
        OpenAIModelClient modelClient = openAIClient.GetOpenAIModelClient();
        ClientResult<OpenAIModelCollection>? models = null;
        try
        {
            models = await modelClient.GetModelsAsync();
        }
        catch (Exception ex)
        {
            ShowErrorDialog(ex.Message);
        }

        List<string> allModel = [];

        // モデル一覧を表示
        if (models != null)
        {
            foreach (OpenAIModel? model in models.Value)
            {
                allModel.Add(model.Id);
            }
        }

        // string[] regexes = ["^dall", ".+audio.+?", "^whisper", "^tts", "^text-embedding", "^babbage", "^davinci", "^omni-moderation", ".*realtime-preview", ".*instruct", "preview$", "\\d{2}$"];
        // string[] allModelFiltered = FilterModelsByRegex(regexes, [.. allModel]);
        // Array.Sort(allModelFiltered);

        return [.. allModel];
    }

    // Define a function to get a model task based on the API key
    Task<string[]> GetModelTask(string apiKey, Func<Task<string[]>> fetchModels)
    {
        return apiKey != string.Empty ? fetchModels() : Task.FromResult(Array.Empty<string>());
    }

    async void SelectModel(object sender, RoutedEventArgs e)
    {
        StackPanel openAIModelContent = new();
        StackPanel grokModelContent = new();

        // Retrieve tasks for existing models
        if (GetOpenAIApiKey() == string.Empty && GetGrokApiKey() == string.Empty)
        {
            ShowErrorDialog(APIKeyIsNotSet.Text);
            return;
        }

        Task<string[]> openAITask = GetModelTask(GetOpenAIApiKey(), GetOpenAIModelsAsync);
        Task<string[]> grokTask = GetModelTask(GetGrokApiKey(), GetGrokModelsAsync);

        // Wait for all tasks to complete
        await Task.WhenAll(openAITask, grokTask);

        // Retrieve model results
        string[] openAIModels = await openAITask;
        string[] grokModels = await grokTask;


        foreach (string model in openAIModels)
        {
            openAIModelContent.Children.Add(new RadioButton
            {
                Content = model,
                GroupName = "models",
                IsChecked = model == GetModel()
            });
        }

        foreach (string model in grokModels)
        {
            grokModelContent.Children.Add(new RadioButton
            {
                Content = model,
                GroupName = "models",
                IsChecked = model == GetModel()
            });
        }

        StackPanel selectModelType = new();
        if (GetOpenAIApiKey() != string.Empty)
        {
            selectModelType.Children.Add(new TextBlock { Text = "OpenAI", FontWeight = FontWeights.Bold, FontSize = 16, Margin = new Thickness(0, 0, 0, 8) });
            selectModelType.Children.Add(openAIModelContent);
        }
        if (GetGrokApiKey() != string.Empty)
        {
            selectModelType.Children.Add(new TextBlock { Text = "Grok", FontWeight = FontWeights.Bold, FontSize = 16, Margin = new Thickness(0, 8, 0, 8) });
            selectModelType.Children.Add(grokModelContent);
        }

        ContentDialog dialog = new()
        {
            XamlRoot = Content.XamlRoot,
            Style = Application.Current.Resources["DefaultContentDialogStyle"] as Style,
            PrimaryButtonText = "OK",
            SecondaryButtonText = Cancel.Text,
            Title = SelectLangModel.Text,
            DefaultButton = ContentDialogButton.Primary,
            Content = new ScrollViewer()
            {
                Content = selectModelType
            }
        };
        ContentDialogResult result = await dialog.ShowAsync();

        if (result == ContentDialogResult.Primary)
        {
            foreach (RadioButton radioButton in openAIModelContent.Children.Cast<RadioButton>())
            {
                if (radioButton.IsChecked == true)
                {
                    if (radioButton.Content != null)
                    {
                        SetModel(radioButton.Content?.ToString() ?? "DefaultModelName");
                    }
                    break;
                }
            }

            foreach (RadioButton radioButton in grokModelContent.Children.Cast<RadioButton>())
            {
                if (radioButton.IsChecked == true)
                {
                    if (radioButton.Content != null)
                    {
                        SetModel(radioButton.Content?.ToString() ?? "DefaultModelName");
                    }
                    break;
                }
            }
        }
        else if (result == ContentDialogResult.Secondary)
        {
            // 何もしない
        }
    }

    // <summary>
    // API キーを設定します。
    // </summary>
    void SetModel(string modelName)
    {
        string subKeyPath = @"SOFTWARE\Yukari";
        // レジストリに書き込む
        using RegistryKey key = Registry.CurrentUser.CreateSubKey(subKeyPath);
        if (key != null)
        {
            try
            {
                key.SetValue("modelName", modelName ?? "GPT-4o mini", RegistryValueKind.String);
            }
            catch (Exception ex)
            {
                AddMessage(ex.Message);
            }
            LanguageModel.Text = modelName ?? "GPT-4o mini";
        }
    }

    // <summary>
    // API キーを取得します。
    // </summary>
    public static string GetModel()
    {
        // レジストリのパスを指定
        string subKeyPath = @"SOFTWARE\Yukari";
        // レジストリから値を読み込む
        using RegistryKey? key = Registry.CurrentUser.OpenSubKey(subKeyPath);
        // key が存在しない場合、空文字列を返す
        if (key == null)
            return string.Empty;
        return key?.GetValue("modelName") as string ?? string.Empty;
    }

    public static Uri GetEndpoint()
    {
        string model = GetModel();
        if (model.StartsWith("grok", StringComparison.OrdinalIgnoreCase))
            return new Uri("https://api.x.ai/v1");
        return new Uri("https://api.openai.com/v1");
    }

    void ClickVoicevoxReading(object sender, RoutedEventArgs e)
    {
        TabViewItem? tabViewItem = Tabs.SelectedItem as TabViewItem;
        if (tabViewItem == null)
        {
            return;
        }
        if (tabViewItem.Content is Client client)
        {
            client.VoicevoxReading();
        }
    }

    public async void Load()
    {
        string historiesPath = GetHistoriesPath();
        if (!File.Exists(historiesPath))
            return;

        string json = File.ReadAllText(historiesPath);

        List<Record>? records = JsonSerializer.Deserialize<List<Record>>(json);
        if (records == null)
            return;

        foreach (Record record in records)
        {
            TabViewItem tabViewItem = CreateNewTab();
            Tabs.TabItems.Add(tabViewItem);
            if (tabViewItem.Content is Client client)
            {
                client.activeIdx = record.ActiveIdx;
                client.historyItems = record.Histories;
            }
            tabViewItem.Header = ConvertFromBase64(record.Header);
        }

        await Task.Delay(1000);
        ClickChatItems(ChatItems);
    }

    public static string ConvertFromBase64(string? base64String)
    {
        if (base64String == null) base64String = "";
        // Base64 文字列をバイト配列に変換
        byte[] bytes = Convert.FromBase64String(base64String);
        // バイト配列を UTF-8 文字列に変換
        string originalString = Encoding.UTF8.GetString(bytes);
        return originalString;
    }

    public static string ConvertToBase64(string? input)
    {
        if (input == null) input = "";
        // 入力文字列を UTF-8 バイト配列に変換
        byte[] bytes = Encoding.UTF8.GetBytes(input);
        // バイト配列を Base64 文字列に変換
        string base64String = Convert.ToBase64String(bytes);
        return base64String;
    }

    public void Save()
    {
        // 各タブを走査して、情報を集める
        List<Record> records = new();
        foreach (TabViewItem tabItem in Tabs.TabItems)
        {
            if (tabItem.Content is Client client)
            {
                Record record = new()
                {
                    Header = ConvertToBase64(tabItem.Header as string),
                    ActiveIdx = client.activeIdx,
                    Histories = client.historyItems
                };

                if (record.Histories.Count != 0)
                    records.Add(record);
            }
        }
        JsonSerializerOptions options = new()
        {
            Encoder = JavaScriptEncoder.Create(UnicodeRanges.All),
            WriteIndented = true  // インデントを有効にする
        };
        string json = JsonSerializer.Serialize(records, options);

        // 保存
        string historiesPath = GetHistoriesPath();
        File.WriteAllText(historiesPath, json, Encoding.UTF8);
    }

    public void SaveReset()
    {
        // 各タブを走査して、情報を集める
        List<Record> records = new();
        string json = JsonSerializer.Serialize(records);
        // 保存
        string historiesPath = GetHistoriesPath();
        File.WriteAllText(historiesPath, json, Encoding.UTF8);
    }

    string GetHistoriesPath()
    {
        string documentsPath = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
        string yukariPath = Path.Join(documentsPath, ".yukari");
        if (!File.Exists(yukariPath))
        {
            Directory.CreateDirectory(yukariPath);
            File.SetAttributes(yukariPath, File.GetAttributes(yukariPath) | FileAttributes.Hidden);
        }
        string historiesPath = Path.Join(yukariPath, "records.json");
        return historiesPath;
    }

    void Click_AddAutomate(object sender, RoutedEventArgs e)
    {
        ChatItems.Items.Clear();
        Automate automate = new()
        {
            mainWindow = this
        };
        TabViewItem newItem = new()
        {
            Header = NewTab.Text,
            IconSource = new FontIconSource
            {
                Glyph = "\uE99A",
            },
            Content = automate
        };
        automate.tabItem = newItem;
        // client.tabViewItem = newItem;
        Grid.SetRow(Tabs, 1);
        EditorPreview.Visibility = Visibility.Collapsed;
        Tabs.TabItems.Add(newItem);
        Tabs.SelectedItem = newItem;
    }
}

internal class EditorMessage
{
    [JsonPropertyName("send")]
    public bool Send { get; set; } = false;

    [JsonPropertyName("message")]
    public string Message { get; set; } = "";
}
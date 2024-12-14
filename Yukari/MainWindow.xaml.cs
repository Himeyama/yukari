using System.Diagnostics;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.UI;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace Yukari;

// API キーを保持するためのクラス
public class ApiKeyRequest
{
    [JsonPropertyName("apikey")]
    public string Apikey { get; set; }
}

public sealed partial class MainWindow : Window
{
    int? apiEnginePort = null;
    Process apiProcess = null;

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

        InitAPI();
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
        try
        {
            if (apiProcess != null && !apiProcess.HasExited)
            {
                apiProcess.Kill();
            }
        }
        catch (Exception ex)
        {
            // ログに出力やエラーハンドリングを行います
            ShowErrorDialog($"Error while killing the process: {ex.Message}");
        }
        finally
        {
            apiProcess?.Dispose();
        }
    }

    // <summary>
    // ステータスバーにメッセージを設定します。
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
    void AddMessage(string message, int delay = 3000)
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
    // 内部 API を初期化します。
    // </summary>
    void InitAPI()
    {
        string exePath = Path.Combine("build", "yukari-engine", "yukari-engine.exe");

        string currentDirectory = Directory.GetCurrentDirectory();
        string relativePath = @"..\yukari-engine\yukari-engine.exe";
        string exePathDeploy = Path.Combine(currentDirectory, relativePath);
        if (File.Exists(exePathDeploy))
            exePath = exePathDeploy;
        if (!File.Exists(exePath))
            return;

        // プロセスの情報を設定
        ProcessStartInfo startInfo = new()
        {
            FileName = exePath,
            WorkingDirectory = Path.GetDirectoryName(exePath),
            UseShellExecute = false, // バックグラウンドで実行するために必要
            CreateNoWindow = true, // 新しいウィンドウを作成しない
        };

        // プロセスをスタート
        apiProcess = new()
        {
            StartInfo = startInfo
        };
        // プロセスを開始
        apiProcess.Start();
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
    async void TabView_Loaded(object sender, RoutedEventArgs e)
    {
        apiEnginePort = await APIManager.FindPortWithVersionAsync("127.0.0.1", 50027, 50050, "/api/version", "yukari-engine");
        TabView tabView = sender as TabView;
        TabViewItem tabViewItem = CreateNewTab();
        tabView.TabItems.Add(tabViewItem);
        tabView.SelectedItem = tabViewItem;
    }

    // <summary>
    // TabView に新しいタブを追加します。
    // </summary>
    void TabView_AddButtonClick(TabView sender, object args)
    {
        TabView tabView = sender;
        TabViewItem tabViewItem = CreateNewTab();
        tabView.TabItems.Add(tabViewItem);
        tabView.SelectedItem = tabViewItem;
    }

    // <summary>
    // タブが閉じられるリクエストを処理します。
    // </summary>
    void TabView_TabCloseRequested(TabView sender, TabViewTabCloseRequestedEventArgs args)
    {
        sender.TabItems.Remove(args.Tab);
    }

    // <summary>
    // 新しいタブを作成します。
    // </summary>
    TabViewItem CreateNewTab()
    {
        if (apiEnginePort == null)
        {
            // エラーを表示
            return null;
        }

        Client client = new(apiEnginePort, this);
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
    async void SetUpAPIKey(string apiKey, int? port)
    {
        if (port == null)
            return;

        // HttpClientのインスタンスを作成
        using HttpClient client = new();

        // APIエンドポイントのURL
        string url = $"http://127.0.0.1:{port}/api/set_apikey";

        // リクエストボディを作成
        ApiKeyRequest requestContent = new() { Apikey = apiKey };
        // オブジェクトをJSONにシリアライズ
        string json = JsonSerializer.Serialize(requestContent);
        StringContent content = new(json, Encoding.UTF8, "application/json");

        try
        {
            // POSTリクエストを送信
            HttpResponseMessage response = await client.PostAsync(url, content);

            // レスポンスのステータスコードを確認
            if (response.IsSuccessStatusCode)
            {
                string responseBody = await response.Content.ReadAsStringAsync();

                string message = responseBody == "API key set successfully" ? APIKeySuccessfullySet.Text : APIKeySettingFailed.Text;
                ContentDialog dialog = new()
                {
                    XamlRoot = Content.XamlRoot,
                    Style = Application.Current.Resources["DefaultContentDialogStyle"] as Style,
                    PrimaryButtonText = "OK",
                    Content = message,
                    DefaultButton = ContentDialogButton.Primary,
                };
                ContentDialogResult result = await dialog.ShowAsync();
            }
            else
            {
                ShowErrorDialog($"Error: {response.StatusCode} ({url})");
            }
        }
        catch (Exception ex)
        {
            ShowErrorDialog($"An exception has occurred: {ex.Message}");
        }
    }

    // <summary>
    // API キーを非同期に取得します。
    // </summary>
    async Task<string> GetApiKeyAsync(int? port)
    {
        if (port == null)
            return "";

        using HttpClient client = new();
        // ベースアドレスを設定
        // APIエンドポイントのURL
        string url = $"http://127.0.0.1:{port}/api/apikey";

        try
        {
            // GETリクエストを送信
            HttpResponseMessage response = await client.GetAsync(url);

            // レスポンスのステータスコードを確認
            if (response.IsSuccessStatusCode)
            {
                return await response.Content.ReadAsStringAsync();
            }
        }
        catch (Exception ex)
        {
            AddMessage($"An exception has occurred: {ex.Message}");
        }
        return "";
    }

    // <summary>
    // API キー設定用のダイアログを表示し、入力された API キーを設定します。
    // </summary>
    async void ClickSetAPIKey(object sender, RoutedEventArgs e)
    {
        string apiKey = await GetApiKeyAsync(apiEnginePort);
        ConfigAPIKey configAPIKey = new();
        configAPIKey.APIKeyBox.Password = apiKey;

        ContentDialog dialog = new()
        {
            XamlRoot = Content.XamlRoot,
            Style = Application.Current.Resources["DefaultContentDialogStyle"] as Style,
            PrimaryButtonText = "OK",
            SecondaryButtonText = Cancel.Text,
            Title = SetAPIKey.Text,
            DefaultButton = ContentDialogButton.Primary,
            Content = configAPIKey
        };
        ContentDialogResult result = await dialog.ShowAsync();

        if (result == ContentDialogResult.Primary)
        {
            apiKey = configAPIKey.APIKeyBox.Password;
            if (apiEnginePort == null)
            {
                ShowErrorDialog("The port number could not be detected. Please contact the developer.");
                return;
            }
            SetUpAPIKey(apiKey, apiEnginePort);
        }
        else if (result == ContentDialogResult.Secondary)
        {
            // 何もしない
        }
    }

    // <summary>
    // 指定されたメッセージを含むエラーダイアログを表示します。
    // </summary>
    public void ShowErrorDialog(string message)
    {
        ErrorDialog.Show(this, message);
    }

    // <summary>
    // アプリケーションを終了します。
    // </summary>
    void ClickExit(object sender, RoutedEventArgs e)
    {
        Close();
    }
}
using System.Diagnostics;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.UI;
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
        // クロージングの処理
        AppWindow thisAppWin = GetCurrentAppWin();
        if (thisAppWin != null)
            thisAppWin.Closing += OnWindowClosing;

        ExtendsContentIntoTitleBar = true;
        SetTitleBar(AppTitleBar);

        InitAPI();
    }

    AppWindow GetCurrentAppWin()
    {
        IntPtr hwnd = WinRT.Interop.WindowNative.GetWindowHandle(this);
        WindowId winId = Win32Interop.GetWindowIdFromWindow(hwnd);
        return AppWindow.GetFromWindowId(winId);
    }

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

    void InitAPI()
    {
        string exePath = Path.Combine("build", "yukari-engine", "yukari-engine.exe");

        string currentDirectory = Directory.GetCurrentDirectory();
        string relativePath = @"..\yukari-engine\yukari-engine.exe";
        string exePathDeploy = Path.Combine(currentDirectory, relativePath);
        if(File.Exists(exePathDeploy))
            exePath = exePathDeploy;
        if(!File.Exists(exePath))
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

    void ClickAbout(object sender, RoutedEventArgs e) {
        string url = "https://github.com/himeyama/yukari"; // 開きたいURLを指定
        Process.Start(
            new ProcessStartInfo { FileName = url, UseShellExecute = true }
        );
    }

    // TabView を読み込んだ時
    async void TabView_Loaded(object sender, RoutedEventArgs e)
    {
        apiEnginePort = await APIManager.FindPortWithVersionAsync("127.0.0.1", 50027, 50050, "/api/version", "yukari-engine");
        TabView tabView = sender as TabView;
        TabViewItem tabViewItem = CreateNewTab();
        tabView.TabItems.Add(tabViewItem);
        tabView.SelectedItem = tabViewItem;
    }

    void TabView_AddButtonClick(TabView sender, object args)
    {
        TabView tabView = sender;
        TabViewItem tabViewItem = CreateNewTab();
        tabView.TabItems.Add(tabViewItem);
        tabView.SelectedItem = tabViewItem;
    }

    void TabView_TabCloseRequested(TabView sender, TabViewTabCloseRequestedEventArgs args)
    {
        sender.TabItems.Remove(args.Tab);
    }

    TabViewItem CreateNewTab()
    {
        if(apiEnginePort == null){
            // エラーを表示
            return null;
        }

        Client client = new(apiEnginePort, this);
        TabViewItem newItem = new()
        {
            Header = NewTab.Text,
            IconSource = new SymbolIconSource() {
                Symbol = Symbol.Contact
            },
            Content = client
        };
        client.tabViewItem = newItem;
        return newItem;
    }

    async Task<string> GetApiKeyAsync(int? port)
    {
        if(port == null)
            return null;

        using HttpClient client = new();
        // ベースアドレスを設定
        client.BaseAddress = new Uri($"http://127.0.0.1:{port}/");
        try
        {
            // GET リクエストを送信
            HttpResponseMessage response = await client.GetAsync("api/apikey");

            // レスポンスを確認
            if (response.IsSuccessStatusCode)
            {
                // レスポンス内容を文字列として取得
                string result = await response.Content.ReadAsStringAsync();
                return result; // API キーを返す
            }
            else
            {
                ShowErrorDialog($"Error: {response.StatusCode}");
                return null;
            }
        }
        catch (Exception ex)
        {
            ShowErrorDialog($"An exception has occurred: {ex.Message}");
            return null;
        }
    }

    async void SetUpAPIKey(string apiKey, int? port)
    {
        if(port == null)
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

    async void ClickSetAPIKey(object sender, RoutedEventArgs e)
    {
        string apiKey = await GetApiKeyAsync(apiEnginePort);

        ConfigAPIKey configAPIKey = new();
        if (apiKey != null)
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
            if (apiEnginePort == null){
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

    public void ShowErrorDialog(string message)
    {
        ErrorDialog.Show(this, message);
    }

    void ClickExit(object sender, RoutedEventArgs e)
    {
        Close();
    }
}

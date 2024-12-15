using System.Diagnostics;
using System.Text.Json.Serialization;
using Microsoft.UI;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.Win32;

namespace Yukari;

// API キーを保持するためのクラス
public class ApiKeyRequest
{
    [JsonPropertyName("apikey")]
    public string Apikey { get; set; }
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
    static void SetUpAPIKey(string apiKey)
    {
        // apiKeyの値と保存先のパスを指定
        string subKeyPath = @"SOFTWARE\Yukari";

        // レジストリに書き込む
        using RegistryKey key = Registry.CurrentUser.CreateSubKey(subKeyPath);
        key?.SetValue("apiKey", apiKey, RegistryValueKind.String);
    }

    // <summary>
    // API キーを非同期に取得します。
    // </summary>
    string GetApiKey()
    {
        // レジストリのパスを指定
        string subKeyPath = @"SOFTWARE\Yukari";

        // レジストリから値を読み込む
        using RegistryKey key = Registry.CurrentUser.OpenSubKey(subKeyPath);
        return key?.GetValue("apiKey") as string;
    }

    // <summary>
    // API キー設定用のダイアログを表示し、入力された API キーを設定します。
    // </summary>
    async void ClickSetAPIKey(object sender, RoutedEventArgs e)
    {
        string apiKey = GetApiKey();
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
            SetUpAPIKey(apiKey);
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
using System.Diagnostics;
using Microsoft.UI;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace Yukari;
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
        catch (Exception)
        {
            // ログに出力やエラーハンドリングを行います
            // Console.WriteLine("Error while killing the process: " + ex.Message);
        }
        finally
        {
            apiProcess?.Dispose();
        }
    }

    void InitAPI()
    {
        string exePath = Path.Combine("build", "yukari-engine", "yukari-engine.exe");
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

    private void TabView_TabCloseRequested(TabView sender, TabViewTabCloseRequestedEventArgs args)
    {
        sender.TabItems.Remove(args.Tab);
    }

    TabViewItem CreateNewTab()
    {
        if(apiEnginePort == null){
            // エラーを表示
            return null;
        }

        TabViewItem newItem = new()
        {
            Header = NewTab.Text,
            IconSource = new SymbolIconSource() {
                Symbol = Symbol.Document
            },
            Content = new Client(apiEnginePort)
        };
        return newItem;
    }

    void ClickExit(object sender, RoutedEventArgs e)
    {
        Close();
    }
}

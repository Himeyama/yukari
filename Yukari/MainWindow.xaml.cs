using System.Diagnostics;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace Yukari;
public sealed partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();

        ExtendsContentIntoTitleBar = true;
        SetTitleBar(AppTitleBar);
    }

    void ClickAbout(object sender, RoutedEventArgs e) {
        string url = "https://github.com/himeyama/yukari"; // 開きたいURLを指定
        Process.Start(
            new ProcessStartInfo { FileName = url, UseShellExecute = true }
        );
    }

    // タブ
    void TabView_Loaded(object sender, RoutedEventArgs e)
    {
        (sender as TabView).TabItems.Add(CreateNewTab());
    }

    void TabView_AddButtonClick(TabView sender, object args)
    {
        sender.TabItems.Add(CreateNewTab());
    }

    private void TabView_TabCloseRequested(TabView sender, TabViewTabCloseRequestedEventArgs args)
    {
        sender.TabItems.Remove(args.Tab);
    }

    TabViewItem CreateNewTab()
    {
        TabViewItem newItem = new()
        {
            Header = NewTab.Text,
            IconSource = new SymbolIconSource() {
                Symbol = Symbol.Document
            },
            Content = new Client()
        };
        return newItem;
    }

    void ClickExit(object sender, RoutedEventArgs e)
    {
        Close();
    }
}

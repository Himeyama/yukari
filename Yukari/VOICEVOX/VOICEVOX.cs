#nullable enable

using System.Diagnostics;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.UI.Xaml;
using System.Media;
using Microsoft.UI.Xaml.Controls;
using Microsoft.Win32;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Net.Http;
using System;
using System.IO;

namespace Yukari;

public class VOICEVOX
{
    public static MainWindow? mainWindow = null;

    // キャラクター情報を格納するクラス
    public class Style
    {
        [JsonPropertyName("name")]
        public string? Name { get; set; }
        [JsonPropertyName("id")]
        public int? Id { get; set; }
        [JsonPropertyName("type")]
        public string? Type { get; set; }
    }

    public class Speaker
    {
        [JsonPropertyName("name")]
        public string? Name { get; set; }
        [JsonPropertyName("styles")]
        public List<Style>? Styles { get; set; }
    }

    public VOICEVOX()
    {
        // Constructor logic here
    }

    public static async Task<string?> GetVersion()
    {
        // APIのエンドポイント
        string url = "http://127.0.0.1:50021/version";

        HttpClientHandler handler = new(){
            UseProxy = false
        };

        using HttpClient client = new(handler);
        try
        {
            // GETリクエストを送信
            HttpResponseMessage response = await client.GetAsync(url);

            // レスポンスが成功したか確認
            response.EnsureSuccessStatusCode();

            // レスポンスの内容を取得
            string responseBody = await response.Content.ReadAsStringAsync();

            // 結果を表示
            return responseBody.Trim('"');
        }
        catch (HttpRequestException)
        {
            // mainWindow?.AddMessage("HTTPリクエストエラー: " + e.Message);
        }
        return null;
    }

    public static void LaunchVoicevox()
    {
        // VOICEVOX アプリケーションのパスを指定
        string voicevoxPath = @"C:\Program Files\VOICEVOX\voicevox.exe"; // 実際のインストールパスに変更してください。
        if(!Path.Exists(voicevoxPath)){
            // ユーザーのプロファイルパスを取得
            string userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            
            // VOICEVOX のパスを構築
            voicevoxPath = Path.Combine(userProfile, "AppData", "Local", "Programs", "VOICEVOX", "VOICEVOX.exe");

            // 確認のため存在を確認
            if (!Path.Exists(voicevoxPath))
            {
                mainWindow?.AddMessage("VOICEVOX が見つかりませんでした。");
                return;
            }
        }

        // プロセス情報を設定
        ProcessStartInfo startInfo = new()
        {
            FileName = voicevoxPath,
            Arguments = "",  // 必要な引数があればここに指定する
            UseShellExecute = true // シェルを使用して実行する
        };

        // VOICEVOX を起動する
        try
        {
            Process? process = Process.Start(startInfo);
            if (process != null)
            {
                using (process)
                {
                    mainWindow?.InitVoicevox();
                }
            }
            else
            {
                mainWindow?.AddMessage("VOICEVOX の起動に失敗しました。");
            }
        }
        catch (Exception ex)
        {
            // エラー処理
            mainWindow?.AddMessage($"起動時にエラーが発生しました: {ex.Message}");
        }
    }

    public static async Task<List<string>?> GetSpeakers()
    {
        // VOICEVOXのAPIエンドポイント
        string url = "http://127.0.0.1:50021/speakers";

        HttpClientHandler handler = new(){
            UseProxy = false
        };

        using HttpClient client = new(handler);
        try
        {
            // GETリクエストを送信
            HttpResponseMessage response = await client.GetAsync(url);

            // レスポンスの確認
            if (response.IsSuccessStatusCode)
            {
                // レスポンス内容を読み取る
                string jsonResponse = await response.Content.ReadAsStringAsync();

                // JSONを解析
                Speaker[]? speakers = JsonSerializer.Deserialize<Speaker[]>(jsonResponse);
                List<string> speakersList = [];
                if(speakers != null){
                    // キャラクターの情報を表示
                    foreach (Speaker speaker in speakers)
                    {
                        string? name = speaker?.Name;
                        if(name != null){
                            speakersList.Add(name);
                        }
                    }
                }
                return speakersList;
            }
            else
            {
                mainWindow?.AddMessage($"エラー: {response.StatusCode}");
            }
        }
        catch (Exception ex)
        {
            mainWindow?.AddMessage($"例外が発生しました: {ex.Message}");
        }
        return null;
    }

    public static async Task<bool> SelectSpeaker(){
        if(mainWindow == null){
            return false;
        }

        List<string>? speakers = await GetSpeakers();
        if (speakers == null)
        {
            mainWindow.AddMessage("スピーカー情報の取得に失敗しました。");
            return false;
        }

        StackPanel stackPanel = new();
        ScrollViewer content = new()
        {
            Content = stackPanel
        };

        ContentDialog dialog = new()
        {
            XamlRoot = mainWindow.Content.XamlRoot,
            Style = Application.Current.Resources["DefaultContentDialogStyle"] as Microsoft.UI.Xaml.Style,
            IsPrimaryButtonEnabled = false,
            PrimaryButtonText = mainWindow.Next.Text,
            SecondaryButtonText = mainWindow.Cancel.Text,
            Title = mainWindow.SelectVoicevoxSpeaker.Text,
            DefaultButton = ContentDialogButton.Primary,
            Content = content
        };

        foreach (string speaker in speakers)
        {
            RadioButton radioButton = new() {
                Content = speaker,
                GroupName = "speakers" // グループ名を設定
            };
            
            // ラジオボタンがチェックされたときにボタンの有効状態を更新
            radioButton.Checked += (s, e) => {
                dialog.IsPrimaryButtonEnabled = true; // ボタンを有効にする
            };

            stackPanel.Children.Add(radioButton);
        }

        ContentDialogResult result = await dialog.ShowAsync();

        string? selectSpeaker = "";
        for(int i = 0; i < stackPanel.Children.Count; i++){
            if(stackPanel.Children[i] is RadioButton radioButton){
                if(radioButton.IsChecked == true){
                    selectSpeaker = radioButton.Content?.ToString();
                    break;
                }
            }
        }

        if (result == ContentDialogResult.Primary)
        {
            if(selectSpeaker != null){
                mainWindow.AddMessage(selectSpeaker);
                await SelectStyle(selectSpeaker);
            }
        }
        else if (result == ContentDialogResult.Secondary)
        {
            SetStyle("");
            SetSpeaker("");
            string? voicevoxVersion = await GetVersion();
            if(voicevoxVersion != null){
                mainWindow.VoicevoxButtonText.Text = $"VOICEVOX {voicevoxVersion}";
            }
        }
        return true;
    }

    public static async Task<List<string>?> GetStyles(string speakerName)
    {
        // VOICEVOXのAPIエンドポイント
        string url = "http://127.0.0.1:50021/speakers";

        HttpClientHandler handler = new(){
            UseProxy = false
        };

        using HttpClient client = new(handler);
        try
        {
            // GETリクエストを送信
            HttpResponseMessage response = await client.GetAsync(url);

            // レスポンスの確認
            if (response.IsSuccessStatusCode)
            {
                // レスポンス内容を読み取る
                string jsonResponse = await response.Content.ReadAsStringAsync();

                // JSONを解析
                Speaker[]? speakers = JsonSerializer.Deserialize<Speaker[]>(jsonResponse);
                List<string>? styles = [];
                if(speakers != null){
                    // キャラクターの情報を表示
                    foreach (Speaker speaker in speakers)
                    {
                        if(speaker?.Name == speakerName && speaker?.Styles != null){
                            foreach(Style style in speaker.Styles){
                                if (style.Name != null)
                                {
                                    styles.Add(style.Name);
                                }
                            }
                        }
                    }
                    return styles;
                }else{
                    mainWindow?.AddMessage("エラー: スピーカー情報が取得できませんでした。");
                    return null;
                }
            }
            else
            {
                mainWindow?.AddMessage($"エラー: {response.StatusCode}");
            }
        }
        catch (Exception ex)
        {
            mainWindow?.AddMessage($"例外が発生しました: {ex.Message}");
        }
        return null;
    }

    public static async Task<bool> SelectStyle(string speakerName){
        if(mainWindow == null){
            return false;
        }

        List<string>? styles = await GetStyles(speakerName);
        if (styles == null)
        {
            mainWindow.AddMessage("スタイル情報の取得に失敗しました。");
            return false;
        }

        if(styles.Count == 1){
            string selectStyleName = styles[0];
            mainWindow.AddMessage(selectStyleName);
            mainWindow.VoicevoxButtonText.Text = $"VOICEVOX: {speakerName} ({selectStyleName})";
            SetSpeaker(speakerName);
            SetStyle(selectStyleName);
            return true;
        }else{
            StackPanel stackPanel = new();
            ScrollViewer content = new()
            {
                Content = stackPanel
            };

            ContentDialog dialog = new()
            {
                XamlRoot = mainWindow.Content.XamlRoot,
                Style = Application.Current.Resources["DefaultContentDialogStyle"] as Microsoft.UI.Xaml.Style,
                IsPrimaryButtonEnabled = false,
                PrimaryButtonText = mainWindow.Next.Text,
                SecondaryButtonText = mainWindow.Cancel.Text,
                Title = mainWindow.SelectVoicevoxStyle.Text,
                DefaultButton = ContentDialogButton.Primary,
                Content = content
            };

            foreach (string style in styles)
            {
                RadioButton radioButton = new() {
                    Content = style,
                    GroupName = "styles" // グループ名を設定
                };
                
                // ラジオボタンがチェックされたときにボタンの有効状態を更新
                radioButton.Checked += (s, e) => {
                    dialog.IsPrimaryButtonEnabled = true; // ボタンを有効にする
                };

                stackPanel.Children.Add(radioButton);
            }

            ContentDialogResult result = await dialog.ShowAsync();

            string? selectStyle = "";
            for(int i = 0; i < stackPanel.Children.Count; i++){
                if(stackPanel.Children[i] is RadioButton radioButton){
                    if(radioButton.IsChecked == true){
                        selectStyle = radioButton.Content?.ToString();
                        break;
                    }
                }
            }

            if (result == ContentDialogResult.Primary)
            {
                if(selectStyle != null){
                    mainWindow.AddMessage(selectStyle);
                    mainWindow.VoicevoxButtonText.Text = $"VOICEVOX: {speakerName} ({selectStyle})";
                    SetSpeaker(speakerName);
                    SetStyle(selectStyle);
                }
            }
            else if (result == ContentDialogResult.Secondary)
            {
                SetStyle("");
                SetSpeaker("");
                string? voicevoxVersion = await GetVersion();
                if(voicevoxVersion != null){
                    mainWindow.VoicevoxButtonText.Text = $"VOICEVOX {voicevoxVersion}";
                }
            }
        }

        return true;
    }

    // <summary>
    // スピーカーを設定
    // </summary>
    static void SetSpeaker(string speakerName)
    {
        // apiKeyの値と保存先のパスを指定
        string subKeyPath = @"SOFTWARE\Yukari";

        // レジストリに書き込む
        using RegistryKey key = Registry.CurrentUser.CreateSubKey(subKeyPath);
        key?.SetValue("voicevoxSpeaker", speakerName, RegistryValueKind.String);
    }

    // <summary>
    // スピーカーを取得
    // </summary>
    public static string GetSpeaker()
    {
        // レジストリのパスを指定
        string subKeyPath = @"SOFTWARE\Yukari";

        // レジストリから値を読み込む
        using RegistryKey? key = Registry.CurrentUser.OpenSubKey(subKeyPath);
        return key?.GetValue("voicevoxSpeaker") as string ?? string.Empty;
    }

    // <summary>
    // スタイルを設定
    // </summary>
    static void SetStyle(string style)
    {
        // apiKeyの値と保存先のパスを指定
        string subKeyPath = @"SOFTWARE\Yukari";

        // レジストリに書き込む
        using RegistryKey key = Registry.CurrentUser.CreateSubKey(subKeyPath);
        key?.SetValue("voicevoxStyle", style, RegistryValueKind.String);
    }

    // <summary>
    // スタイルを取得
    // </summary>
    public static string GetStyle()
    {
        // レジストリのパスを指定
        string subKeyPath = @"SOFTWARE\Yukari";

        // レジストリから値を読み込む
        using RegistryKey? key = Registry.CurrentUser.OpenSubKey(subKeyPath);
        return key?.GetValue("voicevoxStyle") as string ?? string.Empty;
    }

    public static string ComputeSha256Hash(string rawData)
    {
        // SHA256 ハッシュ オブジェクトを作成
        using SHA256 sha256Hash = SHA256.Create();
        // テキストをバイト配列に変換
        byte[] bytes = sha256Hash.ComputeHash(Encoding.UTF8.GetBytes(rawData));

        // バイト配列を 16 進数形式の文字列に変換
        StringBuilder builder = new();
        foreach (byte b in bytes)
        {
            builder.Append(b.ToString("x2")); // 16 進数形式で追加
        }
        return builder.ToString();
    }

    public static async Task<int?> GetSpeakerId()
    {
        string speakerName = GetSpeaker();
        string styleName = GetStyle();

        // VOICEVOXのAPIエンドポイント
        string url = "http://127.0.0.1:50021/speakers";

        HttpClientHandler handler = new(){
            UseProxy = false
        };

        using HttpClient client = new(handler);
        try
        {
            // GETリクエストを送信
            HttpResponseMessage response = await client.GetAsync(url);

            // レスポンスの確認
            if (response.IsSuccessStatusCode)
            {
                // レスポンス内容を読み取る
                string jsonResponse = await response.Content.ReadAsStringAsync();

                // JSONを解析
                Speaker[]? speakers = JsonSerializer.Deserialize<Speaker[]>(jsonResponse);
                List<string>? styles = [];
                if(speakers != null){
                    // キャラクターの情報を表示
                    foreach (Speaker speaker in speakers)
                    {
                        if(speaker?.Name == speakerName && speaker?.Styles != null){
                            foreach(Style style in speaker.Styles){
                                if (style.Name == styleName)
                                {
                                    return style.Id;
                                }
                            }
                        }
                    }
                    return null;
                }else{
                    mainWindow?.AddMessage("エラー: スピーカー情報が取得できませんでした。");
                    return null;
                }
            }
            else
            {
                mainWindow?.AddMessage($"エラー: {response.StatusCode}");
            }
        }
        catch (Exception ex)
        {
            mainWindow?.AddMessage($"例外が発生しました: {ex.Message}");
        }
        return null;
    }

    static async Task<string?> GetAudioQuery(string text)
    {
        string textUriEncoded = Uri.EscapeDataString(text);
        string speakerId = $"{await GetSpeakerId() ?? 1}";
        string url = $"http://127.0.0.1:50021/audio_query?text={textUriEncoded}&speaker={speakerId}";

        HttpClientHandler handler = new()
        {
            UseProxy = false
        };

        using HttpClient client = new(handler);
        try
        {
            StringContent content = new("", Encoding.UTF8, "application/json");
            HttpResponseMessage response = await client.PostAsync(url, content);

            if (response.IsSuccessStatusCode)
            {
                string jsonResponse = await response.Content.ReadAsStringAsync();
                return jsonResponse;
            }
            else
            {
                mainWindow?.AddMessage($"エラー: {response.StatusCode}");
            }
        }
        catch (Exception ex)
        {
            mainWindow?.AddMessage($"例外が発生しました: {ex.Message}");
        }
        return null;
    }

    public static async Task<bool> GenerateVoice(string text)
    {
        string id = $"{await GetSpeakerId() ?? 1}";
        string sha256Hash = ComputeSha256Hash(text);
        string tempPath = Path.Combine(Path.GetTempPath(), $"{sha256Hash}-{id}.wav");
        if(File.Exists(tempPath))
            return true;

        if (mainWindow == null)
        {
            return false;
        }

        if (await GetVersion() == null)
        {
            mainWindow?.AddMessage("VOICEVOX が起動していません。");
            return false;
        }

        string? json = await GetAudioQuery(text);
        if (json == null)
        {
            mainWindow.AddMessage("音声生成情報の取得に失敗しました。");
            return false;
        }

        byte[]? audioData = await SynthesizeVoice(json);
        if (audioData == null)
        {
            mainWindow.AddMessage("音声の生成に失敗しました。");
            return false;
        }
        File.WriteAllBytes(tempPath, audioData);
        mainWindow.AddMessage($"音声が生成され、{tempPath} として保存されました。");
        return true;
    }

    static async Task<byte[]?> SynthesizeVoice(string json)
    {
        string id = $"{await GetSpeakerId()}" ?? "1";
        string uri = $"http://127.0.0.1:50021/synthesis?speaker={id}&enable_interrogative_upspeak=false";

        StringContent content = new(json, Encoding.UTF8, "application/json");
        HttpClientHandler handler = new()
        {
            UseProxy = false
        };

        using HttpClient httpClient = new(handler);
        httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("audio/wav"));

        try
        {
            HttpResponseMessage response = await httpClient.PostAsync(uri, content);
            response.EnsureSuccessStatusCode();
            return await response.Content.ReadAsByteArrayAsync();
        }
        catch (Exception ex)
        {
            mainWindow?.AddMessage($"エラー：{ex.Message}");
            return null;
        }
    }

    public static async Task PlayWavAsync(string path)
    {
        if(!File.Exists(path)){
            mainWindow?.AddMessage("音声ファイルが見つかりません。");
            return;
        }
        await Task.Run(() =>
        {
            // SoundPlayerクラスのインスタンスを作成
            using SoundPlayer player = new(path);
            // WAVファイルを読み込む
            player.Load();
            // 音声の再生
            player.PlaySync(); // このメソッドは再生が完了するのを待ちます
        });
    }
}
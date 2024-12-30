#nullable enable

using System.Diagnostics;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Yukari;

public class VOICEVOX
{
    public static MainWindow? mainWindow = null;

    // キャラクター情報を格納するクラス
    public class Speaker
    {
        [JsonPropertyName("name")]
        public string? Name { get; set; }
    }

    public VOICEVOX()
    {
        // Constructor logic here
    }

    public static async Task<string?> GetVersion()
    {
        // APIのエンドポイント
        string url = "http://localhost:50021/version"; // 実際のURLに置き換えてください

        using HttpClient client = new();
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
                    // プロセスが正常に起動した場合の処理
                    // AddMessage("VOICEVOX が起動しました。");
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

    public static async Task<string[]?> GetSpeakers()
    {
        // VOICEVOXのAPIエンドポイント
        string url = "http://localhost:50021/speakers";

        using HttpClient client = new();
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
                string[] speakersList = [];
                if(speakers != null){
                    // キャラクターの情報を表示
                    foreach (Speaker speaker in speakers)
                    {
                        string? name = speaker?.Name;
                        if(name != null){
                            speakersList.Append(name);
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
}
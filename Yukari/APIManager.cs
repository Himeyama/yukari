using System.Net.Http;
using System.Threading.Tasks;

namespace Yukari;

class APIManager
{
    /// <summary>
    /// 指定したポート範囲で /api/version にリクエストを送信し、
    /// レスポンスが指定したバージョンと一致するポートを見つけます。
    /// </summary>
    /// <param name="host">ホスト名</param>
    /// <param name="startPort">開始ポート番号</param>
    /// <param name="endPort">終了ポート番号</param>
    /// <param name="path">API パス</param>
    /// <param name="expectedVersion">期待するバージョン</param>
    /// <returns>一致するポート番号、見つからない場合は null</returns>
    public static async Task<int?> FindPortWithVersionAsync(string host, int startPort, int endPort, string path, string expectedVersion)
    {
        using HttpClient httpClient = new();
        for (int port = startPort; port <= endPort; port++)
        {
            string url = $"http://{host}:{port}{path}";

            try
            {
                HttpResponseMessage response = await httpClient.GetAsync(url);
                response.EnsureSuccessStatusCode(); // ステータスコードが 200 系でなければ例外をスロー

                string responseBody = await response.Content.ReadAsStringAsync();

                // レスポンスが期待されるバージョンと一致するか確認
                if (responseBody.StartsWith(expectedVersion))
                {
                    return port; // 一致したポートを返す
                }
            }
            catch (HttpRequestException)
            {
                // 該当ポートでの接続が失敗した場合は無視して次へ
                continue;
            }
        }

        return null; // 一致するポートが見つからなかった場合
    }
}
using Microsoft.UI.Xaml.Controls;


namespace Yukari;

public sealed partial class Client : Page
{
    MainWindow mainWindow = null;
    public TabViewItem tabViewItem = null;

    public List<HistoryItem> historyItems = [];
    public int activeIdx = -1;

    public Client(MainWindow mainWindow){
        InitializeComponent();
        this.mainWindow = mainWindow;
    }

    public void ApplyHistory()
    {
        mainWindow.ChatItems.Items.Clear();
        foreach (HistoryItem history in historyItems){
            mainWindow.ChatItems.Items.Insert(0, history);
        }
    }

    public async void VoicevoxReading()
    {
        HistoryItem historyItem;
        try{
            historyItem = historyItems[historyItems.Count - 1 - activeIdx];
        }catch(Exception ex){
            mainWindow.ShowErrorDialog(ex.Message);
            return;
        }
        string assistant = historyItem.Assistant;
        /* VOICEVOX の処理 */
        // 分割文字を設定
        char[] separators = ['？', '！', '、', '。', '\n'];
        // 文字列を分割
        List<string> assistants = [.. assistant.Split(separators, StringSplitOptions.RemoveEmptyEntries)];
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
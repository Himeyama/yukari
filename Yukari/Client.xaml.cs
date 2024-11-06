using Microsoft.UI.Xaml.Controls;

namespace Yukari;
public sealed partial class Client : Page
{
    public Client(int? apiEnginePort){
        InitializeComponent();

        // WebUI.Source = new Uri($"http://127.0.0.1:50027/index.html");
    }
}
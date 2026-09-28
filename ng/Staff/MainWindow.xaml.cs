using System.Net.Http;
using System.Net.Http.Json;
using System.Windows;
using NGPVP.Shared;
namespace NGPVP.Staff;
public partial class MainWindow : Window
{
    private const string ServerUrl = "http://localhost:5090";
    private readonly HttpClient http = new() { BaseAddress = new Uri(ServerUrl + "/"), Timeout = TimeSpan.FromSeconds(10) };
    private CancellationTokenSource? pollCts;
    public MainWindow(){InitializeComponent(); Loaded += async (_,__) => await RefreshPlayers();}
    private async void Refresh_Click(object sender,RoutedEventArgs e)=>await RefreshPlayers();
    private async Task RefreshPlayers()
    {
        try { var list=await http.GetFromJsonAsync<List<PlayerStatus>>("api/sessions") ?? new(); PlayerBox.ItemsSource=list; StatusText.Text=$"ONLINE • {list.Count} PLAYER(S)"; StartButton.IsEnabled=PlayerBox.SelectedItem is PlayerStatus p && p.Connected && !p.Started && !p.Completed; } catch { StatusText.Text="SERVER OFFLINE"; }
    }
    private void PlayerBox_SelectionChanged(object sender,System.Windows.Controls.SelectionChangedEventArgs e) { if(PlayerBox.SelectedItem is PlayerStatus p){PlayerText.Text=$"PLAYER: {p.Player} | {p.Id}"; StartButton.IsEnabled=p.Connected&&!p.Started&&!p.Completed; StartPolling(p.Id);} }
    private async void Start_Click(object sender,RoutedEventArgs e)
    {
        if(PlayerBox.SelectedItem is not PlayerStatus p)return;
        try { var res=await http.PostAsync($"api/session/{p.Id}/start",null); if(!res.IsSuccessStatusCode){StatusText.Text=$"START FAILED ({(int)res.StatusCode})";return;} StartButton.IsEnabled=false; StatusText.Text="SCANNING..."; } catch { StatusText.Text="START ERROR"; }
    }
    private void StartPolling(string id){pollCts?.Cancel();pollCts=new();_=Poll(id,pollCts.Token);}
    private async Task Poll(string id,CancellationToken ct){while(!ct.IsCancellationRequested){try{var s=await http.GetFromJsonAsync<SessionStatus>($"api/session/{id}");if(s is not null){PlayerText.Text=$"PLAYER: {s.Player} | {s.Id}";StartButton.IsEnabled=s.Connected&&!s.Started&&!s.Completed;if(s.Completed){StatusText.Text="CHECK COMPLETE";ResultsList.Items.Clear();foreach(var r in s.Results)ResultsList.Items.Add(r);CountsText.Text=$"   CLEAN: {s.Results.Count(x=>string.Equals(x.Severity,"Clean",StringComparison.OrdinalIgnoreCase))}   KNOWN: {s.Results.Count(x=>string.Equals(x.Severity,"Known",StringComparison.OrdinalIgnoreCase))}   SUSPICIOUS: {s.Results.Count(x=>string.Equals(x.Severity,"Suspicious",StringComparison.OrdinalIgnoreCase))}   DETECTED: {s.Results.Count(x=>string.Equals(x.Severity,"Detected",StringComparison.OrdinalIgnoreCase))}";}}}catch{}await Task.Delay(1000,ct);}}
    record PlayerStatus(string Id,string Player,bool Connected,bool Started,bool Completed);
    record ScanItem(string Category, string Title, string Severity, string Details);
    record SessionStatus(string Id,string Player,bool Connected,bool Started,bool Completed,List<ScanItem> Results);
}

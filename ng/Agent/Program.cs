using System.Net.Http.Json;
using NGPVP.Agent.Services;
using NGPVP.Shared;

var serverUrl = args.Length > 0 ? args[0] : "http://localhost:5090";
using var http = new HttpClient { BaseAddress = new Uri(serverUrl.TrimEnd('/') + "/"), Timeout = TimeSpan.FromSeconds(15) };
Console.Title = "NG PVP PC CHECK - Agent";
Console.WriteLine("========================================");
Console.WriteLine("          NG PVP PC CHECK AGENT");
Console.WriteLine("========================================");
Console.WriteLine("Connecting to NG PVP server...");

RegisterResponse? session;
try
{
    session = await (await http.PostAsync("api/agent/register", null)).Content.ReadFromJsonAsync<RegisterResponse>();
}
catch (Exception ex)
{
    Console.WriteLine("Server connection failed: " + ex.Message); Console.ReadKey(); return;
}
if (session is null) { Console.WriteLine("Invalid server response."); Console.ReadKey(); return; }

Console.WriteLine($"Connected as {session.Player}");
Console.WriteLine("Waiting for NG PVP staff to start the PC check...");
Console.WriteLine("You do not need to enter or send a PIN.");

while (true)
{
    try
    {
        using var req = new HttpRequestMessage(HttpMethod.Get, $"api/session/{session.SessionId}/agent/command");
        req.Headers.Add("X-NGPVP-Agent-Token", session.AgentToken);
        using var response = await http.SendAsync(req);
        if (!response.IsSuccessStatusCode) { await Task.Delay(2000); continue; }
        var status = await response.Content.ReadFromJsonAsync<AgentCommand>();
        if (status?.start == true) break;
        await Task.Delay(1200);
    }
    catch { await Task.Delay(2000); }
}

Console.WriteLine("STAFF STARTED THE CHECK.");
Console.WriteLine("Scanning local PC...");
var scanner = new PcScanner();
var results = new List<ScanResultDto>();
await foreach (var r in scanner.ScanAsync(null)) results.Add(new(r.Category, r.Title, r.Severity, r.Details));

using var resultReq = new HttpRequestMessage(HttpMethod.Post, $"api/session/{session.SessionId}/agent/results");
resultReq.Headers.Add("X-NGPVP-Agent-Token", session.AgentToken);
resultReq.Content = JsonContent.Create(new { results });
using var resultResponse = await http.SendAsync(resultReq);
Console.WriteLine(resultResponse.IsSuccessStatusCode ? "CHECK COMPLETE. Results sent to staff." : $"Results upload failed: {(int)resultResponse.StatusCode}");
Console.WriteLine("You can close this window.");
Console.ReadKey();

record RegisterResponse(string SessionId, string AgentToken, string Player);
record AgentCommand(bool start, bool completed);

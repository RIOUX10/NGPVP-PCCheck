using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using NGPVP.Shared;

var builder = WebApplication.CreateBuilder(args);
var port = Environment.GetEnvironmentVariable("PORT") ?? "5090";
builder.WebHost.UseUrls($"http://0.0.0.0:{port}");
var app = builder.Build();
var sessions = new ConcurrentDictionary<string, Session>();

app.MapGet("/", () => Results.Text("NG PVP PC Check Server OK"));

// Agent registers automatically. No PIN is shown or required.
app.MapPost("/api/agent/register", () =>
{
    var id = Convert.ToHexString(RandomNumberGenerator.GetBytes(6)).ToLowerInvariant();
    var token = Convert.ToHexString(RandomNumberGenerator.GetBytes(24));
    var session = new Session(id, token, "PLAYER-" + id[..6].ToUpperInvariant());
    sessions[id] = session;
    return Results.Ok(new { sessionId = id, agentToken = token, player = session.Player, expiresInMinutes = 30 });
});

// Staff sees connected agents; no player PIN is needed.
app.MapGet("/api/sessions", () => Results.Ok(sessions.Values
    .OrderByDescending(s => s.LastSeenUtc)
    .Select(s => new { id = s.Id, player = s.Player, connected = s.Connected, started = s.Started, completed = s.Completed, lastSeenUtc = s.LastSeenUtc })));

app.MapGet("/api/session/{id}", (string id) =>
{
    if (!sessions.TryGetValue(id, out var s)) return Results.NotFound();
    return Results.Ok(new { id = s.Id, player = s.Player, connected = s.Connected, started = s.Started, completed = s.Completed, results = s.Results });
});

app.MapGet("/api/session/{id}/agent/command", async (string id, HttpRequest req) =>
{
    if (!AuthorizeAgent(req, sessions, id)) return Results.Unauthorized();
    var s = sessions[id];
    s.LastSeenUtc = DateTimeOffset.UtcNow; s.Connected = true;
    return Results.Ok(new { start = s.Started && !s.Completed, completed = s.Completed });
});

app.MapPost("/api/session/{id}/agent/results", async (string id, HttpRequest req) =>
{
    if (!AuthorizeAgent(req, sessions, id)) return Results.Unauthorized();
    var payload = await JsonSerializer.DeserializeAsync<ScanPayload>(req.Body, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
    if (payload is null) return Results.BadRequest();
    var s = sessions[id];
    s.Results = payload.Results ?? new(); s.Completed = true; s.Connected = true; s.LastSeenUtc = DateTimeOffset.UtcNow;
    return Results.Ok();
});

app.MapPost("/api/session/{id}/start", (string id, ILogger<Program> log) =>
{
    if (!sessions.TryGetValue(id, out var s)) return Results.NotFound();
    if (!s.Connected) return Results.Conflict(new { message = "Agent is not connected." });
    s.Started = true; s.Completed = false; s.Results = new();
    log.LogInformation("START PC CHECK accepted for player {Player}", s.Player);
    return Results.Ok(new { started = true, player = s.Player });
});

app.Run();

static bool AuthorizeAgent(HttpRequest req, ConcurrentDictionary<string, Session> sessions, string id)
{
    if (!sessions.TryGetValue(id, out var s)) return false;
    if (!req.Headers.TryGetValue("X-NGPVP-Agent-Token", out var token)) return false;
    return CryptographicOperations.FixedTimeEquals(Encoding.UTF8.GetBytes(token.ToString()), Encoding.UTF8.GetBytes(s.AgentToken));
}

sealed class Session(string id, string agentToken, string player)
{
    public string Id { get; } = id;
    public string AgentToken { get; } = agentToken;
    public string Player { get; } = player;
    public bool Connected { get; set; } = true;
    public bool Started { get; set; }
    public bool Completed { get; set; }
    public DateTimeOffset LastSeenUtc { get; set; } = DateTimeOffset.UtcNow;
    public List<ScanItem> Results { get; set; } = new();
}
sealed class ScanPayload { public List<ScanItem>? Results { get; set; } }
sealed record ScanItem(string Category, string Title, string Severity, string Details);

using Microsoft.Data.Sqlite;
using SteamKit2;
using SteamKit2.Authentication;
using SteamKit2.Internal;
using DotNetEnv;

Env.Load();

var steamUser = Environment.GetEnvironmentVariable("STEAM_USER") ?? throw new Exception("Missing STEAM_USER in .env");
var steamPass = Environment.GetEnvironmentVariable("STEAM_PASS") ?? throw new Exception("Missing STEAM_PASS in .env");

var steamConfig = new SteamConfig(
    User: steamUser,
    Pass: steamPass,
    GuardFile: "steam_guard.json"
);

var builder = WebApplication.CreateBuilder(args);

builder.Logging.ClearProviders();
builder.Services.AddSingleton(steamConfig);
builder.Services.AddHostedService<SteamWorker>();

var app = builder.Build();

using (var db = new SqliteConnection("Data Source=nerts.db")) {
    db.Open();
    new SqliteCommand("PRAGMA journal_mode=WAL;CREATE TABLE IF NOT EXISTS History (Ts INT, Name TEXT, Cur INT, Max INT, LobbyID TEXT, Region TEXT);", db).ExecuteNonQuery();
}

app.MapGet("/", (HttpContext context) => {
    var cacheBuster = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
    int lobbyCount;
    lock (Global.State) lobbyCount = Global.State.Count;

    context.Response.Headers.Append("Cache-Control", "no-cache, no-store, must-revalidate");
    context.Response.Headers.Append("Expires", "0");

    var html = $$"""
    <!DOCTYPE html>
    <html lang="en">
    <head>
        <title>NERTS! Online Status</title>
        <meta charset="utf-8">
        <meta name="viewport" content="width=device-width, initial-scale=1">
        <script src="https://unpkg.com/htmx.org@1.9.10"></script>
        <style>
            :root {
                --bg-dark: #034528;
                --bg-gradient-start: #06603b;
                --bg-gradient-end: #02301c;
                --glass-border: rgba(134, 239, 172, 0.3);
                --glass-bg: rgba(6, 96, 59, 0.4);
                --text-chrome-top: #e6fffa;
                --text-chrome-bot: #5eead4;
            }

            body {
                font-family: 'Segoe UI', Verdana, sans-serif;
                background-color: var(--bg-dark);
                margin: 0;
                height: 100vh;
                display: flex;
                flex-direction: column;
                justify-content: center;
                align-items: center;
                overflow: hidden;
                color: white;
            }

            /* COMPLEX BACKGROUND PATTERN GENERATION */
            .bg-mesh {
                position: fixed;
                top: 0; left: 0; right: 0; bottom: 0;
                z-index: -1;
                /* This creates the dot/mesh pattern using CSS gradients */
                background-image:
                    radial-gradient(rgba(255,255,255,0.1) 1px, transparent 1px),
                    radial-gradient(rgba(255,255,255,0.1) 1px, transparent 1px);
                background-size: 4px 4px;
                background-position: 0 0, 2px 2px;
                /* Vignette overlay */
                mask-image: radial-gradient(circle at center, black 40%, rgba(0,0,0,0.4) 100%);
                -webkit-mask-image: radial-gradient(circle at center, black 40%, rgba(0,0,0,0.4) 100%);
            }

            /* The Horizontal "Glass" Strip */
            .banner-strip {
                width: 100%;
                padding: 2rem 0;
                background: linear-gradient(90deg, transparent 0%, var(--glass-bg) 20%, var(--glass-bg) 80%, transparent 100%);
                border-top: 1px solid var(--glass-border);
                border-bottom: 1px solid var(--glass-border);
                display: flex;
                flex-direction: column;
                align-items: center;
                position: relative;
                backdrop-filter: blur(2px);
                box-shadow: 0 0 50px rgba(0,0,0,0.5);
            }

            /* Typography */
            .logo-container {
                text-align: center;
                position: relative;
                margin-bottom: 1rem;
            }

            .logo-text {
                font-family: Impact, sans-serif;
                font-style: italic;
                font-size: clamp(4rem, 10vw, 7rem); /* Responsive giant text */
                margin: 0;
                line-height: 0.9;
                letter-spacing: 2px;
                text-transform: uppercase;

                /* The "Chrome" Effect */
                background: linear-gradient(to bottom, #fff 20%, #4ade80 50%, #065f46 100%);
                -webkit-background-clip: text;
                background-clip: text;
                color: transparent;

                /* The Outline */
                -webkit-text-stroke: 2px #a7f3d0;
                filter: drop-shadow(0 4px 0px rgba(0,0,0,0.3));
            }

            /* Inner Pattern inside the text (Advanced CSS) */
            .logo-text::after {
                content: 'NERTS!';
                position: absolute;
                left: 0; top: 0; width: 100%; height: 100%;
                background-image: radial-gradient(rgba(0,0,0,0.2) 1px, transparent 1px);
                background-size: 3px 3px;
                -webkit-background-clip: text;
                background-clip: text;
                color: transparent;
                z-index: 1;
                pointer-events: none;
            }

            .subtitle {
                font-family: 'Segoe UI', sans-serif;
                font-weight: 300;
                font-style: italic;
                font-size: 1.5rem;
                letter-spacing: 0.5em;
                color: #6ee7b7; /* Light mint */
                text-shadow: 0 2px 4px rgba(0,0,0,0.5);
                margin-left: 0.5em; /* Optical centering due to spacing */
                text-transform: uppercase;
            }

            /* Lobby List Styling */
            .lobby-area {
                margin-top: 3rem;
                width: 100%;
                max-width: 700px;
                text-align: center;
            }

            .section-header {
                font-size: 0.9rem;
                color: #6ee7b7;
                letter-spacing: 2px;
                text-transform: uppercase;
                opacity: 0.8;
                margin-bottom: 1rem;
                border-bottom: 1px solid rgba(110, 231, 183, 0.3);
                padding-bottom: 5px;
                display: inline-block;
            }

            table {
                width: 100%;
                border-collapse: collapse;
                font-family: monospace;
                font-size: 0.9rem;
            }

            tr { transition: background 0.2s; border-bottom: 1px solid rgba(255,255,255,0.05); }
            tr:hover { background: rgba(255,255,255,0.1); cursor: default; }

            td { padding: 8px 12px; text-align: left; color: #d1fae5; }
            td.region { text-align: right; opacity: 0.6; }
            td.count { text-align: center; width: 60px; }

            .loading { font-style: italic; color: #6ee7b7; opacity: 0.5; }

            /* Open Graph Tags */
            .og-image-container { display: none; }
        </style>

        <meta property="og:title" content="NERTS! Online Lobbies">
        <meta property="og:description" content="Live status: {{lobbyCount}} active lobbies.">
        <meta property="og:image" content="/{{cacheBuster}}.svg">
        <meta property="og:image:width" content="600">
        <meta property="og:image:height" content="315">
        <meta name="twitter:card" content="summary_large_image">
    </head>
    <body>
        <div class="bg-mesh"></div>

        <div class="banner-strip">
            <div class="logo-container">
                <h1 class="logo-text">NERTS!</h1>
                <div class="subtitle">ONLINE</div>
            </div>
        </div>

        <div class="lobby-area">
            <div id="lobby-container"
                 hx-get="/lobbies"
                 hx-trigger="load, every 15s"
                 hx-swap="innerHTML">
                 <div class="loading">Searching for games...</div>
            </div>
        </div>
    </body>
    </html>
    """;

    return Results.Content(html, "text/html");
});

app.MapGet("/lobbies", () => {
    List<string[]> currentLobbies;
    lock(Global.State) currentLobbies = new List<string[]>(Global.State);

    if (currentLobbies.Count == 0)
        return Results.Content("<div style='padding:1rem; color:#6ee7b7; opacity:0.5; letter-spacing:1px;'>NO ACTIVE LOBBIES FOUND</div>", "text/html");

    var rows = currentLobbies.Select(l => $$"""
        <tr>
            <td style="font-weight:bold">{{System.Net.WebUtility.HtmlEncode(l[0])}}</td>
            <td class="count">{{l[1]}}/{{l[2]}}</td>
            <td class="region">{{l[3]}}</td>
        </tr>
    """);

    string html = $$"""
        <table>
            {{string.Join("", rows)}}
        </table>
        <div style="margin-top:10px; font-size:0.7rem; color:#047857;">
            UPDATED: {{DateTime.UtcNow:HH:mm:ss}} UTC
        </div>
    """;

    return Results.Content(html, "text/html");
});

app.MapGet("/{buster}.svg", (string buster) => {
    List<string[]> lobbies;
    lock(Global.State) lobbies = new List<string[]>(Global.State);

    const int width = 600;
    const int rowHeight = 30;
    const int headerHeight = 160; // Taller header for the logo

    int contentHeight = lobbies.Count == 0 ? 50 : (lobbies.Count * rowHeight);
    int totalHeight = headerHeight + contentHeight + 40;

    string content;

    if (lobbies.Count == 0) {
        content = $"""<text x="{width/2}" y="{headerHeight + 30}" class="empty" text-anchor="middle">NO LOBBIES FOUND</text>""";
    } else {
        var textLines = lobbies.Select((l, index) => {
            int y = headerHeight + 25 + (index * rowHeight);
            string name = l[0].Length > 30 ? l[0][..29] + "..." : l[0];
            string region = l[3].Split('[')[0].Trim();

            // Alternating row background opacity
            string bg = index % 2 == 0 ?
                $"<rect x='50' y='{y-18}' width='{width-100}' height='24' fill='#ffffff' fill-opacity='0.05'/>" : "";

            return $$"""
                {{bg}}
                <text x="60" y="{{y}}" class="row-text name">{{System.Net.WebUtility.HtmlEncode(name)}}</text>
                <text x="200" y="{{y}}" class="row-text dim">{{l[1]}}/{{l[2]}}</text>
                <text x="{{width-60}}" y="{{y}}" class="row-text region" text-anchor="end">{{region}}</text>
            """;
        });
        content = string.Join("\n", textLines);
    }

    string svg = $$"""
    <svg fill="none" viewBox="0 0 {{width}} {{totalHeight}}" width="{{width}}" height="{{totalHeight}}" xmlns="http://www.w3.org/2000/svg">
        <defs>
            <radialGradient id="bgGrad" cx="50%" cy="50%" r="50%" fx="50%" fy="50%">
                <stop offset="0%" style="stop-color:#06603b;stop-opacity:1" />
                <stop offset="100%" style="stop-color:#022c19;stop-opacity:1" />
            </radialGradient>

            <linearGradient id="chromeGrad" x1="0" x2="0" y1="0" y2="1">
                <stop offset="0%" stop-color="#ffffff" />
                <stop offset="45%" stop-color="#86efac" />
                <stop offset="55%" stop-color="#059669" />
                <stop offset="100%" stop-color="#022c19" />
            </linearGradient>

            <pattern id="mesh" width="4" height="4" patternUnits="userSpaceOnUse">
                 <circle cx="2" cy="2" r="1" fill="white" fill-opacity="0.1" />
            </pattern>
        </defs>

        <style>
            @import url('https://fonts.googleapis.com/css2?family=Roboto:ital,wght@1,900&amp;display=swap');
            .title { font-family: Impact, sans-serif; font-style: italic; font-weight: 900; font-size: 80px; fill: url(#chromeGrad); stroke: #6ee7b7; stroke-width: 2px; }
            .subtitle { font-family: sans-serif; font-style: italic; font-size: 20px; fill: #6ee7b7; letter-spacing: 8px; font-weight: 300; }

            .row-text { font-family: monospace; font-size: 14px; fill: #d1fae5; }
            .name { font-weight: bold; }
            .dim { fill: #6ee7b7; opacity: 0.7; }
            .region { fill: #34d399; }

            .empty { fill: #6ee7b7; font-family: sans-serif; font-style: italic; letter-spacing: 2px; opacity: 0.5; }

            .strip-border { stroke: #6ee7b7; stroke-width: 1; stroke-opacity: 0.4; }
            .strip-bg { fill: #ffffff; fill-opacity: 0.05; }
        </style>

        <rect width="100%" height="100%" fill="url(#bgGrad)" />
        <rect width="100%" height="100%" fill="url(#mesh)" />

        <rect x="0" y="30" width="{{width}}" height="110" class="strip-bg" />
        <line x1="0" y1="30" x2="{{width}}" y2="30" class="strip-border" />
        <line x1="0" y1="140" x2="{{width}}" y2="140" class="strip-border" />

        <text x="{{width/2}}" y="105" text-anchor="middle" class="title">NERTS!</text>
        <text x="{{width/2}}" y="130" text-anchor="middle" class="subtitle">ONLINE</text>

        <line x1="150" y1="175" x2="{{width-150}}" y2="175" stroke="#6ee7b7" stroke-width="1" stroke-opacity="0.2" />

        {{content}}
    </svg>
    """;

    return Results.Text(svg, "image/svg+xml");
});

app.Run();

public static class Global { public static List<string[]> State = new(); }

public record SteamConfig(string User, string Pass, string GuardFile);

public class SteamWorker : BackgroundService {
    private const uint APPID = 1131190;

    private readonly SteamConfig _config;
    private SteamClient _client;
    private CallbackManager _manager;
    private SteamUser _user;
    private SteamMatchmaking _match;

    private bool _isLoggedOn = false;
    private bool _isConnecting = false;

    public SteamWorker(SteamConfig config) {
        _config = config;
        _client = new SteamClient();
        _manager = new CallbackManager(_client);
        _user = _client.GetHandler<SteamUser>()!;
        _match = _client.GetHandler<SteamMatchmaking>()!;
    }

    protected override async Task ExecuteAsync(CancellationToken ct) {
        _manager.Subscribe<SteamClient.ConnectedCallback>(OnConnected);
        _manager.Subscribe<SteamClient.DisconnectedCallback>(OnDisconnected);
        _manager.Subscribe<SteamUser.LoggedOnCallback>(OnLoggedOn);

        _client.Connect();
        _isConnecting = true;

        var nextScan = DateTime.MinValue;

        while (!ct.IsCancellationRequested) {
            _manager.RunWaitCallbacks(TimeSpan.FromMilliseconds(50));
            if (_isLoggedOn && DateTime.UtcNow > nextScan) {
                await ExecuteAsync();
                nextScan = DateTime.UtcNow.AddSeconds(15);
            }
        }
        _client.Disconnect();
    }

    private async void OnConnected(SteamClient.ConnectedCallback cb) {
        _isConnecting = false;

        try {
            string? previousGuardData = null;

            if (File.Exists(_config.GuardFile)) {
                previousGuardData = await File.ReadAllTextAsync(_config.GuardFile);
            }

            var authSession = await _client.Authentication.BeginAuthSessionViaCredentialsAsync(new AuthSessionDetails {
                Username = _config.User,
                Password = _config.Pass,
                IsPersistentSession = true,
                GuardData = previousGuardData,
                Authenticator = new UserConsoleAuthenticator()
            });

            var result = await authSession.PollingWaitForResultAsync();

            if (result.NewGuardData != null) {
                await File.WriteAllTextAsync(_config.GuardFile, result.NewGuardData);
            }

            _user.LogOn(new SteamUser.LogOnDetails {
                Username = result.AccountName,
                AccessToken = result.RefreshToken,
                ShouldRememberPassword = true
            });
        }
        catch (Exception ex) {
            if (File.Exists(_config.GuardFile)) File.Delete(_config.GuardFile);
            _client.Disconnect();
        }
    }

    private void OnLoggedOn(SteamUser.LoggedOnCallback cb) {
        if (cb.Result == EResult.OK) {
            _isLoggedOn = true;
            _client.GetHandler<SteamFriends>()!.SetPersonaState(EPersonaState.Online);
        }
    }

    private void OnDisconnected(SteamClient.DisconnectedCallback cb) {
        _isLoggedOn = false;
        if (!_isConnecting) {
            Thread.Sleep(5000);
            _client.Connect();
            _isConnecting = true;
        }
    }

    private async Task ExecuteAsync() {
        try {
            _client.Send(new ClientMsgProtobuf<CMsgClientGamesPlayed>(EMsg.ClientGamesPlayed) {
                Body = { games_played = { new CMsgClientGamesPlayed.GamePlayed { game_id = APPID } } }
            });

            var job = _match.GetLobbyList(APPID, null);
            if (job == null) return;

            var task = job.ToTask();
            var timeout = DateTime.UtcNow.AddSeconds(10);

            while (!task.IsCompleted) {
                if (DateTime.UtcNow > timeout) break;
                _manager.RunWaitCallbacks(TimeSpan.FromMilliseconds(50));
            }

            if (task.IsCompleted && task.Result.Result == EResult.OK) {
                var lobbies = task.Result.Lobbies;

                var rows = new List<string[]>();
                foreach(var l in lobbies) {
                    string pingStr = l.Metadata.TryGetValue("PING_LOCATION", out var p) ? p : "";
                    string prettyRegion = RegionMapper.GetRegion(pingStr);

                    string name = l.Metadata.TryGetValue("LOBBY_NAME", out var n) ? n : "Unknown";
                    rows.Add([name, l.NumMembers.ToString(), l.MaxMembers.ToString(), prettyRegion, l.SteamID.ToString()]);
                }

                using (var db = new SqliteConnection("Data Source=nerts.db")) {
                    db.Open();
                    using var tx = db.BeginTransaction();
                    var cmd = db.CreateCommand();
                    cmd.CommandText = "INSERT INTO History (Ts, Name, Cur, Max, LobbyID, Region) VALUES ($ts, $host, $cur, $max, $lid, $region)";
                    cmd.Parameters.AddWithValue("$ts", DateTimeOffset.UtcNow.ToUnixTimeSeconds());
                    var pHost = cmd.Parameters.AddWithValue("$host", "");
                    var pCur = cmd.Parameters.AddWithValue("$cur", 0);
                    var pMax = cmd.Parameters.AddWithValue("$max", 0);
                    var pLid = cmd.Parameters.AddWithValue("$lid", "");
                    var pRegion = cmd.Parameters.AddWithValue("$region", "");

                    foreach(var r in rows) {
                        pHost.Value = r[0];
                        pCur.Value = int.Parse(r[1]);
                        pMax.Value = int.Parse(r[2]);
                        pLid.Value = r[4];
                        pRegion.Value = r[3];
                        cmd.ExecuteNonQuery();
                    }
                    tx.Commit();
                }
                lock(Global.State) Global.State = rows;
            }
        } catch (Exception ex) { }
    }
}

public static class RegionMapper {
    public static string GetRegion(string? rawData) {
        if (string.IsNullOrEmpty(rawData)) return "Unknown";

        var bestCode = rawData.Split(',')
            .Select(segment => segment.Split('='))
            .Where(parts => parts.Length == 2)
            .Select(parts => new {
                Code = new string(parts[0].TakeWhile(char.IsLetter).ToArray()),
                Ping = int.TryParse(new string(parts[1].TakeWhile(char.IsDigit).ToArray()), out int p) ? p : 999
            })
            .OrderBy(x => x.Ping)
            .FirstOrDefault()?.Code ?? "unknown";

        return _names.TryGetValue(bestCode, out var name) ? $"{name} [{bestCode.ToUpper()}]" : bestCode.ToUpper();
    }

    private static readonly Dictionary<string, string> _names = new() {
        { "iad", "US East" },      { "ord", "US Central" },   { "dfw", "US South Central" },
        { "lax", "US West" },      { "sea", "US North West" },{ "atl", "US South East" },
        { "lhr", "London" },       { "fra", "Frankfurt" },    { "par", "Paris" },
        { "ams", "Amsterdam" },    { "sto", "Stockholm" },    { "waw", "Warsaw" },
        { "vie", "Vienna" },       { "mad", "Madrid" },       { "sgp", "Singapore" },
        { "hkg", "Hong Kong" },    { "tyo", "Tokyo" },        { "syd", "Sydney" },
        { "gru", "Brazil" },       { "scl", "Chile" },        { "lim", "Peru" },
        { "jnb", "Johannesburg" }, { "dxb", "Dubai" },        { "bom", "Mumbai" }
    };
}

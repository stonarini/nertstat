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

app.UseStaticFiles();

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
        @font-face {
            font-family: 'Roboto Slab';
            font-style: medium;
            font-weight: 500;
            src: url('RobotoSlab-Medium.woff2') format('woff2');
        }
        body {
            background-image: image-set( url('background.avif') type('image/avif'), url('background.webp') type('image/webp'));
            background-size: contain;
            background-position: center top;
            background-repeat: repeat;

            width: 100vw;
            min-height: 100vh;
            margin: 0;
            padding: 0;

            font-family: 'Roboto Slab', serif;
            font-weight: 500;
        }

        table {
            border-collapse: collapse;
            width: 630px;
            height: 60px;
        }

        table tr {
            background-image: url('/line.webp');
            background-repeat: no-repeat;
            background-size: 630px 60px;
            background-position: center;
        }

        table tr:hover {
            background-image: url('/hover-line.webp');
            cursor: pointer;
        }
        </style>

        <meta property="og:title" content="NERTS! Online Lobbies">
        <meta property="og:description" content="Live status: {{lobbyCount}} active lobbies.">
        <meta property="og:image" content="/{{cacheBuster}}.svg">
        <meta property="og:image:width" content="600">
        <meta property="og:image:height" content="315">
        <meta name="twitter:card" content="summary_large_image">
    </head>
    <body>
        <a href="https://github.com/stonarini/nertstat" target="_blank" style="position:absolute; top:20px; right:15px;" >
            <img src="help.webp" />
        </a>
        <div style="position: absolute; top: 80%; left: 50%; transform: translate(-50%, -50%)">
            <div id="lobby-container"
                 style="max-height: 180px; overflow-y: scroll;"
                 hx-get="/lobbies"
                 hx-trigger="load, every 15s"
                 hx-swap="innerHTML">
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
        return Results.Content("<div style='opacity: 0.9; color: #74c3b2; font-size: 20px;'>NO LOBBIES FOUND</div>", "text/html");

    var rows = currentLobbies.Select(l => {
        int.TryParse(l[1], out int currentPlayers);
        int.TryParse(l[2], out int maxCapacity);

        var activeIcons = string.Concat(Enumerable.Repeat(
            "<img src='/ap.webp'>",
            currentPlayers
        ));

        int freeCount = Math.Max(0, maxCapacity - currentPlayers);
        var freeIcons = string.Concat(Enumerable.Repeat(
            "<img src='/ap.webp' style='opacity: 0.5;'>",
            freeCount
        ));

        return $$"""
            <tr>
                <td style="width: 70px;"></td>
                <td style="opacity: 0.9; color: #74c3b2; font-size: 16px; text-transform: uppercase;">
                    <span style="font-size: 15px; padding-right: 30px; text-transform: lowercase;">{{l[3]}}</span>
                    {{System.Net.WebUtility.HtmlEncode(l[0])}}
                </td>
                <td style="display: flex; justify-content: end; align-items: center; height: 60px; gap: 5px;">
                    {{activeIcons}}{{freeIcons}}
                </td>
                <td style="width: 70px;"></td>
            </tr>
        """;
    });

    string html = $$"""
        <div style='opacity: 0.9; color: #74c3b2; font-size: 20px; margin-bottom: 18px; text-align: center;'>JOIN A LOBBY</div>
        <table>
            {{string.Join("", rows)}}
        </table>
    """;

    return Results.Content(html, "text/html");
});

app.MapGet("/{buster}.svg", (string buster) => {
    List<string[]> lobbies;
    lock(Global.State) lobbies = new List<string[]>(Global.State);

    const int width = 600;
    const int rowHeight = 30;
    const int headerHeight = 160;

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
            .FirstOrDefault()?.Code ?? "?";

        return bestCode.ToString();
    }
}

using Microsoft.Data.Sqlite;
using SteamKit2;
using SteamKit2.Authentication;
using SteamKit2.Internal;


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
        catch (Exception _) {
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
        } catch (Exception _) { }
    }
}

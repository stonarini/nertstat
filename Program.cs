using Microsoft.Data.Sqlite;
using SteamKit2;
using SteamKit2.Authentication;
using SteamKit2.Internal;
using System.Text;
using System.Xml.Serialization;
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

ApiEndpoints.Map(app);
app.UseStaticFiles();

app.Run();


using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Processing;
using SixLabors.ImageSharp.Drawing.Processing;
using SixLabors.ImageSharp.Formats.Png;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.Fonts;


var builder = WebApplication.CreateBuilder(args);
var app = builder.Build();


app.MapGet("/", (HttpContext context) =>
{
    var cacheBuster = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
    var lobbyCount = 0;

    context.Response.Headers.Append("Cache-Control", "no-cache, no-store, must-revalidate");
    context.Response.Headers.Append("Expires", "0");

    var html = $$$"""
    <!DOCTYPE html>
    <html lang="en">
    <head>
        <title>nertstat</title>
        <meta charset="utf-8">
        <meta name="viewport" content="width=device-width, initial-scale=1">

        <script src="https://unpkg.com/htmx.org@1.9.10"></script>

        <meta property="og:title" content="NERTS! Online Lobbies: {{{lobbyCount}}}">
        <meta property="og:description" content="Live status of NERTS! Online lobbies.">
        <meta property="og:image" content="/{{{cacheBuster}}}.png">
        <meta property="og:image:width" content="300">
        <meta property="og:image:height" content="150">
    </head>
    <body>
        <div class="container">
            <h1>NERTS! Online Lobbies</h1>

            <div id="lobby-list"
                 hx-get="/lobbies"
                 hx-trigger="load, every 15s">
                <p>Loading...</p>
            </div>
        </div>
    </body>
    </html>
    """;

    return Results.Content(html, "text/html");
});

app.MapGet("/lobbies", (HttpContext context) =>
{
    context.Response.Headers.Append("Cache-Control", "no-cache, no-store, must-revalidate");
    context.Response.Headers.Append("Expires", "0");

    return Results.Content("<ul><li>No active lobbies found.</li></ul>", "text/html");
});

app.MapGet("/{whatever}.png", async (HttpContext context, string whatever) =>
{
    var fontFamily = SystemFonts.Families.FirstOrDefault();

    var titleFont = fontFamily.CreateFont(16, FontStyle.Bold);
    var bodyFont = fontFamily.CreateFont(14, FontStyle.Regular);

    var bgColor = Color.White;
    var textColor = Color.Black;
    int width = 300;
    int padding = 10;
    int lineHeight = 20;
    int titleHeight = 25;

    int lobbyLines = Math.Max(1, 0);
    int height = padding + titleHeight + (lobbyLines * lineHeight) + padding;

    var stream = new MemoryStream();
    using (var image = new Image<Rgba32>(width, height))
    {
        image.Mutate(ctx => ctx.Fill(bgColor));

        image.Mutate(ctx => ctx.DrawText(
            "NERTS! Online Lobbies",
            titleFont,
            textColor,
            new PointF(padding, padding))
        );

        var yPos = padding + titleHeight;

        image.Mutate(ctx => ctx.DrawText(
            "No active lobbies found.",
            bodyFont,
            textColor,
            new PointF(padding + 15, yPos))
        );

        await image.SaveAsync(stream, new PngEncoder());
    }

    stream.Position = 0;

    context.Response.Headers.Append("Cache-Control", "no-cache, no-store, must-revalidate");
    context.Response.Headers.Append("Pragma", "no-cache");
    context.Response.Headers.Append("Expires", "0");

    return Results.File(stream, "image/png");
});

app.Run();

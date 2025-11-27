using System.Text;
using Svg.Skia;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Gif;
using SkiaSharp;

public static class ApiEndpoints
{
    public static void Map(WebApplication app)
    {
        app.MapGet("/classic", (HttpContext context) => {
            var cacheBuster = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            int lobbyCount;
            lock (Global.State) lobbyCount = Global.State.Count;

            context.Response.Headers.Append("Cache-Control", "no-cache, no-store, must-revalidate");
            context.Response.Headers.Append("Expires", "0");

            var html = $$"""
            <!DOCTYPE html>
            <html lang="en">
            <head>
                <title>nertstat - classic</title>
                <meta charset="utf-8">
                <script src="/htmx.min.js"></script>
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
                <meta property="og:image" content="/{{cacheBuster}}.gif">
                <meta property="og:image:width" content="600">
                <meta property="og:image:height" content="315">
                <meta name="twitter:card" content="summary_large_image">
                <link rel="apple-touch-icon" sizes="180x180" href="/apple-touch-icon.png">
                <link rel="icon" type="image/png" sizes="32x32" href="/favicon-32x32.png">
                <link rel="icon" type="image/png" sizes="16x16" href="/favicon-16x16.png">
                <link rel="manifest" href="/site.webmanifest">
            </head>
            <body>
                <a href="https://github.com/stonarini/nertstat" target="_blank" style="position:absolute; top:20px; right:15px;" >
                    <img src="help.webp" />
                </a>
                <div style="position: absolute; top: 80%; left: 50%; transform: translate(-50%, -50%)">
                    <div id="lobby-container"
                         style="max-height: 180px; overflow-y: scroll;"
                         hx-get="/lobbies_classic"
                         hx-trigger="load, every 15s"
                         hx-swap="innerHTML">
                    </div>
                </div>
            </body>
            </html>
            """;

            return Results.Content(html, "text/html");
        });

        app.MapGet("/", (HttpContext context) => {
            List<LobbyInfo> currentLobbies;
            lock (Global.State) {
                currentLobbies = Global.State.Select(l => new LobbyInfo(l[0], int.Parse(l[1]), int.Parse(l[2]), l[3], l[4])).ToList();
            }

            var acceptHeader = context.Request.Headers.Accept.ToString().ToLowerInvariant();

            switch (acceptHeader) {
                case var s when s.Contains("text/html"):
                    return Results.Content(RenderHtml(currentLobbies), "text/html");
                case var s when s.Contains("application/json"):
                    return Results.Ok(currentLobbies);
                case var s when s.Contains("application/xml"):
                    return Results.Content(XmlHelper.ToXml(currentLobbies), "application/xml");
                case var s when s.Contains("text/plain"):
                    if (currentLobbies.Count == 0)
                        return Results.Text("NO LOBBIES FOUND");
                    var textRows = currentLobbies.Select(l => $"{l.Region,-10} {l.Name, -10} {l.CurrentPlayers}/{l.MaxPlayers,-2}");
                    return Results.Text(string.Join("\n", textRows), "text/plain");
                default:
                    return Results.Content(RenderHtml(currentLobbies), "text/html");
            }
        });

        app.MapGet("/lobbies_classic", () => {
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

        app.MapGet("/docs", () => {
            var html = """
            <!DOCTYPE html>
            <html lang="en">
            <head>
                <title>nertstat - docs</title>
                <meta charset="utf-8">
                <meta name="viewport" content="width=device-width, initial-scale=1">
                <link rel="apple-touch-icon" sizes="180x180" href="/apple-touch-icon.png">
                <link rel="icon" type="image/png" sizes="32x32" href="/favicon-32x32.png">
                <link rel="icon" type="image/png" sizes="16x16" href="/favicon-16x16.png">
                <link rel="manifest" href="/site.webmanifest">
                <style>
                    :root {
                        --bg-color: #f0f0f0;
                        --text-color: #0c0c0c;
                        --link-color: #005f7f;
                        --code-bg-color: #e0e0e0;
                    }
                    @media (prefers-color-scheme: dark) {
                        :root {
                            --bg-color: #0c0c0c;
                            --text-color: #f0f0f0;
                            --link-color: #00bfff;
                            --code-bg-color: #2a2a2a;
                        }
                    }
                    body { font-family: monospace; background-color: var(--bg-color); color: var(--text-color); max-width: 800px; margin: 2rem auto; padding: 0 1rem; transition: background-color 0.3s, color 0.3s; }
                    a { color: var(--link-color); }
                    h1, h2 { border-bottom: 1px solid var(--text-color); padding-bottom: 5px; }
                    code { background-color: var(--code-bg-color); padding: 2px 6px; border-radius: 4px; font-family: monospace; }
                    pre { background-color: var(--code-bg-color); padding: 1rem; border-radius: 4px; white-space: pre-wrap; }
                </style>
            </head>
            <body>
                <h1>nertstat</h1>
                <h2>Primary Endpoint: <code><a href="/">/</a></code></h2>
                <p>The primary endpoint can serve data in multiple formats based on the HTTP <code>Accept</code> header.</p>
                <ul>
                    <li><strong>Default (HTML):</strong> <code>Accept: text/html</code> or no header. Returns a minimal HTML interface.</li>
                    <li><strong>JSON:</strong> <code>Accept: application/json</code>. Returns a JSON array of lobby objects.</li>
                    <li><strong>XML:</strong> <code>Accept: application/xml</code>. Returns an XML representation of the lobbies.</li>
                    <li><strong>Plain Text:</strong> <code>Accept: text/plain</code>. Returns a simple text-based list of lobbies.</li>
                </ul>

                <h3>Example Usage with cURL:</h3>
                <pre><code># For JSON
            curl -H "Accept: application/json" https://nertstat.dost.pt/

            # For XML
            curl -H "Accept: application/xml" https://nertstat.dost.pt/

            # For Plain Text
            curl -H "Accept: text/plain" https://nertstat.dost.pt/</code></pre>

                <h2>Other Endpoints</h2>
                <ul>
                    <li><code><a href="/classic">/classic</a></code>: This is a "replica" of the NERTS! Online main page.</li>
                    <li><code><a href="/image.gif">/{timestamp}.gif</a></code>: A dynamically generated image of the current lobby status. Really it works with any "name", since it's just a trick to invalidate caches.</li>
                </ul>
            </body>
            </html>
            """;
            return Results.Content(html, "text/html");
        });


        app.MapGet("/{buster}.gif", (string buster) => {
            List<LobbyInfo> lobbies;
            lock(Global.State) { lobbies = Global.State.Select(l => new LobbyInfo(l[0], int.Parse(l[1]), int.Parse(l[2]), l[3], l[4])).ToList(); }

            const int width = 600;
            const int rowHeight = 35;
            const int headerHeight = 80;
            const int footerHeight = 40;
            const int totalHeight = 315;

            int availableHeight = totalHeight - headerHeight - footerHeight;
            int maxLobbies = availableHeight > 0 ? availableHeight / rowHeight : 0;
            if (lobbies.Count > maxLobbies) {
                lobbies = lobbies.GetRange(0, maxLobbies);
            }
            int lobbyCount = lobbies.Count;

            var lobbyRows = new StringBuilder();
            if (lobbyCount == 0) {
                lobbyRows.Append($"<text x='{width / 2}' y='{headerHeight + (totalHeight - headerHeight) / 2}' class='empty-message'>No active lobbies</text>");
            } else {
                for (int i = 0; i < lobbies.Count; i++)
                {
                    var lobby = lobbies[i];
                    int y = headerHeight + 30 + (i * rowHeight);
                    string name = System.Net.WebUtility.HtmlEncode(lobby.Name.Length > 35 ? lobby.Name.Substring(0, 32) + "..." : lobby.Name);
                    string region = lobby.Region.Split('[')[0].Trim();

                    lobbyRows.Append($$"""
                        <g class='lobby-row'>
                            <text x='20' y='{{y}}' class='lobby-name'>{{name}}</text>
                            <text x='450' y='{{y}}' class='lobby-players'>{{lobby.CurrentPlayers}}/{{lobby.MaxPlayers}}</text>
                            <text x='580' y='{{y}}' class='lobby-region'>{{region}}</text>
                        </g>
                    """);
                }
            }

            string svgElem = $$$"""
            <svg width='{{{width}}}' height='{{{totalHeight}}}' viewBox='0 0 {{{width}}} {{{totalHeight}}}' fill='none' xmlns='http://www.w3.org/2000/svg'>
                <title>nertstat</title>
                <desc>A dynamically generated list of active game lobbies for NERTS! Online. There are currently {{{lobbyCount}}} active lobbies.</desc>
                <style>
                    .background { fill: #111827; }
                    .header { font-family: -apple-system, BlinkMacSystemFont, "Segoe UI", Roboto, Helvetica, Arial, sans-serif; font-weight: 600; font-size: 28px; fill: #F9FAFB; text-anchor: middle; }
                    .empty-message { font-family: -apple-system, BlinkMacSystemFont, "Segoe UI", Roboto, Helvetica, Arial, sans-serif; font-size: 18px; fill: #9CA3AF; text-anchor: middle; }
                    .lobby-row text { font-family: -apple-system, BlinkMacSystemFont, "Segoe UI", Roboto, Helvetica, Arial, sans-serif; font-size: 16px; fill: #D1D5DB; }
                    .lobby-name { font-weight: 600; }
                    .lobby-players { text-anchor: end; }
                    .lobby-region { text-anchor: end; fill: #6B7280; }
                    .footer-text { font-family: -apple-system, BlinkMacSystemFont, "Segoe UI", Roboto, Helvetica, Arial, sans-serif; font-size: 12px; fill: #4B5563; text-anchor: middle; }
                </style>

                <rect width='100%' height='100%' class='background'/>

                <text x='{{{width / 2}}}' y='50' class='header'>NERTS! Online Lobbies</text>

                {{{lobbyRows}}}

                <text x='{{{width / 2}}}' y='{{{totalHeight - 20}}}' class='footer-text'>nertstat.dost.pt</text>
            </svg>
            """;

            using var svg = new SKSvg();
            svg.Load(new MemoryStream(Encoding.UTF8.GetBytes(svgElem)));
            if (svg is null || svg.Picture is null)
                return Results.NotFound();

            using var bitmap = new SKBitmap((int)svg.Picture.CullRect.Width, (int)svg.Picture.CullRect.Height);
            using var canvas = new SKCanvas(bitmap);
            canvas.Clear(SKColors.Transparent);
            canvas.DrawPicture(svg.Picture);

            using var image = Image.Load(bitmap.Encode(SKEncodedImageFormat.Png, 100).ToArray());
            using var ms = new MemoryStream();
            image.Save(ms, new GifEncoder());
            return Results.Bytes(ms.ToArray(), "image/gif");
        });
    }

    private static string RenderHtml(List<LobbyInfo> lobbies) {
        return $$"""
        <!DOCTYPE html>
        <html lang="en">
        <head>
            <title>nertstat</title>
            <meta charset="utf-8">
            <meta name="viewport" content="width=device-width, initial-scale=1">
            <meta property="og:image" content="/{{DateTimeOffset.UtcNow.ToUnixTimeSeconds()}}.gif">
            <meta property="og:image:width" content="600">
            <meta property="og:image:height" content="315">
            <link rel="apple-touch-icon" sizes="180x180" href="/apple-touch-icon.png">
            <link rel="icon" type="image/png" sizes="32x32" href="/favicon-32x32.png">
            <link rel="icon" type="image/png" sizes="16x16" href="/favicon-16x16.png">
            <link rel="manifest" href="/site.webmanifest">
            <script src="/htmx.min.js"></script>
            <style>
                :root {
                    --bg-color: #f0f0f0;
                    --text-color: #0c0c0c;
                    --link-color: #005f7f;
                }
                @media (prefers-color-scheme: dark) {
                    :root {
                        --bg-color: #0c0c0c;
                        --text-color: #f0f0f0;
                        --link-color: #00bfff;
                    }
                }
                body { font-family: monospace; background-color: var(--bg-color); color: var(--text-color); margin: 2rem; transition: background-color 0.3s, color 0.3s; }
                pre { white-space: pre; font-family: monospace; }
                .link-button, a { color: var(--link-color); text-decoration: underline; }
                .link-button { background: none; border: none; cursor: pointer; padding: 0; font-family: monospace; }
                #lobbies-container { white-space: pre-wrap; }
            </style>
        </head>
        <body>
            <h1>NERTS! Online Status</h1>
            <div id="lobbies-container"><pre hx-get="/" hx-trigger="load, every 15s" hx-swap="textContent" hx-headers='{"Accept": "text/plain"}'></pre></div>
            <p>View as:
                <button class="link-button" hx-get="/" hx-target="#lobbies-container" hx-swap="textContent" hx-headers='{"Accept": "application/json"}'>JSON</button> |
                <button class="link-button" hx-get="/" hx-target="#lobbies-container" hx-swap="textContent" hx-headers='{"Accept": "application/xml"}'>XML</button> |
                <button class="link-button" hx-get="/" hx-target="#lobbies-container" hx-swap="textContent" hx-headers='{"Accept": "text/plain"}'>Plain Text</button>
            </p>
            <p>Also available: <a href="/classic">Classic View</a> | <a href="/docs">API Docs</a></p>
        </body>
        </html>
        """;
    }
}

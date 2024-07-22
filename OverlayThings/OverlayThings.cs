using System;
using System.Collections.Generic;
using System.Drawing;
using System.Net;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace OverlayThings
{
    public class OverlayThings
    {
        private HttpListener listener;
        private CancellationTokenSource cts;
        private Dictionary<string, MetricForm> metrics;
        private Thread uiThread;
        private SynchronizationContext uiContext;

        private readonly Dictionary<string, Color> namedColors = new Dictionary<string, Color>
        {
            { "red", Color.FromArgb(255, 99, 71) },
            { "blue", Color.FromArgb(70, 130, 180) },
            { "green", Color.FromArgb(50, 205, 50) },
            { "yellow", Color.FromArgb(255, 255, 102) },
            { "orange", Color.FromArgb(255, 165, 0) },
            { "purple", Color.FromArgb(128, 0, 128) },
            { "pink", Color.FromArgb(255, 182, 193) },
            { "cyan", Color.FromArgb(0, 255, 255) },
            { "magenta", Color.FromArgb(255, 0, 255) },
            { "gray", Color.FromArgb(169, 169, 169) },
            { "brown", Color.FromArgb(139, 69, 19) },
            { "teal", Color.FromArgb(0, 128, 128) },
            { "warning", Color.FromArgb(255, 215, 0) },
            { "error", Color.FromArgb(255, 69, 58) },
            { "success", Color.FromArgb(144, 238, 144) }
        };

        private readonly Dictionary<string, (string header, string content, Color color, string lastUpdated)> lastKnownValues = new Dictionary<string, (string, string, Color, string)>();

        public OverlayThings()
        {
            metrics = new Dictionary<string, MetricForm>();
        }

        private void StartUiThread()
        {
            uiThread = new Thread(() =>
            {
                Application.Idle += (sender, e) => uiContext = SynchronizationContext.Current;
                Application.Run();
            });
            uiThread.SetApartmentState(ApartmentState.STA);
            uiThread.Start();

            while (uiContext == null) Thread.Sleep(10);
        }

        private async Task StartServer()
        {
            listener = new HttpListener();
            listener.Prefixes.Add("http://localhost:3000/");
            listener.Start();
            cts = new CancellationTokenSource();

            Console.WriteLine("Server started on http://localhost:3000/");

            try
            {
                while (!cts.Token.IsCancellationRequested)
                {
                    var context = await listener.GetContextAsync();
                    _ = Task.Run(() => HandleRequest(context));
                }
            }
            catch (HttpListenerException ex) when (ex.ErrorCode == 995)
            {
                // Listener stopped
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Server error: {ex.Message}");
            }
        }

        private void HandleRequest(HttpListenerContext context)
        {
            try
            {
                var requestUrl = context.Request.Url.LocalPath.ToLower();
                var query = context.Request.QueryString;
                var responseString = string.Empty;
                byte[] buffer;

                if (requestUrl == "/api/upsert")
                {
                    var key = query["key"];
                    var header = query["header"] ?? lastKnownValues.GetValueOrDefault(key).header;
                    var contentText = query["content"] ?? lastKnownValues.GetValueOrDefault(key).content;
                    var colorInput = query["color"];
                    var color = colorInput != null ? ParseColor(colorInput) : lastKnownValues.GetValueOrDefault(key).color;
                    var lastUpdated = query["lastUpdated"] ?? lastKnownValues.GetValueOrDefault(key).lastUpdated;

                    UpsertMetric(key, header, contentText, color, lastUpdated);

                    responseString = "Upsert successful";
                }
                else if (requestUrl == "/api/remove")
                {
                    var key = query["key"];

                    RemoveMetric(key);

                    responseString = "Remove successful";
                }
                else if (requestUrl == "/")
                {
                    responseString = GenerateHelpPage();
                    context.Response.ContentType = "text/html";
                }
                else
                {
                    responseString = "Invalid endpoint";
                }

                buffer = Encoding.UTF8.GetBytes(responseString);
                context.Response.ContentLength64 = buffer.Length;
                context.Response.OutputStream.Write(buffer, 0, buffer.Length);
                context.Response.OutputStream.Close();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Request handling error: {ex.Message}");
            }
        }

        private Color ParseColor(string colorInput)
        {
            return namedColors.TryGetValue(colorInput.ToLower(), out var namedColor) ? namedColor : Color.Transparent;
        }

        private void UpsertMetric(string key, string header, string content, Color color, string lastUpdated)
        {
            uiContext.Send(_ =>
            {
                if (metrics.ContainsKey(key))
                {
                    metrics[key].UpdateMetric(header, content, lastUpdated, color);
                }
                else
                {
                    var metricForm = new MetricForm();
                    metricForm.UpdateMetric(header, content, lastUpdated, color);
                    metricForm.Show();
                    metrics[key] = metricForm;
                }
                // Update last known values
                lastKnownValues[key] = (header, content, color, lastUpdated);
                Console.WriteLine($"Metric upserted: {key}");
            }, null);
        }

        private void RemoveMetric(string key)
        {
            uiContext.Send(_ =>
            {
                if (metrics.ContainsKey(key))
                {
                    metrics[key].Close();
                    metrics.Remove(key);
                    lastKnownValues.Remove(key); // Remove from last known values
                    Console.WriteLine($"Metric removed: {key}");
                }
            }, null);
        }

        private void StopServer()
        {
            cts?.Cancel();
            listener?.Stop();
            listener?.Close();
            Console.WriteLine("Server stopped.");
        }

        public static async Task<bool> IsServerRunning()
        {
            try
            {
                using (var client = new HttpClient())
                {
                    var response = await client.GetAsync($"http://localhost:3000/");
                    return response.IsSuccessStatusCode;
                }
            }
            catch
            {
                return false;
            }
        }

        public async Task RunAsServer()
        {
            StartUiThread();
            await StartServer();
            Console.WriteLine("Press 'q' to quit.");
            while (Console.ReadLine() != "q") { }
            StopServer();
            uiContext.Send(_ => Application.ExitThread(), null);
        }

        public static async Task RunAsClient(string[] args)
        {
            if (args.Length > 0)
            {
                switch (args[0].ToLower())
                {
                    case "upsert":
                        if (args.Length == 6)
                        {
                            await UpsertMetricCli(args[1], args[2], args[3], args[4], args[5]);
                        }
                        else
                        {
                            Console.WriteLine("Usage: upsert <key> <header> <content> <color> <lastUpdated>");
                        }
                        break;

                    case "remove":
                        if (args.Length == 2)
                        {
                            await RemoveMetricCli(args[1]);
                        }
                        else
                        {
                            Console.WriteLine("Usage: remove <key>");
                        }
                        break;

                    default:
                        Console.WriteLine("Invalid action. Use 'upsert' or 'remove'.");
                        break;
                }
            }
            else
            {
                while (true)
                {
                    var action = Sharprompt.Prompt.Select("Select an action", new[] { "Upsert Metric", "Remove Metric", "Exit" });

                    if (action == "Exit") break;

                    switch (action)
                    {
                        case "Upsert Metric":
                            await UpsertMetricInteractive();
                            break;

                        case "Remove Metric":
                            await RemoveMetricInteractive();
                            break;
                    }
                }
            }
        }

        private static async Task UpsertMetricCli(string key, string header, string content, string color, string lastUpdated)
        {
            var query = $"key={key}&header={header}&content={content}&color={color}&lastUpdated={lastUpdated}";
            using (var client = new HttpClient())
            {
                var response = await client.GetAsync($"http://localhost:3000/api/upsert?{query}");
                var responseString = await response.Content.ReadAsStringAsync();
                Console.WriteLine(responseString);
            }
        }

        private static async Task RemoveMetricCli(string key)
        {
            using (var client = new HttpClient())
            {
                var response = await client.GetAsync($"http://localhost:3000/api/remove?key={key}");
                var responseString = await response.Content.ReadAsStringAsync();
                Console.WriteLine(responseString);
            }
        }

        private static async Task UpsertMetricInteractive()
        {
            var key = Sharprompt.Prompt.Input<string>("Enter the key");
            var header = Sharprompt.Prompt.Input<string>("Enter the header (leave empty to keep current)");
            var content = Sharprompt.Prompt.Input<string>("Enter the content (leave empty to keep current)");
            var color = Sharprompt.Prompt.Input<string>("Enter the color (name or HTML format, leave empty to keep current)");
            var lastUpdated = Sharprompt.Prompt.Input<string>("Enter the last updated timestamp (leave empty to keep current)");

            await UpsertMetricCli(key, header, content, color, lastUpdated);
        }

        private static async Task RemoveMetricInteractive()
        {
            var key = Sharprompt.Prompt.Input<string>("Enter the key to remove");
            await RemoveMetricCli(key);
        }

        private string GenerateHelpPage()
        {
            return @"<!doctype html>
<html lang='en'>
<head>
    <meta charset='utf-8'>
    <meta name='viewport' content='width=device-width, initial-scale=1'>
    <meta name='color-scheme' content='light dark' />
    <link rel='stylesheet' href='https://cdn.jsdelivr.net/npm/@picocss/pico@2/css/pico.min.css'>
    <style>
        .color-card {
            display: flex;
            flex-direction: column;
            align-items: center;
            justify-content: center;
            padding: 1rem;
            color: white;
            border-radius: 0.25rem;
            text-align: center;
            font-weight: bold;
            word-wrap: break-word;
        }
        .color-grid {
            display: grid;
            grid-template-columns: repeat(auto-fit, minmax(150px, 1fr));
            gap: 1rem;
        }
    </style>
    <title>Overlay Things Documentation</title>
</head>
<body>
    <main class='container'>
        <h1>Overlay Things Documentation</h1>
        <p>Welcome to the Overlay Things documentation page! Here you'll find all the information you need to use our features:</p>

        <section>
            <h2>Supported Colors</h2>
            <p>Here is a demo of the supported color values:</p>
            <div class=""color-grid"">
                <article class=""color-card"" style=""background-color: #FF6347;"">red<br>#FF6347</article>
                <article class=""color-card"" style=""background-color: #4682B4;"">blue<br>#4682B4</article>
                <article class=""color-card"" style=""background-color: #32CD32;"">green<br>#32CD32</article>
                <article class=""color-card"" style=""background-color: #FFFF66;"">yellow<br>#FFFF66</article>
                <article class=""color-card"" style=""background-color: #FFA500;"">orange<br>#FFA500</article>
                <article class=""color-card"" style=""background-color: #800080;"">purple<br>#800080</article>
                <article class=""color-card"" style=""background-color: #FFB6C1;"">pink<br>#FFB6C1</article>
                <article class=""color-card"" style=""background-color: #00FFFF;"">cyan<br>#00FFFF</article>
                <article class=""color-card"" style=""background-color: #FF00FF;"">magenta<br>#FF00FF</article>
                <article class=""color-card"" style=""background-color: #A9A9A9;"">gray<br>#A9A9A9</article>
                <article class=""color-card"" style=""background-color: #8B4513;"">brown<br>#8B4513</article>
                <article class=""color-card"" style=""background-color: #008080;"">teal<br>#008080</article>
                <article class=""color-card"" style=""background-color: #FFD700;"">warning<br>#FFD700</article>
                <article class=""color-card"" style=""background-color: #FF4458;"">error<br>#FF4458</article>
                <article class=""color-card"" style=""background-color: #90EE90;"">success<br>#90EE90</article>
            </div>
        </section>

        <section>
            <h2>Upsert Metric</h2>
            <p><strong>URL:</strong> <code>http://localhost:3000/api/upsert</code></p>
            <p><strong>Method:</strong> <code>GET</code></p>
            <p><strong>Query Parameters:</strong></p>
            <ul>
                <li><code>key</code> - Unique identifier for the metric</li>
                <li><code>header</code> - Header text for the metric</li>
                <li><code>content</code> - Content text for the metric</li>
                <li><code>color</code> - Color name or HTML format (e.g., <code>red</code>, <code>#FF5733</code>)</li>
                <li><code>lastUpdated</code> - Last updated timestamp</li>
            </ul>
            <p><strong>Example Request:</strong> <code>http://localhost:3000/api/upsert?key=metric1&header=Temperature&content=25°C&color=warning&lastUpdated=2024-07-22T12:34:56</code></p>
            <button onclick=""copyToClipboard('http://localhost:3000/api/upsert?key=metric1&header=Temperature&content=25°C&color=warning&lastUpdated=2024-07-22T12:34:56')"">Copy Command</button>
        </section>

        <section>
            <h2>Remove Metric</h2>
            <p><strong>URL:</strong> <code>http://localhost:3000/api/remove</code></p>
            <p><strong>Method:</strong> <code>GET</code></p>
            <p><strong>Query Parameters:</strong></p>
            <ul>
                <li><code>key</code> - Unique identifier for the metric</li>
            </ul>
            <p><strong>Example Request:</strong> <code>http://localhost:3000/api/remove?key=metric1</code></p>
            <button onclick=""copyToClipboard('http://localhost:3000/api/remove?key=metric1')"">Copy Command</button>
        </section>

        <section>
            <h2>CLI Usage</h2>
            <p>You can also use the CLI to interact with the overlay:</p>
            <p><strong>Upsert Metric:</strong></p>
            <p><code>.\OverlayThings.exe upsert metric1 Temperature ""25°C"" warning 2024-07-22T12:34:56</code></p>
            <p><strong>Remove Metric:</strong></p>
            <p><code>.\OverlayThings.exe remove metric1</code></p>
        </section>
    </main>

    <script>
        function copyToClipboard(text) {
            const tempInput = document.createElement('input');
            tempInput.value = text;
            document.body.appendChild(tempInput);
            tempInput.select();
            document.execCommand('copy');
            document.body.removeChild(tempInput);
            alert('Copied to clipboard: ' + text);
        }
    </script>
</body>
</html>
";
        }
    }
}

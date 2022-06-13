// PEKAT VISION api
//
// A .NET module for communication with PEKAT VISION 3.10.2 and higher
//
// Author: developers@pekatvision.com
// Date:   10 June 2022
// Web:    https://github.com/pekat-vision

using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Web;

namespace PekatVisionSDK {
    /// <summary>
    /// Image analyzer instance.
    /// </summary>
    public class Analyzer : IDisposable {
        private static readonly HttpClient Client = new HttpClient();
        private const int FirstPort = 10000;
        private const int LastPort = 30000;

        // Must be in sync with ResultType enum
        private static readonly string[] ResultTypeString = {"context", "annotated_image", "heatmap"};

        private static string defaultWindowsDistPath;

        private string baseUri;
        private string apiKey;
        private int stopKey;
        private TaskCompletionSource<bool> processExit;
        public bool contextInBody { get; set; }

        /// <summary>
        /// Create analyzer by running server in background.
        /// </summary>
        /// <param name="distPath">path to server distribution. On Windows can be null for automatic lookup</param>
        /// <param name="projectPath">path to project</param>
        /// <param name="apiKey">optional API key</param>
        /// <param name="options">optional additional parameters for server executable</param>
        public static async Task<Analyzer> CreateLocalAnalyzer(string distPath, string projectPath, string apiKey, string options = null) {
            if (distPath == null) {
                distPath = DefaultWindowsDistPath;
            }
            if (string.IsNullOrWhiteSpace(distPath)) {
                throw new ArgumentException("Distribution path must not be empty");
            }

            if (string.IsNullOrWhiteSpace(projectPath)) {
                throw new ArgumentException("Project path must not be empty");
            }

            // Start process
            string host = "localhost";
            int port = FindFreePort();
            int stopKey = new Random().Next();

            string exe = Path.Combine(distPath, "pekat_vision", "pekat_vision");
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows)) {
                exe += ".exe";
            }
            StringBuilder sb = new StringBuilder();
            sb.Append("-data \"").Append(projectPath).Append("\"");
            sb.Append(" -host ").Append(host).Append(" -port ").Append(port);
            if (!string.IsNullOrWhiteSpace(apiKey)) {
                sb.Append(" -api_key \"").Append(apiKey).Append("\"");
            }
            sb.Append(" -stop_key ").Append(stopKey);

            if (!string.IsNullOrWhiteSpace(options)) {
                sb.Append(" ").Append(options);
            }

            TaskCompletionSource<bool> processExit = new TaskCompletionSource<bool>();
            Process process = new Process();
            process.StartInfo.FileName = exe;
            process.StartInfo.Arguments = sb.ToString();
            process.StartInfo.CreateNoWindow = true;
            process.StartInfo.WindowStyle = ProcessWindowStyle.Hidden;
            process.EnableRaisingEvents = true;
            process.Exited += (s, e) => processExit.TrySetResult(true);
            process.Start();

            return await Start(host, port, apiKey, stopKey, process, processExit);
        }

        private static int FindFreePort() {
            // We need two consecutive ports free
            int lastFree = -1;
            for (int port = FirstPort; port < LastPort; port++) {
                var ep = new IPEndPoint(IPAddress.Parse("127.0.0.1"), port);
                Socket socket = new Socket(ep.AddressFamily, SocketType.Stream, ProtocolType.Tcp);
                try {
                    socket.Bind(ep);
                    if (lastFree == -1) {
                        // Last was not free - this one is first
                        lastFree = port;
                    } else {
                        // Last was free - we have two - return
                        return lastFree;
                    }
                } catch (Exception) {
                    // Not free
                    lastFree = -1;
                } finally {
                    socket.Close();
                }
            }
            throw new Exception("Unable to find free TCP port");
        }

        /// <summary>
        /// Create analyzer connecting to remote server.
        /// </summary>
        public static async Task<Analyzer> CreateRemoteAnalyzer(string host, int port, string apiKey) {
            return await Start(host, port, apiKey, 0, null, null);
        }

        private static async Task<Analyzer> Start(string host, int port, string apiKey, int stopKey, Process process, TaskCompletionSource<bool> processExit) {
            string url = "http://" + host + ":" + port;
            while (true) {
                Task<string> responseTask = Analyzer.Client.GetStringAsync(url + "/ping");
                if (processExit != null)
                {
                    await Task.WhenAny(responseTask, processExit.Task);
                    if (processExit.Task.IsCompleted)
                    {
                        // Process exited - error
                        throw new Exception("Process exited with code " + process.ExitCode);
                    }
                    else
                    {
                        try
                        {
                            var response = await responseTask;
                            // Started successfully
                            return new Analyzer(url, apiKey, stopKey, processExit);
                        }
                        catch (HttpRequestException)
                        {
                            if (process == null)
                            {
                                // Process not started - means the server is probably not running - throw
                                throw;
                            }
                            // Otherwise we are starting the process and it is probably not running yet
                            // Wait few ms and try again
                            Thread.Sleep(100);
                        }
                    }
                } else {
                    return new Analyzer(url, apiKey, stopKey, processExit);
                }
            }
        }

        private static string DefaultWindowsDistPath {
            get {
                if (defaultWindowsDistPath == null) {
                    string dir = Environment.GetEnvironmentVariable("ProgramFiles");
                    if (dir != null) {
                        defaultWindowsDistPath = Directory.EnumerateDirectories(dir).FirstOrDefault(d => d.Contains("\\PEKAT VISION"));
                    }

                    if (defaultWindowsDistPath == null) {
                        throw new IOException("Unable to detect default path");
                    }
                }

                return defaultWindowsDistPath;
            }
        }

        private Analyzer(string baseUri, string apiKey, int stopKey, TaskCompletionSource<bool> processExit) {
            this.baseUri = baseUri;
            this.apiKey = apiKey;
            this.stopKey = stopKey;
            this.processExit = processExit;
        }

        /// <summary>
        /// Analyze image stored in file.
        /// </summary>
        /// <param name="imagePath">path to image file</param>
        /// <param name="resultType">requested output</param>
        /// <param name="data">additional data</param>
        /// <returns>analysis result</returns>
        public async Task<Result> Analyze(string imagePath, ResultType resultType = ResultType.Context, string data = null) {
            using (var file = new FileStream(imagePath, FileMode.Open)) {
                return await Analyze("/analyze_image", -1, -1, new StreamContent(file), resultType, data);
            }
        }

        /// <summary>
        /// Analyze image stored in memory.
        /// </summary>
        /// <param name="imageData">image bytes</param>
        /// <param name="resultType">requested output</param>
        /// <param name="data">additional data</param>
        /// <returns>analysis result</returns>
        public async Task<Result> Analyze(byte[] imageData, ResultType resultType = ResultType.Context, string data = null) {
            return await Analyze("/analyze_image", -1, -1, new ByteArrayContent(imageData), resultType, data);
        }

        /// <summary>
        /// Analyze raw image stored in memory.
        /// </summary>
        /// <param name="imageData">image bytes, width * height * 3</param>
        /// <param name="width">image width</param>
        /// <param name="height">image height</param>
        /// <param name="resultType">requested output</param>
        /// <param name="data">additional data</param>
        /// <returns>analysis result</returns>
        public async Task<Result> AnalyzeRaw(byte[] imageData, int width, int height, ResultType resultType = ResultType.Context, string data = null) {
            return await Analyze("/analyze_raw_image", width, height, new ByteArrayContent(imageData), resultType, data);
        }

        private async Task<Result> Analyze(string path, int width, int height, HttpContent content, ResultType resultType = ResultType.Context, string data = null) {
            var query = HttpUtility.ParseQueryString("");
            query.Add("response_type", ResultTypeString[(int)resultType]);
            if (apiKey != null) {
                query.Add("api_key", apiKey);
            }
            if (data != null) {
                query.Add("data", data);
            }
            if (width > 0) {
                query.Add("width", width.ToString());
                query.Add("height", height.ToString());
            }
            if (contextInBody) {
                query.Add("context_in_body", 1.ToString());
            }

            var uri = baseUri + path + "?" + query;
            content.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");
            var response = await Client.PostAsync(uri, content);
            response.EnsureSuccessStatusCode();
            if (resultType == ResultType.Context) {
                // Just context
                var context = await response.Content.ReadAsStringAsync();
                return new Result {ResultType = resultType, Context = context};
            } else {
                string context = null;
                if (contextInBody) {
                    if (!response.Headers.TryGetValues("ImageLen", out var values)) {
                        context = await response.Content.ReadAsStringAsync();
                        return new Result {ResultType = resultType, Context = context};
                    }
                    
                    int imageLength = int.Parse(values.First());
                    
                    byte[] bytes = await response.Content.ReadAsByteArrayAsync();
                    
                    var image           = bytes.Take(imageLength).ToArray();
                    var contextArray    = bytes.Skip(imageLength).ToArray();
                    
                    context = Encoding.UTF8.GetString(contextArray);
                    return new Result {ResultType = resultType, Context = context, Image = image};
                } else {
                    if (response.Headers.TryGetValues("ContextBase64utf", out var values)) {
                        var bytes = Convert.FromBase64String(values.First());
                        context = Encoding.UTF8.GetString(bytes);
                    }

                    var image = await response.Content.ReadAsByteArrayAsync();
                    return new Result {ResultType = resultType, Context = context, Image = image};
                }
            }
        }

        public async Task DisposeAsync() {
            if (processExit != null) {
                _ = Client.GetStringAsync(baseUri + "/stop?key=" + stopKey);
                await processExit.Task;
            }
        }

        public void Dispose() {
            DisposeAsync().Wait();
        }
    }
}

using Microsoft.Extensions.Logging;
using ModelContextProtocol.Client;

namespace QueryReview.Mcp.Client
{
    internal class McpClientWrapper : IAsyncDisposable
    {
        public static McpClientWrapper CreateFromCommandLineArguments(Uri endpoint, string[] args, bool streamProtocol)
        {
            (string Command, string[] Arguments) parsed = args switch
            {
                [var script] when script.EndsWith(".py") => ("python", args),
                [var script] when script.EndsWith(".js") => ("node", args),
                [var script] when
                    Directory.Exists(script) ||
                    (File.Exists(script) && script.EndsWith(".csproj"))
                    => ("dotnet", ["run", "--project", script, "--no-build"]),
                _ => throw new NotSupportedException("An unsupported server script was provided. Supported scripts are .py, .js, or .csproj")
            };
            return new McpClientWrapper(endpoint, parsed.Command, parsed.Arguments, streamProtocol);
        }

        private readonly CancellationTokenSource _cancellationTokenSource;
        private readonly CancellationToken _cancellationToken;
        private readonly Lazy<Task<IMcpClient>> _client;
        private readonly HttpClient _httpClient;

        public McpClientWrapper(Uri endpoint, string command, string[] arguments, bool streamProtocol)
        {
            _cancellationTokenSource = new CancellationTokenSource();
            _cancellationToken = _cancellationTokenSource.Token;
            _httpClient = new HttpClient();

            _client = new Lazy<Task<IMcpClient>>(
                () =>
                {
                    return ConnectAsync(
                        endpoint: endpoint,
                        httpClient: _httpClient,
                        streambleHttp: true,
                        cancellationToken: _cancellationToken);
                });
        }

        public async Task<IList<McpClientTool>> GetTools()
        {
            var client = await _client.Value;
            return await client.ListToolsAsync(cancellationToken: _cancellationToken);
        }

        public async ValueTask DisposeAsync()
        {
            _cancellationTokenSource.Cancel();
            _cancellationTokenSource.Dispose();

            try
            {
                _httpClient.Dispose();
            }
            catch
            { 
                // ignore for now
            }

            try
            {
                var client = await _client.Value;
                await client.DisposeAsync();
            }
            catch
            {
                // ignore for now
            }
        }

        private async Task<IMcpClient> ConnectAsync(
            Uri endpoint,
            HttpClient httpClient,
            ILoggerFactory? loggerFactory = null,
            string? pattern = null,
            SseClientTransportOptions? transportOptions = null,
            McpClientOptions? clientOptions = null,
            bool streambleHttp = true,
            CancellationToken cancellationToken = default)
        {
            // Default behavior when no options are provided
            pattern ??= streambleHttp ? "/" : "/sse"; // sse is hardcoded url segment related to legacy SSE transport

            await using var transport = new SseClientTransport(
                transportOptions ??
                new SseClientTransportOptions()
                {
                    Endpoint = endpoint,
                    TransportMode = streambleHttp ? HttpTransportMode.StreamableHttp : HttpTransportMode.Sse,
                }, 
                httpClient, 
                loggerFactory);

            return await McpClientFactory.CreateAsync(transport, clientOptions, loggerFactory, cancellationToken);
        }
    }
}

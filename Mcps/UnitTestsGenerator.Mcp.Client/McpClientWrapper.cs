using Microsoft.Extensions.Logging;
using ModelContextProtocol.Client;

namespace UnitTestsGenerator.Mcp.Client
{
    internal class McpClientWrapper : IAsyncDisposable
    {
        private readonly CancellationTokenSource _cancellationTokenSource;
        private readonly CancellationToken _cancellationToken;
        private readonly Lazy<Task<IMcpClient>> _client;
        private readonly HttpClient _httpClient;

        public McpClientWrapper(Uri endpoint, bool streamProtocol)
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
            if (!string.IsNullOrWhiteSpace(pattern) && !endpoint.Segments[^1].Contains("/") && !endpoint.Segments[^1].Contains("/sse", StringComparison.OrdinalIgnoreCase))
            {
                endpoint = new Uri(endpoint, pattern);
            }

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

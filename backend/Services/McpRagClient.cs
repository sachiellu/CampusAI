using System.Text.Json;
using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;

namespace CampusAI.Api.Services;

/// <summary>
/// MCP Client：連線至僅提供 RAG 檢索的 MCP Server。
/// 對話管理仍在本 API／前端；Server 不負責聊天。
/// </summary>
public sealed class McpRagClient(IConfiguration config, ILogger<McpRagClient> logger) : IAsyncDisposable
{
    private readonly string _endpoint = config["Mcp:RagServerUrl"] ?? "http://localhost:5099/";
    private readonly SemaphoreSlim _gate = new(1, 1);
    private McpClient? _client;
    private bool _unavailable;

    public bool IsConfigured => !string.IsNullOrWhiteSpace(_endpoint);

    public async Task<(bool Ok, string Json, string Detail)> RetrieveAsync(string query, int topK = 3, CancellationToken ct = default)
    {
        if (_unavailable) return (false, "", "MCP RAG 先前連線失敗，已暫時略過");

        await _gate.WaitAsync(ct);
        try
        {
            var client = await EnsureClientAsync(ct);
            if (client is null) return (false, "", "無法建立 MCP Client");

            var result = await client.CallToolAsync(
                "rag_retrieve",
                new Dictionary<string, object?>
                {
                    ["query"] = query,
                    ["topK"] = topK
                },
                cancellationToken: ct);

            var text = string.Join("\n", result.Content.OfType<TextContentBlock>().Select(c => c.Text));
            if (string.IsNullOrWhiteSpace(text))
                text = JsonSerializer.Serialize(result);

            return (true, text, "MCP rag_retrieve");
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "MCP RAG 呼叫失敗 endpoint={Endpoint}", _endpoint);
            _unavailable = true;
            _client = null;
            return (false, "", ex.Message);
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task<bool> PingAsync(CancellationToken ct = default)
    {
        try
        {
            using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(2) };
            var baseUri = _endpoint.TrimEnd('/');
            // health 在根路徑；MCP 在 /
            var healthUrl = baseUri.Contains("/mcp", StringComparison.OrdinalIgnoreCase)
                ? baseUri.Replace("/mcp", "/health", StringComparison.OrdinalIgnoreCase)
                : baseUri + "/health";
            var res = await http.GetAsync(healthUrl, ct);
            return res.IsSuccessStatusCode;
        }
        catch
        {
            return false;
        }
    }

    private async Task<McpClient?> EnsureClientAsync(CancellationToken ct)
    {
        if (_client is not null) return _client;
        try
        {
            var transport = new HttpClientTransport(new HttpClientTransportOptions
            {
                Name = "CampusAI-RAG",
                Endpoint = new Uri(_endpoint)
            });
            _client = await McpClient.CreateAsync(transport, cancellationToken: ct);
            logger.LogInformation("已連線 MCP RAG Server: {Endpoint}", _endpoint);
            return _client;
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "MCP RAG Server 連線失敗");
            _unavailable = true;
            return null;
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (_client is not null)
        {
            await _client.DisposeAsync();
            _client = null;
        }
        _gate.Dispose();
    }
}

using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;

var endpoint = args.Length > 0 ? args[0] : "http://localhost:5099/";
var transport = new HttpClientTransport(new HttpClientTransportOptions
{
    Name = "smoke",
    Endpoint = new Uri(endpoint)
});
await using var client = await McpClient.CreateAsync(transport);
var tools = await client.ListToolsAsync();
Console.WriteLine("tools=" + string.Join(",", tools.Select(t => t.Name)));
var result = await client.CallToolAsync("rag_retrieve", new Dictionary<string, object?>
{
    ["query"] = "獎學金申請辦法",
    ["topK"] = 2
});
var text = string.Join("\n", result.Content.OfType<TextContentBlock>().Select(c => c.Text));
Console.WriteLine(text.Length > 200 ? text[..200] + "..." : text);
Console.WriteLine("MCP_SMOKE_OK");

namespace CampusAI.Api.Options;

/// <summary>
/// LLM 設定。商家 ApiKey 僅能透過密鑰注入（User Secrets／環境變數／Key Vault），
/// 禁止寫進 appsettings 與前端。
/// </summary>
public class LlmOptions
{
    public const string SectionName = "Llm";

    /// <summary>gemini | openai（可空，由哪個 Key 有注入自動決定）</summary>
    public string? Provider { get; set; }

    public string GeminiModel { get; set; } = "gemini-flash-latest";
    public string OpenAiModel { get; set; } = "gpt-4o-mini";
}

public class GeminiOptions
{
    public const string SectionName = "Gemini";
    public string ApiKey { get; set; } = string.Empty;
    public bool IsConfigured => !string.IsNullOrWhiteSpace(ApiKey);
}

public class OpenAiOptions
{
    public const string SectionName = "OpenAI";
    public string ApiKey { get; set; } = string.Empty;
    public string Model { get; set; } = "gpt-4o-mini";

    public bool IsConfigured =>
        !string.IsNullOrWhiteSpace(ApiKey) &&
        ApiKey.StartsWith("sk-", StringComparison.OrdinalIgnoreCase);
}

public class KeyVaultOptions
{
    public const string SectionName = "KeyVault";
    public string? Uri { get; set; }
    public bool IsConfigured =>
        !string.IsNullOrWhiteSpace(Uri) &&
        Uri.StartsWith("https://", StringComparison.OrdinalIgnoreCase);
}

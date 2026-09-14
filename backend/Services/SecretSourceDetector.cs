using CampusAI.Api.Options;

namespace CampusAI.Api.Services;

/// <summary>只回報金鑰「從哪注入」，绝不回傳金鑰內容。</summary>
public class SecretSourceInfo
{
    public string Source { get; init; } = "none";
    public bool LlmEnabled { get; init; }
    public string Guidance { get; init; } = string.Empty;
    public string? ConfiguredProvider { get; init; }
}

public class SecretSourceDetector
{
    private readonly IConfiguration _config;
    private readonly OpenAiOptions _openAi;

    public SecretSourceDetector(IConfiguration config, Microsoft.Extensions.Options.IOptions<OpenAiOptions> openAi)
    {
        _config = config;
        _openAi = openAi.Value;
    }

    public SecretSourceInfo Detect()
    {
        var hasGemini = !string.IsNullOrWhiteSpace(_config["Gemini:ApiKey"]);
        var hasOpenAi = _openAi.IsConfigured;
        var llmEnabled = hasGemini || hasOpenAi;
        var provider = hasGemini ? "gemini" : hasOpenAi ? "openai" : null;

        var keyVaultUri = _config["KeyVault:Uri"];
        var fromEnv =
            HasEnv("Gemini__ApiKey") || HasEnv("GEMINI_API_KEY") ||
            HasEnv("OPENAI__ApiKey") || HasEnv("OpenAI__ApiKey");

        string source;
        string guidance;

        if (!llmEnabled)
        {
            source = "none";
            guidance =
                "尚未注入商家金鑰。本機請執行 scripts/Inject-LlmSecret.ps1；" +
                "正式環境用 Azure Key Vault。前端只打後端 API，永不持有商家 Key。";
        }
        else if (!string.IsNullOrWhiteSpace(keyVaultUri))
        {
            source = "azure_key_vault";
            guidance = "正式注入：啟動時由 Azure Key Vault 載入；應用不落地存金鑰檔。";
        }
        else if (fromEnv)
        {
            source = "environment_variable";
            guidance = "由部署環境變數注入（CI/CD、容器、雲端 App Settings）。";
        }
        else
        {
            source = "user_secrets";
            guidance = "本機注入：User Secrets（使用者機密存放處，不進 Git）。";
        }

        return new SecretSourceInfo
        {
            Source = source,
            LlmEnabled = llmEnabled,
            Guidance = guidance,
            ConfiguredProvider = provider
        };
    }

    private static bool HasEnv(string name) =>
        !string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable(name));
}

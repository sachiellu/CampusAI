# LLM 金鑰（本機注入）

前端**永不**放商家 API Key；後端用 User Secrets／環境變數／Key Vault 注入。

## 本機（作品集夠用）

```powershell
cd backend
dotnet user-secrets set "Gemini:ApiKey" "<從 Google AI Studio 複製的 Key>"
dotnet user-secrets list   # 只應看到鍵名
```

或跑：`scripts/Inject-LlmSecret.ps1`

重啟 API 後 `GET /api/health`：

- `llmEnabled`: true  
- `mode` / `configuredProvider`: `gemini`（AI Studio 發的就是 Gemini API）  
- **不會回傳金鑰內容**

備選：`OpenAI:ApiKey`

## 正式環境（文件備註即可，作品集不必實作）

Azure Key Vault + Managed Identity；`appsettings` 只放 `KeyVault:Uri`。

# Campus AI Agent｜學生服務 — 個人化 AI 助理

真實作品集：**Vue 前端 + ASP.NET Core 後端 + JWT 登入 + SQL Server（SSMS）持久化 + Tools／LLM**。

## 登入帳號（密碼皆 `Passw0rd!`）

| 帳號 | 角色 | 姓名 |
|------|------|------|
| `lin.yun` | 學生 | 林小芸 |
| `chen.wei` | 學生 | 陳大偉 |
| `wang.ya` | 學生 | 王雅婷 |
| `advisor.chen` | 教師 | 陳導師 |
| `admin.lin` | 行政 | 林承辦 |

## 資料庫（SQL Server）

本機實例：`.\TEW_SQLEXPRESS`  
資料庫名：`CampusAI`（後端第一次啟動會自動建庫與種子帳號）

SSMS 連線：
- 伺服器名稱：`.\TEW_SQLEXPRESS` 或 `localhost\TEW_SQLEXPRESS`
- 驗證：Windows 驗證
- 資料庫：`CampusAI`
- 可看資料表：`Users`、`ChatMessages`、`ScholarshipApplications`

連線字串在 `backend/appsettings.json` 的 `ConnectionStrings:Default`。

## 啟動

```powershell
# 建議：先開 MCP RAG Server，再開 API／前端
cd C:\C_projects\CampusAI-Agent\mcp-rag-server
dotnet run

cd C:\C_projects\CampusAI-Agent\backend
dotnet run

cd C:\C_projects\CampusAI-Agent\frontend
npx vite --host --port 5173
```

或執行 `.\scripts\Start-McpAndApi.ps1`。

- UI：http://localhost:5173  
- API：http://localhost:5088  
- MCP RAG Server：http://localhost:5099/health  

金鑰注入見 `docs/SECRETS.md` 與 `scripts/Inject-LlmSecret.ps1`。  
MCP 架構與簡報大綱見 `docs/MCP.md`。

## 架構重點

- **MCP**：Server 只做 `rag_retrieve`；Client（API＋Vue）管對話，校園問答走 MCP  
- **JWT**：`/api/auth/login` 發 token；`/api/chat` 需 Bearer  
- **SQL Server**：使用者、多對話、獎學金草稿（可用 SSMS 查看）  
- **流程**：Tools／MCP RAG → Gemini／OpenAI 潤飾回覆  
- **密鑰**：User Secrets／環境變數／Key Vault，不進前端  

# Campus AI Agent — 文件

專案說明與紀錄統一放在 `docs/`（軟體開發常見做法）。

## 技術棧

| 層級 | 技術 |
|------|------|
| 前端 | React + TypeScript + Vite |
| 後端 | C# ASP.NET Core（JWT、Agent、校務 API、MCP Client） |
| 資料 | SQL Server（`CampusAI`） |
| LLM | Gemini（Google AI Studio Key → User Secrets） |
| 通訊 | REST JSON、**camelCase** |

## 架構（對齊簡報圖）

```
應用層（React：主畫面／校務／平台／AI 功能）
  ↓
Agent 層：Personal Orchestrator
  ├ 推薦／規劃／申請／溝通／提醒追蹤／資料分析 Agent
  ↓
API 服務層：校務 API + 平台服務
  ↓
資料層：SQL Server 正式表 + MCP RAG（校園問答）
```

程式：`backend/Services/Agents/`、`backend/Services/CampusApis/`

## 啟動

```powershell
# 後端（會自動建表／Seed）
dotnet run --project backend/CampusAI.Api.csproj
# → http://localhost:5088

# 前端
cd frontend
npm run dev
# → http://localhost:5173

# （選用）校園問答 MCP RAG
cd mcp-rag-server
dotnet run
# → http://localhost:5099
```

Demo 密碼：`Passw0rd!`  
建置被鎖檔時先跑：`scripts/Stop-CampusAI.ps1`

## 文件索引

| 文件 | 內容 |
|------|------|
| [API.md](./API.md) | 前端路由、REST 端點 |
| [DATABASE.md](./DATABASE.md) | SQL 資料表、SSMS 連線 |
| [MCP.md](./MCP.md) | MCP Client／Server（考題規格） |
| [SECRETS.md](./SECRETS.md) | LLM 金鑰注入（本機 User Secrets） |
| **CampusAI-MCP-Deck-v2.pptx** | 簡報 v2 |
| **CampusAI-MCP-Deck-v3.pptx** / **v3-saved** | 簡報 v3 |
| **CampusAI-TechReport-cream.pptx** | 目前版（米白／規格與架構） |

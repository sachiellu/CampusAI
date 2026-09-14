# 前端路由與 REST API

API：`http://localhost:5088`（Vite 將 `/api` proxy 到此）  
JSON：**camelCase**（例如 `studentProfileId`、`hasFinancialNeed`）

## 前端路由

| 路徑 | 說明 |
|------|------|
| `/login` | 登入 |
| `/` | 主畫面（校務／平台／AI 三大入口） |
| `/campus` | 校務系統主頁 |
| `/campus/:serviceId` | 校務服務頁（僅供展示文案，銜接校務系統） |
| `/platform` | 平台服務主頁 |
| `/platform/:serviceId` | 平台服務頁 |
| `/agent` | AI 功能主頁（點功能卡才建立對話） |
| `/agent/chat/:id` | 對話 |

側欄：主畫面 → 校務系統／平台服務／AI 功能（accordion）

## REST 端點

| Method | Path | 說明 |
|--------|------|------|
| GET | `/api/health` | 健康檢查（含 `llmEnabled`、`mode`） |
| GET | `/api/auth/accounts` | Demo 帳戶 |
| POST | `/api/auth/login` | JWT 登入 |
| GET | `/api/services/catalog` | 服務目錄（依角色） |
| GET | `/api/services/{id}` | 服務資料 |
| GET/POST/DELETE | `/api/conversations`… | 多對話 |
| POST | `/api/chat` | 聊天（Agent → SQL 校務資料 → Gemini 潤飾） |

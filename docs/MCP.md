# MCP Client／Server

考題規格：Server **只做 RAG**；Client（產品）管對話。

```
React 對話
  → CampusAI.Api（MCP Client + Orchestrator）
       ├ 獎學金／選課／活動 → SQL Server 校務表
       └ 校園問答 → MCP RAG Server（僅 rag_retrieve）
```

| 元件 | 位置 | 埠 |
|------|------|----|
| MCP RAG Server | `mcp-rag-server/` | 5099 |
| API（Client） | `backend/` | 5088 |

啟動順序與前端見 [README.md](./README.md)。`GET /api/health` 的 `mcp.ragServer` 為 `up`／`down`。

面試 Demo：開 MCP → API → 前端 → 校園問答 → 執行紀錄應見 `rag_retrieve`。

最新簡報：`CampusAI-MCP-Deck-v5.pptx`（v3 留存：`CampusAI-MCP-Deck-v3-saved.pptx`）

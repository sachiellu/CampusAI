# 資料庫（SQL Server）

- 伺服器：`.\TEW_SQLEXPRESS`（SSMS 可選 `DESKTOP-xxx\TEW_SQLEXPRESS`）
- 資料庫：`CampusAI`
- 連線字串：`backend/appsettings.json` → `ConnectionStrings:Default`
- 啟動 API 時 `DbSeeder` 會建表並 Seed（表空才寫入）

## SSMS 連線

1. 伺服器：`.\TEW_SQLEXPRESS`  
2. Windows 驗證  
3. **勾選「信任伺服器憑證」**  
4. 連上後開資料庫 `CampusAI`

## 主要資料表

| 表名 | 說明 |
|------|------|
| `Users` | 登入；`StudentProfileId` → 學生 |
| `Students` | GPA、系級、興趣等（**Agent 讀這裡**） |
| `StudentCompletedCourses` | 已修課 |
| `Courses` / `Scholarships` / `Activities` | 課程／獎助／活動 |
| `CampusDocuments` | 規章（本機問答備援） |
| `CalendarEvents` / `Notifications` | 行事曆／通知 |
| `ScholarshipApplications` | 申請草稿 |
| `ChatConversations` / `ChatMessages` | 對話 |

```sql
USE CampusAI;
SELECT Id, Name, Department, Gpa FROM Students;
SELECT * FROM StudentCompletedCourses WHERE StudentId = N'S001';
```

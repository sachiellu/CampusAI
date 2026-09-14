// 已改由 SQL Server 正式資料表提供校務資料（見 DbSeeder.SeedCampusDataAsync）。
// 保留此檔僅避免舊引用編譯錯誤；請勿再新增使用。
namespace CampusAI.Api.Data;

[Obsolete("改用 AppDbContext Students／Courses／Scholarships 等資料表")]
public static class MockCampusStore
{
}

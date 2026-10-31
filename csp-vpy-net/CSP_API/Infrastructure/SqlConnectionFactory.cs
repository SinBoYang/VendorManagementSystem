using Microsoft.Data.SqlClient;

namespace CSP_API.Infrastructure;

/// <summary>
/// 使用 ASP.NET Core 設定建立 SQL Server 連線。
/// </summary>
public sealed class SqlConnectionFactory : ISqlConnectionFactory
{
    private readonly string _connectionString;

    public SqlConnectionFactory(IConfiguration configuration)
    {
        _connectionString = configuration.GetConnectionString("DefaultConnection")
            ?? throw new InvalidOperationException(
                "找不到 ConnectionStrings:DefaultConnection，請檢查 appsettings.Development.json。");
    }

    public SqlConnection CreateConnection()
    {
        return new SqlConnection(_connectionString);
    }
}
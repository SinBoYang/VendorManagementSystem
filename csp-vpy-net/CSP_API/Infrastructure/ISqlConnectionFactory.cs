using Microsoft.Data.SqlClient;

namespace CSP_API.Infrastructure;

/// <summary>
/// 建立 ADO.NET SQL Server 連線的抽象層。
/// </summary>
public interface ISqlConnectionFactory
{
    SqlConnection CreateConnection();
}
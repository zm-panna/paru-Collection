using Microsoft.Data.SqlClient;
namespace Ecommerce.Web;
public static class SecureConnection
{
    public static string Resolve(IConfiguration config)
    {
        var full = Environment.GetEnvironmentVariable("PARU_DB_CONNECTION");
        if (!string.IsNullOrWhiteSpace(full)) return full;
        var value = config.GetConnectionString("Default");
        if (string.IsNullOrWhiteSpace(value))
            throw new InvalidOperationException("Set PARU_DB_CONNECTION or ConnectionStrings__Default through the server environment or development user-secrets.");
        var connection = new SqlConnectionStringBuilder(value);
        var user = Environment.GetEnvironmentVariable("PARU_DB_USER");
        var password = Environment.GetEnvironmentVariable("PARU_DB_PASSWORD");
        if (!string.IsNullOrWhiteSpace(user))
        {
            if (string.IsNullOrEmpty(password)) throw new InvalidOperationException("PARU_DB_PASSWORD is required with PARU_DB_USER.");
            connection.IntegratedSecurity = false;
            connection.UserID = user; connection.Password = password;
        }
        return connection.ConnectionString;
    }
}

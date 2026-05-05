using Microsoft.Data.SqlClient;

namespace EPI_Shel.Data;

public interface IDbConnection {
    SqlConnection CreateConnection();
}

public class SqlServerConnection : IDbConnection {
    private readonly string _connectionString;

    public SqlServerConnection(IConfiguration config) {
        _connectionString = config.GetConnectionString("DefaultConnection")
            ?? throw new InvalidOperationException("Connection string 'DefaultConnection' not found.");
    }

    public SqlConnection CreateConnection() => new(_connectionString);
}

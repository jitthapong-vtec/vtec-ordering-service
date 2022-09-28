using MySql.Data.MySqlClient;
using System;
using System.Collections.Generic;
using System.Data;
using System.Text;
using System.Threading.Tasks;

namespace VerticalTec.POS.Database
{
    public class MySqlDatabase : IDatabase
    {
        string _connectionString;

        public MySqlDatabase() { }

        public MySqlDatabase(string dbServer, string dbName, string dbPort)
        {
            var connectionStringBuilder = new MySqlConnectionStringBuilder();
            connectionStringBuilder.Server = dbServer;
            connectionStringBuilder.Database = dbName;
            connectionStringBuilder.UserID = "vtecPOS";
            connectionStringBuilder.Password = "vtecpwnet";
            connectionStringBuilder.Port = uint.Parse(dbPort);
            connectionStringBuilder.AllowUserVariables = true;
            connectionStringBuilder.DefaultCommandTimeout = 60;
            connectionStringBuilder.SslMode = MySqlSslMode.None;

            _connectionString = connectionStringBuilder.ConnectionString;//$"Port={dbPort};Connection Timeout=28800;Allow User Variables=True;default command timeout=28800;UID=vtecPOS;PASSWORD=vtecpwnet;SERVER={dbServer};DATABASE={dbName};old guids=true;";
        }

        public MySqlDatabase(string conStr)
        {
            _connectionString = conStr;
        }

        public IDbConnection Connect()
        {
            var conn = new MySqlConnection(_connectionString);
            conn.Open();
            return conn;
        }

        public async Task<IDbConnection> ConnectAsync()
        {
            var conn = new MySqlConnection(_connectionString);
            await conn.OpenAsync();
            return conn;
        }

        public IDbCommand CreateCommand(IDbConnection conn)
        {
            return new MySqlCommand("", ((MySqlConnection)conn));
        }

        public IDbCommand CreateCommand(string commandText, IDbConnection conn)
        {
            return new MySqlCommand(commandText, ((MySqlConnection)conn));
        }

        public IDataAdapter CreateDataAdapter(IDbCommand command)
        {
            return new MySqlDataAdapter((MySqlCommand)command);
        }

        public IDataParameter CreateParameter(string parameterName, object parameterValue)
        {
            return new MySqlParameter(parameterName, parameterValue ?? DBNull.Value);
        }

        public async Task ExecuteNonQueryAsync(IDbCommand command)
        {
            await ((MySqlCommand)command).ExecuteNonQueryAsync();
        }

        public async Task<IDataReader> ExecuteReaderAsync(IDbCommand command)
        {
            return await ((MySqlCommand)command).ExecuteReaderAsync();
        }

        public void SetConnectionString(string connStr)
        {
            _connectionString = connStr;
        }
    }
}

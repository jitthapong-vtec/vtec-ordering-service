using System;
using System.Collections.Generic;
using System.Text;
using System.Data;
using System.Data.SqlClient;
using System.Threading.Tasks;

namespace VerticalTec.POS.Database
{
    public class SqlServerDatabase : IDatabase
    {
        string _connectionString;

        public SqlServerDatabase() { }

        public SqlServerDatabase(string dbAddr, string dbName)
        {
            _connectionString = $"Data Source={dbAddr}; Initial Catalog={dbName};User ID=vtecPOS; Password=vtecpwnet";
        }

        public SqlServerDatabase(string conStr)
        {
            _connectionString = conStr;
        }

        public IDbConnection Connect()
        {
            var conn = new SqlConnection(_connectionString);
            conn.Open();
            return conn;
        }

        public async Task<IDbConnection> ConnectAsync()
        {
            var conn = new SqlConnection(_connectionString);
            await conn.OpenAsync();
            return conn;
        }

        public IDbCommand CreateCommand(IDbConnection conn)
        {
            return ((SqlConnection)conn).CreateCommand();
        }

        public IDbCommand CreateCommand(string commandText, IDbConnection conn)
        {
            var cmd = ((SqlConnection)conn).CreateCommand();
            cmd.CommandText = commandText;
            return cmd;
        }

        public IDataAdapter CreateDataAdapter(IDbCommand command)
        {
            var sqlCommand = (SqlCommand)command;
            return new SqlDataAdapter(sqlCommand);
        }

        public async Task<IDataReader> ExecuteReaderAsync(IDbCommand command)
        {
            return await ((SqlCommand)command).ExecuteReaderAsync();
        }

        public IDataParameter CreateParameter(string parameterName, object parameterValue)
        {
            return new SqlParameter(parameterName, parameterValue);
        }

        public async Task ExecuteNonQueryAsync(IDbCommand command)
        {
            await ((SqlCommand)command).ExecuteNonQueryAsync();
        }

        public void SetConnectionString(string connStr)
        {
            _connectionString = connStr;
        }
    }
}

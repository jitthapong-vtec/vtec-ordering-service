using System.Data;
using System.Threading.Tasks;

namespace VerticalTec.POS.Database
{
    public interface IDatabase
    {
        IDbConnection Connect();

        Task<IDbConnection> ConnectAsync();

        IDbCommand CreateCommand(IDbConnection conn);

        IDbCommand CreateCommand(string commandText, IDbConnection conn);

        IDataAdapter CreateDataAdapter(IDbCommand command);

        IDataParameter CreateParameter(string parameterName, object parameterValue);

        Task ExecuteNonQueryAsync(IDbCommand command);

        Task<IDataReader> ExecuteReaderAsync(IDbCommand command);
    }
}

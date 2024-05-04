using System;
using System.Collections.Generic;
using System.Data;
using System.Data.Common;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Microsoft.Data.SqlClient.SqlClientX.DevAPIs
{
    /// <summary>
    /// Represents a Sql command
    /// </summary>
    public class SqlCommandX : DbCommand
    {

        /// <summary>
        /// Create a command with a connection and command text
        /// </summary>
        /// <param name="commandText"></param>
        /// <param name="connection"></param>
        public SqlCommandX(string commandText, SqlConnectionX connection) : this(commandText)
        {
            Connection = connection;
        }

        /// <summary>
        /// Command created with command text
        /// </summary>
        /// <param name="commandText"></param>
        public SqlCommandX(string commandText) : this()
        {
            CommandText = commandText;
        }

        /// <summary>
        /// Default constuctor;
        /// </summary>
        public SqlCommandX() : base()
        {

        }

        /// <summary>
        /// The command text
        /// </summary>
        public override string CommandText { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }

        /// <summary>
        /// The command timeout
        /// </summary>
        public override int CommandTimeout { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }

        /// <summary>
        /// The command type
        /// </summary>
        public override CommandType CommandType { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }

        /// <summary>
        /// Whether the command is design time visible
        /// </summary>
        public override bool DesignTimeVisible { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }

        /// <summary>
        /// Updates source of row.
        /// </summary>
        public override UpdateRowSource UpdatedRowSource { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }

        /// <summary>
        /// Get the DbConnection for the command or set it.
        /// </summary>
        protected override DbConnection DbConnection { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }

        /// <summary>
        /// The collection of parameters on the command.
        /// </summary>
        protected override DbParameterCollection DbParameterCollection => throw new NotImplementedException();

        /// <summary>
        /// The transaction for the command.
        /// </summary>
        protected override DbTransaction DbTransaction { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }

        /// <summary>
        /// Cancels the command.
        /// </summary>
        /// <exception cref="NotImplementedException"></exception>
        public override void Cancel()
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// Execute a query which is not expected to return results.
        /// </summary>
        /// <returns></returns>
        /// <exception cref="NotImplementedException"></exception>
        public override int ExecuteNonQuery()
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// Execute a query which is expected to return a single result.
        /// </summary>
        /// <returns></returns>
        /// <exception cref="NotImplementedException"></exception>
        public override object ExecuteScalar()
        {
            // TODO: Implemet the execute scalar.
            SqlDataReaderX reader = (SqlDataReaderX)ExecuteReader();
        }

        /// <summary>
        /// Prepare the command for execution.
        /// </summary>
        /// <exception cref="NotImplementedException"></exception>
        public override void Prepare()
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// Create a parameter for the command.
        /// </summary>
        /// <returns></returns>
        /// <exception cref="NotImplementedException"></exception>
        protected override DbParameter CreateDbParameter()
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// Execute the command and return a reader.
        /// </summary>
        /// <param name="behavior"></param>
        /// <returns></returns>
        /// <exception cref="NotImplementedException"></exception>
        protected override DbDataReader ExecuteDbDataReader(CommandBehavior behavior)
        {
            throw new NotImplementedException();
        }
    }
}

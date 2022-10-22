namespace Microsoft.Data.SqlClient
{
#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member
    public class SqlBatch : System.Data.Common.DbBatch
    {
        public SqlBatch() { throw null; }
        public SqlBatch(Microsoft.Data.SqlClient.SqlConnection connection, Microsoft.Data.SqlClient.SqlTransaction transaction = null) { throw null; }
        public override int Timeout { get => throw null; set { } }
        public new Microsoft.Data.SqlClient.SqlConnection Connection { get => throw null; set { } }
        public new Microsoft.Data.SqlClient.SqlTransaction Transaction { get => throw null; set { } }
        protected override System.Collections.Generic.IList<System.Data.Common.DbBatchCommand> DbBatchCommands { get => throw null; }
        protected override System.Data.Common.DbConnection DbConnection { get => throw null; set { } }
        protected override System.Data.Common.DbTransaction DbTransaction { get => throw null; set { } }
        public override void Cancel() => throw null;
        public override int ExecuteNonQuery() => throw null;
        public override System.Threading.Tasks.Task<int> ExecuteNonQueryAsync(System.Threading.CancellationToken cancellationToken = default) => throw null;
        public override object ExecuteScalar() => throw null;
        public override System.Threading.Tasks.Task<object> ExecuteScalarAsync(System.Threading.CancellationToken cancellationToken = default) => throw null;
        public override void Prepare() => throw null;
        public override System.Threading.Tasks.Task PrepareAsync(System.Threading.CancellationToken cancellationToken = default) => throw null;
        protected override System.Data.Common.DbDataReader ExecuteDbDataReader() => throw null;
        protected override System.Threading.Tasks.Task<System.Data.Common.DbDataReader> ExecuteDbDataReaderAsync(System.Threading.CancellationToken cancellationToken) => throw null;
        public System.Collections.Generic.List<Microsoft.Data.SqlClient.SqlBatchCommand> Commands { get { throw null; } }
        public Microsoft.Data.SqlClient.SqlDataReader ExecuteReader() => throw null;
    }
    public partial class SqlBatchCommand : System.Data.Common.DbBatchCommand
    {
        public SqlBatchCommand() { throw null; }
        public SqlBatchCommand(string commandText, System.Data.CommandType commandType = System.Data.CommandType.Text, System.Collections.Generic.IEnumerable<Microsoft.Data.SqlClient.SqlParameter> parameters = null) { throw null; }
        public new Microsoft.Data.SqlClient.SqlParameterCollection Parameters { get { throw null; } }
        public override string CommandText { get { throw null; } set { } }
        public override System.Data.CommandType CommandType { get { throw null; } set { } }
        public override System.Data.CommandBehavior CommandBehavior { get { throw null; } set { } }
        public override int RecordsAffected { get { throw null; } set { } }
        protected override System.Data.Common.DbParameterCollection DbParameterCollection => throw new System.NotImplementedException();
    }

    public sealed partial class SqlException
    {
        public Microsoft.Data.SqlClient.SqlBatchCommand SqlBatchCommand { get { throw null; } }
    }
#pragma warning restore CS1591 // Missing XML comment for publicly visible type or member
}

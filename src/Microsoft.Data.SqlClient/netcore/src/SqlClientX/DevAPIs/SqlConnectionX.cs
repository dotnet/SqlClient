using System;
using System.Collections.Generic;
using System.Data;
using System.Data.Common;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Data.SqlClient.SNI;
using Microsoft.Identity.Client;
using simplesqlclient;

namespace Microsoft.Data.SqlClient.SqlClientX
{
    /// <summary>
    /// SqlConnection
    /// </summary>
    public class SqlConnectionX : DbConnection, ICloneable
    {
        private string _database;
        private SqlPhysicalConnection physicalConnection;

        internal SqlPhysicalConnection PhysicalConnection => physicalConnection;
        /// <summary>
        /// Constructor with Connection String.
        /// </summary>
        /// <param name="connectionString"></param>
        public SqlConnectionX(string connectionString) : this()
        {
            ConnectionString = connectionString;
            SqlConnectionStringBuilder builder = new SqlConnectionStringBuilder(connectionString);
            string datasource = builder.DataSource;
            DataSource details = Microsoft.Data.SqlClient.SNI.DataSource.ParseServerName(datasource);
            
            this._database = builder.InitialCatalog;
            
            ConnectionSettings settings = new ConnectionSettings();
            settings.ApplicationName = builder.ApplicationName;
            settings.WorkstationId = "string-anton";
            settings.ReadOnlyIntent = false;
            settings.UseSSPI = false;
            settings.PacketSize = 4096;
            settings.ApplicationName = builder.ApplicationName;
            this.physicalConnection = new SqlPhysicalConnection(details.ServerName,
                details.Port == -1 ? 1433 : details.Port,
                AuthenticationOptions.Create(builder.UserID, builder.Password),
                this._database,
                settings);
        }

        /// <summary>
        /// Default Constructor
        /// </summary>
        public SqlConnectionX()
        {

        }
        /// <summary>
        /// Connection String 
        /// </summary>
        public override string ConnectionString { get; set; }

        /// <summary>
        ///  The data base for the connection
        /// </summary>
        public override string Database => this._database;

        /// <summary>
        ///  The data source for the connection
        /// </summary>
        public override string DataSource => throw new NotImplementedException();

        /// <summary>
        /// The server version for the connection
        /// </summary>
        public override string ServerVersion => throw new NotImplementedException();

        /// <summary>
        /// The state of the connection
        /// </summary>
        public override ConnectionState State => throw new NotImplementedException();

        /// <summary>
        /// Change the database.
        /// </summary>
        /// <param name="databaseName"></param>
        /// <exception cref="NotImplementedException"></exception>
        public override void ChangeDatabase(string databaseName)
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// Clone the connection
        /// </summary>
        /// <returns></returns>
        /// <exception cref="NotImplementedException"></exception>
        public object Clone()
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// Close the connection
        /// </summary>
        /// <exception cref="NotImplementedException"></exception>
        public override void Close()
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// Open the connection
        /// </summary>
        /// <exception cref="NotImplementedException"></exception>
        public override void Open()
        {
            physicalConnection.TcpConnect();
            // Send prelogin
            physicalConnection.SendPrelogin();

            if (!physicalConnection.TryConsumePrelogin())
            {
                throw new Exception("Failed to consume prelogin");
            }

            physicalConnection.EnableSsl();
            // Send login
            physicalConnection.SendLogin();
            physicalConnection.ProcessTokenStreamPackets();
        }

        /// <summary>
        /// Begin a transaction
        /// </summary>
        /// <param name="isolationLevel"></param>
        /// <returns></returns>
        /// <exception cref="NotImplementedException"></exception>
        protected override DbTransaction BeginDbTransaction(IsolationLevel isolationLevel)
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// Create a command.
        /// </summary>
        /// <returns></returns>
        /// <exception cref="NotImplementedException"></exception>
        protected override DbCommand CreateDbCommand()
        {
            return new SqlCommandX(null, this);
        }

        /// <summary>
        /// Open the connection asynchronously
        /// </summary>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        public override async Task OpenAsync(CancellationToken cancellationToken)
        {
            await OpenCore(cancellationToken);
        }

        private ValueTask OpenCore(CancellationToken cancellationToken)
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// Create a SqlCommand X 
        /// </summary>
        /// <returns></returns>
        new public SqlCommandX CreateCommand()
        {
            return new SqlCommandX(null, this);
        }
    }
}

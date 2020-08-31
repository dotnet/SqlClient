// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.IO;
using Microsoft.Data.SqlClient.TestUtilities;
using Microsoft.SqlServer.Management.Common;

namespace Microsoft.Data.SqlClient.ExtUtilities
{
    public static class SqlDbManager
    {
        private static Config s_configJson;
        private static Dictionary<string, string> s_activeConnectionStrings;

        private const string DB_Northwind = "Northwind";
        private const string DB_Master = "master";
        private const string NorthWindScriptPath = @"../../../../../tools/testsql/createNorthwindDb.sql";
        private const string ConfigPath = @"../Microsoft.Data.SqlClient.TestUtilities/config.json";

        private const string TCPConnectionString = "TCPConnectionString";
        private const string NPConnectionString = "NPConnectionString";
        private const string TCPConnectionStringAASSGX = "TCPConnectionStringAASSGX";
        private const string TCPConnectionStringAASVBS = "TCPConnectionStringAASVBS";
        private const string TCPConnectionStringHGSVBS = "TCPConnectionStringHGSVBS";

        /// <summary>
        /// Creates/ drops database as requested.
        /// </summary>
        /// <param name="args">
        ///      [0] = CreateDatabase, DropDatabase
        ///      [1] = Name of Database
        /// </param>
        public static void Run(string[] args)
        {
            if (args == null || args.Length < 2)
            {
                throw new InvalidArgumentException("Incomplete arguments provided.");
            }

            try
            {
                var dbName = args[1];
                s_configJson = Config.Load(ConfigPath);
                LoadActiveConnectionStrings();

                foreach (KeyValuePair<string, string> activeConnString in s_activeConnectionStrings)
                {
                    SqlConnectionStringBuilder builder = new SqlConnectionStringBuilder((activeConnString.Value));
                    if (!Utils.IsAzureSqlServer(builder.DataSource))
                    {
                        builder.InitialCatalog = DB_Master;
                        using (SqlConnection conn = new SqlConnection(builder.ConnectionString))
                        {
                            SqlServer.Management.Smo.Server server = new SqlServer.Management.Smo.Server(new ServerConnection(conn));
                            ServerConnection context = server.ConnectionContext;

                            if (args[0] == "CreateDatabase")
                            {
                                // We do not create database for HGS-VBS since SQL Server for AASVBS and HGSVBS connection strings is same.
                                // Do not create database for NP connection string, since server is always same as TCP
                                if (activeConnString.Key != TCPConnectionStringHGSVBS && activeConnString.Key != NPConnectionString)
                                {
                                    //Create a new database
                                    CreateDatabase(dbName, context);
                                    Console.WriteLine($"Database [{dbName}] created successfully in {builder.DataSource}");
                                }
                                // Update Config.json accordingly
                                builder.InitialCatalog = dbName;
                                UpdateConfig(activeConnString.Key, builder);
                            }
                            else if (args[0] == "DropDatabase")
                            {
                                // We do not drop database for HGS-VBS since SQL Server for AASVBS and HGSVBS connection strings is same.
                                // Do not drop database for NP connection string, since server is always same as TCP
                                if (activeConnString.Key != TCPConnectionStringHGSVBS && activeConnString.Key != NPConnectionString)
                                {
                                    // Drop Northwind for test run.
                                    DropIfExistsDatabase(dbName, context);
                                    Console.WriteLine($"Database [{dbName}] dropped successfully in {builder.DataSource}");
                                }
                            }
                            else
                            {
                                Console.WriteLine($"Utility '{args[0]}' not supported in {builder.DataSource}");
                            }
                        }
                    }
                    else
                    {
                        Console.WriteLine($"Database Utilities are not supported for Azure SQL in {activeConnString.Key}");
                    }
                }
                if (args[0] == "CreateDatabase")
                {
                    // Update config.json with Initial Catalog = <dbName> for "Active Connection Strings"
                    Config.UpdateConfig(s_configJson, ConfigPath);
                }
            }
            catch (Exception e)
            {
                throw new Exception($"{args[0]} execution failed with Error: {e.Message}");
            }
        }

        private static void LoadActiveConnectionStrings()
        {
            s_activeConnectionStrings = new Dictionary<string, string>();
            if (!string.IsNullOrEmpty(s_configJson.TCPConnectionString))
            {
                s_activeConnectionStrings.Add(TCPConnectionString, s_configJson.TCPConnectionString);
            }
            if (!string.IsNullOrEmpty(s_configJson.NPConnectionString))
            {
                s_activeConnectionStrings.Add(NPConnectionString, s_configJson.NPConnectionString);
            }
            if (s_configJson.EnclaveEnabled)
            {
                if (!string.IsNullOrEmpty(s_configJson.TCPConnectionStringAASSGX))
                {
                    s_activeConnectionStrings.Add(TCPConnectionStringAASSGX, s_configJson.TCPConnectionStringAASSGX);
                }
                if (!string.IsNullOrEmpty(s_configJson.TCPConnectionStringAASVBS))
                {
                    s_activeConnectionStrings.Add(TCPConnectionStringAASVBS, s_configJson.TCPConnectionStringAASVBS);
                }
                if (!string.IsNullOrEmpty(s_configJson.TCPConnectionStringHGSVBS))
                {
                    s_activeConnectionStrings.Add(TCPConnectionStringHGSVBS, s_configJson.TCPConnectionStringHGSVBS);
                }
            }
        }

        private static void UpdateConfig(string key, SqlConnectionStringBuilder builder)
        {
            switch (key)
            {
                case TCPConnectionString:
                    s_configJson.TCPConnectionString = builder.ConnectionString;
                    break;
                case NPConnectionString:
                    s_configJson.NPConnectionString = builder.ConnectionString;
                    break;
                case TCPConnectionStringAASSGX:
                    s_configJson.TCPConnectionStringAASSGX = builder.ConnectionString;
                    break;
                case TCPConnectionStringAASVBS:
                    s_configJson.TCPConnectionStringAASVBS = builder.ConnectionString;
                    break;
                case TCPConnectionStringHGSVBS:
                    s_configJson.TCPConnectionStringHGSVBS = builder.ConnectionString;
                    break;
            }
        }

        private static void DropIfExistsDatabase(string dbName, ServerConnection context)
        {
            try
            {
                string dropScript = $"IF EXISTS (select * from sys.databases where name = '{dbName}') BEGIN DROP DATABASE [{dbName}] END;";
                context.ExecuteNonQuery(dropScript);
            }
            catch
            {
                Console.WriteLine($"FAILED to drop database '{dbName}'");
            }
        }

        private static void CreateDatabase(string dbName, ServerConnection context)
        {
            DropIfExistsDatabase(dbName, context);
            string createScript = File.ReadAllText(NorthWindScriptPath);

            try
            {
                createScript = createScript.Replace(DB_Northwind, dbName);
                context.ExecuteNonQuery(createScript);
            }
            catch (Exception)
            {
                throw;
            }
        }
    }
}

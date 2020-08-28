// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;

namespace Microsoft.Data.SqlClient.ExtUtilities
{
    public static class Runner
    {
        /// <summary>
        /// Runs utility tools for SqlClient Tests
        /// </summary>
        /// <param name="args">
        /// SqlDbManager Tools:
        ///      [0] = CreateDatabase, DropDatabase
        ///      [1] = Name of Database
        /// </param>
        public static void Main(string [] args)
        {
            if (args == null || args.Length < 1)
            {
                throw new ArgumentException("Utility name not provided.");
            }

            if (args[0].Contains("Database"))
            {
                SqlDbManager.Run(args);
            }
            else
            {
                throw new ArgumentException("Utility not supported.");
            }
        }
    }
}

// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.Data.SqlClient
{

    /// <summary>
    /// SQL notification source.
    /// </summary>
    public enum SqlNotificationSource
    {

        /// <summary>
        /// Data
        /// </summary>
        /// <Value>0</Value>
        Data = 0,

        /// <summary>
        /// Time out
        /// </summary>
        /// <value>1</value>
        Timeout = 1,

        /// <summary>
        /// object
        /// </summary>
        /// <Value>2</Value>
        Object = 2,

        /// <summary>
        /// Database
        /// </summary>
        /// <Value>3</Value>
        Database = 3,

        /// <summary>
        /// System
        /// </summary>
        /// <value>4</value>
        System = 4,

        /// <summary>
        /// Statement
        /// </summary>
        /// <value>5</value>
        Statement = 5,

        /// <summary>
        /// Environemnt
        /// </summary>
        /// <value>6</value>
        Environment = 6,

        /// <summary>
        /// Execution
        /// </summary>
        /// <value>7</value>
        Execution = 7,

        /// <summary>
        /// Owner
        /// </summary>
        /// <value>8</value>
        Owner = 8,

        // use negative values for client-only-generated values
        /// <summary>
        /// Unknown
        /// </summary>
        /// <value>-1</value>
        Unknown = -1,

        /// <summary>
        /// Client
        /// </summary>
        /// <value>-2</value>
        Client = -2
    }
}


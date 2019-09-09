// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;

namespace Microsoft.Data.SqlClient
{

    ///<summary>
    ///Specifies a value for <see cref="SqlConnectionStringBuilder.ApplicationIntent" />. Possible values are  <see langword="ReadWrite" /> and <see langword="ReadOnly"/>.
    ///</summary>
    ///<remarks>To be added.</remarks>
    [Serializable]
    public enum ApplicationIntent
    {

        ///<summary>
        ///The application workload type when connecting to a server is read write.
        ///</summary>
        ///<value>0</value>
        ReadWrite = 0,

        /// <summary>
        /// The application workload type when connecting to a server is read only.
        /// </summary>
        /// <value>1</value>
        ReadOnly = 1,
    }
}

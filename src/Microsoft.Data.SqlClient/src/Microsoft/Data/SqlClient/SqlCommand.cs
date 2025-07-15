// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.ComponentModel;
using System.Data.Common;
using System.Threading;

namespace Microsoft.Data.SqlClient
{
    /// <include file='../../../../../../doc/snippets/Microsoft.Data.SqlClient/SqlCommand.xml' path='docs/members[@name="SqlCommand"]/SqlCommand/*'/>
    [DefaultEvent("RecordsAffected")]
    [DesignerCategory("")]
    [ToolboxItem(true)]
    public sealed partial class SqlCommand : DbCommand, ICloneable
    {
        #region Fields

        /// <summary>
        /// Number of instances of SqlCommand that have been created. Used to generate ObjectId
        /// </summary>
        private static int _objectTypeCount = 0;

        /// <summary>
        /// Current state of preparation of the command.
        /// By default, assume the user is not sharing a connection so the command has not been prepared.
        /// </summary>
        private EXECTYPE _execType = EXECTYPE.UNPREPARED;
        
        /// <summary>
        /// Whether the current instance is in the middle of preparation.
        /// </summary>
        // @TODO: Make auto-property
        private bool _inPrepare = false;
        
        #endregion

        #region Enums
        
        // @TODO: Rename to match naming conventions
        private enum EXECTYPE
        {
            /// <summary>
            /// Execute unprepared commands, all server versions (results in sp_execsql call)
            /// </summary>
            UNPREPARED, 
            
            /// <summary>
            /// Prepare and execute command, 8.0 and above only  (results in sp_prepexec call)
            /// </summary>
            PREPAREPENDING,
            
            /// <summary>
            /// execute prepared commands, all server versions   (results in sp_exec call)
            /// </summary>
            PREPARED,           
        }
        
        #endregion
        
        #region Properties
        
        internal bool InPrepare => _inPrepare;
        
        internal int ObjectID { get; } = Interlocked.Increment(ref _objectTypeCount);

        #endregion
    }
}

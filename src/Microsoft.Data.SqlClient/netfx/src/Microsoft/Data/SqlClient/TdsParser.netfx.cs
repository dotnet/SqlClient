using System.Diagnostics;
using System.Runtime.CompilerServices;
using System;
using System.Net;
using System.Threading;
using Interop.Windows.Sni;

namespace Microsoft.Data.SqlClient
{
    internal sealed partial class TdsParser
    {
        // This is called from a ThreadAbort - ensure that it can be run from a CER Catch
        internal void BestEffortCleanup()
        {
            _state = TdsParserState.Broken;

            var stateObj = _physicalStateObj;
            if (stateObj != null)
            {
                var stateObjHandle = stateObj.Handle;
                if (stateObjHandle != null)
                {
                    stateObjHandle.Dispose();
                }
            }

            if (_fMARS)
            {
                var sessionPool = _sessionPool;
                if (sessionPool != null)
                {
                    sessionPool.BestEffortCleanup();
                }

                var marsStateObj = _pMarsPhysicalConObj;
                if (marsStateObj != null)
                {
                    var marsStateObjHandle = marsStateObj.Handle;
                    if (marsStateObjHandle != null)
                    {
                        marsStateObjHandle.Dispose();
                    }
                }
            }
        }
        
        // @TODO: Remove.
        internal bool RunReliably(RunBehavior runBehavior, SqlCommand cmdHandler, SqlDataReader dataStream, BulkCopySimpleResultSet bulkCopyHandler, TdsParserStateObject stateObj)
        {
            return Run(runBehavior, cmdHandler, dataStream, bulkCopyHandler, stateObj);
            // @TODO: CER Exception Handling was removed here (see GH#3581)
        }
    }
}

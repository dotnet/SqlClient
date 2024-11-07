using System.Diagnostics;
using System.Runtime.CompilerServices;
using System;

namespace Microsoft.Data.SqlClient
{
    internal sealed partial class TdsParser
    {
        // ReliabilitySection Usage:
        //
        // #if DEBUG
        //        TdsParser.ReliabilitySection tdsReliabilitySection = new TdsParser.ReliabilitySection();
        //
        //        RuntimeHelpers.PrepareConstrainedRegions();
        //        try {
        //            tdsReliabilitySection.Start();
        // #else
        //        {
        // #endif //DEBUG
        //
        //        // code that requires reliability
        //
        //        }
        // #if DEBUG
        //        finally {
        //            tdsReliabilitySection.Stop();
        //        }
        //  #endif //DEBUG

        internal struct ReliabilitySection
        {
#if DEBUG
            // do not allocate TLS data in RETAIL bits
            [ThreadStatic]
            private static int s_reliabilityCount; // initialized to 0 by CLR

            private bool m_started;  // initialized to false (not started) by CLR
#endif //DEBUG

            [Conditional("DEBUG")]
            internal void Start()
            {
#if DEBUG
                Debug.Assert(!m_started);

                RuntimeHelpers.PrepareConstrainedRegions();
                try
                {
                }
                finally
                {
                    ++s_reliabilityCount;
                    m_started = true;
                }
#endif //DEBUG
            }

            [Conditional("DEBUG")]
            internal void Stop()
            {
#if DEBUG
                // cannot assert m_started - ThreadAbortException can be raised before Start is called

                if (m_started)
                {
                    Debug.Assert(s_reliabilityCount > 0);

                    RuntimeHelpers.PrepareConstrainedRegions();
                    try
                    {
                    }
                    finally
                    {
                        --s_reliabilityCount;
                        m_started = false;
                    }
                }
#endif //DEBUG
            }

            // you need to setup for a thread abort somewhere before you call this method
            [Conditional("DEBUG")]
            internal static void Assert(string message)
            {
#if DEBUG
                Debug.Assert(s_reliabilityCount > 0, message);
#endif //DEBUG
            }
        }
    }
}

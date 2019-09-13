using System;
using System.Collections.Generic;
using System.Text;

namespace Microsoft.Data.SqlClient.Samples
{
    class Runner
    {
        internal static void Main(string[] args)
        {
#if NET46
            TransactionIsolationLevelsProgram.Main(null);
#endif
        }
    }
}

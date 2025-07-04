// See the LICENSE file in the project root for more information.

using System;

namespace Microsoft.Data.SqlClient
{
    internal static class BitConverterCompatible
    {
        public static unsafe int SingleToInt32Bits(float value) 
        {
           return *(int*)(&value);
        }
        public static unsafe float Int32BitsToSingle(int value) 
        {
            return *(float*)(&value);
        }       
    }
}

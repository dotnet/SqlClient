// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Diagnostics;

namespace Microsoft.Data.SqlClient
{
    internal sealed partial class SqlBuffer
    {
        internal void SetToDate(DateTime date)
        {
            Debug.Assert(IsEmpty, "setting value a second time?");

            _type = StorageType.Date;
            _value._int32 = date.Subtract(DateTime.MinValue).Days;
            _isNull = false;
        }

        internal void SetToDateTime2(DateTime dateTime, byte scale)
        {
            Debug.Assert(IsEmpty, "setting value a second time?");

            _type = StorageType.DateTime2;
            _value._dateTime2Info._timeInfo._ticks = dateTime.TimeOfDay.Ticks;
            _value._dateTime2Info._timeInfo._scale = scale;
            _value._dateTime2Info._date = dateTime.Subtract(DateTime.MinValue).Days;
            _isNull = false;
        }
    }
}

namespace DataWorks_SqlUserDefinedTypeAttribute_Sample;

using System;
using System.Data.SqlTypes;
using Microsoft.SqlServer.Server;
using System.Text;

// <Snippet1>
[Serializable]
[Microsoft.SqlServer.Server.SqlUserDefinedType(Format.Native,
     IsByteOrdered=true,
     Name="Point",ValidationMethodName = "ValidatePoint")]
public struct Point : INullable
{
//</Snippet1>
    private bool is_Null;
    private int _x;
    private int _y;

    public bool IsNull
    {
        get
        {
            return (is_Null);
        }
    }

    public static Point Null
    {
        get
        {
            Point pt = new Point();
            pt.is_Null = true;
            return pt;
        }
    }

    // Use StringBuilder to provide string representation of UDT.
    public override string ToString()
    {
        // Since InvokeIfReceiverIsNull defaults to 'true'
        // this test is unnecessary if Point is only being called
        // from SQL.
        if (this.IsNull)
        {
            return "NULL";
        }
        else
        {
            StringBuilder builder = new StringBuilder();
            builder.Append(_x);
            builder.Append(",");
            builder.Append(_y);
            return builder.ToString();
        }
    }

    [SqlMethod(OnNullCall = false)]
    public static Point Parse(SqlString s)
    {
        // With OnNullCall=false, this check is unnecessary if
        // Point only called from SQL.
        if (s.IsNull)
            return Null;

        // Parse input string to separate out points.
        Point pt = new Point();
        string[] xy = s.Value.Split(",".ToCharArray());
        pt.X = int.Parse(xy[0]);
        pt.Y = int.Parse(xy[1]);

        // Call ValidatePoint to enforce validation
        // for string conversions.
        if (!pt.ValidatePoint())
            throw new ArgumentException("Invalid XY coordinate values.");
        return pt;
    }

    // X and Y coordinates exposed as properties.
    public int X
    {
        get
        {
            return this._x;
        }
        // Call ValidatePoint to ensure valid range of Point values.
        set
        {
            int temp = _x;
            _x = value;
            if (!ValidatePoint())
            {
                _x = temp;
                throw new ArgumentException("Invalid X coordinate value.");
            }
        }
    }

    public int Y
    {
        get
        {
            return this._y;
        }
        set
        {
            int temp = _y;
            _y = value;
            if (!ValidatePoint())
            {
                _y = temp;
                throw new ArgumentException("Invalid Y coordinate value.");
            }
        }
    }

    // Validation method to enforce valid X and Y values.
    private bool ValidatePoint()
    {
        return true;
    }
}

// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using static Microsoft.Data.SqlClient.PerformanceTests.Constants;

namespace Microsoft.Data.SqlClient.PerformanceTests
{
    /// <summary>
    /// ADO for datatypes.json
    /// </summary>
    public class DataTypes
    {
        public MinMaxType[] Numerics;
        public PrecisionScaleType[] Decimals;
        public ValueFormatType[] DateTimes;
        public MaxLengthValueType[] Characters;
        public MaxLengthBinaryType[] Binary;
        public MaxLengthValueLengthType[] MaxTypes;
        public DataType[] Others;

        /// <summary>
        /// Load the data types configuration from a JSON file.
        ///
        /// If the environment variable "DATATYPES_CONFIG" is set, it will be
        /// used as the path to the config file.  Otherwise, the file
        /// "datatypes.json" in the current working directory will be used.
        /// </summary>
        ///
        /// <returns>
        ///   The DataTypes instance populated from the JSON file.
        /// </returns>
        ///
        /// <exception cref="InvalidOperationException">
        ///   Thrown if the config file cannot be read or deserialized.
        /// </exception>
        ///
        public static DataTypes Load()
        {
            return Loader.FromJsonFile<DataTypes>(
                "datatypes.json", "DATATYPES_CONFIG");
        }
    }

    /// <summary>
    /// Base type for all datatypes
    /// </summary>
    public class DataType
    {
        /// <summary>
        /// Default value of all datatypes
        /// </summary>
        public object DefaultValue;
        public string Name;

        public override string ToString() => Name;
    }

    public class MinMaxType : DataType
    {
        public object MinValue;
        public object MaxValue;
    }

    public class PrecisionScaleType : DataType
    {
        public short MaxPrecision;
        public short MaxScale;
        public short Precision;
        public short Scale;

        public double MinValue
        {
            get
            {
                return Name switch
                {
                    Float => -1.79E+308,
                    Real => -3.40E+38,
                    Decimal or Numeric => (-10 ^ 38) + 1,
                    _ => default,
                };
            }
        }

        public double MaxValue
        {
            get
            {
                return Name switch
                {
                    Float => 1.79E+308,
                    Real => 3.40E+38,
                    Decimal or Numeric => (10 ^ 38) - 1,
                    _ => default,
                };
            }
        }

        public override string ToString()
        {
            if (Name == Float)
            {
                // Syntax: float(n)
                return base.ToString() + "(" + Precision + ")";
            }
            else if (Name != Real)
            {
                // Syntax: decimal(p[,s]) or numeric(p[,s])
                return base.ToString() + "(" + Precision + "," + Scale + ")";
            }
            return base.ToString();
        }
    }

    public class ValueFormatType : DataType
    {
        public string Format;
    }

    public class MaxLengthValueType : DataType
    {
        public int MaxLength;
    }

    public class MaxLengthBinaryType : MaxLengthValueType
    {
        public override string ToString() => base.ToString() + "(" + DefaultValue.ToString().Length * 2 + ")";
    }

    public class MaxLengthValueLengthType : MaxLengthValueType
    {
        public override string ToString() => base.ToString() + "(max)";
    }
}

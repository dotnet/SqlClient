// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Data.SqlTypes;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace Microsoft.Data.SqlClient.Server
{
    // The class that holds the offset, field, and normalizer for
    // a particular field.
    internal sealed class FieldInfoEx : IComparable
    {
        private readonly int _offset;

        internal FieldInfoEx(FieldInfo fi, int offset, Normalizer normalizer)
        {
            _offset = offset;
            FieldInfo = fi;
            Debug.Assert(normalizer != null, "normalizer argument should not be null!");
            Normalizer = normalizer;
        }

        internal FieldInfo FieldInfo { get; private set; }
        internal Normalizer Normalizer { get; private set; }

        // Sort fields by field offsets.
        public int CompareTo(object other) => other is FieldInfoEx otherEx ? _offset.CompareTo(otherEx._offset) : -1;
    }

    // The most complex normalizer, a udt normalizer
    internal sealed class BinaryOrderedUdtNormalizer : Normalizer
    {
        private readonly FieldInfoEx[] _fieldsToNormalize;
        private int _size;
        private readonly byte[] _padBuffer;
        private readonly object _nullInstance;

#if NETFRAMEWORK
        [System.Security.Permissions.ReflectionPermission(System.Security.Permissions.SecurityAction.Assert, MemberAccess = true)]
#endif
        private FieldInfo[] GetFields(
#if NET
            [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicFields | DynamicallyAccessedMemberTypes.NonPublicFields)]
#endif
            Type t)
        {
            return t.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        }

        internal BinaryOrderedUdtNormalizer(
#if NET
            [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicFields | DynamicallyAccessedMemberTypes.NonPublicFields | DynamicallyAccessedMemberTypes.PublicProperties | DynamicallyAccessedMemberTypes.NonPublicProperties)]
#endif
            Type t, bool isTopLevelUdt)
        {
            _skipNormalize = false;

            FieldInfo[] fields = GetFields(t);

            _fieldsToNormalize = new FieldInfoEx[fields.Length];

            int i = 0;

            foreach (FieldInfo fi in fields)
            {
                int offset = Marshal.OffsetOf(fi.DeclaringType, fi.Name).ToInt32();
                _fieldsToNormalize[i++] = new FieldInfoEx(fi, offset, GetNormalizer(fi.FieldType));
            }

            //sort by offset
            Array.Sort(_fieldsToNormalize);
        }

        internal bool IsNullable => _nullInstance != null;

        // Normalize the top-level udt
        internal void NormalizeTopObject(object udt, Stream s) => Normalize(null, udt, s);

        // Denormalize a top-level udt and return it
        internal object DeNormalizeTopObject(
#if NET
            [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicParameterlessConstructor)]
#endif
            Type t, Stream s) => DeNormalizeInternal(t, s);

        // Prevent inlining so that reflection calls are not moved to caller that may be in a different assembly that may have a different grant set.
        [MethodImpl(MethodImplOptions.NoInlining)]
        private object DeNormalizeInternal(
#if NET
            [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicParameterlessConstructor)]
#endif
            Type t, Stream s)
        {
            object result = Activator.CreateInstance(t);

            foreach (FieldInfoEx field in _fieldsToNormalize)
            {
                field.Normalizer.DeNormalize(field.FieldInfo, result, s);
            }
            return result;
        }

        internal override void Normalize(FieldInfo fi, object obj, Stream s)
        {
            object inner;
            if (fi == null)
            {
                inner = obj;
            }
            else
            {
                inner = GetValue(fi, obj);
            }

            foreach (FieldInfoEx field in _fieldsToNormalize)
            {
                field.Normalizer.Normalize(field.FieldInfo, inner, s);
            }
        }

        internal override void DeNormalize(FieldInfo fi, object recvr, Stream s) => SetValue(fi, recvr, DeNormalizeInternal(fi.FieldType, s));

        internal override int Size
        {
            get
            {
                if (_size != 0)
                {
                    return _size;
                }
                foreach (FieldInfoEx field in _fieldsToNormalize)
                {
                    _size += field.Normalizer.Size;
                }
                return _size;
            }
        }
    }

    internal abstract class Normalizer
    {
        protected bool _skipNormalize;

        internal static Normalizer GetNormalizer(
#if NET
            [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicFields | DynamicallyAccessedMemberTypes.NonPublicFields | DynamicallyAccessedMemberTypes.PublicProperties | DynamicallyAccessedMemberTypes.NonPublicProperties)]
#endif
            Type t)
        {
            Normalizer n = null;
            if (t.IsPrimitive)
            {
                if (t == typeof(byte))
                    n = new ByteNormalizer();
                else if (t == typeof(sbyte))
                    n = new SByteNormalizer();
                else if (t == typeof(bool))
                    n = new BooleanNormalizer();
                else if (t == typeof(short))
                    n = new ShortNormalizer();
                else if (t == typeof(ushort))
                    n = new UShortNormalizer();
                else if (t == typeof(int))
                    n = new IntNormalizer();
                else if (t == typeof(uint))
                    n = new UIntNormalizer();
                else if (t == typeof(float))
                    n = new FloatNormalizer();
                else if (t == typeof(double))
                    n = new DoubleNormalizer();
                else if (t == typeof(long))
                    n = new LongNormalizer();
                else if (t == typeof(ulong))
                    n = new ULongNormalizer();
            }
            else if (t.IsValueType)
            {
                n = new BinaryOrderedUdtNormalizer(t, false);
            }
            if (n == null)
            {
                throw new Exception(StringsHelper.GetString(Strings.SQL_CannotCreateNormalizer, t.FullName));
            }
            n._skipNormalize = false;
            return n;
        }

        internal abstract void Normalize(FieldInfo fi, object recvr, Stream s);

        internal abstract void DeNormalize(FieldInfo fi, object recvr, Stream s);

        protected void FlipAllBits(byte[] b)
        {
            for (int i = 0; i < b.Length; i++)
            {
                b[i] = (byte)~b[i];
            }
        }
#if NETFRAMEWORK
        [System.Security.Permissions.ReflectionPermission(System.Security.Permissions.SecurityAction.Assert, MemberAccess = true)]
#endif
        protected object GetValue(FieldInfo fi, object obj) => fi.GetValue(obj);
#if NETFRAMEWORK
        [System.Security.Permissions.ReflectionPermission(System.Security.Permissions.SecurityAction.Assert, MemberAccess = true)]
#endif
        protected void SetValue(FieldInfo fi, object recvr, object value) => fi.SetValue(recvr, value);

        internal abstract int Size { get; }
    }

    internal sealed class BooleanNormalizer : Normalizer
    {
        internal override void Normalize(FieldInfo fi, object obj, Stream s)
        {
            bool b = (bool)GetValue(fi, obj);
            s.WriteByte((byte)(b ? 1 : 0));
        }

        internal override void DeNormalize(FieldInfo fi, object recvr, Stream s)
        {
            byte b = (byte)s.ReadByte();
            SetValue(fi, recvr, b == 1);
        }

        internal override int Size => 1;
    }

    internal sealed class SByteNormalizer : Normalizer
    {
        internal override void Normalize(FieldInfo fi, object obj, Stream s)
        {
            sbyte sb = (sbyte)GetValue(fi, obj);
            byte b;
            unchecked
            {
                b = (byte)sb;
            }
            if (!_skipNormalize)
            {
                b ^= 0x80; //flip the sign bit
            }
            s.WriteByte(b);
        }

        internal override void DeNormalize(FieldInfo fi, object recvr, Stream s)
        {
            byte b = (byte)s.ReadByte();
            if (!_skipNormalize)
            {
                b ^= 0x80; //flip the sign bit
            }
            sbyte sb;
            unchecked
            {
                sb = (sbyte)b;
            }
            SetValue(fi, recvr, sb);
        }

        internal override int Size => 1;
    }

    internal sealed class ByteNormalizer : Normalizer
    {
        internal override void Normalize(FieldInfo fi, object obj, Stream s)
        {
            byte b = (byte)GetValue(fi, obj);
            s.WriteByte(b);
        }

        internal override void DeNormalize(FieldInfo fi, object recvr, Stream s)
        {
            byte b = (byte)s.ReadByte();
            SetValue(fi, recvr, b);
        }

        internal override int Size => 1;
    }

    internal sealed class ShortNormalizer : Normalizer
    {
        internal override void Normalize(FieldInfo fi, object obj, Stream s)
        {
            byte[] b = BitConverter.GetBytes((short)GetValue(fi, obj));
            if (!_skipNormalize)
            {
                Array.Reverse(b);
                b[0] ^= 0x80;
            }
            s.Write(b, 0, b.Length);
        }

        internal override void DeNormalize(FieldInfo fi, object recvr, Stream s)
        {
            byte[] b = new byte[2];
            s.ReadExactly(b, 0, b.Length);
            if (!_skipNormalize)
            {
                b[0] ^= 0x80;
                Array.Reverse(b);
            }
            SetValue(fi, recvr, BitConverter.ToInt16(b, 0));
        }

        internal override int Size { get { return 2; } }
    }

    internal sealed class UShortNormalizer : Normalizer
    {
        internal override void Normalize(FieldInfo fi, object obj, Stream s)
        {
            byte[] b = BitConverter.GetBytes((ushort)GetValue(fi, obj));
            if (!_skipNormalize)
            {
                Array.Reverse(b);
            }
            s.Write(b, 0, b.Length);
        }

        internal override void DeNormalize(FieldInfo fi, object recvr, Stream s)
        {
            byte[] b = new byte[2];
            s.ReadExactly(b, 0, b.Length);
            if (!_skipNormalize)
            {
                Array.Reverse(b);
            }
            SetValue(fi, recvr, BitConverter.ToUInt16(b, 0));
        }

        internal override int Size => 2;
    }

    internal sealed class IntNormalizer : Normalizer
    {
        internal override void Normalize(FieldInfo fi, object obj, Stream s)
        {
            byte[] b = BitConverter.GetBytes((int)GetValue(fi, obj));
            if (!_skipNormalize)
            {
                Array.Reverse(b);
                b[0] ^= 0x80;
            }
            s.Write(b, 0, b.Length);
        }

        internal override void DeNormalize(FieldInfo fi, object recvr, Stream s)
        {
            byte[] b = new byte[4];
            s.ReadExactly(b, 0, b.Length);
            if (!_skipNormalize)
            {
                b[0] ^= 0x80;
                Array.Reverse(b);
            }
            SetValue(fi, recvr, BitConverter.ToInt32(b, 0));
        }

        internal override int Size => 4;
    }

    internal sealed class UIntNormalizer : Normalizer
    {
        internal override void Normalize(FieldInfo fi, object obj, Stream s)
        {
            byte[] b = BitConverter.GetBytes((uint)GetValue(fi, obj));
            if (!_skipNormalize)
            {
                Array.Reverse(b);
            }
            s.Write(b, 0, b.Length);
        }

        internal override void DeNormalize(FieldInfo fi, object recvr, Stream s)
        {
            byte[] b = new byte[4];
            s.ReadExactly(b, 0, b.Length);
            if (!_skipNormalize)
            {
                Array.Reverse(b);
            }
            SetValue(fi, recvr, BitConverter.ToUInt32(b, 0));
        }

        internal override int Size => 4;
    }

    internal sealed class LongNormalizer : Normalizer
    {
        internal override void Normalize(FieldInfo fi, object obj, Stream s)
        {
            byte[] b = BitConverter.GetBytes((long)GetValue(fi, obj));
            if (!_skipNormalize)
            {
                Array.Reverse(b);
                b[0] ^= 0x80;
            }
            s.Write(b, 0, b.Length);
        }

        internal override void DeNormalize(FieldInfo fi, object recvr, Stream s)
        {
            byte[] b = new byte[8];
            s.ReadExactly(b, 0, b.Length);
            if (!_skipNormalize)
            {
                b[0] ^= 0x80;
                Array.Reverse(b);
            }
            SetValue(fi, recvr, BitConverter.ToInt64(b, 0));
        }

        internal override int Size => 8;
    }

    internal sealed class ULongNormalizer : Normalizer
    {
        internal override void Normalize(FieldInfo fi, object obj, Stream s)
        {
            byte[] b = BitConverter.GetBytes((ulong)GetValue(fi, obj));
            if (!_skipNormalize)
            {
                Array.Reverse(b);
            }
            s.Write(b, 0, b.Length);
        }

        internal override void DeNormalize(FieldInfo fi, object recvr, Stream s)
        {
            byte[] b = new byte[8];
            s.ReadExactly(b, 0, b.Length);
            if (!_skipNormalize)
            {
                Array.Reverse(b);
            }
            SetValue(fi, recvr, BitConverter.ToUInt64(b, 0));
        }

        internal override int Size => 8;
    }

    internal sealed class FloatNormalizer : Normalizer
    {
        internal override void Normalize(FieldInfo fi, object obj, Stream s)
        {
            float f = (float)GetValue(fi, obj);
            byte[] b = BitConverter.GetBytes(f);
            if (!_skipNormalize)
            {
                Array.Reverse(b);
                if ((b[0] & 0x80) == 0)
                {
                    // This is a positive number.
                    // Flip the highest bit
                    b[0] ^= 0x80;
                }
                else
                {
                    // This is a negative number.

                    // If all zeroes, means it was a negative zero.
                    // Treat it same as positive zero, so that
                    // the normalized key will compare equal.
                    if (f < 0)
                    {
                        FlipAllBits(b);
                    }
                }
            }
            s.Write(b, 0, b.Length);
        }

        internal override void DeNormalize(FieldInfo fi, object recvr, Stream s)
        {
            byte[] b = new byte[4];
            s.ReadExactly(b, 0, b.Length);
            if (!_skipNormalize)
            {
                if ((b[0] & 0x80) > 0)
                {
                    // This is a positive number.
                    // Flip the highest bit
                    b[0] ^= 0x80;
                }
                else
                {
                    // This is a negative number.
                    FlipAllBits(b);
                }
                Array.Reverse(b);
            }
            SetValue(fi, recvr, BitConverter.ToSingle(b, 0));
        }

        internal override int Size => 4;
    }

    internal sealed class DoubleNormalizer : Normalizer
    {
        internal override void Normalize(FieldInfo fi, object obj, Stream s)
        {
            double d = (double)GetValue(fi, obj);
            byte[] b = BitConverter.GetBytes(d);
            if (!_skipNormalize)
            {
                Array.Reverse(b);
                if ((b[0] & 0x80) == 0)
                {
                    // This is a positive number.
                    // Flip the highest bit
                    b[0] ^= 0x80;
                }
                else
                {
                    // This is a negative number.
                    if (d < 0)
                    {
                        // If all zeroes, means it was a negative zero.
                        // Treat it same as positive zero, so that
                        // the normalized key will compare equal.
                        FlipAllBits(b);
                    }
                }
            }
            s.Write(b, 0, b.Length);
        }

        internal override void DeNormalize(FieldInfo fi, object recvr, Stream s)
        {
            byte[] b = new byte[8];
            s.ReadExactly(b, 0, b.Length);
            if (!_skipNormalize)
            {
                if ((b[0] & 0x80) > 0)
                {
                    // This is a positive number.
                    // Flip the highest bit
                    b[0] ^= 0x80;
                }
                else
                {
                    // This is a negative number.
                    FlipAllBits(b);
                }
                Array.Reverse(b);
            }
            SetValue(fi, recvr, BitConverter.ToDouble(b, 0));
        }

        internal override int Size => 8;
    }
}

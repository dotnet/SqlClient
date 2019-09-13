// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections;
using System.IO;
using System.Reflection;
using System.Runtime.CompilerServices;
using Microsoft.Data.Common;

namespace Microsoft.Data.SqlClient.Server
{

    internal class SerializationHelperSql9
    {
        // Don't let anyone create an instance of this class.
        private SerializationHelperSql9() { }

        // Get the m_size of the serialized stream for this type, in bytes.
        // This method creates an instance of the type using the public
        // no-argument constructor, serializes it, and returns the m_size
        // in bytes.
        // Prevent inlining so that reflection calls are not moved to caller that may be in a different assembly that may have a different grant set.
        [MethodImpl(MethodImplOptions.NoInlining)]
        internal static int SizeInBytes(Type t)
        {
            return SizeInBytes(Activator.CreateInstance(t));
        }

        // Get the m_size of the serialized stream for this type, in bytes.
        internal static int SizeInBytes(object instance)
        {
            Type t = instance.GetType();
            Format k = GetFormat(t);
            DummyStream stream = new DummyStream();
            Serializer ser = GetSerializer(instance.GetType());
            ser.Serialize(stream, instance);
            return (int)stream.Length;
        }

        internal static void Serialize(Stream s, object instance)
        {
            GetSerializer(instance.GetType()).Serialize(s, instance);
        }

        internal static object Deserialize(Stream s, Type resultType)
        {
            return GetSerializer(resultType).Deserialize(s);
        }

        private static Format GetFormat(Type t)
        {
            return GetUdtAttribute(t).Format;
        }

        //cache the relationship between a type and its serializer
        //this is expensive to compute since it involves traversing the
        //custom attributes of the type using reflection.
        //
        //use a per-thread cache, so that there are no synchronization
        //issues when accessing cache entries from multiple threads.
        [ThreadStatic]
        private static Hashtable m_types2Serializers;

        private static Serializer GetSerializer(Type t)
        {
            if (m_types2Serializers == null)
                m_types2Serializers = new Hashtable();

            Serializer s = (Serializer)m_types2Serializers[t];
            if (s == null)
            {
                s = (Serializer)GetNewSerializer(t);
                m_types2Serializers[t] = s;
            }
            return s;
        }

        internal static int GetUdtMaxLength(Type t)
        {
            SqlUdtInfo udtInfo = SqlUdtInfo.GetFromType(t);

            if (Format.Native == udtInfo.SerializationFormat)
            {
                //In the native format, the user does not specify the
                //max byte size, it is computed from the type definition
                return SerializationHelperSql9.SizeInBytes(t);
            }
            else
            {
                //In all other formats, the user specifies the maximum size in bytes.
                return udtInfo.MaxByteSize;
            }
        }

        private static object[] GetCustomAttributes(Type t)
        {
            object[] attrs = t.GetCustomAttributes(typeof(SqlUserDefinedTypeAttribute), false);

            // If we don't find a Microsoft.Data.SqlClient.Server.SqlUserDefinedTypeAttribute,
            // search for a Microsoft.SqlServer.Server.SqlUserDefinedTypeAttribute from the
            // old System.Data.SqlClient assembly and copy it to our
            // Microsoft.Data.SqlClient.Server.SqlUserDefinedTypeAttribute for reference.
            if (attrs == null || attrs.Length == 0)
            {
                object[] attr = t.GetCustomAttributes(false);
                attrs = new object[0];
                if (attr != null && attr.Length > 0)
                {
                    for (int i = 0; i < attr.Length; i++)
                    {
                        if (attr[i].GetType().FullName.Equals("Microsoft.SqlServer.Server.SqlUserDefinedTypeAttribute"))
                        {
                            SqlUserDefinedTypeAttribute newAttr = null;
                            PropertyInfo[] sourceProps = attr[i].GetType().GetProperties();

                            foreach (PropertyInfo sourceProp in sourceProps)
                            {
                                if (sourceProp.Name.Equals("Format"))
                                {
                                    newAttr = new SqlUserDefinedTypeAttribute((Format)sourceProp.GetValue(attr[i], null));
                                    break;
                                }
                            }
                            if (newAttr != null)
                            {
                                foreach (PropertyInfo targetProp in newAttr.GetType().GetProperties())
                                {
                                    if (targetProp.CanRead && targetProp.CanWrite)
                                    {
                                        object copyValue = attr[i].GetType().GetProperty(targetProp.Name).GetValue(attr[i]);
                                        targetProp.SetValue(newAttr, copyValue);
                                    }
                                }
                            }

                            attrs = new object[1] { newAttr };
                            break;
                        }
                    }
                }
            }

            return attrs;
        }

        internal static SqlUserDefinedTypeAttribute GetUdtAttribute(Type t)
        {
            SqlUserDefinedTypeAttribute udtAttr = null;
            object[] attr = GetCustomAttributes(t);

            if (attr != null && attr.Length == 1)
            {
                udtAttr = (SqlUserDefinedTypeAttribute)attr[0];
            }
            else
            {
                Type InvalidUdtExceptionType = typeof(InvalidUdtException);
                var arguments = new Type[] { typeof(Type), typeof(String) };
                MethodInfo Create = InvalidUdtExceptionType.GetMethod("Create", arguments);
                Create.Invoke(null, new object[] { t, Strings.SqlUdtReason_NoUdtAttribute });
            }
            return udtAttr;
        }

        // Create a new serializer for the given type.
        private static Serializer GetNewSerializer(Type t)
        {
            SqlUserDefinedTypeAttribute udtAttr = GetUdtAttribute(t);

            switch (udtAttr.Format)
            {
                case Format.Native:
                    return new NormalizedSerializer(t);
                case Format.UserDefined:
                    return new BinarySerializeSerializer(t);
                case Format.Unknown: // should never happen, but fall through
                default:
                    throw ADP.InvalidUserDefinedTypeSerializationFormat(udtAttr.Format);
            }
        }
    }

    // The base serializer class.
    internal abstract class Serializer
    {
        public abstract object Deserialize(Stream s);
        public abstract void Serialize(Stream s, object o);
        protected Type m_type;

        protected Serializer(Type t)
        {
            this.m_type = t;
        }
    }

    internal sealed class NormalizedSerializer : Serializer
    {
        BinaryOrderedUdtNormalizer m_normalizer;
        bool m_isFixedSize;
        int m_maxSize;

        internal NormalizedSerializer(Type t) : base(t)
        {
            SqlUserDefinedTypeAttribute udtAttr = SerializationHelperSql9.GetUdtAttribute(t);
            this.m_normalizer = new BinaryOrderedUdtNormalizer(t, true);
            this.m_isFixedSize = udtAttr.IsFixedLength;
            this.m_maxSize = this.m_normalizer.Size;
        }

        public override void Serialize(Stream s, object o)
        {
            m_normalizer.NormalizeTopObject(o, s);
        }

        public override object Deserialize(Stream s)
        {
            object result = m_normalizer.DeNormalizeTopObject(this.m_type, s);
            return result;
        }
    }

    internal sealed class BinarySerializeSerializer : Serializer
    {
        internal BinarySerializeSerializer(Type t) : base(t)
        {
        }

        public override void Serialize(Stream s, object o)
        {
            BinaryWriter w = new BinaryWriter(s);
            if (o is Microsoft.SqlServer.Server.IBinarySerialize)
            {
                ((Microsoft.SqlServer.Server.IBinarySerialize)o).Write(w);
            }
            else
            {
                ((IBinarySerialize)o).Write(w);
            }
        }

        // Prevent inlining so that reflection calls are not moved to caller that may be in a different assembly that may have a different grant set.
        [MethodImpl(MethodImplOptions.NoInlining)]
        public override object Deserialize(Stream s)
        {
            object instance = Activator.CreateInstance(m_type);
            BinaryReader r = new BinaryReader(s);
            if (instance is Microsoft.SqlServer.Server.IBinarySerialize)
            {
                ((Microsoft.SqlServer.Server.IBinarySerialize)instance).Read(r);
            }
            else
            {
                ((IBinarySerialize)instance).Read(r);
            }
            return instance;
        }
    }

    // A dummy stream class, used to get the number of bytes written
    // to the stream.
    internal sealed class DummyStream : Stream
    {
        private long m_size;

        public DummyStream()
        {
        }

        private void DontDoIt()
        {
            throw new Exception(StringsHelper.GetString(Strings.Sql_InternalError));
        }

        public override bool CanRead
        {
            get
            {
                return false;
            }
        }

        public override bool CanWrite
        {
            get
            {
                return true;
            }
        }

        public override bool CanSeek
        {
            get
            {
                return false;
            }
        }

        public override long Position
        {
            get
            {
                return this.m_size;
            }
            set
            {
                this.m_size = value;
            }
        }

        public override long Length
        {
            get
            {
                return this.m_size;
            }
        }

        public override void SetLength(long value)
        {
            this.m_size = value;
        }

        public override long Seek(long value, SeekOrigin loc)
        {
            DontDoIt();
            return -1;
        }

        public override void Flush()
        {
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            DontDoIt();
            return -1;
        }

        public override void Write(byte[] buffer, int offset, int count)
        {
            this.m_size += count;
        }
    }
}

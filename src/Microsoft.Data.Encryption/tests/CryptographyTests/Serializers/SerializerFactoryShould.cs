using Microsoft.Data.Encryption.Cryptography.Serializers;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Xunit;

namespace Microsoft.Data.Encryption.CryptographyTests.Serializers
{
    public class SerializerFactoryShould
    {
        [Fact]
        public void ContainAllBuiltinSerializerTypes()
        {
            int serializerImplementationCount = Assembly.GetAssembly(typeof(ISerializer))
                .GetTypes()
                .Count(IsConcreteSerializer);

            Dictionary<string, ISerializer> registeredserializerDictionary = (Dictionary<string, ISerializer>)typeof(StandardSerializerFactory)
                .GetField("serializerByIdentifier", BindingFlags.NonPublic | BindingFlags.Instance)
                .GetValue(StandardSerializerFactory.Default);

            Dictionary<string, ISerializer> registeredSqlSerializerDictionary = (Dictionary<string, ISerializer>)typeof(SqlSerializerFactory)
                .GetField("serializerByIdentifier", BindingFlags.NonPublic | BindingFlags.Instance)
                .GetValue(SqlSerializerFactory.Default);

            Assert.Equal(serializerImplementationCount, registeredserializerDictionary.Count + registeredSqlSerializerDictionary.Count);

            #region Local Methods
            static bool IsConcreteClass(Type t) => t.IsClass && !t.IsAbstract && !t.IsInterface;
            static bool IsSerializer(Type t) => t.GetInterfaces().Contains(typeof(ISerializer));
            static bool IsConcreteSerializer(Type t) => IsConcreteClass(t) && IsSerializer(t);
            #endregion
        }
    }
}

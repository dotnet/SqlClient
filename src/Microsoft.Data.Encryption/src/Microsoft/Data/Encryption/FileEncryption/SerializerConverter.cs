using Microsoft.Data.Encryption.Cryptography.Serializers;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;

namespace Microsoft.Data.Encryption.FileEncryption
{
    internal class SerializerConverter : JsonConverter
    {
        public override bool CanWrite => false;
        public override bool CanRead => true;
        public override bool CanConvert(Type objectType)
        {
            return objectType == typeof(ISerializer);
        }

        private List<SerializerFactory> lookupFactories = new List<SerializerFactory>();

        public SerializerConverter build(SerializerFactory factory)
        {
            this.lookupFactories.Add(factory);
            return this;
        }

        public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
        {
            throw new InvalidOperationException("Use default serialization.");
        }

        public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
        {
            JObject jsonObject = JObject.Load(reader);
            string identifier = jsonObject[nameof(ISerializer.Identifier)].Value<string>();
            ISerializer iSerializer = (ISerializer)CreateDelegate(jsonObject, identifier).DynamicInvoke();
            return iSerializer;
        }

        private Delegate CreateDelegate(JObject jobject, string identifier)
        {
            ISerializer serializer = null;

            foreach (SerializerFactory factory in lookupFactories)
            {
                serializer = factory.GetSerializer(identifier);
                if (serializer != null)
                {
                    break;
                }
            }

            Type returnType = serializer?.GetType() ?? throw new InvalidOperationException($"No serializer found for {identifier}");

            MethodInfo methodInfo = typeof(JObject).GetMethods()
                .Where(x => x.Name == "ToObject")
                .FirstOrDefault(x => x.IsGenericMethod)
                .MakeGenericMethod(returnType);
            Type genericFunc = typeof(Func<>).MakeGenericType(returnType);
            return Delegate.CreateDelegate(genericFunc, jobject, methodInfo);
        }
    }
}

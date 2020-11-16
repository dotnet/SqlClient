using Microsoft.Data.Encryption.Cryptography;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Microsoft.Data.Encryption
{
    internal static class ArgumentValidationExtensions
    {
        internal static bool IsNull<T>(this T parameter)
        {
            return null == parameter;
        }

        internal static void ValidateNotNull<T>(this T parameter, string name)
        {
            if (parameter.IsNull())
            {
                throw new ArgumentNullException(string.Concat(name, " [", typeof(T), "]"));
            }
        }

        internal static void ValidateNotNullOrWhitespace(this string parameter, string name)
        {
            if (string.IsNullOrWhiteSpace(parameter))
            {
                throw new ArgumentException($"{name} cannot be null or empty or consist of only whitespace.");
            }
        }

        internal static void ValidateNotNullForEach<T>(this IEnumerable<T> parameters, string name)
        {
            if (parameters.Any(t => t.IsNull()))
            {
                throw new ArgumentNullException($"One of more of the elements in {name} is null.");
            }
        }

        internal static void ValidateNotNullOrWhitespaceForEach(this IEnumerable<string> parameters, string name)
        {
            if (parameters.Any(s => string.IsNullOrWhiteSpace(s)))
            {
                throw new ArgumentException($"One of more of the elements in {name} is null or empty or consist of only whitespace.");
            }
        }

        internal static void ValidateNotEmpty<T>(this IEnumerable<T> parameter, string name)
        {
            if (!parameter.Any())
            {
                throw new ArgumentException($"{name} cannot be empty.");
            }
        }

        internal static void ValidateGreaterThanSize<T>(this IEnumerable<T> parameter, int size, string name)
        {
            if (parameter.Count() < size)
            {
                throw new ArgumentOutOfRangeException(nameof(parameter), parameter, $"{name} must contain at least {size} elements.");
            }
        }

        internal static void ValidateSize<T>(this IEnumerable<T> parameter, int size, string name)
        {
            if (parameter.Count() != size)
            {
                throw new ArgumentOutOfRangeException(nameof(parameter), parameter, $"{name} must contain {size} elements.");
            }
        }

        internal static void ValidateType(this object parameter, Type type, string name)
        {
            if (!(parameter.GetType().Equals(type)))
            {
                throw new InvalidCastException($"Expected {name} to be of type {type}");
            }
        }

        internal static void ValidatePositive(this int parameter, string name)
        {
            if (parameter <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(parameter), parameter, $"{name} must be a positive integer.");
            }
        }

        internal static void ValidateNotPlaintext(this EncryptionSettings encryptionSettings, string name)
        {
            if (encryptionSettings.EncryptionType == EncryptionType.Plaintext)
            {
                throw new ArgumentException($"The {name} {nameof(EncryptionType)} cannot be Plaintext in this context.");
            }
        }
    }
}

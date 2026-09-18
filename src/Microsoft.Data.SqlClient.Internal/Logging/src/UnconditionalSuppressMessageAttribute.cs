// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace System.Diagnostics.CodeAnalysis
{
    /// <summary>
    /// Polyfill for netstandard2.0. The trimmer recognizes this attribute by name.
    /// </summary>
    /// <remarks>
    /// Unlike <see cref="SuppressMessageAttribute"/>, it is not conditional, so it is kept in the
    /// compiled assembly where the trimmer can see it.
    /// </remarks>
    [AttributeUsage(AttributeTargets.All, Inherited = false, AllowMultiple = true)]
    internal sealed class UnconditionalSuppressMessageAttribute : Attribute
    {
        public UnconditionalSuppressMessageAttribute(string category, string checkId)
        {
            Category = category;
            CheckId = checkId;
        }

        public string Category { get; }

        public string CheckId { get; }

        public string? Scope { get; set; }

        public string? Target { get; set; }

        public string? MessageId { get; set; }

        public string? Justification { get; set; }
    }
}

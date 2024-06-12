﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;

namespace Microsoft.Data.SqlClientX.Handlers
{
    /// <summary>
    /// The request type for the handler.
    /// </summary>
    internal abstract class HandlerRequest
    {
        /// <summary>
        /// Exposes the request type for this handler.
        /// </summary>
        public HandlerRequestType RequestType { get; internal set; }

        /// <summary>
        /// When the Exception is set, that means that the next handler knows about the exception,
        /// and it can choose to execute or perform any clean ups.
        /// </summary>
        public Exception Exception { get; set; }
    }
}
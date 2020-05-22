// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.Data
{
    using System;
    using System.ComponentModel;
    using System.ComponentModel.Design.Serialization;

    sealed internal class RelationshipConverter : ExpandableObjectConverter
    {

        // converter classes should have public ctor
        public RelationshipConverter()
        {
        }

        /// <devdoc>
        ///    <para>Gets a value indicating whether this converter can
        ///       convert an object to the given destination type using the context.</para>
        /// </devdoc>
        public override bool CanConvertTo(ITypeDescriptorContext context, Type destinationType)
        {
            if (destinationType == typeof(InstanceDescriptor))
            {
                return true;
            }
            return base.CanConvertTo(context, destinationType);
        }
    }
}


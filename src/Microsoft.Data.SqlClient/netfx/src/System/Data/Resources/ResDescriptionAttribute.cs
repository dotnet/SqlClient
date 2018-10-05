//------------------------------------------------------------------------------
// <copyright file="ResDescriptionAttribute.cs" company="Microsoft">
//     Copyright (c) Microsoft Corporation.  All rights reserved.
// </copyright>
//------------------------------------------------------------------------------

namespace Microsoft.Data
{
    using System;
    using System.ComponentModel;

    [AttributeUsage(AttributeTargets.All)]
    internal sealed class ResDescriptionAttribute : DescriptionAttribute
    {

        private bool replaced = false;

        /// <summary>
        ///     Constructs a new sys description.
        /// </summary>
        /// <param name='description'>
        ///     description text.
        /// </param>
        public ResDescriptionAttribute(string description) : base(description)
        {
        }

        /// <summary>
        ///     Retrieves the description text.
        /// </summary>
        /// <returns>
        ///     description
        /// </returns>
        public override string Description
        {
            get
            {
                if (!replaced)
                {
                    replaced = true;
                    DescriptionValue = ResHelper.GetString(base.Description);
                }
                return base.Description;
            }
        }
    }
}

// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.ComponentModel;
using System.Reflection;
using Microsoft.Data.SqlClient;
using Xunit;

namespace Microsoft.Data.UnitTests;

public class LocalisedAttributeTest
{
    /// <summary>
    /// This property must have both the ResDescription and the ResCategory attribute.
    /// </summary>
    private const string PropertyName = nameof(SqlConnectionStringBuilder.CommandTimeout);

    /// <summary>
    /// Type must contain the PropertyName property with both attributes.
    /// </summary>
    private static readonly Type s_exampleType = typeof(SqlConnectionStringBuilder);

    /// <summary>
    /// Verifies that the <see cref="ResDescriptionAttribute"/> correctly localizes its value.
    /// </summary>
    /// <remarks>
    /// This test validates the description when accessed via both PropertyDescriptor and directly
    /// via the attribute.
    /// </remarks>
    [Fact]
    public void ResDescription_Attribute_Localizes_Description()
    {
        PropertyDescriptorCollection typeDescriptorProperties = TypeDescriptor.GetProperties(s_exampleType);
        string expectedDescription = Strings.DbCommand_CommandTimeout;

        PropertyDescriptor? csbCommandTimeoutDescriptor = typeDescriptorProperties[PropertyName];
        PropertyInfo? csbCommandTimeoutPI = s_exampleType.GetProperty(PropertyName);
        ResDescriptionAttribute? descriptionAttribute = csbCommandTimeoutPI is null
            ? null 
            : (ResDescriptionAttribute?)Attribute.GetCustomAttribute(csbCommandTimeoutPI, typeof(ResDescriptionAttribute), false);

        Assert.NotNull(csbCommandTimeoutDescriptor);
        Assert.NotNull(descriptionAttribute);

        Assert.NotEqual(nameof(Strings.DbCommand_CommandTimeout), expectedDescription);
        Assert.Equal(expectedDescription, csbCommandTimeoutDescriptor.Description);
        Assert.Equal(expectedDescription, descriptionAttribute.Description);
    }

    /// <summary>
    /// Verifies that the <see cref="ResCategoryAttribute"/> correctly localizes its value.
    /// </summary>
    /// <remarks>
    /// This test validates the description when accessed via both PropertyDescriptor and directly
    /// via the attribute.
    /// </remarks>
    [Fact]
    public void ResCategory_Attribute_Localizes_Category()
    {
        PropertyDescriptorCollection typeDescriptorProperties = TypeDescriptor.GetProperties(s_exampleType);
        string expectedCategory = Strings.DataCategory_Initialization;

        PropertyDescriptor? csbCommandTimeoutDescriptor = typeDescriptorProperties[PropertyName];
        PropertyInfo? csbCommandTimeoutPI = s_exampleType.GetProperty(PropertyName);
        ResCategoryAttribute? categoryAttribute = csbCommandTimeoutPI is null
            ? null 
            : (ResCategoryAttribute?)Attribute.GetCustomAttribute(csbCommandTimeoutPI, typeof(ResCategoryAttribute), false);

        Assert.NotNull(csbCommandTimeoutDescriptor);
        Assert.NotNull(categoryAttribute);

        Assert.NotEqual(nameof(Strings.DataCategory_Initialization), expectedCategory);
        Assert.Equal(expectedCategory, csbCommandTimeoutDescriptor.Category);
        Assert.Equal(expectedCategory, categoryAttribute.Category);
    }
}

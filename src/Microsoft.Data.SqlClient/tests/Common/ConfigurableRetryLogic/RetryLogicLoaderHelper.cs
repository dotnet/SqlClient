// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Reflection;

namespace Microsoft.Data.SqlClient.Tests.Common.ConfigurableRetryLogic;

/// <summary>
/// Contains helper methods for creating and configuring instances of
/// SqlConfigurableRetryLogicLoader and its associated configuration objects without needing
/// to populate an app.config file.
/// </summary>
public static class RetryLogicLoaderHelper
{
    private const string ConfigurationLoaderTypeName = "Microsoft.Data.SqlClient.SqlConfigurableRetryLogicLoader";
    private const string InterfaceCnnCfgTypeName = "Microsoft.Data.SqlClient.ISqlConfigurableRetryConnectionSection";
    private const string InterfaceCmdCfgTypeName = "Microsoft.Data.SqlClient.ISqlConfigurableRetryCommandSection";
    private const string CnnCfgTypeName = "Microsoft.Data.SqlClient.SqlConfigurableRetryConnectionSection";
    private const string CmdCfgTypeName = "Microsoft.Data.SqlClient.SqlConfigurableRetryCommandSection";
    private const string DefaultTransientErrors = "1204, 1205, 1222, 49918, 49919, 49920, 4060, 4221, 40143, 40613, 40501, 40540, 40197, 42108, 42109, 10929, 10928, 10060, 10054, 10053, 997, 233, 64, 20, 0, -2, 207, 102, 2812";

    private static readonly Random s_random = new();

    private static readonly Assembly s_sqlClientAssembly = typeof(SqlConnection).Assembly;
    private static readonly Type s_configurationLoaderType = s_sqlClientAssembly.GetType(ConfigurationLoaderTypeName)!;
    private static readonly Type s_interfaceCnnCfgType = s_sqlClientAssembly.GetType(InterfaceCnnCfgTypeName)!;
    private static readonly Type s_interfaceCmdCfgType = s_sqlClientAssembly.GetType(InterfaceCmdCfgTypeName)!;
    private static readonly Type s_cnnCfgType = s_sqlClientAssembly.GetType(CnnCfgTypeName)!;
    private static readonly Type s_cmdCfgType = s_sqlClientAssembly.GetType(CmdCfgTypeName)!;

    private static readonly Type[] s_cfgLoaderParamsType =
        [
            s_interfaceCnnCfgType,
            s_interfaceCmdCfgType,
            typeof(string), typeof(string)
        ];
    private static readonly ConstructorInfo s_loaderCtorInfo = s_configurationLoaderType.GetConstructor(s_cfgLoaderParamsType)!;

    /// <summary>
    /// Creates and returns an instance of SqlConfigurableRetryLogicLoader with the specified
    /// connection and command retry logic configurations.
    /// </summary>
    /// <param name="cnnConfig">The configuration for the connection retry logic.</param>
    /// <param name="cmdConfig">The configuration for the command retry logic.</param>
    /// <returns>The created loader instance.</returns>
    public static object CreateLoader(RetryLogicConfigs cnnConfig, RetryLogicConfigs cmdConfig)
    {
        object cnnCfgObj = Activator.CreateInstance(s_cnnCfgType)!;
        SetValue(cnnCfgObj, s_cnnCfgType, "DeltaTime", cnnConfig.DeltaTime);
        SetValue(cnnCfgObj, s_cnnCfgType, "MinTimeInterval", cnnConfig.MinTimeInterval);
        SetValue(cnnCfgObj, s_cnnCfgType, "MaxTimeInterval", cnnConfig.MaxTimeInterval);
        SetValue(cnnCfgObj, s_cnnCfgType, "NumberOfTries", cnnConfig.NumberOfTries);
        SetValue(cnnCfgObj, s_cnnCfgType, "AuthorizedSqlCondition", cnnConfig.AuthorizedSqlCondition);
        SetValue(cnnCfgObj, s_cnnCfgType, "TransientErrors", cnnConfig.TransientErrors);
        SetValue(cnnCfgObj, s_cnnCfgType, "RetryLogicType", cnnConfig.RetryLogicType);
        SetValue(cnnCfgObj, s_cnnCfgType, "RetryMethod", cnnConfig.RetryMethodName);

        object cmdCfgObj = Activator.CreateInstance(s_cmdCfgType)!;
        SetValue(cmdCfgObj, s_cmdCfgType, "DeltaTime", cmdConfig.DeltaTime);
        SetValue(cmdCfgObj, s_cmdCfgType, "MinTimeInterval", cmdConfig.MinTimeInterval);
        SetValue(cmdCfgObj, s_cmdCfgType, "MaxTimeInterval", cmdConfig.MaxTimeInterval);
        SetValue(cmdCfgObj, s_cmdCfgType, "NumberOfTries", cmdConfig.NumberOfTries);
        SetValue(cmdCfgObj, s_cmdCfgType, "AuthorizedSqlCondition", cmdConfig.AuthorizedSqlCondition);
        SetValue(cmdCfgObj, s_cmdCfgType, "TransientErrors", cmdConfig.TransientErrors);
        SetValue(cmdCfgObj, s_cmdCfgType, "RetryLogicType", cmdConfig.RetryLogicType);
        SetValue(cmdCfgObj, s_cmdCfgType, "RetryMethod", cmdConfig.RetryMethodName);

        return s_loaderCtorInfo.Invoke([cnnCfgObj, cmdCfgObj, default!, default!]);
    }

    /// <summary>
    /// Creates and returns an instance of SqlConfigurableRetryLogicLoader with the specified
    /// connection and command retry logic configurations, and also retrieves the associated
    /// connection and command providers.
    /// </summary>
    /// <param name="cnnCfg">The configuration for the connection retry logic.</param>
    /// <param name="cmdCfg">The configuration for the command retry logic.</param>
    /// <param name="cnnProvider">When this method returns, contains the connection provider.</param>
    /// <param name="cmdProvider">When this method returns, contains the command provider.</param>
    /// <returns>The created loader instance.</returns>
    public static object ReturnLoaderAndProviders(RetryLogicConfigs cnnCfg, RetryLogicConfigs cmdCfg, out SqlRetryLogicBaseProvider cnnProvider, out SqlRetryLogicBaseProvider cmdProvider)
    {
        object loaderObj = CreateLoader(cnnCfg, cmdCfg);

        cnnProvider = GetValue<SqlRetryLogicBaseProvider>(loaderObj, s_configurationLoaderType, "ConnectionProvider");
        cmdProvider = GetValue<SqlRetryLogicBaseProvider>(loaderObj, s_configurationLoaderType, "CommandProvider");
        return loaderObj;
    }

    /// <summary>
    /// Creates a RetryLogicConfigs object with random values for its properties, optionally specifying
    /// default values for certain mandatory properties.
    /// </summary>
    /// <param name="method">The retry method to use.</param>
    /// <param name="authorizedSqlCondition">The authorized SQL condition.</param>
    /// <param name="transientErrors">The transient errors.</param>
    /// <returns>The created retry logic configuration.</returns>
    public static RetryLogicConfigs CreateRandomConfig(string? method, string? authorizedSqlCondition = null, string transientErrors = DefaultTransientErrors)
    {
        TimeSpan start = TimeSpan.Zero;
        TimeSpan end = TimeSpan.FromSeconds(60);
        TimeSpan min = GenerateTimeSpan(start, end);

        return new RetryLogicConfigs()
        {
            DeltaTime = GenerateTimeSpan(start, end),
            MinTimeInterval = min,
            MaxTimeInterval = GenerateTimeSpan(min, end),
            NumberOfTries = s_random.Next(1, 60),
            AuthorizedSqlCondition = authorizedSqlCondition,
            TransientErrors = transientErrors,
            RetryMethodName = method
        };
    }

    private static T GetValue<T>(object obj, Type type, string propName, BindingFlags flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance) =>
        (T)type.GetProperty(propName, flags)?.GetValue(obj)!;

    private static void SetValue<T>(object obj, Type type, string propName, T value, BindingFlags flags = BindingFlags.Public | BindingFlags.Instance) =>
        type.GetProperty(propName, flags)?.SetValue(obj, value);

    private static TimeSpan GenerateTimeSpan(TimeSpan start, TimeSpan end)
    {
        int max = (int)(end - start).TotalSeconds;
        return start.Add(TimeSpan.FromSeconds(s_random.Next(max)));
    }
}

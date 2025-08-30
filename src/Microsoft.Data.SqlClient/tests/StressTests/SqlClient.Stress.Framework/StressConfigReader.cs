// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Xml;
using System.Xml.XPath;
using static Stress.Data.DataStressSettings;

namespace Stress.Data
{
    /// <summary>
    /// Reads the configuration from a configuration file and provides the configuration
    /// </summary>
    internal class StressConfigReader
    {
        private readonly string _configFilePath;
        private readonly bool _configIsJson;
        private const string dataStressSettings = "dataStressSettings";
        private const string sourcePath = "//dataStressSettings/sources/source";
        internal List<DataSourceElement> Sources
        {
            get; private set;
        }

        public StressConfigReader(string configFilePath)
        {
            _configFilePath = configFilePath;

            // If the config filename extension is 'json' or 'jsonc', we parse
            // it as JSON.
            if (configFilePath.EndsWith(".json", StringComparison.OrdinalIgnoreCase) ||
                configFilePath.EndsWith(".jsonc", StringComparison.OrdinalIgnoreCase))
            {
                _configIsJson = true;
            }
            // Otherwise, parse it as XML.
            else
            {
                _configIsJson = false;

                // The original code always prepended the Framework project
                // directory onto whatever path was given, so we do the same if
                // that isn't already present.
                if (!_configFilePath.StartsWith("SqlClient.Stress.Framework/"))
                {
                    _configFilePath = Path.Combine("SqlClient.Stress.Framework", _configFilePath);
                }
            }
        }

        internal void Load()
        {
            if (_configIsJson)
            {
                LoadJson();
            }
            else
            {
                LoadXml();
            }
        }

        private struct JsonDataSource
        {
            public string Name { get; set; }
            public string Type { get; set; }
            public bool IsDefault { get; set; }
            public string DataSource { get; set; }
            public string EntraIdUser { get; set; }
            public string EntraIdPassword { get; set; }
            public string User { get; set; }
            public string Password { get; set; }
            public bool SupportsWindowsAuthentication { get; set; }
            public bool IsLocal { get; set; }
            public bool DisableMultiSubnetFailover { get; set; }
            public bool DisableNamedPipes { get; set; }
            public bool Encrypt { get; set; }
        }

        private void LoadJson()
        {
            var sources = JsonSerializer.Deserialize<List<JsonDataSource>>(
                File.ReadAllText(_configFilePath),
                new JsonSerializerOptions()
                {
                    IncludeFields = true,
                    PropertyNameCaseInsensitive = true,
                    ReadCommentHandling = JsonCommentHandling.Skip
                });

            Sources = new(sources.Count);

            foreach (var source in sources)
            {
                Sources.Add(new DataSourceElement(
                    source.Name,
                    source.Type,
                    null,
                    source.DataSource,
                    source.EntraIdUser,
                    source.EntraIdPassword,
                    source.User,
                    source.Password,
                    ds_isDefault: source.IsDefault,
                    ds_isLocal: source.IsLocal,
                    disableMultiSubnetFailoverSetup: source.DisableMultiSubnetFailover,
                    disableNamedPipes: source.DisableNamedPipes,
                    encrypt: source.Encrypt));
            }
        }

        private void LoadXml()
        {
            XmlReader reader = null;
            try
            {
                Sources = new List<DataSourceElement>();
                reader = CreateReader();

                XPathDocument xpathDocument = new XPathDocument(reader);

                XPathNavigator navigator = xpathDocument.CreateNavigator();

                XPathNodeIterator sourceIterator = navigator.Select(sourcePath);

                foreach (XPathNavigator sourceNavigator in sourceIterator)
                {
                    string nsUri = sourceNavigator.NamespaceURI;
                    string sourceName = sourceNavigator.GetAttribute("name", nsUri);
                    string sourceType = sourceNavigator.GetAttribute("type", nsUri);
                    bool isDefault;
                    isDefault = bool.TryParse(sourceNavigator.GetAttribute("isDefault", nsUri), out isDefault) ? isDefault : false;
                    string dataSource = sourceNavigator.GetAttribute("dataSource", nsUri);
                    string user = sourceNavigator.GetAttribute("user", nsUri);
                    string password = sourceNavigator.GetAttribute("password", nsUri);
                    bool supportsWindowsAuthentication;
                    supportsWindowsAuthentication = bool.TryParse(sourceNavigator.GetAttribute("supportsWindowsAuthentication", nsUri), out supportsWindowsAuthentication) ? supportsWindowsAuthentication : false;
                    bool isLocal;
                    isLocal = bool.TryParse(sourceNavigator.GetAttribute("isLocal", nsUri), out isLocal) ? isLocal : false;
                    bool disableMultiSubnetFailover;
                    disableMultiSubnetFailover = bool.TryParse(sourceNavigator.GetAttribute("disableMultiSubnetFailover", nsUri), out disableMultiSubnetFailover) ? disableMultiSubnetFailover : false;
                    bool disableNamedPipes;
                    disableMultiSubnetFailover = bool.TryParse(sourceNavigator.GetAttribute("disableNamedPipes", nsUri), out disableNamedPipes) ? disableNamedPipes : false;
                    bool encrypt;
                    encrypt = bool.TryParse(sourceNavigator.GetAttribute("encrypt", nsUri), out encrypt) ? encrypt : false;

                    DataSourceElement element = new(
                        sourceName,
                        sourceType,
                        null,
                        dataSource,
                        string.Empty,
                        string.Empty,
                        user,
                        password,
                        ds_isDefault: isDefault,
                        ds_isLocal: isLocal,
                        disableMultiSubnetFailoverSetup: disableMultiSubnetFailover,
                        disableNamedPipes: disableNamedPipes,
                        encrypt: encrypt);
                    Sources.Add(element);
                }
            }
            catch (XmlException e)
            {
                throw new InvalidDataException($"Error reading configuration file '{_configFilePath}': {e.Message}", e);
            }
            catch (IOException e)
            {
                throw new InvalidDataException($"Error reading configuration file '{_configFilePath}': {e.Message}", e);
            }
            catch (System.Exception e)
            {
                throw new InvalidDataException($"Error reading configuration file '{_configFilePath}': {e.Message}", e);
            }
            finally
            {
                reader?.Dispose();
            }
        }

        private XmlReader CreateReader()
        {
            FileStream configurationStream = new FileStream("SqlClient.Stress.Framework/" + _configFilePath, FileMode.Open);
            XmlReaderSettings settings = new XmlReaderSettings();
            settings.DtdProcessing = DtdProcessing.Prohibit;
            XmlReader reader = XmlReader.Create(configurationStream, settings);
            return reader;
        }
    }
}

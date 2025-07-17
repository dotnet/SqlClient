// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Threading;
using Microsoft.SqlServer.TDS.Servers;
using Xunit;

namespace Microsoft.Data.SqlClient.Tests
{
    public class LocalizationTest
    {
        private Dictionary<string, string> _expectedCultureErrorMessage = new Dictionary<string, string>
        {
            { "en-EN", "A network-related or instance-specific error occurred while establishing a connection to SQL Server." },
            { "de-DE", "Netzwerkbezogener oder instanzspezifischer Fehler beim Herstellen einer Verbindung mit SQL Server." },
            { "es-ES", "Error relacionado con la red o específico de la instancia mientras se establecía una conexión con el servidor SQL Server." },
            { "fr-FR", "Une erreur liée au réseau ou spécifique à l'instance s'est produite lors de l'établissement d'une connexion à SQL Server." },
            { "it-IT", "Si è verificato un errore di rete o specifico dell'istanza mentre si cercava di stabilire una connessione con SQL Server." },
            { "ja-JA", "SQL Server への接続を確立しているときにネットワーク関連またはインスタンス固有のエラーが発生しました。" },
            { "ko-KO", "SQL Server에 연결을 설정하는 중에 네트워크 관련 또는 인스턴스 관련 오류가 발생했습니다." },
            { "pt-BR", "Erro de rede ou específico à instância ao estabelecer conexão com o SQL Server." },
            { "ru-RU", "При установлении соединения с SQL Server произошла ошибка, связанная с сетью или с определенным экземпляром." },
            { "zh-Hans", "在与 SQL Server 建立连接时出现与网络相关的或特定于实例的错误。" },
            { "zh-Hant", "建立連接至 SQL Server 時，發生網路相關或執行個體特定的錯誤。" },
        };

        [Theory]
        [InlineData("en-EN")]
        [InlineData("de-DE")]
        [InlineData("es-ES")]
        [InlineData("fr-FR")]
        [InlineData("it-IT")]
        [InlineData("ja-JA")]
        [InlineData("ko-KO")]
        [InlineData("pt-BR")]
        [InlineData("ru-RU")]
        [InlineData("zh-Hans")]
        [InlineData("zh-Hant")]
        public void Localization_Tests(string culture)
        {
            string localized = GetLocalizedErrorMessage(culture);
            Assert.Contains(_expectedCultureErrorMessage[culture], localized);
        }

        private string GetLocalizedErrorMessage(string culture)
        {
            var localized = "";

            CultureInfo savedCulture = Thread.CurrentThread.CurrentCulture;
            CultureInfo savedUICulture = Thread.CurrentThread.CurrentUICulture;

            Thread.CurrentThread.CurrentCulture = new CultureInfo(culture);
            Thread.CurrentThread.CurrentUICulture = new CultureInfo(culture);

            using TdsServer server = new TdsServer(new TdsServerArguments() { });
            server.Start();
            var connStr = new SqlConnectionStringBuilder() { DataSource = $"dummy,{server.EndPoint.Port}" }.ConnectionString;
            using SqlConnection connection = new SqlConnection(connStr);

            try
            {
                connection.Open();
            }
            catch (Exception ex)
            {
                localized = ex.Message;
            }

            // Restore saved culture if necessary
            if (Thread.CurrentThread.CurrentCulture != savedCulture)
                Thread.CurrentThread.CurrentCulture = savedCulture;
            if (Thread.CurrentThread.CurrentUICulture != savedUICulture)
                Thread.CurrentThread.CurrentUICulture = savedUICulture;

            return localized;
        }
    }
}

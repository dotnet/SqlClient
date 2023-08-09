// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Data;
using System.Data.Common;
using System.Globalization;
using System.Reflection;
using System.Security;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.SqlServer.TDS.Servers;
using Xunit;

namespace Microsoft.Data.SqlClient.Tests
{
    public class LocalizationTest
    {
        [Fact]
        public void Localization_EN_Test()
        {
            var localized = "";
            var expected = "A network-related or instance-specific error occurred while establishing a connection to SQL Server.";

            using TestTdsServer server = TestTdsServer.StartTestServer();
            var connStr = server.ConnectionString;
            connStr = connStr.Replace("localhost", "dummy");
            using SqlConnection connection = new SqlConnection(connStr);

            try
            {
                connection.Open();
            }
            catch (Exception ex)
            {
                localized = ex.Message;
            }

            Assert.Contains(expected, localized);
        }

        [Fact]
        public void Localization_DE_Test()
        {
            var expected = "Netzwerkbezogener oder instanzspezifischer Fehler beim Herstellen einer Verbindung mit SQL Server.";

            string localized = GetLocalizedErrorMessage("de-DE");

            Assert.Contains(expected, localized);
        }

        [Fact]
        public void Localization_ES_Test()
        {
            var expected = "Error relacionado con la red o específico de la instancia mientras se establecía una conexión con el servidor SQL Server.";

            string localized = GetLocalizedErrorMessage("es-ES");

            Assert.Contains(expected, localized);
        }

        [Fact]
        public void Localization_FR_Test()
        {
            var expected = "Une erreur liée au réseau ou spécifique à l'instance s'est produite lors de l'établissement d'une connexion à SQL Server.";

            string localized = GetLocalizedErrorMessage("fr-FR");

            Assert.Contains(expected, localized);
        }

        [Fact]
        public void Localization_IT_Test()
        {
            var expected = "Si è verificato un errore di rete o specifico dell'istanza mentre si cercava di stabilire una connessione con SQL Server.";

            string localized = GetLocalizedErrorMessage("it-IT");

            Assert.Contains(expected, localized);
        }

        [Fact]
        public void Localization_JA_Test()
        {
            var expected = "SQL Server への接続を確立しているときにネットワーク関連またはインスタンス固有のエラーが発生しました。";

            string localized = GetLocalizedErrorMessage("ja-JA");

            Assert.Contains(expected, localized);
        }

        [Fact]
        public void Localization_KO_Test()
        {
            var expected = "SQL Server에 연결을 설정하는 중에 네트워크 관련 또는 인스턴스 관련 오류가 발생했습니다.";

            string localized = GetLocalizedErrorMessage("ko-KO");

            Assert.Contains(expected, localized);
        }

        [Fact]
        public void Localization_PT_BR_Test()
        {
            var expected = "Erro de rede ou específico à instância ao estabelecer conexão com o SQL Server.";

            string localized = GetLocalizedErrorMessage("pt-BR");

            Assert.Contains(expected, localized);
        }

        [Fact]
        public void Localization_RU_Test()
        {
            var expected = "При установлении соединения с SQL Server произошла ошибка, связанная с сетью или с определенным экземпляром.";

            string localized = GetLocalizedErrorMessage("ru-RU");

            Assert.Contains(expected, localized);
        }

        [Fact]
        public void Localization_ZH_HANS_Test()
        {
            var expected = "在与 SQL Server 建立连接时出现与网络相关的或特定于实例的错误。";

            string localized = GetLocalizedErrorMessage("zh-Hans");

            Assert.Contains(expected, localized);
        }

        [Fact]
        public void Localization_ZH_HANT_Test()
        {
            var expected = "建立連接至 SQL Server 時，發生網路相關或執行個體特定的錯誤。";

            string localized = GetLocalizedErrorMessage("zh-Hant");

            Assert.Contains(expected, localized);
        }

        private string GetLocalizedErrorMessage(string culture)
        {
            var localized = "";

            CultureInfo savedCulture = Thread.CurrentThread.CurrentCulture;
            CultureInfo savedUICulture = Thread.CurrentThread.CurrentUICulture;

            Thread.CurrentThread.CurrentCulture = new CultureInfo(culture);
            Thread.CurrentThread.CurrentUICulture = new CultureInfo(culture);

            using TestTdsServer server = TestTdsServer.StartTestServer();
            var connStr = server.ConnectionString;
            connStr = connStr.Replace("localhost", "dummy");
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

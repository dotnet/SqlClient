// =============================================================================
// ODBC Driver Native C++ Memory Benchmark
// =============================================================================
// Compares native memory behavior of ODBC Driver 17/18 for SQL Server under
// different encryption modes. This is the native C++ version of the .NET
// OdbcMemoryBenchmark, created to reproduce the SChannel TLS 1.3 session
// ticket cache leak without any .NET runtime overhead.
//
// Environment variables:
//   BENCHMARK_SERVER   - SQL Server hostname (required)
//   BENCHMARK_DATABASE - Database name (default: master)
//   BENCHMARK_USER     - SQL Auth username (required)
//   BENCHMARK_PASSWORD - SQL Auth password (required)
//
// Build (MSVC):
//   cl /EHsc /O2 OdbcMemoryBenchmark.cpp /link odbc32.lib
//
// Build (CMake):
//   cmake -B build && cmake --build build --config Release
//
// Usage:
//   OdbcMemoryBenchmark.exe
//   OdbcMemoryBenchmark.exe --connections 10000 --encrypt Strict
//   OdbcMemoryBenchmark.exe --connections 5000 --encrypt Mandatory --driver "ODBC Driver 18 for SQL Server"
//   OdbcMemoryBenchmark.exe --connections 5000 --encrypt Strict --no-pooling
//   OdbcMemoryBenchmark.exe --connections 5000 --encrypt Strict --mimic-net-odbc
//
// Pooling modes:
//   default              : SQL_CP_ONE_PER_DRIVER + same connection string -> DM pool hits
//   --no-pooling         : SQL_CP_OFF + unique APP= per iter             -> fresh handshake each iter
//   --mimic-net-odbc     : SQL_CP_ONE_PER_HENV + unique APP= per iter    -> mirrors System.Data.Odbc
//                          behavior so we can directly compare to the .NET ODBC bench. Each unique
//                          connection string accumulates a separate DM pool entry that retains
//                          its own SChannel context.
// =============================================================================

#define WIN32_LEAN_AND_MEAN
#include <windows.h>
#include <psapi.h>
#include <sql.h>
#include <sqlext.h>

#include <cstdio>
#include <cstdlib>
#include <cstring>
#include <string>
#include <vector>
#include <chrono>
#include <algorithm>
#include <fstream>
#include <ctime>
#include <iomanip>
#include <sstream>

#pragma comment(lib, "odbc32.lib")
#pragma comment(lib, "psapi.lib")

// ─── Helpers ─────────────────────────────────────────────────────────────────

static std::string GetEnvVar(const char* name, const char* defaultValue = "")
{
    char buf[4096];
    DWORD len = GetEnvironmentVariableA(name, buf, sizeof(buf));
    if (len == 0 || len >= sizeof(buf))
        return defaultValue ? defaultValue : "";
    return std::string(buf, len);
}

static int GetArgInt(int argc, char* argv[], const char* name, int defaultValue)
{
    for (int i = 1; i < argc - 1; i++)
    {
        if (_stricmp(argv[i], name) == 0)
            return atoi(argv[i + 1]);
    }
    return defaultValue;
}

static std::string GetArgStr(int argc, char* argv[], const char* name, const char* defaultValue)
{
    for (int i = 1; i < argc - 1; i++)
    {
        if (_stricmp(argv[i], name) == 0)
            return argv[i + 1];
    }
    return defaultValue ? defaultValue : "";
}

static bool HasArg(int argc, char* argv[], const char* name)
{
    for (int i = 1; i < argc; i++)
    {
        if (_stricmp(argv[i], name) == 0)
            return true;
    }
    return false;
}

static SIZE_T GetPrivateBytes()
{
    PROCESS_MEMORY_COUNTERS_EX pmc = {};
    pmc.cb = sizeof(pmc);
    if (GetProcessMemoryInfo(GetCurrentProcess(), (PROCESS_MEMORY_COUNTERS*)&pmc, sizeof(pmc)))
        return pmc.PrivateUsage;
    return 0;
}

static SIZE_T GetWorkingSet()
{
    PROCESS_MEMORY_COUNTERS_EX pmc = {};
    pmc.cb = sizeof(pmc);
    if (GetProcessMemoryInfo(GetCurrentProcess(), (PROCESS_MEMORY_COUNTERS*)&pmc, sizeof(pmc)))
        return pmc.WorkingSetSize;
    return 0;
}

static std::string FormatTdsVersion(int protocolVersion)
{
    int firstByte = (protocolVersion >> 24) & 0xFF;
    switch (firstByte)
    {
    case 0x08: return "8.0";
    case 0x74: return "7.4";
    case 0x73: return "7.3";
    case 0x72: return "7.2";
    case 0x71: return "7.1";
    case 0x70: return "7.0";
    default:
        char buf[32];
        snprintf(buf, sizeof(buf), "%d.?", firstByte);
        return buf;
    }
}

static std::string BuildConnectionString(const std::string& driver, const std::string& server,
    const std::string& database, const std::string& user,
    const std::string& password, const std::string& encryptMode)
{
    std::string cs;
    cs += "Driver={" + driver + "};";
    cs += "Server=" + server + ";";
    cs += "Database=" + database + ";";
    cs += "UID=" + user + ";";
    cs += "PWD=" + password + ";";

    // Case-insensitive comparison for encrypt mode
    std::string encLower = encryptMode;
    std::transform(encLower.begin(), encLower.end(), encLower.begin(), ::tolower);

    if (encLower == "strict")
    {
        cs += "Encrypt=Strict;";
    }
    else if (encLower == "mandatory" || encLower == "yes")
    {
        cs += "Encrypt=Mandatory;TrustServerCertificate=yes;";
    }
    else if (encLower == "optional" || encLower == "no")
    {
        cs += "Encrypt=Optional;";
    }
    else
    {
        cs += "Encrypt=" + encryptMode + ";";
    }

    cs += "Connection Timeout=30;";
    return cs;
}

static std::string SanitizeConnectionString(const std::string& cs, const std::string& password)
{
    std::string result = cs;
    size_t pos = result.find(password);
    while (pos != std::string::npos)
    {
        result.replace(pos, password.length(), "****");
        pos = result.find(password, pos + 4);
    }
    return result;
}

static void PrintOdbcError(SQLSMALLINT handleType, SQLHANDLE handle)
{
    SQLCHAR sqlState[6], message[1024];
    SQLINTEGER nativeError;
    SQLSMALLINT msgLen;
    SQLSMALLINT i = 1;

    while (SQLGetDiagRecA(handleType, handle, i, sqlState, &nativeError,
        message, sizeof(message), &msgLen) == SQL_SUCCESS)
    {
        fprintf(stderr, "  [%s] %s (native error: %d)\n", sqlState, message, (int)nativeError);
        i++;
    }
}

static std::string GetCurrentTimestamp()
{
    auto now = std::chrono::system_clock::now();
    auto time_t_now = std::chrono::system_clock::to_time_t(now);
    struct tm tm_now;
    gmtime_s(&tm_now, &time_t_now);
    char buf[64];
    strftime(buf, sizeof(buf), "%Y-%m-%dT%H:%M:%SZ", &tm_now);
    return buf;
}

// ─── Main ────────────────────────────────────────────────────────────────────

int main(int argc, char* argv[])
{
    // Read environment variables
    std::string server = GetEnvVar("BENCHMARK_SERVER");
    std::string database = GetEnvVar("BENCHMARK_DATABASE", "master");
    std::string user = GetEnvVar("BENCHMARK_USER");
    std::string password = GetEnvVar("BENCHMARK_PASSWORD");

    if (server.empty() || user.empty() || password.empty())
    {
        printf("ERROR: Set environment variables before running:\n");
        printf("  BENCHMARK_SERVER   - SQL Server hostname\n");
        printf("  BENCHMARK_USER     - SQL Auth username\n");
        printf("  BENCHMARK_PASSWORD - SQL Auth password\n");
        printf("\n");
        printf("Optional:\n");
        printf("  BENCHMARK_DATABASE - Database (default: master)\n");
        printf("\n");
        printf("Usage:\n");
        printf("  OdbcMemoryBenchmark.exe\n");
        printf("  OdbcMemoryBenchmark.exe --connections 10000 --encrypt Strict\n");
        printf("  OdbcMemoryBenchmark.exe --encrypt Mandatory --driver \"ODBC Driver 18 for SQL Server\"\n");
        printf("  OdbcMemoryBenchmark.exe --encrypt Strict --no-pooling\n");
        printf("  OdbcMemoryBenchmark.exe --encrypt Strict --mimic-net-odbc\n");
        printf("  OdbcMemoryBenchmark.exe --encrypt Strict --unique-connstr\n");
        return 1;
    }

    // Parse command-line arguments
    int totalConnections = GetArgInt(argc, argv, "--connections", 5000);
    int batchSize = GetArgInt(argc, argv, "--batch", 100);
    std::string encryptMode = GetArgStr(argc, argv, "--encrypt", "Strict");
    std::string driver = GetArgStr(argc, argv, "--driver", "ODBC Driver 18 for SQL Server");
    bool mimicNetOdbc = HasArg(argc, argv, "--mimic-net-odbc");
    bool noPooling = HasArg(argc, argv, "--no-pooling");
    bool forceUniqueConnStr = HasArg(argc, argv, "--unique-connstr");
    bool pooling = !noPooling;  // legacy display flag
    std::string csvPath = GetArgStr(argc, argv, "--csv", "");

    if (mimicNetOdbc && noPooling)
    {
        fprintf(stderr, "[FATAL] --mimic-net-odbc and --no-pooling are mutually exclusive.\n");
        return 1;
    }

    // Unique conn string per iter is implied by --no-pooling or --mimic-net-odbc
    // (defeats pool dedup so each iter takes a fresh code path). --unique-connstr
    // forces the same behavior on top of default pool mode (SQL_CP_ONE_PER_DRIVER)
    // for isolation testing.
    bool useUniqueConnStr = noPooling || mimicNetOdbc || forceUniqueConnStr;

    const char* poolingModeDescription;
    if (mimicNetOdbc)
    {
        poolingModeDescription = "SQL_CP_ONE_PER_HENV + unique conn str (mimics System.Data.Odbc)";
    }
    else if (noPooling)
    {
        poolingModeDescription = "SQL_CP_OFF + unique conn str (genuine no-pool)";
    }
    else if (forceUniqueConnStr)
    {
        poolingModeDescription = "SQL_CP_ONE_PER_DRIVER + unique conn str (default pool + unique strings)";
    }
    else
    {
        poolingModeDescription = "SQL_CP_ONE_PER_DRIVER + same conn str (default; pool hits)";
    }

    // Build connection string
    std::string connectionString = BuildConnectionString(driver, server, database, user, password, encryptMode);

    printf("=== ODBC Driver (Native C++) - SChannel TLS Session Cache Leak Detector ===\n");
    printf("Driver:          %s\n", driver.c_str());
    printf("Server:          %s\n", server.c_str());
    printf("Database:        %s\n", database.c_str());
    printf("User:            %s\n", user.c_str());
    printf("Encrypt:         %s\n", encryptMode.c_str());
    printf("Pooling mode:    %s\n", poolingModeDescription);
    printf("Total conns:     %d\n", totalConnections);
    printf("Batch size:      %d\n", batchSize);
    printf("\n");
    printf("Connection string (sanitized):\n");
    printf("  %s\n", SanitizeConnectionString(connectionString, password).c_str());
    printf("\n");

    // ─── Configure ODBC Driver Manager pooling ───
    // This MUST happen before allocating any environment handle. The value
    // affects every env handle subsequently allocated in the process.
    //
    //   SQL_CP_OFF             - no DM pooling at all
    //   SQL_CP_ONE_PER_DRIVER  - one pool per driver DLL (typical apps)
    //   SQL_CP_ONE_PER_HENV    - one pool per env handle (what System.Data.Odbc uses)
    SQLHENV hEnv = SQL_NULL_HENV;
    SQLRETURN ret;

    SQLPOINTER poolingAttr;
    const char* poolingAttrName;
    if (mimicNetOdbc)
    {
        poolingAttr = (SQLPOINTER)SQL_CP_ONE_PER_HENV;
        poolingAttrName = "SQL_CP_ONE_PER_HENV";
    }
    else if (noPooling)
    {
        poolingAttr = (SQLPOINTER)SQL_CP_OFF;
        poolingAttrName = "SQL_CP_OFF";
    }
    else
    {
        poolingAttr = (SQLPOINTER)SQL_CP_ONE_PER_DRIVER;
        poolingAttrName = "SQL_CP_ONE_PER_DRIVER";
    }

    ret = SQLSetEnvAttr(SQL_NULL_HENV, SQL_ATTR_CONNECTION_POOLING,
        poolingAttr, SQL_IS_INTEGER);
    if (ret != SQL_SUCCESS && ret != SQL_SUCCESS_WITH_INFO)
    {
        fprintf(stderr, "[WARN] SQLSetEnvAttr(SQL_ATTR_CONNECTION_POOLING, %s) failed.\n",
            poolingAttrName);
    }

    // Allocate environment handle
    ret = SQLAllocHandle(SQL_HANDLE_ENV, SQL_NULL_HENV, &hEnv);
    if (ret != SQL_SUCCESS && ret != SQL_SUCCESS_WITH_INFO)
    {
        fprintf(stderr, "[FATAL] SQLAllocHandle(ENV) failed.\n");
        return 1;
    }

    ret = SQLSetEnvAttr(hEnv, SQL_ATTR_ODBC_VERSION, (SQLPOINTER)SQL_OV_ODBC3_80, SQL_IS_INTEGER);
    if (ret != SQL_SUCCESS && ret != SQL_SUCCESS_WITH_INFO)
    {
        fprintf(stderr, "[FATAL] SQLSetEnvAttr(ODBC_VERSION) failed.\n");
        SQLFreeHandle(SQL_HANDLE_ENV, hEnv);
        return 1;
    }

    // ─── Validate connectivity ───
    {
        SQLHDBC hDbc = SQL_NULL_HDBC;
        SQLHSTMT hStmt = SQL_NULL_HSTMT;

        ret = SQLAllocHandle(SQL_HANDLE_DBC, hEnv, &hDbc);
        if (ret != SQL_SUCCESS && ret != SQL_SUCCESS_WITH_INFO)
        {
            fprintf(stderr, "[FATAL] SQLAllocHandle(DBC) failed.\n");
            SQLFreeHandle(SQL_HANDLE_ENV, hEnv);
            return 1;
        }

        SQLCHAR outConnStr[1024];
        SQLSMALLINT outConnStrLen;
        ret = SQLDriverConnectA(hDbc, NULL, (SQLCHAR*)connectionString.c_str(),
            (SQLSMALLINT)connectionString.length(),
            outConnStr, sizeof(outConnStr), &outConnStrLen,
            SQL_DRIVER_NOPROMPT);

        if (ret != SQL_SUCCESS && ret != SQL_SUCCESS_WITH_INFO)
        {
            fprintf(stderr, "[FATAL] Cannot connect:\n");
            PrintOdbcError(SQL_HANDLE_DBC, hDbc);
            printf("\nTroubleshooting:\n");
            printf("  1. Ensure the ODBC driver is installed:\n");
            printf("     https://learn.microsoft.com/sql/connect/odbc/download-odbc-driver-for-sql-server\n");
            printf("  2. List installed drivers: odbcad32.exe or 'Get-OdbcDriver' in PowerShell\n");
            printf("  3. Verify the driver name matches: --driver \"%s\"\n", driver.c_str());
            printf("  4. For Encrypt=Strict, ensure SQL Server 2022+ with TDS 8.0 support\n");
            SQLFreeHandle(SQL_HANDLE_DBC, hDbc);
            SQLFreeHandle(SQL_HANDLE_ENV, hEnv);
            return 1;
        }

        // Print server version
        ret = SQLAllocHandle(SQL_HANDLE_STMT, hDbc, &hStmt);
        if (ret == SQL_SUCCESS || ret == SQL_SUCCESS_WITH_INFO)
        {
            ret = SQLExecDirectA(hStmt, (SQLCHAR*)"SELECT @@VERSION", SQL_NTS);
            if (ret == SQL_SUCCESS || ret == SQL_SUCCESS_WITH_INFO)
            {
                char versionBuf[512];
                SQLLEN ind;
                if (SQLFetch(hStmt) == SQL_SUCCESS)
                {
                    SQLGetData(hStmt, 1, SQL_C_CHAR, versionBuf, sizeof(versionBuf), &ind);
                    // Trim at first newline
                    char* nl = strchr(versionBuf, '\n');
                    if (nl) *nl = '\0';
                    printf("[OK] Connected. Server: %s\n", versionBuf);
                }
            }
            SQLFreeHandle(SQL_HANDLE_STMT, hStmt);
            hStmt = SQL_NULL_HSTMT;
        }

        // Query encryption and TDS info from DMVs
        ret = SQLAllocHandle(SQL_HANDLE_STMT, hDbc, &hStmt);
        if (ret == SQL_SUCCESS || ret == SQL_SUCCESS_WITH_INFO)
        {
            const char* query =
                "SELECT c.encrypt_option, c.net_transport, c.protocol_version, "
                "s.client_interface_name "
                "FROM sys.dm_exec_connections c "
                "JOIN sys.dm_exec_sessions s ON c.session_id = s.session_id "
                "WHERE c.session_id = @@SPID";

            ret = SQLExecDirectA(hStmt, (SQLCHAR*)query, SQL_NTS);
            if (ret == SQL_SUCCESS || ret == SQL_SUCCESS_WITH_INFO)
            {
                if (SQLFetch(hStmt) == SQL_SUCCESS)
                {
                    char encOption[64], transport[64], clientIface[64];
                    SQLINTEGER protocolVer;
                    SQLLEN ind;

                    SQLGetData(hStmt, 1, SQL_C_CHAR, encOption, sizeof(encOption), &ind);
                    SQLGetData(hStmt, 2, SQL_C_CHAR, transport, sizeof(transport), &ind);
                    SQLGetData(hStmt, 3, SQL_C_SLONG, &protocolVer, 0, &ind);
                    SQLGetData(hStmt, 4, SQL_C_CHAR, clientIface, sizeof(clientIface), &ind);

                    std::string tdsStr = FormatTdsVersion(protocolVer);
                    printf("[OK] Encryption: %s, Transport: %s, TDS: %s (0x%08X), Client: %s\n",
                        encOption, transport, tdsStr.c_str(), protocolVer, clientIface);
                }
            }
            SQLFreeHandle(SQL_HANDLE_STMT, hStmt);
            hStmt = SQL_NULL_HSTMT;
        }

        // Try to get TLS version
        ret = SQLAllocHandle(SQL_HANDLE_STMT, hDbc, &hStmt);
        if (ret == SQL_SUCCESS || ret == SQL_SUCCESS_WITH_INFO)
        {
            const char* tlsQuery =
                "SELECT tls_version FROM sys.dm_exec_connections WHERE session_id = @@SPID";
            ret = SQLExecDirectA(hStmt, (SQLCHAR*)tlsQuery, SQL_NTS);
            if (ret == SQL_SUCCESS || ret == SQL_SUCCESS_WITH_INFO)
            {
                if (SQLFetch(hStmt) == SQL_SUCCESS)
                {
                    char tlsVer[32];
                    SQLLEN ind;
                    if (SQLGetData(hStmt, 1, SQL_C_CHAR, tlsVer, sizeof(tlsVer), &ind) == SQL_SUCCESS && ind > 0)
                    {
                        printf("[OK] TLS Version: %s\n", tlsVer);
                    }
                    else
                    {
                        printf("[OK] TLS Version: unknown\n");
                    }
                }
            }
            else
            {
                printf("[OK] TLS Version: unknown (column not available)\n");
            }
            SQLFreeHandle(SQL_HANDLE_STMT, hStmt);
        }

        SQLDisconnect(hDbc);
        SQLFreeHandle(SQL_HANDLE_DBC, hDbc);
    }

    // ─── Establish baseline ───
    SIZE_T baselinePrivateBytes = GetPrivateBytes();
    SIZE_T baselineWorkingSet = GetWorkingSet();

    printf("\n");
    printf("Baseline - Private: %.2f MB, WorkingSet: %.2f MB\n",
        baselinePrivateBytes / (1024.0 * 1024.0),
        baselineWorkingSet / (1024.0 * 1024.0));
    printf("\n");

    // Header
    printf("%8s%12s%11s%10s%12s%10s\n",
        "Conns", "Private MB", "D Priv MB", "WS MB", "Per-Conn KB", "Batch ms");
    printf("------------------------------------------------------------------------\n");

    // ─── CSV output ───
    std::ofstream csvFile;
    if (!csvPath.empty())
    {
        bool exists = false;
        {
            std::ifstream test(csvPath);
            exists = test.good();
        }
        csvFile.open(csvPath, std::ios::app);
        if (!exists && csvFile.is_open())
        {
            csvFile << "Timestamp,Driver,EncryptMode,Pooling,ConnectionsDone,PrivateMB,DeltaPrivateMB,WorkingSetMB,PerConnKB,BatchMs\n";
        }
    }

    // ─── Main benchmark loop ───
    SIZE_T previousPrivate = baselinePrivateBytes;
    int connectionsDone = 0;
    int errors = 0;
    int batches = totalConnections / batchSize;

    for (int batch = 0; batch < batches; batch++)
    {
        auto batchStart = std::chrono::high_resolution_clock::now();

        for (int i = 0; i < batchSize; i++)
        {
            SQLHDBC hDbc = SQL_NULL_HDBC;
            SQLHSTMT hStmt = SQL_NULL_HSTMT;

            ret = SQLAllocHandle(SQL_HANDLE_DBC, hEnv, &hDbc);
            if (ret != SQL_SUCCESS && ret != SQL_SUCCESS_WITH_INFO)
            {
                errors++;
                continue;
            }

            // To defeat pooling-by-conn-string-match, append a unique APP= value.
            // - In --no-pooling mode this is belt-and-suspenders on top of SQL_CP_OFF.
            // - In --mimic-net-odbc mode DM pooling is ON; the unique conn string
            //   forces each iter to register a new DM pool entry that retains its
            //   own SChannel context. This mirrors the .NET OdbcMemoryBenchmark
            //   --no-pooling path, which is the same trick.
            std::string connStr = connectionString;
            if (useUniqueConnStr)
            {
                connStr += "APP=bench_" + std::to_string(connectionsDone + i) + ";";
            }

            SQLCHAR outStr[1024];
            SQLSMALLINT outLen;
            ret = SQLDriverConnectA(hDbc, NULL, (SQLCHAR*)connStr.c_str(),
                (SQLSMALLINT)connStr.length(),
                outStr, sizeof(outStr), &outLen,
                SQL_DRIVER_NOPROMPT);

            if (ret != SQL_SUCCESS && ret != SQL_SUCCESS_WITH_INFO)
            {
                errors++;
                SQLFreeHandle(SQL_HANDLE_DBC, hDbc);
                continue;
            }

            // Execute a simple query
            ret = SQLAllocHandle(SQL_HANDLE_STMT, hDbc, &hStmt);
            if (ret == SQL_SUCCESS || ret == SQL_SUCCESS_WITH_INFO)
            {
                SQLExecDirectA(hStmt, (SQLCHAR*)"SELECT 1", SQL_NTS);
                SQLFreeHandle(SQL_HANDLE_STMT, hStmt);
            }

            SQLDisconnect(hDbc);
            SQLFreeHandle(SQL_HANDLE_DBC, hDbc);
        }

        auto batchEnd = std::chrono::high_resolution_clock::now();
        auto batchMs = std::chrono::duration_cast<std::chrono::milliseconds>(batchEnd - batchStart).count();

        connectionsDone += batchSize;

        // Measure memory
        SIZE_T currentPrivate = GetPrivateBytes();
        SIZE_T currentWorkingSet = GetWorkingSet();
        long long deltaPrivate = (long long)currentPrivate - (long long)previousPrivate;
        double perConnKb = (double)(currentPrivate - baselinePrivateBytes) / (1024.0 * connectionsDone);

        printf("%8d%12.2f%11.2f%10.2f%12.2f%10lld\n",
            connectionsDone,
            currentPrivate / (1024.0 * 1024.0),
            deltaPrivate / (1024.0 * 1024.0),
            currentWorkingSet / (1024.0 * 1024.0),
            perConnKb,
            (long long)batchMs);

        if (csvFile.is_open())
        {
            csvFile << GetCurrentTimestamp() << ","
                << driver << ","
                << encryptMode << ","
                << poolingAttrName << ","
                << connectionsDone << ","
                << std::fixed << std::setprecision(2)
                << currentPrivate / (1024.0 * 1024.0) << ","
                << deltaPrivate / (1024.0 * 1024.0) << ","
                << currentWorkingSet / (1024.0 * 1024.0) << ","
                << perConnKb << ","
                << batchMs << "\n";
        }

        previousPrivate = currentPrivate;
    }

    // ─── Final summary ───
    SIZE_T finalPrivate = GetPrivateBytes();
    SIZE_T finalWorkingSet = GetWorkingSet();

    printf("\n");
    printf("===========================================================================\n");
    printf("SUMMARY\n");
    printf("===========================================================================\n");
    printf("Driver:                %s\n", driver.c_str());
    printf("Encrypt mode:          %s\n", encryptMode.c_str());
    printf("Pooling mode:          %s\n", poolingModeDescription);
    printf("Total connections:     %d\n", connectionsDone);
    printf("Connection errors:     %d\n", errors);
    printf("\n");
    printf("Private Bytes:         %.2f MB (baseline: %.2f MB)\n",
        finalPrivate / (1024.0 * 1024.0),
        baselinePrivateBytes / (1024.0 * 1024.0));
    printf("Native growth:         %.2f MB\n",
        (finalPrivate - baselinePrivateBytes) / (1024.0 * 1024.0));
    printf("Per-conn native avg:   %.2f KB\n",
        (double)(finalPrivate - baselinePrivateBytes) / (1024.0 * connectionsDone));
    printf("\n");
    printf("Working Set:           %.2f MB\n", finalWorkingSet / (1024.0 * 1024.0));

    if (errors > 0)
    {
        printf("\n");
        printf("WARNING: %d connection errors occurred.\n", errors);
    }

    double perConnFinalKb = (double)(finalPrivate - baselinePrivateBytes) / (1024.0 * connectionsDone);
    printf("\n");
    if (perConnFinalKb > 20)
    {
        printf("!! LEAK DETECTED: ~%.1f KB per connection native growth.\n", perConnFinalKb);
        printf("   This is consistent with SChannel TLS 1.3 session ticket caching.\n");
    }
    else if (perConnFinalKb > 5)
    {
        printf("** MODERATE GROWTH: ~%.1f KB per connection. May be caching, monitor with more connections.\n", perConnFinalKb);
    }
    else
    {
        printf("OK MINIMAL GROWTH: ~%.1f KB per connection. No significant native memory leak detected.\n", perConnFinalKb);
    }

    // Cleanup
    SQLFreeHandle(SQL_HANDLE_ENV, hEnv);

    if (csvFile.is_open())
    {
        csvFile.flush();
        csvFile.close();
    }

    return 0;
}

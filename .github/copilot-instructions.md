This is a .NET repo that targets .NET Framework 4.2.6, .NET 8.0, and .NET 9.0.

This repo contains many C# projects.  The main solution file is src/Microsoft.Data.SqlClient.sln.

Consider the build.proj file to understand what build targets are available.

Consider the ADO pipeline configurations in the eng/ directory to understand how the we perform continuous integration and release processes.

Consider instructions in the policy/ directory to understand our development policies and practices.

We create the following NuGet packages as our release artifacts:
  - Microsoft.Data.SqlClient
  - Microsoft.Data.SqlClient.SNI
  - Microsoft.Data.SqlClient.AlwaysEncrypted.AzureKeyVaultProvider

Our public API documentation is formatted as XML snippets in doc/ directory.

The historical release notes are in the release-notes/ directory.

@echo off
REM Run LocalDB integration tests
REM Requires SQL Server LocalDB to be installed
dotnet test "%~dp0tests\LogicaSharp.Tests\LogicaSharp.Tests.csproj" --filter "FullyQualifiedName~LocalDbIntegrationTests" %*

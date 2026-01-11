@echo off
REM Compile ancestor example to T-SQL
dotnet run --project "%~dp0src/LogicaCompiler" -- "%~dp0examples/ancestor_example.l" -d mssql %*

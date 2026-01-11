@echo off
REM Compile ancestor example to ClickHouse
dotnet run --project "%~dp0src/LogicaCompiler" -- "%~dp0examples/ancestor_example.l" -d clickhouse %*

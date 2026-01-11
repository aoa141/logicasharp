@echo off
REM Compile orders example to ClickHouse
dotnet run --project "%~dp0src/LogicaCompiler" -- "%~dp0examples/orders_example.l" -d clickhouse %*

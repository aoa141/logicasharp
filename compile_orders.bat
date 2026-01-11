@echo off
REM Compile orders example to T-SQL
dotnet run --project "%~dp0src/LogicaCompiler" -- "%~dp0examples/orders_example.l" -d mssql %*

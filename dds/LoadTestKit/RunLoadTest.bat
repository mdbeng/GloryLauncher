@echo off
echo 🔥 Glory Launcher Load Tester
echo ==============================
echo.

REM Check if dotnet is installed
dotnet --version >nul 2>&1
if %ERRORLEVEL% NEQ 0 (
    echo ❌ .NET SDK not found!
    echo Please install .NET SDK from: https://dotnet.microsoft.com/download
    echo.
    pause
    exit /b 1
)

echo ✅ .NET SDK found
echo.

REM Create a temporary console project
if not exist "LoadTester" mkdir LoadTester
cd LoadTester

REM Create project file if it doesn't exist
if not exist "LoadTester.csproj" (
    echo 📦 Creating .NET project...
    dotnet new console --force >nul 2>&1
    
    REM Replace the default Program.cs with our load tester
    copy "..\LoadTester.cs" "Program.cs" >nul 2>&1
)

echo 🔨 Building load tester...
dotnet build --configuration Release >nul 2>&1

if %ERRORLEVEL% EQU 0 (
    echo ✅ Build successful!
    echo.
    echo 🚀 Starting load test...
    echo.
    dotnet run --configuration Release
) else (
    echo ❌ Build failed!
    echo.
    dotnet build --configuration Release
)

cd ..

echo.
echo Press any key to exit...
pause >nul
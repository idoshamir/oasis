# Resets oasis.db to a clean demo state using reset-db.sql and Identity seeding.
# Creates schema if missing, wipes data, then seeds demo users via UserManager.
#
# Usage (from this directory):
#   .\reset-demo.ps1
#
# Stop the API (dotnet run) before running — SQLite cannot be reset while the file is locked.
# Requires: .NET 9 SDK, dotnet-ef global tool

$ErrorActionPreference = 'Stop'

$projectDir = $PSScriptRoot
$dbPath = Join-Path $projectDir 'oasis.db'
$sqlPath = Join-Path $projectDir 'reset-db.sql'
$runnerDir = Join-Path $env:TEMP 'oasis-demo-sql-runner'

if (-not (Test-Path $sqlPath)) {
    Write-Error "reset-db.sql not found at $sqlPath"
}

function Ensure-SqlRunner {
    if (Test-Path (Join-Path $runnerDir 'ApplySql.dll')) {
        return
    }

    New-Item -ItemType Directory -Force -Path $runnerDir | Out-Null

    @'
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <OutputType>Exe</OutputType>
    <TargetFramework>net9.0</TargetFramework>
    <ImplicitUsings>enable</ImplicitUsings>
    <Nullable>enable</Nullable>
  </PropertyGroup>
  <ItemGroup>
    <PackageReference Include="Microsoft.Data.Sqlite" Version="9.0.4" />
  </ItemGroup>
</Project>
'@ | Set-Content -Path (Join-Path $runnerDir 'ApplySql.csproj') -Encoding utf8

    @'
if (args.Length != 2)
{
    Console.Error.WriteLine("Usage: ApplySql <database-path> <sql-file-path>");
    return 1;
}

var dbPath = Path.GetFullPath(args[0]);
var sqlPath = Path.GetFullPath(args[1]);

if (!File.Exists(sqlPath))
{
    Console.Error.WriteLine($"SQL file not found: {sqlPath}");
    return 1;
}

var sql = File.ReadAllText(sqlPath);
using var connection = new Microsoft.Data.Sqlite.SqliteConnection($"Data Source={dbPath}");
connection.Open();
using var command = connection.CreateCommand();
command.CommandText = sql;
command.ExecuteNonQuery();
return 0;
'@ | Set-Content -Path (Join-Path $runnerDir 'Program.cs') -Encoding utf8

    Push-Location $runnerDir
    try {
        dotnet build --verbosity quiet | Out-Null
        if ($LASTEXITCODE -ne 0) {
            throw 'Failed to build SQL runner.'
        }
    }
    finally {
        Pop-Location
    }
}

$serverProcess = Get-Process -Name 'JiraIntegration.Server' -ErrorAction SilentlyContinue
if ($serverProcess) {
    Write-Warning 'JiraIntegration.Server is running. Stop it first to avoid database lock errors.'
}

Push-Location $projectDir
try {
    Write-Host 'Building JiraIntegration.Server...'
    dotnet build --verbosity quiet | Out-Null
    if ($LASTEXITCODE -ne 0) {
        throw 'dotnet build failed.'
    }

    Write-Host 'Removing existing database...'
    Remove-Item $dbPath, "$dbPath-shm", "$dbPath-wal" -ErrorAction SilentlyContinue

    Write-Host 'Applying EF Core migrations...'
    dotnet ef database update --no-build
    if ($LASTEXITCODE -ne 0) {
        throw 'dotnet ef database update failed. Install the tool with: dotnet tool install --global dotnet-ef'
    }

    Write-Host 'Applying reset-db.sql...'
    Ensure-SqlRunner
    dotnet (Join-Path $runnerDir 'bin\Debug\net9.0\ApplySql.dll') $dbPath $sqlPath
    if ($LASTEXITCODE -ne 0) {
        throw 'Failed to apply reset-db.sql.'
    }

    Write-Host 'Seeding demo users (Identity)...'
    dotnet run --no-build -- --seed-demo
    if ($LASTEXITCODE -ne 0) {
        throw 'Failed to seed demo users.'
    }

    Write-Host ''
    Write-Host 'Demo database ready.'
    Write-Host '  demo / Demo123!'
    Write-Host '  testuser / Test123!'
}
finally {
    Pop-Location
}

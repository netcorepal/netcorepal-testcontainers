# netcorepal-testcontainers

[![Release Build](https://img.shields.io/github/actions/workflow/status/netcorepal/netcorepal-testcontainers/release.yml?label=release%20build)](https://github.com/netcorepal/netcorepal-testcontainers/actions/workflows/release.yml)
[![Preview Build](https://img.shields.io/github/actions/workflow/status/netcorepal/netcorepal-testcontainers/dotnet.yml?label=preview%20build)](https://github.com/netcorepal/netcorepal-testcontainers/actions/workflows/dotnet.yml)
[![NuGet](https://img.shields.io/nuget/v/NetCorePal.Testcontainers.OpenGauss.svg)](https://www.nuget.org/packages/NetCorePal.Testcontainers.OpenGauss)
[![NuGet Version](https://img.shields.io/nuget/vpre/NetCorePal.Testcontainers.OpenGauss?label=nuget-pre)](https://www.nuget.org/packages/NetCorePal.Testcontainers.OpenGauss)
[![MyGet Version](https://img.shields.io/myget/netcorepal/vpre/NetCorePal.Testcontainers.OpenGauss?label=myget-nightly)](https://www.myget.org/feed/netcorepal/package/nuget/NetCorePal.Testcontainers.OpenGauss)
[![GitHub license](https://img.shields.io/badge/license-MIT-blue.svg)](https://github.com/netcorepal/netcorepal-testcontainers/blob/main/LICENSE)

面向 .NET 的 Testcontainers 扩展包集合：为常用基础设施提供容器封装，便于集成测试与本地开发。

## 已实现的 Testcontainers

| 包名 | Release版本 | Preview版本 |
| --- | --- | --- |
| [NetCorePal.Testcontainers.OpenGauss](https://www.nuget.org/packages/NetCorePal.Testcontainers.OpenGauss)（[文档](./src/NetCorePal.Testcontainers.OpenGauss/README.md)） | [![NuGet](https://img.shields.io/nuget/v/NetCorePal.Testcontainers.OpenGauss.svg)](https://www.nuget.org/packages/NetCorePal.Testcontainers.OpenGauss) | [![NuGet Version](https://img.shields.io/nuget/vpre/NetCorePal.Testcontainers.OpenGauss?label=nuget-pre)](https://www.nuget.org/packages/NetCorePal.Testcontainers.OpenGauss) |
| [NetCorePal.Testcontainers.DMDB](https://www.nuget.org/packages/NetCorePal.Testcontainers.DMDB)（[文档](./src/NetCorePal.Testcontainers.DMDB/README.md)） | [![NuGet](https://img.shields.io/nuget/v/NetCorePal.Testcontainers.DMDB.svg)](https://www.nuget.org/packages/NetCorePal.Testcontainers.DMDB) | [![NuGet Version](https://img.shields.io/nuget/vpre/NetCorePal.Testcontainers.DMDB?label=nuget-pre)](https://www.nuget.org/packages/NetCorePal.Testcontainers.DMDB) |
| [NetCorePal.Testcontainers.KingbaseES](https://www.nuget.org/packages/NetCorePal.Testcontainers.KingbaseES)（[文档](./src/NetCorePal.Testcontainers.KingbaseES/README.md)） | [![NuGet](https://img.shields.io/nuget/v/NetCorePal.Testcontainers.KingbaseES.svg)](https://www.nuget.org/packages/NetCorePal.Testcontainers.KingbaseES) | [![NuGet Version](https://img.shields.io/nuget/vpre/NetCorePal.Testcontainers.KingbaseES?label=nuget-pre)](https://www.nuget.org/packages/NetCorePal.Testcontainers.KingbaseES) |

## 本机快速验证

前置要求：已安装 .NET SDK；如需运行 Docker 集成测试，请先安装并启动 Docker（Docker Desktop / Linux Docker）。

### Windows（PowerShell）

```powershell
dotnet restore
dotnet build -c Release

# 运行全部测试（需要 Docker）
dotnet test -c Release

# 仅运行非 Docker 测试（跳过所有 [DockerFact]）
$env:SKIP_DOCKER_TESTS = "1"
dotnet test -c Release
Remove-Item Env:SKIP_DOCKER_TESTS
```

### macOS / Linux（bash）

```bash
dotnet restore
dotnet build -c Release

# 运行全部测试（需要 Docker）
dotnet test -c Release

# 仅运行非 Docker 测试（跳过所有 [DockerFact]）
SKIP_DOCKER_TESTS=1 dotnet test -c Release
```

## 预览版源

```text
https://www.myget.org/F/netcorepal/api/v3/index.json
```

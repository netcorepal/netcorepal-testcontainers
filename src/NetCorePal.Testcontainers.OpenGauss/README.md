# NetCorePal.Testcontainers.OpenGauss

[![Release Build](https://img.shields.io/github/actions/workflow/status/OWNER/REPO/release.yml?label=release%20build)](https://github.com/OWNER/REPO/actions/workflows/release.yml)
[![Preview Build](https://img.shields.io/github/actions/workflow/status/OWNER/REPO/dotnet.yml?label=preview%20build)](https://github.com/OWNER/REPO/actions/workflows/dotnet.yml)

> 包名：`NetCorePal.Testcontainers.OpenGauss`
>
> 命名空间：`Testcontainers.OpenGauss`（不包含 `NetCorePal` 前缀）

## 快速开始

```csharp
using Testcontainers.OpenGauss;

await using var container = new OpenGaussBuilder()
  .WithImage("opengauss/opengauss:latest")
  .WithPassword("Test@123")
  .Build();

await container.StartAsync();

var connectionString = container.GetConnectionString();
```

默认连接信息：

- Database：`postgres`
- Username：`gaussdb`
- Password：`Test@123`

可以通过 `WithDatabase` / `WithUsername` / `WithPassword` 覆盖。

## Docker 集成测试

默认不会启动 Docker 容器。

- 运行：`RUN_DOCKER_TESTS=1 dotnet test`

说明：OpenGauss 可能启动较慢，建议在业务测试中增加“连接重试/探活”（本仓库测试中使用 `WaitForDatabaseReadyAsync`）。

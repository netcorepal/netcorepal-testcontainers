# NetCorePal.Testcontainers.KingbaseES

基于 [Testcontainers](https://testcontainers.com/) 的 KingbaseES 容器封装，用于 .NET 集成测试。

## 功能特性

- 快速启动 KingbaseES 容器用于集成测试
- 支持自定义数据库名、用户名、密码
- 自动生成连接字符串
- 兼容 .NET 8.0, 9.0, 10.0

## 安装

```bash
dotnet add package NetCorePal.Testcontainers.KingbaseES
```

## 使用示例

```csharp
using Testcontainers.KingbaseES;

// 创建并启动 KingbaseES 容器
await using var kingbaseESContainer = new KingbaseESBuilder()
    .WithDatabase("mydb")
    .WithUsername("myuser")
    .WithPassword("mypassword")
    .Build();

await kingbaseESContainer.StartAsync();

// 获取连接字符串
var connectionString = kingbaseESContainer.GetConnectionString();

// 使用连接字符串进行数据库操作
// ...
```

## 启动说明（重要）

该镜像 `apecloud/kingbase:v008r006c009b0014-unit` 的入口是 systemd（`/usr/sbin/init`），容器处于 `running` 并不代表数据库已启动。

本库的默认行为是：

- 在默认等待策略中执行镜像内置的初始化/启动脚本：`/home/kingbase/cluster/bin/docker-entrypoint.sh`
- 再通过 `sys_ctl status` 判断数据库进程已运行后，才认为容器就绪
- 启动完成后，如你调用了 `.WithDatabase()` / `.WithUsername()` / `.WithPassword()`，会在 `StartAsync()` 之后按需创建用户/数据库（幂等）
- 默认以 `privileged` 模式启动容器（systemd 镜像通常需要该权限；如果你的运行环境禁止 `privileged`，则此镜像可能无法正常工作）

关于密码：该镜像的脚本要求环境变量 `DB_PASSWORD` 使用 base64 编码。本库的 `.WithPassword("...")` 接收**明文**密码，并会自动完成 base64 编码注入。

单节点默认会注入以下关键环境/配置以满足脚本要求：

- `ALL_NODE_IP=localhost`
- `REPLICA_COUNT=1`
- `TRUST_IP=127.0.0.1`
- hostname 设置为 `kingbase-0`（脚本会根据末尾 `-0` 判定由该节点执行初始化）

注意：如果你自行覆盖 `WithWaitStrategy(...)`，请确保你的等待策略仍会触发上述脚本（或以其它方式启动数据库），否则可能出现“容器已运行但端口永远不开放”的卡死情况。

## 默认配置

- **镜像**: `apecloud/kingbase:v008r006c009b0014-unit`
- **端口**: `54321`
- **数据库**: `test`
- **用户名**: `system`
- **密码**: `12345678ab`

## 自定义配置

```csharp
var kingbaseESContainer = new KingbaseESBuilder()
    // 如需指定镜像版本，请使用与默认镜像行为一致的 tag。
    .WithImage("apecloud/kingbase:v008r006c009b0014-unit")
    .WithDatabase("customdb")
    .WithUsername("customuser")
    .WithPassword("custompassword")
    .Build();
```

## 许可证

MIT

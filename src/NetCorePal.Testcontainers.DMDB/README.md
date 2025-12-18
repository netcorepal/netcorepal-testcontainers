# NetCorePal.Testcontainers.DMDB

[![NuGet](https://img.shields.io/nuget/v/NetCorePal.Testcontainers.DMDB.svg)](https://www.nuget.org/packages/NetCorePal.Testcontainers.DMDB)

面向 .NET 的达梦数据库（DM8）Testcontainers 实现。

## 安装

```bash
dotnet add package NetCorePal.Testcontainers.DMDB
```

## 使用

### 基本用法

```csharp
using Testcontainers.DMDB;

var dmdbContainer = new DmdbBuilder()
    .Build();

await dmdbContainer.StartAsync();

var connectionString = dmdbContainer.GetConnectionString();
// 使用 connectionString 连接到 DMDB
```

### 自定义配置

```csharp
var dmdbContainer = new DmdbBuilder()
    .WithDatabase("mydb")
    // 当前仅支持 SYSDBA 用户（不支持通过 WithUsername 修改/创建用户）
    // 修改密码时，需要同时设置 WithPassword 和 WithDbaPassword（通常使用相同密码）
    .WithPassword("MyPassword123")
    .WithDbaPassword("MyPassword123")
    .Build();

await dmdbContainer.StartAsync();
```

### 在测试中使用

```csharp
public class MyDatabaseTests : IAsyncLifetime
{
    private readonly DmdbContainer _dmdbContainer;

    public MyDatabaseTests()
    {
        _dmdbContainer = new DmdbBuilder()
            .Build();
    }

    public async Task InitializeAsync()
    {
        await _dmdbContainer.StartAsync();
    }

    public async Task DisposeAsync()
    {
        await _dmdbContainer.DisposeAsync();
    }

    [Fact]
    public async Task TestDatabaseConnection()
    {
        var connectionString = _dmdbContainer.GetConnectionString();
        // 测试代码...
    }
}
```

## 默认配置

- **镜像**: cnxc/dm8:20250423-kylin
- **端口**: 5236
- **数据库**: testdb
- **用户名**: SYSDBA
- **密码**: SYSDBA_abc123
- **DBA密码**: SYSDBA_abc123
- **特权模式**: true

## 等待策略

容器启动时会自动等待数据库完全就绪：
- 首先等待 TCP 端口 5236 可用
- 然后在容器内使用 `disql` 执行 `SELECT 1`，确保数据库已可接受连接
- 默认超时时间为 2 分钟

## 重要说明

- **当前仅支持 SYSDBA 用户**：镜像启动阶段不会根据 `WithUsername(...)` 自动创建/切换用户。
    `WithUsername(...)` 目前只会影响连接字符串中的 `Username` 字段，但不保证数据库中存在该用户。
- **修改密码需同时设置两个密码**：达梦镜像使用不同的环境变量分别控制普通连接与 DBA 登录。
    因此当你想修改默认 `SYSDBA` 的密码时，需要同时调用 `WithPassword(...)` 与 `WithDbaPassword(...)`（通常设置为同一个值），否则可能出现“等待策略通过但业务连接失败”或相反的情况。

## 注意事项

- 达梦数据库的密码有格式要求，建议使用包含大小写字母和数字的密码
- 容器需要以特权模式运行（Privileged = true）
- 达梦数据库没有官方的 Docker 镜像，本包默认使用社区镜像 `cnxc/dm8:20250423-kylin`

## License

MIT

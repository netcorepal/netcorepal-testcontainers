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
    .WithUsername("myuser")
    .WithPassword("MyPassword123")
    .WithDbaPassword("SYSDBA_pass123")
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
- **用户名**: testdb
- **密码**: TestDm123
- **DBA密码**: SYSDBA_abc123
- **特权模式**: true

## 等待策略

容器启动时会自动等待数据库完全就绪：
- 首先等待 TCP 端口 5236 可用
- 然后使用 [DM.DmProvider](https://www.nuget.org/packages/DM.DmProvider) 验证数据库连接
- 执行 `SELECT 1` 查询确保数据库可以接受连接
- 默认超时时间为 2 分钟

## 注意事项

- 达梦数据库的密码有格式要求，建议使用包含大小写字母和数字的密码
- 容器需要以特权模式运行（Privileged = true）
- 达梦数据库没有官方的 Docker 镜像，本包默认使用社区镜像 `cnxc/dm8:20250423-kylin`

## License

MIT

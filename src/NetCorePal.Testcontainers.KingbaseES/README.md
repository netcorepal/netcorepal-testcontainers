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
var kingbaseESContainer = new KingbaseESBuilder()
    .WithDatabase("mydb")
    .WithUsername("myuser")
    .WithPassword("mypassword")
    .Build();

await kingbaseESContainer.StartAsync();

// 获取连接字符串
var connectionString = kingbaseESContainer.GetConnectionString();

// 使用连接字符串进行数据库操作
// ...

// 停止并清理容器
await kingbaseESContainer.DisposeAsync();
```

## 默认配置

- **镜像**: `apecloud/kingbase:v008r006c009b0014-unit`
- **端口**: `54321`
- **数据库**: `TEST`
- **用户名**: `system`
- **密码**: `Test@123`

## 自定义配置

```csharp
var kingbaseESContainer = new KingbaseESBuilder()
    .WithImage("apecloud/kingbase:latest")
    .WithDatabase("customdb")
    .WithUsername("customuser")
    .WithPassword("custompassword")
    .Build();
```

## 许可证

MIT

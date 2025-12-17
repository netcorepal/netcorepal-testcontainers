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

关于密码：该镜像的脚本要求环境变量 `DB_PASSWORD` 使用**明文**密码。本库的 `.WithPassword("...")` 接收明文密码，并会将其写入 `DB_PASSWORD`。

单节点默认会注入以下关键环境/配置以满足脚本要求：

- `ALL_NODE_IP=localhost`
- `REPLICA_COUNT=1`
- `TRUST_IP=127.0.0.1`
- hostname 设置为 `kingbase-0`（脚本会根据末尾 `-0` 判定由该节点执行初始化）

注意：如果你自行覆盖 `WithWaitStrategy(...)`，请确保你的等待策略仍会触发上述脚本（或以其它方式启动数据库），否则可能出现“容器已运行但端口永远不开放”的卡死情况。

### 关于 SSH 卡住（重要）

该镜像的 `docker-entrypoint.sh` 在初始化过程中会通过 SSH 连接 `ALL_NODE_IP`（默认 `localhost/127.0.0.1`）。
在部分环境（尤其是 Linux CI 上的 systemd 变体、缺失 dbus 的 systemd 容器）中，`sshd` 可能不会自动启动或缺少 host key，导致 entrypoint 在等待 SSH 时表现为“卡住”。

本库的默认等待策略会在执行 entrypoint 前尝试：

- 生成 SSH host key（`ssh-keygen -A`，幂等）
- 启动 `sshd`（后台运行）

如果你自定义了 `WithWaitStrategy(...)` 并自行执行 entrypoint，请务必包含同等的 SSH 准备步骤，否则可能会遇到启动卡死。

排查建议：

- `docker exec <container> sh -lc 'pgrep -x sshd || echo "sshd not running"'`
- 查看 `/tmp/sshd.log`（如果你的等待策略/脚本使用了该路径输出日志）

## 本机快速验证命令（推荐）

### 运行 .NET 测试（推荐）

前置要求：已安装 .NET SDK；如需运行 Docker 集成测试，请先安装并启动 Docker。

Windows（PowerShell）：

```powershell
# 运行 KingbaseES 单测（包含 Docker 集成测试，需本机 Docker 可用）
dotnet test .\test\NetCorePal.Testcontainers.KingbaseES.UnitTests\ -c Release -f net10.0

# 仅运行非 Docker 测试（跳过所有 [DockerFact]）
$env:SKIP_DOCKER_TESTS = "1"
dotnet test .\test\NetCorePal.Testcontainers.KingbaseES.UnitTests\ -c Release -f net10.0
Remove-Item Env:SKIP_DOCKER_TESTS

# 如需测试其它目标框架，可替换为 net8.0 / net9.0
```

说明：

- Docker 集成测试使用 `DockerFactAttribute` 控制开关：设置 `SKIP_DOCKER_TESTS=1` 或 `RUN_DOCKER_TESTS=0` 时会自动跳过。
- 如需在本机/CI 固定镜像版本，可设置环境变量 `KINGBASEES_IMAGE`（测试中会读取该变量来覆盖默认镜像）。

### 镜像启动机制验证（可选）

下面是一组用于验证该镜像启动机制的命令：容器默认只启动 systemd，必须执行 `docker-entrypoint.sh` 才会安装并启动数据库。

Windows（PowerShell）：

```powershell
docker pull apecloud/kingbase:v008r006c009b0014-unit

$KB_NAME = "kb_local"
$KB_PASS = "12345678ab"
$HOST_PORT = 15432

# 1) 默认方式启动容器（此时 DB 通常还未启动）
docker run -d --rm --name $KB_NAME --privileged --hostname kingbase-0 `
    -p "${HOST_PORT}:54321" `
    apecloud/kingbase:v008r006c009b0014-unit

# 2) 观察 PID1 与是否存在 DB/端口
$inspectScript = @'
echo "PID1:"; ps -p 1 -o pid,comm,args;
echo;
echo "DB processes:";
ps -ef | grep -E "(kingbase|repmgrd|kbha)" | grep -v grep || true;
echo;
echo "Listen:";
(ss -lntp 2>/dev/null || netstat -lntp 2>/dev/null || true) | head -n 50
'@
docker exec $KB_NAME sh -lc $inspectScript

# 3) 准备 SSH（该镜像的 entrypoint 会通过 SSH 连接 ALL_NODE_IP）
$prepareSshScript = @'
pgrep -x sshd >/dev/null 2>&1 || {
    (command -v ssh-keygen >/dev/null 2>&1 && ssh-keygen -A || /usr/bin/ssh-keygen -A || true);
    (/usr/sbin/sshd -D -E /tmp/sshd.log >/dev/null 2>&1 &) || true;
    sleep 1;
};
pgrep -x sshd >/dev/null 2>&1 && echo "sshd running" || { echo "sshd not running"; tail -n 50 /tmp/sshd.log 2>/dev/null || true; }
'@
docker exec $KB_NAME sh -lc ($prepareSshScript -replace "`r", "")

# 4) 触发镜像内置初始化/启动脚本（会解压生成 sys_ctl/ksql 并启动 DB）
$entrypointScript = @'
HOSTNAME=$(hostname) /home/kingbase/cluster/bin/docker-entrypoint.sh > /tmp/entrypoint.out 2>&1;
echo "exit=$?";
tail -n 60 /tmp/entrypoint.out
'@
docker exec `
    -e ALL_NODE_IP=localhost `
    -e REPLICA_COUNT=1 `
    -e TRUST_IP=127.0.0.1 `
    -e DB_PASSWORD=$KB_PASS `
    $KB_NAME sh -lc $entrypointScript

# 5) 再次确认 DB 进程与监听端口
$checkScript = @'
ps -ef | grep -E "(kingbase(d)?|repmgrd|kbha)" | grep -v grep || true
echo
(ss -lntp 2>/dev/null || netstat -lntp 2>/dev/null || true) | grep 54321 || true
'@
# PowerShell 的 here-string 默认是 CRLF，可能导致容器内 sh 解析异常；执行前先移除 `\r`。
docker exec $KB_NAME sh -lc ($checkScript -replace "`r", "")

# 6) 使用容器内 ksql 探活（SELECT 1）
$probeScript = @'
set -e
printf "%s\n" "127.0.0.1:54321:*:system:${KB_PASS}" > /home/kingbase/.pgpass
chown kingbase:kingbase /home/kingbase/.pgpass
chmod 600 /home/kingbase/.pgpass
runuser -u kingbase -- env PGPASSFILE=/home/kingbase/.pgpass \
    /home/kingbase/cluster/bin/ksql -w -h 127.0.0.1 -p 54321 -U system -d test -t -A -c "SELECT 1;"
'@
docker exec -e "KB_PASS=$KB_PASS" $KB_NAME sh -lc ($probeScript -replace "`r", "")

# 7) 清理
docker rm -f $KB_NAME
```

macOS / Linux / WSL（bash）：

```bash
docker pull apecloud/kingbase:v008r006c009b0014-unit

KB_NAME=kb_local
KB_PASS='12345678ab'
HOST_PORT=15432

# 1) 默认方式启动容器（此时 DB 通常还未启动）
docker run -d --rm --name "$KB_NAME" --privileged --hostname kingbase-0 \
    -p "$HOST_PORT:54321" \
    apecloud/kingbase:v008r006c009b0014-unit

# 2) 观察 PID1 与是否存在 DB/端口
docker exec "$KB_NAME" sh -lc '
    echo "PID1:"; ps -p 1 -o pid,comm,args;
    echo;
    echo "DB processes:";
    ps -ef | grep -E "(kingbase|repmgrd|kbha)" | grep -v grep || true;
    echo;
    echo "Listen:";
    (ss -lntp 2>/dev/null || netstat -lntp 2>/dev/null || true) | head -n 50
'

# 3) 准备 SSH（该镜像的 entrypoint 会通过 SSH 连接 ALL_NODE_IP）
#    - 生成 host key（幂等）
#    - 启动 sshd（后台）
docker exec "$KB_NAME" sh -lc '
    pgrep -x sshd >/dev/null 2>&1 || {
        (command -v ssh-keygen >/dev/null 2>&1 && ssh-keygen -A || /usr/bin/ssh-keygen -A || true);
        (/usr/sbin/sshd -D -E /tmp/sshd.log >/dev/null 2>&1 &) || true;
        sleep 1;
    };
    pgrep -x sshd >/dev/null 2>&1 && echo "sshd running" || { echo "sshd not running"; tail -n 50 /tmp/sshd.log 2>/dev/null || true; }
'

# 4) 触发镜像内置初始化/启动脚本（会解压生成 sys_ctl/ksql 并启动 DB）
docker exec \
    -e ALL_NODE_IP=localhost \
    -e REPLICA_COUNT=1 \
    -e TRUST_IP=127.0.0.1 \
    -e DB_PASSWORD="$KB_PASS" \
    "$KB_NAME" sh -lc '
        HOSTNAME=$(hostname) /home/kingbase/cluster/bin/docker-entrypoint.sh > /tmp/entrypoint.out 2>&1;
        echo "exit=$?";
        tail -n 60 /tmp/entrypoint.out
    '

# 5) 再次确认 DB 进程与监听端口
docker exec "$KB_NAME" sh -lc '
    ps -eo pid,user,comm,args | grep -E "(kingbase(d)?|repmgrd|kbha)" | grep -v grep | head -n 50;
    echo;
    (ss -lntp 2>/dev/null || netstat -lntp 2>/dev/null || true) | grep 54321 || true
'

# 6) 使用容器内 ksql 探活（SELECT 1）
docker exec "$KB_NAME" sh -lc '
    printf "%s\\n" "127.0.0.1:54321:*:system:'"$KB_PASS"'" > /home/kingbase/.pgpass \
    && chown kingbase:kingbase /home/kingbase/.pgpass \
    && chmod 600 /home/kingbase/.pgpass \
    && runuser -u kingbase -- env PGPASSFILE=/home/kingbase/.pgpass \
         /home/kingbase/cluster/bin/ksql -w -h 127.0.0.1 -p 54321 -U system -d test -t -A -c "SELECT 1;"
'

# 7) 清理
docker rm -f "$KB_NAME"
```

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

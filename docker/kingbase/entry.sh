# 确保 shell 及数据库使用 UTF-8 编码，防止因编码不一致导致初始化失败
export ALL_NODE_IP=localhost
export TRUST_IP=localhost
export REPLICA_COUNT=1
HOSTNAME=$(hostname)
#export DB_PASSWORD=12345678ab
#sleep 5
#sh  pgrep -x sshd >/dev/null 2>&1 || { (command -v ssh-keygen >/dev/null 2>&1 && ssh-keygen -A || /usr/bin/ssh-keygen -A || true); (/usr/sbin/sshd -D -E /tmp/sshd.log >/dev/null 2>&1 &) || true; sleep 1; }
echo $REPLICA_COUNT
echo $HOSTNAME
echo $ALL_NODE_IP
echo $TRUST_IP
echo $DB_PASSWORD

# 以后台线程方式启动 docker-entrypoint.sh
sh /home/kingbase/cluster/bin/docker-entrypoint.sh &
# 主线程保持存活，防止容器退出
while true; do sleep 3600; done
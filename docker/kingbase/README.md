
docker build -t a:a .    
docker run -e REPLICA_COUNT=1 -e ALL_NODE_IP=127.0.0.1 -e TRUST_IP=127.0.0.1  -e DB_PASSWORD=12345678ab  -e HOSTNAME=kingbase-0 -p 54321:54321 --name aaa --privileged   a:a

docker run  -p 54321:54321 --name aaa --privileged   a:a


docker run  -p 54321:54321 --name bbb --privileged -d apecloud/kingbase:v008r006c009b0014-unit




# windows


docker run  -p 54321:54321 --name bbbc -e REPLICA_COUNT=1 -e ALL_NODE_IP=localhost  -e TRUST_IP=127.0.0.1 --privileged -d apecloud/kingbase:v008r006c009b0014-unit




docker exec bbbc sh  -lc 'pgrep -x sshd >/dev/null 2>&1 || { (command -v ssh-keygen >/dev/null 2>&1 && ssh-keygen -A || /usr/bin/ssh-keygen -A || true); (/usr/sbin/sshd -D -E /tmp/sshd.log >/dev/null 2>&1 &) || true; sleep 1; }'


 docker exec bbbc sh -lc 'HOSTNAME=$(hostname) /home/kingbase/cluster/bin/docker-entrypoint.sh'

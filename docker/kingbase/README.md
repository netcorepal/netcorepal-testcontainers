
docker build -t a:a .    
docker run -e REPLICA_COUNT=1 -e ALL_NODE_IP=127.0.0.1 -e TRUST_IP=127.0.0.1  -e DB_PASSWORD=12345678ab  -e HOSTNAME=kingbase-0 -p 54321:54321 --name aaa --privileged   a:a

docker run  -p 54321:54321 --name aaa --privileged   a:a


docker run  -p 54321:54321 --name bbb --privileged -d apecloud/kingbase:v008r006c009b0014-unit
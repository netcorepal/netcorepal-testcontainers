
docker build -t a:a .    
docker run -e REPLICA_COUNT=1 -e ALL_NODE_IP=127.0.0.1 -e TRUST_IP=127.0.0.1  -e DB_PASSWORD=12345678ab  -e HOSTNAME=kingbase-0  --privileged   a:a
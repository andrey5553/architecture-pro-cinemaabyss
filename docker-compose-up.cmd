docker-compose down
docker-compose build --no-cache proxy-service
docker-compose up -d events-service
docker-compose up -d kafka-ui
docker-compose up proxy-service


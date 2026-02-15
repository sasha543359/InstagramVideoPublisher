#!/bin/bash
set -e

echo "=== Установка Docker ==="
apt-get update
apt-get install -y docker.io docker-compose-v2
systemctl enable docker
systemctl start docker

echo "=== Клонируем репозиторий ==="
cd /opt
git clone https://github.com/sasha543359/InstagramVideoPublisher.git
cd InstagramVideoPublisher

echo "=== Запускаем ==="
docker compose up -d --build

echo "=== Готово! ==="
echo "Проверить логи: docker compose logs -f app"
echo "Статус: docker compose ps"

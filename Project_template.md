## Изучите [README.md](README.md) файл и структуру проекта.

## Задание 1

1. Спроектируйте to be архитектуру КиноБездны, разделив всю систему на отдельные домены 
и организовав интеграционное взаимодействие и единую точку вызова сервисов.
[Схема to-be](schemas/задание1/to-be.puml)

## Задание 2

### 1. Proxy
Команда КиноБездны уже выделила сервис метаданных о фильмах movies и вам необходимо реализовать бесшовный переход с применением паттерна Strangler Fig в части реализации прокси-сервиса (API Gateway), с помощью которого можно будет постепенно переключать траффик, используя фиче-флаг.

1. Реализован прокси сервис API Gateway (Proxy Service) на C# + yarp библиотека (без динамического обновления MOVIES_MIGRATION_PERCENT),
который позволяет переключать траффик, используя флаг.

  [Proxy Service](src/microservices/proxy/)

2. Запущены postman тесты, их результат можно увидеть на картинке:
  [Unit Tests](schemas/задание2/результат%20выполнения%20тестов.png)

  - Отправляем запросы к API Gateway:
     ```bash
     curl http://localhost:8000/api/movies
     ```
  - Тестируем постепенный переход, изменив переменную окружения MOVIES_MIGRATION_PERCENT в файле docker-compose.yml.
  Рассмотрены три случая 
    2.1 MOVIES_MIGRATION_PERCENT = 1 
    [Прием данных через монолит](schemas/задание2/миграция%20данных%20с%20минимальным%20трафиком%20(работаем%20через%20monolith)%20(percent=1).png)

    2.2 MOVIES_MIGRATION_PERCENT = 50 
    [Прием данных через монолит/микросервис](schemas/задание2/миграция%20данных%20с%20срединным%20трафиком%20(работаем%20через%20monolith)%20(percent=50).png)
    [Прием данных через монолит/микросервис](schemas/задание2/миграция%20данных%20с%20срединным%20трафиком%20(работаем%20через%20movies-service)%20(percent=50).png)

    2.3 MOVIES_MIGRATION_PERCENT = 99
    [Прием данных через монолит](schemas/задание2/миграция%20данных%20с%20переводом%20трафика%20на%20новый%20сервис%20(работаем%20через%20movies-service)%20(percent=99).png)


### 2. Kafka
 Сделан MVP сервис events, который будет при вызове API создавать и сам же читать сообщения в топике Kafka.
    [Event Service](src/microservices/events/)

    - Сервис разработан на python.
    - Реализован простой API, при вызове которого будут создаваться события User/Payment/Movie и обрабатываться внутри сервиса с записью в лог
    - Сервс добавлен в docker-compose

  [Скриншоты тестов](schemas/задание2/результат%20выполнения%20тестов.png)
  [Скриншот состояния топиков Kafka http://localhost:8090](schemas/задание2/топики%20kafka.png)

## Задание 3

Команда начала переезд в Kubernetes для лучшего масштабирования и повышения надежности. 
Вам, как архитектору осталось самое сложное:
 - реализовать CI/CD для сборки прокси сервиса
 - реализовать необходимые конфигурационные файлы для переключения трафика.


### CI/CD

В папке .github/worflows доработан деплой новых сервисов proxy и events в docker-build-push.yml, 
api-tests при сборке отрабатывают успешно при отправке коммита в ваш репозиторий,
докер образы сервисов также успешно создаются

["Скриншот пайплайна тестов.](./schemas/задание3/Шаг1/1%20CI_CD%20(пайплайн%20тестов%20и%20докер%20образы).png)

[Скриншот образов сервисов в github registry.](./schemas/задание3/Шаг1/2%20github%20registry%20образы%20сервисов.png) 

### Proxy в Kubernetes

#### Шаг 1
Для деплоя в kubernetes необходимо залогиниться в docker registry Github'а.
1. Создайте Personal Access Token (PAT) https://github.com/settings/tokens . Создавайте class с правом read:packages

Для деплоя в kubernetes необходимо залогиниться в docker registry Github'а.

1. Создан Personal Access Token (PAT) https://github.com/settings/tokens с правом read:packages
2. В src/kubernetes/*.yaml (event-service, monolith, movies-service и proxy-service) отредактированы пути до образов сервисов
3. Добавлен секрет в src/kubernetes/dockerconfigsecret.yaml 

#### Шаг 2
  Доработан src/kubernetes/event-service.yaml и src/kubernetes/proxy-service.yaml

  - Необходимо создать Deployment и Service 
  - Доработан ingress.yaml, чтобы можно было с помощью тестов проверить создание событий
  Шаги для поднятия кластера (делаем в контексте minikube):

  1. Создайте namespace:
  ```bash
  kubectl apply -f src/kubernetes/namespace.yaml - готово
  ```
  2. Создайте секреты и переменные - готово
  ```bash
  kubectl apply -f src/kubernetes/configmap.yaml
  kubectl apply -f src/kubernetes/secret.yaml
  kubectl apply -f src/kubernetes/dockerconfigsecret.yaml
  kubectl apply -f src/kubernetes/postgres-init-configmap.yaml
  ```

  3. Разверните базу данных: - готово
  ```bash
  kubectl apply -f src/kubernetes/postgres.yaml
  ```

  На этом этапе если вызвать команду
  ```bash
  kubectl -n cinemaabyss get pod
  ```
  Вы увидите

  NAME         READY   STATUS    
  postgres-0   1/1     Running   

  4. Разверните Kafka: - готово
  ```bash
  kubectl apply -f src/kubernetes/kafka/kafka.yaml
  ```

  Проверьте, теперь должно быть запущено 3 пода, если что-то не так, то посмотрите логи
  ```bash
  kubectl -n cinemaabyss logs имя_пода (например - kafka-0)
  ```

  5. Разверните монолит: - готово
  ```bash
  kubectl apply -f src/kubernetes/monolith.yaml
  ```
  6. Разверните микросервисы: - готово
  ```bash
  kubectl apply -f src/kubernetes/movies-service.yaml
  kubectl apply -f src/kubernetes/events-service.yaml
  ```
  7. Разверните прокси-сервис: - готово
  ```bash
  kubectl apply -f src/kubernetes/proxy-service.yaml
  ```

  После запуска и поднятия подов вывод команды - готово
  ```bash
  kubectl -n cinemaabyss get pod
  ```
  [Результат развертывания](/schemas//задание3/Шаг2/7%20все%20поды.png)

  8. Добавим ingress

  - добавьте аддон
  ```bash
  minikube addons enable ingress - готово
  ```
  ```bash
  kubectl apply -f src/kubernetes/ingress.yaml - готово
  ```
  9. Добавьте в /etc/hosts - готово
  127.0.0.1 cinemaabyss.example.com

  10. Вызовите
  ```bash
  minikube tunnel - готово
    ```
  11. Вызовите https://cinemaabyss.example.com/api/movies - готово
  Вы должны увидеть вывод списка фильмов
  Можно поэкспериментировать со значением   MOVIES_MIGRATION_PERCENT в src/kubernetes/configmap.yaml и убедится, что вызовы movies уходят полностью в новый сервис

  12. Запустите тесты из папки tests/postman
  ```bash
   npm run test:kubernetes
  ```
  Часть тестов с health-чек упадет, но создание событий отработает.
  [Результат выполнения тестов в k8s](/schemas/задание3/Шаг2/результат%20работы%20тестов%20k8s.png)


#### Шаг 3
Cкриншот вывода при вызове https://cinemaabyss.example.com/api/movies 
[Результат получения списка фильмов](/schemas/задание3/Шаг3/список%20фильмов%20minikube.png)

Скриншот вывода event-service после вызова тестов
[Логи event-service после запуска](/schemas/задание3/Шаг3/логи%20event-service%20после%20запуска%20тестов.png)


## Задание 4
Для простоты дальнейшего обновления и развертывания вам как архитектуру необходимо так же реализовать helm-чарты для прокси-сервиса и проверить работу 

Для этого:
1. Перейдите в директорию helm и отредактируйте файл values.yaml - готово

```yaml
# Proxy service configuration
proxyService:
  enabled: true
  image:
    repository: ghcr.io/db-exp/cinemaabysstest/proxy-service
    tag: latest
    pullPolicy: Always
  replicas: 1
  resources:
    limits:
      cpu: 300m
      memory: 256Mi
    requests:
      cpu: 100m
      memory: 128Mi
  service:
    port: 80
    targetPort: 8000
    type: ClusterIP
```

- Вместо ghcr.io/db-exp/cinemaabysstest/proxy-service напишите свой путь до образа для всех сервисов
- для imagePullSecret проставьте свое значение (скопируйте из конфигурации kubernetes) - готово
  ```yaml
  imagePullSecrets:
      dockerconfigjson: ewoJImF1dGhzIjogewoJCSJnaGNyLmlvIjogewoJCQkiYXV0aCI6ICJaR0l0Wlhod09tZG9jRjl2UTJocVZIa3dhMWhKVDIxWmFVZHJOV2hRUW10aFVXbFZSbTVaTjJRMFNYUjRZMWM9IgoJCX0KCX0sCgkiY3JlZHNTdG9yZSI6ICJkZXNrdG9wIiwKCSJjdXJyZW50Q29udGV4dCI6ICJkZXNrdG9wLWxpbnV4IiwKCSJwbHVnaW5zIjogewoJCSIteC1jbGktaGludHMiOiB7CgkJCSJlbmFibGVkIjogInRydWUiCgkJfQoJfSwKCSJmZWF0dXJlcyI6IHsKCQkiaG9va3MiOiAidHJ1ZSIKCX0KfQ==
  ```

2. В папке ./templates/services заполните шаблоны для proxy-service.yaml и events-service.yaml (опирайтесь на свою kubernetes конфигурацию - смысл helm'а сделать шаблоны для быстрого обновления и установки) - готово

```yaml
template:
    metadata:
      labels:
        app: proxy-service
    spec:
      containers:
       Тут ваша конфигурация
```

3. Проверьте установку
Сначала удалим установку руками

```bash
kubectl delete all --all -n cinemaabyss
kubectl delete  namespace cinemaabyss
```
Запустите 
```bash
helm install cinemaabyss .\src\kubernetes\helm --namespace cinemaabyss --create-namespace - готово
```
Если в процессе будет ошибка
```code
[2025-04-08 21:43:38,780] ERROR Fatal error during KafkaServer startup. Prepare to shutdown (kafka.server.KafkaServer)
kafka.common.InconsistentClusterIdException: The Cluster ID OkOjGPrdRimp8nkFohYkCw doesn't match stored clusterId Some(sbkcoiSiQV2h_mQpwy05zQ) in meta.properties. The broker is trying to join the wrong cluster. Configured zookeeper.connect may be wrong.
```

Проверьте развертывание:
```bash
kubectl get pods -n cinemaabyss
minikube tunnel
```

[https://cinemaabyss.example.com/api/movies](/schemas/задание4/скриншот%20сайта%20через%20helm.png)
[Cкриншот развертывания helm](/schemas/задание4/скриншот%20развертывания%20helm.png)
[Поды созданные после запуска helm](/schemas/задание4/поды%20созданные%20на%20основе%20helm.png)

# Задание 5
Компания планирует активно развиваться и для повышения надежности, безопасности, реализации сетевых паттернов типа Circuit Breaker и канареечного деплоя вам как архитектору необходимо развернуть istio и настроить circuit breaker для monolith и movies сервисов.

```bash

helm repo add istio https://istio-release.storage.googleapis.com/charts
helm repo update

helm install istio-base istio/base -n istio-system --set defaultRevision=default --create-namespace
helm install istio-ingressgateway istio/gateway -n istio-system
helm install istiod istio/istiod -n istio-system --wait

helm install cinemaabyss .\src\kubernetes\helm --namespace cinemaabyss --create-namespace

kubectl label namespace cinemaabyss istio-injection=enabled --overwrite

kubectl get namespace -L istio-injection

kubectl apply -f .\src\kubernetes\circuit-breaker-config.yaml -n cinemaabyss

```

Тестирование

# fortio
```bash
kubectl apply -f https://raw.githubusercontent.com/istio/istio/release-1.25/samples/httpbin/sample-client/fortio-deploy.yaml -n cinemaabyss
```

# Get the fortio pod name
```bash
FORTIO_POD=$(kubectl get pod -n cinemaabyss | grep fortio | awk '{print $1}')

kubectl exec -n cinemaabyss $FORTIO_POD -c fortio -- fortio load -c 50 -qps 0 -n 500 -loglevel Warning http://movies-service:8081/api/movies
```
Например,

```bash
kubectl exec -n cinemaabyss fortio-deploy-b6757cbbb-7c9qg  -c fortio -- fortio load -c 50 -qps 0 -n 500 -loglevel Warning http://movies-service:8081/api/movies
```

Вывод будет типа такого

```bash
IP addresses distribution:
10.106.113.46:8081: 421
Code 200 : 79 (15.8 %)
Code 500 : 22 (4.4 %)
Code 503 : 399 (79.8 %)
```
Можно еще проверить статистику

```bash
kubectl exec -n cinemaabyss fortio-deploy-b6757cbbb-7c9qg -c istio-proxy -- pilot-agent request GET stats | grep movies-service | grep pending
```

И там смотрим 

```bash
cluster.outbound|8081||movies-service.cinemaabyss.svc.cluster.local;.upstream_rq_pending_total: 311 - столько раз срабатывал circuit breaker
You can see 21 for the upstream_rq_pending_overflow value which means 21 calls so far have been flagged for circuit breaking.
```

Приложите скриншот работы circuit breaker'а

Удаляем все
```bash
istioctl uninstall --purge
kubectl delete namespace istio-system
kubectl delete all --all -n cinemaabyss
kubectl delete namespace cinemaabyss
```

# Nemoryn in Docker

[**English**](docker.md) · [Čeština](docker.cs.md) · [Deutsch](docker.de.md)

The server and Postgres run as two services. The API starts on its own once the database is healthy.

## Start

```bash
cd V2.0
cp .env.example .env
# set POSTGRES_PASSWORD (do not leave the default `change-me`) and MEMORY_AI_BASE_URL
docker compose up --build -d
```

Console: `http://<host>:5022/`  
Open WebUI model endpoint: `http://<host>:5022/v1`

Data stays in volume `nemoryn-pgdata` (Postgres) and `nemoryn-data` (AI/DB connections saved from the console).

## Database

Compose creates Postgres with `pgvector`. From the Nemoryn container the host is `postgres`, port `5432`. Onto the host it maps `5433` (`POSTGRES_PORT`), because `5432` is often taken by a local Postgres. Changing `POSTGRES_PORT` only affects access from the Mac; Nemoryn still calls `postgres:5432` inside the network.

In the console **Runtime → Database** you can switch to another Postgres (existing server, `host.docker.internal`, LAN IP). The password is not returned in GET; an empty field keeps the current one. It takes effect immediately and is written to `/app/data/memory-db.connection.json`.

`localhost` inside the container is not Postgres on the host. For a DB on the same machine use `host.docker.internal`.

## AI model

Ollama/OpenAI stays outside Compose. Set the base URL and models in Runtime, or in `.env` (`MEMORY_AI_BASE_URL`). From Docker onto the LAN a direct IP usually works (e.g. `http://192.168.1.2:11434`). Model on the host: `http://host.docker.internal:11434`.

## Publishing the image

```bash
docker compose build
docker tag nemoryn:local <registry>/nemoryn:latest
docker push <registry>/nemoryn:latest
```

On the target server you only need `docker-compose.yml`, `.env`, and the image. For the `nemoryn` service then use `image: <registry>/nemoryn:latest` instead of `build`.

## Stop

```bash
docker compose down
```

Volumes are removed only with `-v` — that deletes memories in Postgres.

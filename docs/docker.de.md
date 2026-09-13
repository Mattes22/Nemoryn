# Nemoryn in Docker

[English](docker.md) · [Čeština](docker.cs.md) · [**Deutsch**](docker.de.md)

Server und Postgres laufen als zwei Dienste. Die API startet von selbst, sobald die Datenbank gesund ist.

## Start

```bash
cd V2.0
cp .env.example .env
# POSTGRES_PASSWORD anpassen (Default `change-me` nicht lassen) und MEMORY_AI_BASE_URL
docker compose up --build -d
```

Konsole: `http://<host>:5022/`  
Open-WebUI-Model-Endpoint: `http://<host>:5022/v1`

Daten bleiben im Volume `nemoryn-pgdata` (Postgres) und `nemoryn-data` (AI/DB-Verbindungen aus der Konsole).

## Datenbank

Compose legt Postgres mit `pgvector` an. Aus dem Nemoryn-Container ist der Host `postgres`, Port `5432`. Auf den Host wird `5433` (`POSTGRES_PORT`) gemappt, weil `5432` oft ein lokales Postgres belegt. Eine Änderung von `POSTGRES_PORT` betrifft nur den Zugriff vom Mac; Nemoryn ruft intern weiter `postgres:5432` auf.

In der Konsole **Runtime → Datenbank** kannst du auf ein anderes Postgres wechseln (bestehender Server, `host.docker.internal`, LAN-IP). Das Passwort kommt im GET nicht zurück; ein leeres Feld behält das aktuelle. Es gilt sofort und wird nach `/app/data/memory-db.connection.json` geschrieben.

`localhost` im Container ist nicht Postgres auf dem Host. Für eine DB auf derselben Maschine `host.docker.internal` verwenden.

## AI-Modell

Ollama/OpenAI bleibt außerhalb von Compose. Base-URL und Modelle in Runtime setzen, oder in `.env` (`MEMORY_AI_BASE_URL`). Von Docker ins LAN funktioniert meist die direkte IP (z. B. `http://192.168.1.2:11434`). Modell auf dem Host: `http://host.docker.internal:11434`.

## Image veröffentlichen

```bash
docker compose build
docker tag nemoryn:local <registry>/nemoryn:latest
docker push <registry>/nemoryn:latest
```

Auf dem Zielserver reichen `docker-compose.yml`, `.env` und das Image. Beim Dienst `nemoryn` dann statt `build` `image: <registry>/nemoryn:latest` verwenden.

## Stoppen

```bash
docker compose down
```

Volumes löscht nur `-v` — damit sind die Erinnerungen in Postgres weg.

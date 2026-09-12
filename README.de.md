# Nemoryn

[English](README.md) | [Čeština](README.cs.md) | [Deutsch](README.de.md)

OpenAI-kompatibler Memory-Server. Postgres läuft in Docker. Das Chat-Modell (Ollama oder OpenAI) bringst du selbst mit.

Konsole: `http://localhost:5022/`  
Open WebUI: `http://localhost:5022/v1`

## Was du brauchst

- Docker Desktop (oder einen Daemon) mit `docker compose`
- Ollama (oder eine OpenAI-kompatible API) mit Chat-Modell und Embedding-Modell
- freien Port `5022`; Compose mappt Postgres auf Host-Port `5433`, damit es nicht mit lokalem `5432` kollidiert

## Start

```bash
cp .env.example .env
```

In `.env` mindestens setzen:

```bash
POSTGRES_PASSWORD=your-password
MEMORY_AI_BASE_URL=http://host.docker.internal:11434
MEMORY_AI_CHAT_MODEL=gpt-oss:20b
MEMORY_AI_EMBEDDING_MODEL=nomic-embed-text
```

Ollama auf einem anderen Rechner im LAN: `MEMORY_AI_BASE_URL=http://192.168.x.x:11434`.

```bash
docker compose up --build -d
```

Öffne `http://localhost:5022/`. Unter **Runtime** prüfe, ob Datenbank und Modell leben. In Docker ist der Datenbank-Host `postgres`, Port **`5432`** (nicht `5433`).

Stoppen: `docker compose down`. Volumes löschst du nur mit `-v`.

## Open WebUI

Admin → Settings → Connections → OpenAI:

| Feld | Wert |
|---|---|
| API Base URL | `http://127.0.0.1:5022/v1` |
| API Key | beliebig, wenn `NEMORYN_API_KEY` leer ist; sonst derselbe String |

Modell und Ollama-Base-URL setzt du in Nemoryn Runtime, nicht in Open WebUI.

## Gehört nicht hierher

- Open WebUI ist kein Plugin-Host. Kandidaten, Konflikte, Pin und Audit gibt es nur in der Konsole.
- Die Konsole hat kein Login. `5022` ohne Proxy/VPN nicht ins öffentliche Internet legen.
- `.env` und `memory-*.connection.json` werden nicht committet.

Mehr zu Docker: `docs/docker.md`. Open WebUI: `docs/openwebui.md`.

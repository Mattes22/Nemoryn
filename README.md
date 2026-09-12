# Nemoryn

[English](README.md) | [Čeština](README.cs.md) | [Deutsch](README.de.md)

OpenAI-compatible memory server. Postgres runs in Docker. You bring your own chat model (Ollama or OpenAI).

Console: `http://localhost:5022/`  
Open WebUI: `http://localhost:5022/v1`

## What you need

- Docker Desktop (or a daemon) with `docker compose`
- Ollama (or an OpenAI-compatible API) with a chat model and an embedding model
- Port `5022` free. Compose maps Postgres to host port `5433` so it does not clash with local `5432`

## Start

```bash
cp .env.example .env
```

In `.env` set at least:

```bash
POSTGRES_PASSWORD=your-password
MEMORY_AI_BASE_URL=http://host.docker.internal:11434
MEMORY_AI_CHAT_MODEL=gpt-oss:20b
MEMORY_AI_EMBEDDING_MODEL=nomic-embed-text
```

Ollama on another machine on the LAN: `MEMORY_AI_BASE_URL=http://192.168.x.x:11434`.

```bash
docker compose up --build -d
```

Open `http://localhost:5022/`. In **Runtime**, check that the database and the model are live. Inside Docker the database host is `postgres`, port **`5432`** (not `5433`).

Stop: `docker compose down`. Volumes are removed only with `-v`.

## Open WebUI

Admin → Settings → Connections → OpenAI:

| Field | Value |
|---|---|
| API Base URL | `http://127.0.0.1:5022/v1` |
| API Key | anything if `NEMORYN_API_KEY` is empty; otherwise the same string |

The model and the Ollama base URL are set in Nemoryn Runtime, not in Open WebUI.

## Out of scope

- Open WebUI is not a plugin host. Candidates, conflicts, pin, and audit live in the console only.
- The console has no login. Do not expose `5022` to the public internet without a proxy or VPN.
- `.env` and `memory-*.connection.json` are not committed.

More on Docker: `docs/docker.md`. Open WebUI: `docs/openwebui.md`.

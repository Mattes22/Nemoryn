# Nemoryn

[English](README.md) | [Čeština](README.cs.md) | [Deutsch](README.de.md)

OpenAI-compatible memory server. Postgres běží v Dockeru. Chat model (Ollama/OpenAI) si připojuješ sám.

Konzole: `http://localhost:5022/`  
Open WebUI: `http://localhost:5022/v1`

## Co potřebuješ

- Docker Desktop (nebo daemon) s `docker compose`
- Ollama (nebo OpenAI-compatible API) s chat modelem a embedding modelem
- volný port `5022`; Postgres v Compose mapuje na hostitele `5433`, aby se nepotkal s lokálním `5432`

## Start

```bash
git clone https://github.com/Mattes22/Nemoryn.git
cd Nemoryn
cp .env.example .env
```

V `.env` nastav aspoň:

```bash
POSTGRES_PASSWORD=your-password
MEMORY_AI_BASE_URL=http://host.docker.internal:11434
MEMORY_AI_CHAT_MODEL=gpt-oss:20b
MEMORY_AI_EMBEDDING_MODEL=nomic-embed-text
```

Ollama na jiném stroji v LAN: `MEMORY_AI_BASE_URL=http://192.168.x.x:11434`.

```bash
docker compose up --build -d
```

Otevři `http://localhost:5022/`. V **Runtime** zkontroluj, že žije databáze i model. Host databáze v Dockeru je `postgres`, port **`5432`** (ne `5433`).

Zastavení: `docker compose down`. Volume smažeš jen s `-v`.

## Open WebUI

Admin → Settings → Connections → OpenAI:

| Pole | Hodnota |
|---|---|
| API Base URL | `http://127.0.0.1:5022/v1` |
| API Key | cokoliv, pokud je `NEMORYN_API_KEY` prázdný; jinak stejný řetězec |

Model a Base URL Ollamy se nastavují v Nemoryn Runtime, ne v Open WebUI.

## Co sem nepatří

- Open WebUI není plugin host. Kandidáti, konflikty, pin a audit jsou jen v konzoli.
- Konzole nemá přihlášení. Veřejný internet bez proxy/VPN na `5022` nepouštěj.
- `.env` a `memory-*.connection.json` se necommitují.

Víc k Dockeru: `docs/docker.md`. Open WebUI: `docs/openwebui.md`.

# Nemoryn v Dockeru

Server i Postgres běží jako dvě služby. API se spustí samo, jakmile je databáze zdravá.

## Spuštění

```bash
cd V2.0
cp .env.example .env
# uprav POSTGRES_PASSWORD (výchozí `change-me` nenechávej) a MEMORY_AI_BASE_URL
docker compose up --build -d
```

Konzole: `http://<host>:5022/`  
Open WebUI model endpoint: `http://<host>:5022/v1`

Data zůstanou ve volume `nemoryn-pgdata` (Postgres) a `nemoryn-data` (uložené AI/DB připojení z konzole).

## Databáze

Compose založí Postgres s `pgvector`. Z kontejneru Nemoryn je host `postgres`, port `5432`. Na hostitele se mapuje `5433` (`POSTGRES_PORT`), protože `5432` často zabírá lokální Postgres. Změna `POSTGRES_PORT` se týká jen přístupu z Macu; Nemoryn pořád volá `postgres:5432` uvnitř sítě.

V konzoli **Runtime → Databáze** můžeš přepnout na jiný Postgres (existující server, `host.docker.internal`, LAN IP). Heslo se v GET nevrací; prázdné pole nechá stávající. Platí hned a zapíše se do `/app/data/memory-db.connection.json`.

`localhost` uvnitř kontejneru není Postgres na hostiteli. Pro DB na stejném stroji použij `host.docker.internal`.

## AI model

Ollama/OpenAI zůstává mimo Compose. Base URL a modely nastav v Runtime, nebo v `.env` (`MEMORY_AI_BASE_URL`). Z Dockeru na LAN obvykle funguje přímo IP (např. `http://192.168.1.2:11434`). Model na hostiteli: `http://host.docker.internal:11434`.

## Publikace image

```bash
docker compose build
docker tag nemoryn:local <registry>/nemoryn:latest
docker push <registry>/nemoryn:latest
```

Na cílovém serveru stačí `docker-compose.yml`, `.env` a image. Služba `nemoryn` pak místo `build` použij `image: <registry>/nemoryn:latest`.

## Zastavení

```bash
docker compose down
```

Volume smažeš jen s `-v` — tím přijdeš o paměti v Postgresu.

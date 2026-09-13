# Nemoryn Tools Gateway

[English](tools.md) · [**Čeština**](tools.cs.md) · [Deutsch](tools.de.md)

Samostatná tool vrstva vedle Memory Core. Klienti (Open WebUI, vlastní UI, MCP později) volají REST, ne Memory agent loop.

OpenAPI jen pro tyto endpointy: `http://<host>:5022/openapi/tools.json`

## Nástroje

| Nástroj | Capability | Účel |
|---|---|---|
| `web.search` | `web.search` | fulltext na internetu přes SearXNG |
| `web.fetch` | `web.read` | jedna veřejná HTTP(S) stránka, vyčištěný text |

`web.fetch` není obecný HTTP client. Loopback, link-local, RFC1918 a lokální IPv6 jsou blokované. Redirect na privátní adresu taky. Capability `network.local` tento tool nepoužívá.

Chat přes Open WebUI `/v1` používá stejné SearXNG vyhledávání jako agent nástroje `web_search` / `web_fetch` (profil Safe). Tento REST gateway je pro ostatní volající.

## Konfigurace SearXNG

V konzoli: **Tools → SearXNG → Base URL**. Platí hned a po uložení i po restartu (`memory-tools.connection.json`).

Stejné hodnoty jdou i přes `appsettings.json` / env, dokud je konzole nepřepíše:

```json
{
  "Tools": {
    "DefaultCapabilities": [ "web.search", "web.read" ],
    "Web": {
      "SearchProvider": "SearXNG",
      "SearXNG": {
        "BaseUrl": "http://127.0.0.1:8080"
      }
    }
  }
}
```

Docker: `TOOLS_SEARXNG_BASE_URL=http://host.docker.internal:8080` v `.env` (SearXNG na hostiteli). Uvnitř kódu žádná URL není.

SearXNG musí vracet JSON (`format=json`). Bez `BaseUrl` `web.search` skončí chybou poskytovatele.

## Endpointy

```text
GET  /api/v1/tools
POST /api/v1/tools/web.search/execute
POST /api/v1/tools/web.fetch/execute
POST /api/v1/tools/{toolName}/execute
```

Hlavičky:

- `X-Nemoryn-Caller` — kdo volá (audit)
- `X-Nemoryn-Capabilities` — volitelný subset; vždy se protne se serverovým `Tools:DefaultCapabilities`

Bez hlavičky capabilities platí `DefaultCapabilities`.

## curl

Seznam:

```bash
curl -s http://127.0.0.1:5022/api/v1/tools
```

Search:

```bash
curl -s -X POST http://127.0.0.1:5022/api/v1/tools/web.search/execute \
  -H 'Content-Type: application/json' \
  -H 'X-Nemoryn-Caller: local-test' \
  -d '{"query":"nemoryn memory","maxResults":5}'
```

Fetch:

```bash
curl -s -X POST http://127.0.0.1:5022/api/v1/tools/web.fetch/execute \
  -H 'Content-Type: application/json' \
  -H 'X-Nemoryn-Caller: local-test' \
  -d '{"url":"https://example.com"}'
```

SSRF kontrola (má vrátit 400):

```bash
curl -s -X POST http://127.0.0.1:5022/api/v1/tools/web.fetch/execute \
  -H 'Content-Type: application/json' \
  -d '{"url":"http://127.0.0.1/"}'
```

## MCP a Open WebUI

Ještě není. Stejný `IToolExecutor` půjde obalit MCP serverem. Open WebUI External Tools / OpenAPI server má mířit na `/openapi/tools.json`, ne na celé `/openapi/v1.json` (to obsahuje Memory API).

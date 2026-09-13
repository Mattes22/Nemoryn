# Nemoryn Tools Gateway

[English](tools.md) · [Čeština](tools.cs.md) · [**Deutsch**](tools.de.md)

Eigene Tool-Schicht neben dem Memory Core. Clients (Open WebUI, eigenes UI, später MCP) rufen REST auf, nicht den Memory-Agent-Loop.

OpenAPI nur für diese Endpunkte: `http://<host>:5022/openapi/tools.json`

## Tools

| Tool | Capability | Zweck |
|---|---|---|
| `web.search` | `web.search` | Volltextsuche im Internet über SearXNG |
| `web.fetch` | `web.read` | eine öffentliche HTTP(S)-Seite, bereinigter Text |

`web.fetch` ist kein allgemeiner HTTP-Client. Loopback, Link-Local, RFC1918 und lokales IPv6 sind blockiert. Redirect auf eine private Adresse ebenfalls. Die Capability `network.local` nutzt dieses Tool nicht.

Chat über Open WebUI `/v1` verwendet dieselbe SearXNG-Suche wie die Agent-Tools `web_search` / `web_fetch` (Profil Safe). Dieses REST-Gateway ist für andere Aufrufer.

## SearXNG-Konfiguration

In der Konsole: **Tools → SearXNG → Base URL**. Gilt sofort und nach dem Speichern auch nach einem Neustart (`memory-tools.connection.json`).

Dieselben Werte gehen auch über `appsettings.json` / Env, bis die Konsole sie überschreibt:

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

Docker: `TOOLS_SEARXNG_BASE_URL=http://host.docker.internal:8080` in `.env` (SearXNG auf dem Host). Im Code steht keine URL.

SearXNG muss JSON liefern (`format=json`). Ohne `BaseUrl` endet `web.search` mit einem Provider-Fehler.

## Endpunkte

```text
GET  /api/v1/tools
POST /api/v1/tools/web.search/execute
POST /api/v1/tools/web.fetch/execute
POST /api/v1/tools/{toolName}/execute
```

Header:

- `X-Nemoryn-Caller` — wer aufruft (Audit)
- `X-Nemoryn-Capabilities` — optionales Subset; immer geschnitten mit dem Server-`Tools:DefaultCapabilities`

Ohne Capabilities-Header gilt `DefaultCapabilities`.

## curl

Liste:

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

SSRF-Check (sollte 400 liefern):

```bash
curl -s -X POST http://127.0.0.1:5022/api/v1/tools/web.fetch/execute \
  -H 'Content-Type: application/json' \
  -d '{"url":"http://127.0.0.1/"}'
```

## MCP und Open WebUI

Noch nicht. Derselbe `IToolExecutor` lässt sich später als MCP-Server wrappen. Open WebUI External Tools / OpenAPI-Server sollen auf `/openapi/tools.json` zeigen, nicht auf das ganze `/openapi/v1.json` (das enthält die Memory API).

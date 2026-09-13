# Nemoryn Tools Gateway

[**English**](tools.md) · [Čeština](tools.cs.md) · [Deutsch](tools.de.md)

A separate tool layer next to Memory Core. Clients (Open WebUI, custom UI, MCP later) call REST, not the Memory agent loop.

OpenAPI for these endpoints only: `http://<host>:5022/openapi/tools.json`

## Tools

| Tool | Capability | Purpose |
|---|---|---|
| `web.search` | `web.search` | full-text web search via SearXNG |
| `web.fetch` | `web.read` | one public HTTP(S) page, cleaned text |

`web.fetch` is not a general HTTP client. Loopback, link-local, RFC1918, and local IPv6 are blocked. Redirects to a private address too. This tool does not use the `network.local` capability.

Chat through Open WebUI `/v1` uses the same SearXNG search as the agent tools `web_search` / `web_fetch` (Safe profile). This REST gateway is for other callers.

## SearXNG configuration

In the console: **Tools → SearXNG → Base URL**. Takes effect immediately and after save also after a restart (`memory-tools.connection.json`).

The same values can go through `appsettings.json` / env until the console overwrites them:

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

Docker: `TOOLS_SEARXNG_BASE_URL=http://host.docker.internal:8080` in `.env` (SearXNG on the host). There is no URL hardcoded in the source.

SearXNG must return JSON (`format=json`). Without `BaseUrl`, `web.search` fails with a provider error.

## Endpoints

```text
GET  /api/v1/tools
POST /api/v1/tools/web.search/execute
POST /api/v1/tools/web.fetch/execute
POST /api/v1/tools/{toolName}/execute
```

Headers:

- `X-Nemoryn-Caller` — who is calling (audit)
- `X-Nemoryn-Capabilities` — optional subset; always intersected with the server `Tools:DefaultCapabilities`

Without the capabilities header, `DefaultCapabilities` applies.

## curl

List:

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

SSRF check (should return 400):

```bash
curl -s -X POST http://127.0.0.1:5022/api/v1/tools/web.fetch/execute \
  -H 'Content-Type: application/json' \
  -d '{"url":"http://127.0.0.1/"}'
```

## MCP and Open WebUI

Not yet. The same `IToolExecutor` can later be wrapped as an MCP server. Open WebUI External Tools / OpenAPI server should point at `/openapi/tools.json`, not at the full `/openapi/v1.json` (that includes the Memory API).

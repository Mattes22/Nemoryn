# Open WebUI → Memory `/v1`

[English](openwebui.md) · [Čeština](openwebui.cs.md) · [**Deutsch**](openwebui.de.md)

Memory stellt einen **OpenAI-kompatiblen Model-Endpoint** bereit, kein Plugin und keinen Fork von Open WebUI. Tool-Loop, Kandidaten, Konflikte und Audit leben in Memory (Agent + Konsole). Open WebUI ist nur ein Chat-Client.

## Verbindung

In Open WebUI: **Admin → Settings → Connections → OpenAI**.

| Feld | Wert |
|---|---|
| API Base URL | `http://<host>:5022/v1` (lokal `http://127.0.0.1:5022/v1`) |
| API Key | derselbe String wie `OpenAiCompatible:ApiKey` |

Wenn `OpenAiCompatible:ApiKey` leer ist, prüft `/v1` keinen Schlüssel. Open WebUI verlangt das Feld trotzdem — beliebiges Zeichen reicht. Sobald ein Schlüssel gesetzt ist, sendet Open WebUI ihn als `Authorization: Bearer …`.

### Pflicht-Header

In den **Advanced**-Einstellungen derselben Verbindung hinzufügen:

```json
{
  "X-OpenWebUI-User-Id": "{{USER_ID}}",
  "X-OpenWebUI-Chat-Id": "{{CHAT_ID}}"
}
```

Ohne diese Header bekommt Nemoryn keine stabile Benutzer- und Chat-Identität. Open WebUI füllt `{{USER_ID}}` und `{{CHAT_ID}}` erst im echten Chat — der Button Verify Connection kann sie leer lassen.

Ollama/OpenAI-**Base URL** und das Standard-Chat-Modell setzt du in der Nemoryn-Konsole (**Runtime**), nicht in Open WebUI Connections. Ein Klick auf das Modell speichert es und überlebt einen API-Neustart. Open WebUI ruft immer nur `http://<host>:5022/v1` auf. Docker: siehe [docker.de.md](docker.de.md).

`GET /v1/models` liefert Chat-Modelle aus dem Upstream-Katalog (kein Embedding-Modell). Das **erste** in der Liste ist das Standardmodell aus Runtime. In Open WebUI kannst du ein anderes aus der Liste wählen. Ein unbekanntes `model` in `POST /v1/chat/completions` liefert 400.

## Identität

Owner und Konversation kommen aus dem Request, nicht aus dem Memory-Konsolenkonto.

Owner (erster nicht-leerer Wert):

1. `X-OpenWebUI-User-Id`
2. `X-OpenWebUI-User-Email`
3. `X-OpenWebUI-User-Name`
4. `X-User-Id`
5. `metadata.owner_id`
6. `user`
7. sonst `anonymous`

Konversation (erster nicht-leerer Wert):

1. `X-OpenWebUI-Chat-Id`
2. `X-Chat-Id`
3. `metadata.chat_id`
4. `metadata.conversation_id`
5. `conversation_id`
6. sonst `{ownerId}-default`

Eine Chat-ID, die keine GUID ist, wird als `externalId` upsertet. Derselbe Open-WebUI-Chat hält damit eine Memory-Konversation.

## Was funktioniert

- `GET /v1/models`
- `POST /v1/chat/completions` non-stream und stream
- Retrieval + Agent-Prompt (`memory.agent.v1.1`)
- Ingest von User-Nachrichten in den Speicher (nur echter Chat, siehe Side-Tasks)
- eingebaute Tools im Profil **Safe** (`get_time`, `search_memories`, `web_search`, `web_fetch`) — Nemoryn bietet sie dem Modell selbst an; Open WebUI sieht sie nicht

Beim Agenten sendet der Stream den **finalen** Assistant-Text (nach dem Tool-Loop), keine Tokens von innen aus den Tools.

## Was absichtlich nicht

- Open WebUI ist **kein** Plugin-Host. `/v1` nimmt keine OWUI Functions/Tools an und startet kein MCP. Websuche läuft in Nemoryn (SearXNG), nicht in Open WebUI.
- Open WebUI bekommt **kein** `NetworkOnce` / Untrusted HTTP. Das Profil ist immer Safe.
- Kandidaten, Konflikte, Pin, Forget und Audit sind **nicht** in Open WebUI — nur in der Nemoryn-Konsole (`http://<host>:5022/`).
- Title- / Tags- / Follow-up- / Image-Prompt-Side-Tasks von Open WebUI sind **kein Speicher**. Sie gehen als Passthrough an das Modell, ohne Ingest und ohne Agent-Turn.
- Ein leerer `OpenAiCompatible:ApiKey` lässt `/v1` offen. Auf einem geteilten Host den Schlüssel setzen.

## Smoke-Checkliste

Gegen eine Instanz, die dieselbe Postgres sieht wie der Ingest (lokales API gegen eine entfernte DB reicht nicht).

1. **Models**  
   `GET /v1/models` → 200, `data` ist die Liste der Chat-Modelle. Open WebUI zeigt dieselben Namen; eines davon wählen.

2. **Non-stream chat**  
   In Open WebUI neuen Chat, Streaming aus, etwas aus dem Speicher fragen (Name, Wohnort). Eine Antwort kommt. In der Memory-Konsole derselbe Owner + Chat: User + Assistant, Ingest-Job zur User-Nachricht.

3. **Stream chat**  
   Streaming an, weitere Frage senden. Der Text erscheint. Die Konversation in der Konsole bleibt nur User/Assistant, keine `Tool`-Rolle in der Historie.

4. **Title- / Tag-Side-Tasks landen nicht im Speicher**  
   Nach der ersten Nachricht startet Open WebUI meist Title (und oft Tags).  
   - Log `POST /v1/chat/completions`: `passthrough=true`, `task=title_generation` oder `tags_generation`.  
   - Konsole: kein Ingest-Job und kein Speicher mit `### Task:`, `3-5 word title` oder `"tags": ["tag1"`.  
   - Speicher entsteht nur aus einem normalen User-Satz, nicht aus einem Title/Tag-Prompt.

Schnelles curl (Host und Schlüssel einsetzen):

```bash
BASE=http://127.0.0.1:5022
KEY=change-me

curl -sS "$BASE/v1/models" -H "Authorization: Bearer $KEY"

curl -sS "$BASE/v1/chat/completions" \
  -H "Authorization: Bearer $KEY" \
  -H "Content-Type: application/json" \
  -H "X-OpenWebUI-User-Id: matej" \
  -H "X-OpenWebUI-Chat-Id: owui-smoke-1" \
  -d '{"model":"gpt-oss:20b","stream":false,"messages":[{"role":"user","content":"Jak se jmenuju?"}]}'
```

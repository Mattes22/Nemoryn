# Open WebUI → Memory `/v1`

[**English**](openwebui.md) · [Čeština](openwebui.cs.md) · [Deutsch](openwebui.de.md)

Memory exposes an **OpenAI-compatible model endpoint**, not a plugin and not a fork of Open WebUI. The tool loop, candidates, conflicts, and audit live in Memory (agent + console). Open WebUI is only a chat client.

## Connection

In Open WebUI: **Admin → Settings → Connections → OpenAI**.

| Field | Value |
|---|---|
| API Base URL | `http://<host>:5022/v1` (locally `http://127.0.0.1:5022/v1`) |
| API Key | the same string as `OpenAiCompatible:ApiKey` |

When `OpenAiCompatible:ApiKey` is empty, `/v1` does not check a key. Open WebUI still requires the field — anything works. Once you set a key, Open WebUI sends it as `Authorization: Bearer …`.

### Required headers

In **Advanced** settings for the same connection, add:

```json
{
  "X-OpenWebUI-User-Id": "{{USER_ID}}",
  "X-OpenWebUI-Chat-Id": "{{CHAT_ID}}"
}
```

Without these headers Nemoryn does not get a stable user and chat identity. Open WebUI fills `{{USER_ID}}` and `{{CHAT_ID}}` only in a real chat — the Verify Connection button may leave them empty.

Set the Ollama/OpenAI **Base URL** and default chat model in the Nemoryn console (**Runtime**), not in Open WebUI Connections. Clicking a model saves it and survives an API restart. Open WebUI always calls only `http://<host>:5022/v1`. Docker: see [docker.md](docker.md).

`GET /v1/models` returns chat models from the upstream catalogue (no embedding model). The **first** in the list is the default Runtime model. In Open WebUI you can pick another from the list. An unknown `model` in `POST /v1/chat/completions` returns 400.

## Identity

Owner and conversation come from the request, not from the Memory console account.

Owner (first non-empty):

1. `X-OpenWebUI-User-Id`
2. `X-OpenWebUI-User-Email`
3. `X-OpenWebUI-User-Name`
4. `X-User-Id`
5. `metadata.owner_id`
6. `user`
7. otherwise `anonymous`

Conversation (first non-empty):

1. `X-OpenWebUI-Chat-Id`
2. `X-Chat-Id`
3. `metadata.chat_id`
4. `metadata.conversation_id`
5. `conversation_id`
6. otherwise `{ownerId}-default`

A chat id that is not a GUID is upserted as `externalId`. The same Open WebUI chat therefore maps to one Memory conversation.

## What works

- `GET /v1/models`
- `POST /v1/chat/completions` non-stream and stream
- retrieval + agent prompt (`memory.agent.v1.1`)
- ingest of user messages into memory (real chat only; see side-tasks)
- built-in tools on the **Safe** profile (`get_time`, `search_memories`, `web_search`, `web_fetch`) — Nemoryn advertises them to the model; Open WebUI does not see them

On the agent path, the stream sends the **final** assistant text (after the tool loop), not tokens from inside tools.

## What it deliberately does not

- Open WebUI is **not** a plugin host. `/v1` does not accept OWUI Functions/Tools and does not run MCP. Web search runs in Nemoryn (SearXNG), not in Open WebUI.
- Open WebUI does **not** get `NetworkOnce` / untrusted HTTP. The profile is always Safe.
- Candidates, conflicts, pin, forget, and audit are **not** in Open WebUI — only in the Nemoryn console (`http://<host>:5022/`).
- Open WebUI title / tags / follow-up / image-prompt side-tasks are **not memory**. They pass through to the model, with no ingest and no agent turn.
- An empty `OpenAiCompatible:ApiKey` leaves `/v1` open. Set a key on a shared host.

## Smoke checklist

Against an instance that sees the same Postgres as ingest (a local API against a remote DB is not enough).

1. **Models**  
   `GET /v1/models` → 200, `data` is the list of chat models. Open WebUI shows the same names; pick one of them.

2. **Non-stream chat**  
   In Open WebUI start a new chat, turn streaming off, ask something from memory (name, where you live). A reply arrives. In the Memory console, same owner + chat: User + Assistant, ingest job for the user message.

3. **Stream chat**  
   Turn streaming on, send another question. The text renders. The conversation in the console is still only User/Assistant, no `Tool` role in history.

4. **Title / tag side-tasks do not go into memory**  
   After the first message Open WebUI usually runs title (and often tags).  
   - Log `POST /v1/chat/completions`: `passthrough=true`, `task=title_generation` or `tags_generation`.  
   - Console: no ingest job and no memory with `### Task:`, `3-5 word title`, or `"tags": ["tag1"`.  
   - Memory is created only from a normal user sentence, not from a title/tag prompt.

Quick curl (fill in host and key):

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

# Open WebUI → Memory `/v1`

Memory vystavuje **OpenAI-compatible model endpoint**, ne plugin a ne fork Open WebUI. Tool loop, kandidáti, konflikty a audit žijí v Memory (agent + konzole). Open WebUI je jen chat klient.

## Připojení

V Open WebUI: **Admin → Settings → Connections → OpenAI**.

| Pole | Hodnota |
|---|---|
| API Base URL | `http://<host>:5022/v1` (lokálně `http://127.0.0.1:5022/v1`) |
| API Key | stejný řetězec jako `OpenAiCompatible:ApiKey` |

Když je `OpenAiCompatible:ApiKey` prázdný, `/v1` klíč neověřuje. Open WebUI pole stejně vyplnit musí — může tam být cokoliv. Až klíč nastavíš, Open WebUI ho posílá jako `Authorization: Bearer …`.

Ollama/OpenAI **Base URL** a výchozí chat model nastav v Nemoryn konzoli (**Runtime**), ne v Open WebUI Connections. Kliknutí na model se uloží a platí i po restartu API. Open WebUI vždy volá jen `http://<host>:5022/v1`. Docker: viz `docs/docker.md`.

`GET /v1/models` vrací chat modely z upstream katalogu (bez embedding modelu). **První** v seznamu je výchozí model z Runtime. V Open WebUI můžeš vybrat i jiný ze seznamu. Neznámý `model` v `POST /v1/chat/completions` vrátí 400.

## Identita

Owner a konverzace se berou z requestu, ne z účtu Memory konzole.

Owner (první neprázdné):

1. `X-OpenWebUI-User-Id`
2. `X-OpenWebUI-User-Email`
3. `X-OpenWebUI-User-Name`
4. `X-User-Id`
5. `metadata.owner_id`
6. `user`
7. jinak `anonymous`

Konverzace (první neprázdné):

1. `X-OpenWebUI-Chat-Id`
2. `X-Chat-Id`
3. `metadata.chat_id`
4. `metadata.conversation_id`
5. `conversation_id`
6. jinak `{ownerId}-default`

Chat id, které není GUID, se upsertne jako `externalId`. Stejný Open WebUI chat tedy drží jednu Memory konverzaci.

## Co funguje

- `GET /v1/models`
- `POST /v1/chat/completions` non-stream i stream
- retrieval + agent prompt (`memory.agent.v1.1`)
- ingest user zprávy do paměti (jen skutečný chat, viz side-tasky)
- vestavěné tools na profilu **Safe** (`get_time`, `search_memories`)

Stream u agenta pošle **finální** assistant text (po tool loopu), ne tokeny zevnitř nástrojů.

## Co záměrně ne

- Open WebUI **není** plugin host. `/v1` nepřijímá OWUI Functions/Tools a nespouští MCP.
- Open WebUI **nedostane** `NetworkOnce` / Untrusted HTTP. Profil je vždy Safe.
- Kandidáti, konflikty, pin, forget a audit **nejsou** v Open WebUI — jen v Nemoryn konzoli (`http://<host>:5022/`).
- Title / tags / follow-up / image-prompt side-tasky Open WebUI **nejsou paměť**. Jdou passthrough na model, bez ingestu a bez agent turnu.
- Prázdný `OpenAiCompatible:ApiKey` nechá `/v1` otevřené. Na sdíleném hostu klíč nastav.

## Smoke checklist

Proti instanci, která vidí stejnou Postgres jako ingest (lokální API proti vzdálené DB nestačí).

1. **Models**  
   `GET /v1/models` → 200, `data` je seznam chat modelů. Open WebUI ukáže stejná jména; vyber jeden z nich.

2. **Non-stream chat**  
   V Open WebUI nový chat, vypni streaming, zeptej se na něco z paměti (jméno, bydliště). Odpověď přijde. V Memory konzoli stejný owner + chat: User + Assistant, Ingest job k user zprávě.

3. **Stream chat**  
   Zapni streaming, pošli další otázku. Text se vykreslí. Konverzace v konzoli pořád jen User/Assistant, žádný `Tool` role v historii.

4. **Title / tag side-tasky nejdou do paměti**  
   Po první zprávě Open WebUI obvykle spustí title (a často tags).  
   - Log `POST /v1/chat/completions`: `passthrough=true`, `task=title_generation` nebo `tags_generation`.  
   - Konzole: žádný ingest job a žádná paměť s `### Task:`, `3-5 word title` nebo `"tags": ["tag1"`.  
   - Paměť vzniká jen z běžné user věty, ne z title/tag promptu.

Rychlý curl (doplň host a klíč):

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

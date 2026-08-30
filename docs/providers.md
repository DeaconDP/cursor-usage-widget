# Provider setup

Configure sources from the **gear** in the widget header. Toggle **Cursor**, **Codex / Claude.ai / Gemini App**, and **API** independently per provider.

| Provider | Cursor usage (automatic) | Subscription / app limits | Platform API (optional) |
|----------|--------------------------|---------------------------|-------------------------|
| **Cursor** | Sign in to Cursor IDE on this machine — no API key needed | **Grok Bot** allowance (Ultra / Premium) via the same login — shown with Cursor Models & Cursor API | — |
| **OpenAI** | Aggregated from your Cursor plan | **Codex** (ChatGPT Plus/Pro 5h + weekly + ChatGPT credits) via `~/.codex/auth.json` or session cookie | **OpenAI API** prepaid credit balance when available; otherwise Admin key (`api.usage.read`) + monthly budget |
| **Claude** | — | **Claude.ai** plan windows | **Claude API** Console Admin key + budget |
| **Gemini** | Aggregated from your Cursor plan | **Gemini App** limits (5h + weekly) via Antigravity IDE or Gemini CLI (`gemini login` → `~/.gemini/oauth_creds.json`) | Not metered yet |
| **fal.ai** | — | — | Prepaid **credit balance remaining** via Admin API key |
| **xAI** | — | — | Prepaid **credit balance remaining** via Management API key + team ID |

- **Easy setup** (per provider section) turns on subscription-limit bars, checks local auth, runs the same connection tests as **Test**, and opens login pages or `codex login` when manual steps are still needed.
- **Spend details** shows remaining quota (Cursor) or dollar/token breakdown (API).
- Use **Test** buttons to verify API keys without waiting for the 5-minute refresh.
- API keys are stored encrypted under the settings folder (`credentials/`). They are never written to `settings.json` or committed to Git.

## Grok Bot

Included with **Cursor Ultra** / eligible **Premium** team seats (and SuperGrok Heavy). Uses the same Cursor IDE login as the Cursor bar — no separate API key. Shown in Settings under **Cursor → Grok Bot**, and on the overlay as a third row beside **Cursor Models** and **Cursor API**. Usage percent and reset come from Cursor’s undocumented `GetSandUsageStatus` API (`api2.cursor.sh`). This bucket is separate from Cursor Auto/API monthly pools. Endpoint may change without notice.

## OpenAI (Codex / ChatGPT)

If you use the [Codex CLI](https://developers.openai.com/codex), run `codex login` once — the widget reads `~/.codex/auth.json` automatically and shows the same 5-hour and weekly limits as ChatGPT's Usage & billing page (including ChatGPT plan credits). If auth is stored in the OS keyring instead, paste a ChatGPT session cookie from DevTools as a fallback. This uses an undocumented ChatGPT endpoint and may change without notice. Separate from OpenAI API credits.

## OpenAI API (optional)

Tries prepaid credit balance via an undocumented `credit_grants` endpoint (best-effort; may require a browser/session key and can break). Falls back to an [organization admin key](https://platform.openai.com) with `api.usage.read` for org spend against a monthly budget. Empty API balance shows as 100% used. This is separate from ChatGPT/Codex subscription limits.

## Claude

**Claude.ai** plan rate limits (OAuth / Claude Code login) are separate from **Claude API** Console spend. When **Usage credits** (extra usage) are enabled on your Pro or Max plan, Claude also enforces a **monthly spend limit** for pay-as-you-go usage after plan windows are exhausted. The fuel gauge reads this from the same Claude usage API as the 5-hour and weekly bars (`extra_usage` inline, plus `overage_spend_limit` when signed in via session cookie). Enable **Claude.ai** limits in Settings and sign in with Claude (or run `claude login`) to see all three rows: 5h, weekly, and monthly spend.

## Gemini App (limits)

Sign in to **Antigravity IDE** on this machine, or run **`gemini login`** with the [Gemini CLI](https://github.com/google-gemini/gemini-cli) (`npm i -g @google/gemini-cli`). Connect tries Gemini CLI first when installed, otherwise launches Antigravity IDE. The widget reads your local OAuth session and shows grouped **Gemini Models** and **Claude and GPT models** 5-hour and weekly limits (Antigravity), or per-model Gemini CLI quotas as a fallback. No API keys or project IDs needed. Gemini Developer API billing is not metered yet. Uses undocumented Google Cloud Code endpoints and may change without notice.

## fal.ai (credits)

Paste an **Admin** API key from [fal.ai/dashboard/keys](https://fal.ai/dashboard/keys). Choose **ADMIN** scope (not API) and confirm the correct **personal or team** account is selected in the dashboard before creating the key. API keys do not expire on their own — create a new one only if the old key was deleted or revoked. **Prepaid credits** can expire (see [fal billing dashboard](https://fal.ai/dashboard/billing)); that is separate from key validity.

The widget calls `GET /v1/account/billing?expand=credits` and shows **remaining balance** (fal does not expose total purchased / lifetime used). The bar tracks an observed **prepaid baseline**: first successful balance seeds the tank size; when remaining credit **increases** (a top-up), the baseline becomes the new remaining total (e.g. `$1` left then load `$25` → `$26` baseline). Percent used is `(baseline − remaining) / baseline`. Quota alerts fire when that percent reaches your unused-quota threshold.

**Troubleshooting:** `401` usually means an invalid or revoked key — paste a fresh Admin key. `403` usually means API scope or the wrong account/team — recreate the key under the account whose balance you want.

## xAI (credits)

Paste a **Management API key** from [console.x.ai](https://console.x.ai) → **Settings → Management Keys**. This is a different key from **API Keys** (inference / Grok chat). Using an API key against the Management API returns **invalid bearer token**. This meter is also separate from **Grok Bot** (Cursor Ultra weekly allowance).

For **team-scoped** Management keys, the widget resolves the **team UUID** automatically via `GET /auth/management-keys/validation`. You only need to paste a team UUID when the key is **organization-scoped**, or when auto-detect fails. Do **not** paste the `default` slug from the console URL — that is not a team id (billing expects a UUID like `65c1e471-…`).

The widget calls `GET .../prepaid/balance` for the prepaid **tank** (`total.val`, inverted USD cents — a `$10` top-up appears as `"-1000"`) and `GET .../postpaid/invoice/preview` for live **usage** (`coreInvoice.prepaidCreditsUsed.val`). **Remaining** is `tank − used` (matching Console “Credits remaining”), and the bar is `used / tank`. Detail text shows `$X.XX left · $Y.YY used of $Z.ZZ` when both values are available. If the invoice preview is unavailable, the widget falls back to the posted prepaid ledger total as remaining (which can still look high mid-cycle until spend posts).

**Troubleshooting:** **invalid bearer token** / `401` means you pasted an API (inference) key — create a **Management** key under Settings → Management Keys. `403` usually means missing billing ACL. Errors mentioning **uuid** / team not found usually mean the team field was empty, set to `default`, or belongs to a different team — leave Team UUID blank to auto-detect, or paste the UUID from team settings.

## Settings location

| Platform | Path |
|----------|------|
| Windows | `%LOCALAPPDATA%\deez-fuel-gauge\settings.json` |
| macOS | `~/Library/Application Support/deez-fuel-gauge/settings.json` |

Encrypted API keys: `credentials/` in the same folder.

## How it works

1. Reads `cursorAuth/accessToken` from Cursor's local SQLite database:
   - **Windows:** `%APPDATA%\Cursor\User\globalStorage\state.vscdb`
   - **macOS:** `~/Library/Application Support/Cursor/User/globalStorage/state.vscdb`
2. Calls Cursor's unofficial `GetCurrentPeriodUsage` API (Pro/Ultra/Team plans), or falls back to `GET /auth/usage` for legacy Enterprise request-based quotas.
3. Optionally fetches **Grok Bot** weekly usage from `GetSandUsageStatus` with the same Cursor token.
4. Optionally enriches OpenAI / Gemini bars from Cursor's aggregated usage events.
5. Optionally fetches **Codex / ChatGPT** 5-hour and weekly limits from `chatgpt.com` when Codex auth or a session cookie is available.
6. Optionally fetches **Gemini App** grouped Gemini and third-party 5-hour and weekly limits from Google Cloud Code when Antigravity IDE or Gemini CLI is signed in locally.
7. Optionally fetches **OpenAI API** prepaid credits (`credit_grants`, best-effort) or Admin Platform spend vs budget when configured in settings.
8. Optionally fetches **fal.ai** prepaid credit balance via the official Platform billing API when an Admin key is saved.
9. Optionally fetches **xAI** prepaid credit balance via the Management API when a Management key and team ID are saved.

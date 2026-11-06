# Connect external AI clients to MoneyBrain

MoneyBrain can act as a first-party Model Context Protocol (MCP) server. It
provides tools for the signed-in user's finances; it does not call an AI model.
MCP is **disabled by default**, and writes require a separate instance setting,
OAuth write consent, and confirmation on each mutation.

Connecting an external client permits it to read the financial data you authorize.
That client may transmit tool results to its AI provider. Review that provider's
privacy policy and the client's approval settings before connecting. MoneyBrain
does not add telemetry or send data to a provider on your behalf.

## Transport and authentication

- Endpoint: `https://your-moneybrain.example/mcp`.
- Transport: stateless Streamable HTTP. Each request carries its own access token;
  no long-lived MCP session can continue after its grant is revoked.
- Authentication: OAuth authorization code with PKCE S256 and existing MoneyBrain
  account sign-in. Public clients do not need a client secret.
- Scopes: `moneybrain.read` and, optionally, `moneybrain.write`. Write scope also
  requires read scope, administrator approval for the client, and enabled writes.
- Access tokens expire after 15 minutes. No refresh tokens are issued in this
  implementation; reconnect/authorize again when a token expires.
- Client registration is administrative and static. Dynamic registration, client
  credentials, password grants, personal access tokens, and anonymous access are
  not supported.

The server uses the official .NET MCP SDK 2.3.1 and OpenIddict 7.8.0. Protocol
version negotiation is handled by the SDK rather than a custom JSON-RPC
implementation. See the integration tests for the exercised protocol version
and authorization flows.

The configured public origin is the OAuth issuer. The exact MCP URL, including
`/mcp`, is the resource/audience. Clients must include that `resource` in both the
authorization and token requests.

Discovery is available at the issuer's OAuth discovery endpoint and
`/.well-known/oauth-protected-resource/mcp`. An unauthenticated MCP request returns
a `401` challenge pointing to the protected-resource metadata. Do not copy
MoneyBrain browser cookies or passwords into your AI client.

## Configure the instance

Set the following using deployment configuration or environment variables. This
example registers MCP Inspector's browser callback; copy the **actual** callback
from your chosen client rather than registering wildcards.

```json
{
  "Mcp": {
    "Enabled": true,
    "EnableWrites": false,
    "PublicUrl": "https://your-moneybrain.example",
    "Clients": [
      {
        "ClientId": "moneybrain-inspector",
        "DisplayName": "MCP Inspector",
        "RedirectUris": [
          "http://127.0.0.1:6274/oauth/callback",
          "http://127.0.0.1:6276/oauth/callback"
        ],
        "AllowedOrigins": ["http://127.0.0.1:6274"],
        "AllowWrites": false
      }
    ]
  }
}
```

Equivalent environment keys include `Mcp__Enabled`, `Mcp__PublicUrl`,
`Mcp__Clients__0__ClientId`, and `Mcp__Clients__0__RedirectUris__0`.
Restart after changing client registrations or capabilities.

Redirect URIs must match exactly. HTTPS callbacks and HTTP loopback callbacks
are supported. `localhost` and `127.0.0.1` are different redirect URIs.
`AllowedOrigins` is an exact allowlist for browser clients; native clients
without an Origin header do not need an entry. Never use `*`.

### TLS and production keys

Serve MoneyBrain through HTTPS with a stable public origin. `PublicUrl` must be
an HTTPS origin without a path, query, credentials, or fragment. Use a valid
certificate trusted by the external client.

Production requires signing and encryption PFX files with private keys:

```text
Mcp__SigningCertificatePath=/run/secrets/mcp-signing.pfx
Mcp__SigningCertificatePassword=<from your secret provider>
Mcp__EncryptionCertificatePath=/run/secrets/mcp-encryption.pfx
Mcp__EncryptionCertificatePassword=<from your secret provider>
```

The Linux paths above are container mount paths; use absolute local paths for
Windows hosting. Mount these files read-only, restrict filesystem permissions,
and keep passwords out of source control and chat. Development uses
OpenIddict development certificates; they are not production credentials.
Back up the keys securely and preserve them across restarts. Certificate
replacement invalidates tokens protected by old keys; schedule reauthorization
and test the change before removing the old material.

When terminating TLS at a proxy, configure ASP.NET Core forwarded headers to
trust **only your known proxy/network**, before authentication. The server must
observe the original HTTPS scheme. Do not expose a plaintext MCP endpoint or
trust arbitrary forwarded headers from the internet. This does not resolve the
broader deployment hardening tracked in #8 and #9.

The existing SQL Server database stores OAuth clients, grants, codes, and token
entries through OpenIddict. Apply the included EF migration before serving MCP
requests; normal MoneyBrain startup applies migrations. Back up the database
before upgrading. Disabling MCP does not delete grants or financial data.
If migrations are applied externally with `Database:ApplyMigrations=false`,
apply them before startup: configured MCP clients are still synchronized at
startup independently of that switch. Synchronization only manages applications
marked as MoneyBrain MCP clients; an identifier already used by another OAuth
application is rejected rather than overwritten.

## Connect a client

### MCP Inspector

Use the [official Inspector](https://modelcontextprotocol.io/docs/tools/inspector)
with a runtime supported by that Inspector version:

```text
npx @modelcontextprotocol/inspector
```

Select **Streamable HTTP**, enter your MCP URL, and configure the pre-registered
client ID `moneybrain-inspector` without a client secret. Copy the Inspector's
exact redirect URI into the server registration if it differs from the example.
Request `moneybrain.read`, authorize in the browser, sign into MoneyBrain, and
review the consent screen.

For Inspector versions supporting its OAuth CLI options, the corresponding
static-client connection is:

```text
npx @modelcontextprotocol/inspector --cli https://your-moneybrain.example/mcp --transport http --client-id moneybrain-inspector --method tools/list
```

The browser and CLI callbacks can differ (typically ports 6274 and 6276).
See the Inspector's [authorization documentation](https://modelcontextprotocol.io/docs/tools/inspector/authorization).
Never disable Inspector authentication or paste a token into a committed file.

### VS Code

Register a public client such as `moneybrain-vscode` with the exact callback URI
shown by your installed VS Code version's OAuth flow. Then add this entry to
your personal VS Code MCP configuration (not this repository's developer-tool
configuration):

```json
{
  "servers": {
    "moneybrain": {
      "type": "http",
      "url": "https://your-moneybrain.example/mcp",
      "oauth": {
        "clientId": "moneybrain-vscode"
      }
    }
  }
}
```

Use **MCP: Add Server** or the MCP customization UI, start the server, and follow
the browser authorization flow. See the [VS Code configuration reference](https://code.visualstudio.com/docs/agents/reference/mcp-configuration).
Do not add an Authorization header containing a permanent token.

Clients must support static client IDs, authorization code with PKCE, resource
indicators, and Streamable HTTP. A client requiring dynamic client registration
only is not supported. Client UI names and callbacks vary by release.

### Check the first operation

After authorization, list tools/resources/prompts and call an accounts-list tool.
Confirm the returned accounts belong to the signed-in MoneyBrain user. Ask the
client to summarize a selected period without making any changes. Merely seeing
a plausible chat response is not evidence that the server was called: inspect
the actual tool invocation and result.

To enable writes, set both `Mcp:EnableWrites` and that client's `AllowWrites` to
`true`, restart, and authorize again requesting both scopes. Review each
mutation's target and arguments before approval. Read-only tokens cannot mutate
data even when the instance allows writes. Write tool names may still appear in
discovery; authorization is enforced when they are called.

## Available capabilities and limits

Tools cover accounts/balances, categories/groups, payees, budgets, transactions,
splits, transfers, recurring transactions, reconciliation, adjustments, CSV
workflows, reports, saved Insight Explorer queries, and safe user preferences.
Use tool discovery for the exact names, parameters, and supported actions.
Resources describe MoneyBrain's finance rules; prompts help prepare summaries,
budget reviews, and reconciliation without executing writes.

No tool exposes Identity credentials, OAuth secrets, licensing secrets, payment
provider configuration, infrastructure settings, arbitrary SQL, or arbitrary
filesystem/network access. Reconciled data remains immutable. Transfer and split
rules are the same as the application, not AI-generated approximations.
User-entered names, notes, tags, and memos are untrusted data, never instructions.

Reads are bounded and paginated. Reports accept at most 366 days and 120
snapshots; large reports are rejected rather than silently truncated. Insight
Explorer properties are allowlisted; aggregate queries are limited to 10,000
source records and 200 result groups. For larger datasets use narrower ranges
or the paginated transaction tools. CSV imports must be previewed before
committing: a preview accepts at most 512 KiB and 100 data rows and returns an
owner-bound, single-use token valid for 10 minutes. A restart loses pending
previews; multi-instance deployments need the preview and commit routed to the
same instance. Quoted cells containing embedded newlines are not supported by
the current CSV parser. MCP requests and tool results are limited to 1 MiB;
report CSV exports have a tighter 512 KiB limit.
Bulk/destructive operations require review and explicit confirmation.
An error response is not permission to retry a non-idempotent write blindly:
check the resulting records first.

## Revoke or disable access

Open **Account > Manage > Connected clients**
(`/Account/Manage/ConnectedClients`) in MoneyBrain. Review the client and granted
scopes, then revoke the relevant grant. Both its outstanding tokens/codes and
future calls are rejected; the user's ordinary MoneyBrain sign-in is unchanged.
Use **Revoke all** to remove all your MCP grants, including any not shown in the
bounded list. Repeated consent for the same client and exact scopes reuses its
valid grant; consent for different scopes remains separate. Authorizing after
revocation requires consent and creates a new grant.

To stop all MCP access, set `Mcp:Enabled=false` and restart. To stop only writes,
set `Mcp:EnableWrites=false` and restart. Removing a registered client, changing its redirect URIs, or changing its
permissions revokes the existing grants during startup synchronization as well
as controlling future authorizations.

## Troubleshooting

| Symptom | Check |
| --- | --- |
| MCP endpoint unavailable | MCP is disabled by default; enable it and restart. |
| Startup validation failure | Public URL must be an HTTPS origin; production needs valid PFX files/private keys and passwords. |
| `401` on a tool request | Complete OAuth, check token expiry/resource/issuer, or reauthorize after revocation. |
| Read works, write fails | Check both instance/client write switches, granted write scope, and per-operation confirmation. |
| Redirect rejected | Register the exact URI, including host, port, path, and scheme. |
| Origin rejected | Add only the exact trusted browser origin to that client's allowlist. |
| HTTPS required behind proxy | Configure trusted forwarded headers before authentication; do not bypass TLS validation. |
| Import/report too large | Split inputs or narrow the date range; follow discovered tool limits. |
| `429` | Reduce request frequency and retry later; do not blindly retry financial writes. |
| No insights returned | Check the user's tip/insight visibility preferences. |

Never include tokens, authorization codes, financial CSV files, or certificate
passwords in logs, screenshots, or issue reports.

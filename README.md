# FleetComb Agent

FleetComb Agent is the cross-platform service installed on a managed instrument or computer. It
represents one physical FleetComb Asset, maintains the authenticated cloud connection, exposes a
generic local integration API to customer software, reports installed Applications, and
orchestrates locally approved software updates.

The initial supported deployment targets are Windows x64 and Debian/Ubuntu Linux x64. Shared code
also detects macOS, but macOS packaging and installation are not currently supported.

This README is the authoritative development handoff for the Agent repository. It distinguishes
working behavior from prototypes and pending production work. The FleetComb repository also has
the broader protocol design in `docs/agent-protocol.md` and the cross-repository feature ledger in
`docs/Features/Agent-Features.MD`.

## Feature status

### Implemented

- CLI and LAN-accessible web enrollment using a FleetComb-generated, single-use, 15-minute code.
- A locally generated ECDSA P-256 installation identity. The private key remains on the instrument.
- Signed cloud requests containing installation ID, timestamp, nonce, body hash, and signature.
- FleetComb rejection of stale requests, replayed nonces, bad signatures, and revoked identities.
- Replacement enrollment and authenticated local reset with recoverable timestamped backups.
- Windows Service and systemd hosting from one .NET codebase.
- Periodic heartbeat with hostname, OS/version, architecture, Agent/protocol version, uptime,
  Application inventory, and queued producer messages.
- Desired Product, active Software Platform, Application, and matching latest Published Release
  synchronization from FleetComb.
- Capability-negotiated, versioned Asset authorization rosters with a 30-day lease, certification-
  bounded `notAfter`, durable restart recovery, and local expiry filtering.
- Durable Agent-installed and externally reported Application inventory, separate from software
  Entitlements.
- LAN-accessible Razor Pages UI with local administrator setup/login, connection and Product status,
  desired Applications, authorized users, available versions, update actions/progress, optional adapter status,
  enrollment reset, and SignalR refresh without reloading.
- Bootstrap local API credential plus separately issued adapter credentials. Only adapter-token
  SHA-256 hashes are persisted; returned plaintext tokens are shown once.
- Per-adapter scopes, registration, heartbeat, configuration acknowledgement, listing, and
  revocation.
- Versioned REST/JSON customer API for desired state, inventory, update control, health, events,
  logs, protocol limits, and diagnostics.
- Authenticated SignalR stream for connection, configuration, inventory, adapter, and update change
  notifications.
- Scoped `GET /local/v1/authorized-users` access and `authorized-users` invalidation events without
  passwords, email, certification details, or denial reasons.
- Bounded 64 KiB telemetry payloads, structured secret redaction for logs, a bounded 10,000-message
  durable offline queue, and local queue diagnostics.
- Idempotent producer-message delivery in signed heartbeats. FleetComb acknowledges exact message
  IDs before the Agent marks them delivered. FleetComb stores provenance and JSON under tenant RLS.
- Generic opaque file uploads from allowlisted local roots with SHA-256, 4 MiB resumable chunks,
  durable progress, automatic retry, cancellation, adapter isolation, and server verification.
- Manual update discovery and execution request, streamed artifact download, declared length and
  SHA-256 verification, extension/type validation, and EXE/DEB/PKG/ZIP binary-header validation.
- Durable bounded update-attempt history, live progress, concurrent-run prevention, explicit restart
  recovery, successful installed-version tracking, and customer-adapter installation handoff.
- Clean Architecture projects, MediatR CQRS features, FluentValidation pipeline, thin controllers,
  infrastructure implementations behind Application abstractions, and dependency tests.
- A System Manager simulator demonstrating scoped registration, heartbeat, desired state, external
  version reporting, update requests, adapter handoff, and status polling.

### Implemented as a prototype, not production-ready

- Linux `.deb` execution currently invokes `dpkg --install` directly.
- Windows `.exe` execution currently starts the executable without a publisher-defined silent
  installation contract.
- Release signatures are opaque stored text. They are required for publication but are not yet
  verified against a trusted publisher key.
- The development server defaults to plaintext `http://0.0.0.0:5137` so a headless instrument can
  be configured from a laptop. Production deployments must provision HTTPS and network controls.

Do not run an untrusted `.deb` or `.exe` through the prototype installer. Use an adapter-handled
`.zip` for safe workflow testing.

### Pending

The following five areas remain before the Agent can be considered production-ready:

1. **Trusted release signatures.** Define a structured manifest containing version, target OS and
   architecture, package type, checksum, and installation instructions. FleetComb must sign it and
   the Agent must verify it against a trusted publisher key before executing an artifact. Include
   tenant/publisher trust roots and key rotation/revocation.
2. **Safe `.deb` and `.exe` installation.** Replace direct package execution with a narrowly
   privileged, allowlisted installer helper. Support publisher-approved silent arguments,
   administrator/root permissions, timeouts, captured and sanitized output, reboot requirements,
   clear failure and `RecoveryRequired` states, and rollback where the package supports it.
3. **Agent self-update.** Add a separate controlled and recoverable mechanism for updating the Agent
   itself. Do not process an Agent update as an ordinary customer Application update.
4. **Production deployment security.** Finish HTTPS provisioning for the LAN UI/API, OS-backed key
   storage, Agent and adapter credential rotation, Windows directory ACLs, Linux service
   permissions, log rotation, signed installers, and documented upgrade/uninstall procedures.
5. **Final end-to-end validation.** Test enrollment, reconnect, offline telemetry, generic uploads,
   Application installation, restart recovery, cancellation, credential revocation, rollback, and
   Agent service upgrades on real Windows x64 and Debian/Ubuntu Linux x64 systems.

Additional pending FleetComb/Agent capabilities:

- FleetComb/local-UI browsing, download/reassembly, retention policies, and format-specific
  processing for uploaded files. Metadata and private opaque chunks are stored now.
- FleetComb and local-UI browsing, retention controls, and search for ingested health/events/logs.
  Durable ingestion and forwarding are implemented; those read surfaces are not.
- Rich producer-originated messages on the event stream. It currently announces Agent state changes,
  not the contents of producer telemetry.
- Resumable partial update downloads, distribution/version/prerequisite targeting, malware scanning,
  diagnostic support bundles, and broader integration/load/security tests.
- macOS packages and offline enrollment.

## Architecture

```text
FleetComb.Agent.Domain
        ↑
FleetComb.Agent.Application
        ↑
Infrastructure.Cloud   Infrastructure.Persistence   Infrastructure.Updates
        ↑                         ↑                         ↑
                     FleetComb.Agent.Api
                              ↓
                     FleetComb.Agent.Ui
```

Dependency arrows point inward:

- `FleetComb.Agent.Domain` contains immutable state contracts for registration, desired software,
  inventory, update attempts, adapter identities, and producer messages. It has no Agent-project
  dependencies.
- `FleetComb.Agent.Application` owns use cases. Features are grouped by area and then by
  `Commands`/`Queries`; each action has one request, validator, and handler per file. Interfaces for
  cloud, persistence, platform, notification, and installation behavior live in `Abstractions`.
- `FleetComb.Agent.Infrastructure.Cloud` implements enrollment, signed heartbeat, artifact download,
  and platform/key operations.
- `FleetComb.Agent.Infrastructure.Persistence` stores restricted local JSON state and implements
  reset/backup behavior.
- `FleetComb.Agent.Infrastructure.Updates` validates artifacts and implements standard package
  execution prototypes.
- `FleetComb.Agent.Api` is the executable/composition root. Controllers are transport adapters;
  they send CQRS messages rather than containing business logic. It also hosts authentication,
  rate limiting, the synchronization worker, and SignalR.
- `FleetComb.Agent.Ui` is a Razor class library with bundled offline JavaScript/CSS. It does not
  reference Infrastructure.
- `FleetComb.SystemManagerSimulator` is a customer-adapter example, not production Agent code.

Architecture tests prevent Domain/Application/UI dependency inversions.

## Core implementation

### Enrollment and machine authentication

FleetComb creates a random one-time enrollment code and stores only its hash. On claim, the Agent
generates an ECDSA P-256 key pair, sends only the public key and platform claims, and saves the
returned tenant/Asset/installation identity locally. A successful replacement claim invalidates the
old installation identity.

For every protected cloud request the Agent serializes the exact body, calculates SHA-256, and signs
a canonical payload containing installation ID, Unix timestamp, nonce, and body hash. FleetComb
loads the enrolled public key, checks the five-minute clock tolerance, verifies the signature, and
persists the nonce under a unique key to prevent replay.

### Synchronization and desired/reported state

`SynchronizationWorker` starts the Application synchronization command and retries failures. A
heartbeat sends platform status, Application observations, and up to 100 pending producer messages.
FleetComb resolves the Asset's Product, compatible active Platforms and Applications, and the latest
Published Release matching OS and architecture. The Agent persists the response as desired state.

The heartbeat advertises `authorized-users.v1`. FleetComb then includes a nullable schema-versioned
authorization section with a monotonic policy revision, Asset serial number, and authorized Memberships.
The Agent persists it in `desired-state.json`; a 30-day lease and earlier certification boundaries
limit each user's `notAfter`. Older desired-state files without this optional section remain readable.

Reported installations are observations, not entitlements. `AgentInstalled` means the Agent or an
adapter successfully completed a known Release. `ExternallyReported` means customer software stated
what is present. Both are synchronized to FleetComb; arbitrary real-world reported versions are
preserved when the catalog does not know them.

### Durable telemetry delivery

Health, event, and log submissions require a dedicated adapter token with `telemetry.write`; the
bootstrap token cannot impersonate an adapter submission. Each accepted record receives Agent,
adapter, sequence, schema, severity, payload, and timestamps, then enters
`producer-messages.json`. The queue accepts at most 10,000 pending records and the diagnostic API
exposes count, bytes, and oldest age.

Log redaction parses JSON and recursively replaces values whose property names contain `password`,
`token`, `secret`, or `authorization`. This preserves valid JSON; it is a safety net, not permission
to submit credentials.

The next signed heartbeat batches at most 100 pending messages. FleetComb inserts messages using
stable IDs and installation/adapter sequences, returns the accepted IDs, and only those records are
marked delivered locally. A failed or lost response therefore retries idempotently. Delivered local
records are retained for seven days by the current file-store cleanup behavior.

### Software updates

Only Applications own Releases. Modules are licensed functionality within an Application and are
not independently updated executables.

The update state flow is:

```text
Idle → Downloading → Verified → Installing → Completed
                         └────→ AwaitingAdapter → Completed
                 any active built-in state ─────→ Failed
```

The Agent permits one update at a time. It streams the artifact to the data directory, verifies
length and SHA-256, then independently checks declared package type, extension, and recognizable
binary header. ZIP/PKG currently enter `AwaitingAdapter`; the adapter performs vendor-specific
service/database/FPGA behavior and reports completion. A successful result records the known
Release/version as `AgentInstalled`.

Every transition is saved in the current status and bounded 100-attempt history. On startup, an
interrupted `Downloading`, `Verified`, or `Installing` attempt becomes explicitly `Failed` instead
of appearing active forever. `AwaitingAdapter` survives restart so an adapter may complete it.

### Generic file uploads

An adapter with `uploads.write` submits a local path, category (`scan`, `project`, `diagnostic`, or
`other`), versioned schema, content type, capture time, and arbitrary JSON metadata. The Agent
accepts files only below a configured root. With no explicit configuration, only
`DATA_DIRECTORY/upload-inbox` is allowed. It rejects symbolic links so an allowed path cannot escape
through a link.

Creation inspects the file, enforces the 100 GiB limit, and calculates SHA-256 before returning
`202 Accepted`. A durable background worker creates a stable FleetComb session and transfers 4 MiB
chunks. Recreating that session returns existing chunk indexes, so restart or network recovery
sends only missing chunks. The Agent verifies the source has not changed before resuming.

FleetComb authenticates every create/chunk/complete/cancel call with the signed-request protocol.
Chunks are private objects and metadata is tenant-isolated by PostgreSQL RLS. On completion,
FleetComb reads chunks in order and recomputes total length and SHA-256. It does not interpret the
file format yet. Transient failures retry after 15 seconds; cancellation is durable. If the source
file changes, create a new upload session so its identity and checksum describe the new bytes.

### Local persistence and reset

The data directory contains restricted JSON state such as:

- Agent registration/private key and bootstrap token;
- desired state and synchronization status;
- Application observations;
- current update and update-attempt history;
- adapter identities and token hashes;
- queued/delivered producer messages;
- durable file-upload sessions and progress;
- local UI administrator credentials.

Unix files are restricted to the owning account. Production Windows installation must ACL the data
directory to the Agent service identity. Reset moves current state to
`reset-backups/TIMESTAMP` before clearing enrollment, making accidental local reset recoverable.

## Customer adapter API

The development base URL is `http://127.0.0.1:5137` on the instrument or
`http://INSTRUMENT-IP:5137` from an authorized LAN computer. Production must use HTTPS.

### Credential lifecycle

Enrollment creates an installation-wide bootstrap token. Retrieve it using the same data directory
as the service:

```bash
dotnet run --project src/FleetComb.Agent.Api -- local-token
```

Send tokens as:

```http
Authorization: Bearer TOKEN
```

Use the bootstrap token only to register/list/revoke adapters. `POST /local/v1/adapter/register`
returns a dedicated plaintext adapter token once; securely store it and use it for later calls. The
Agent stores only its SHA-256 hash. A missing credential produces `401`, a valid credential lacking
the required scope produces `403`, and revoked adapter credentials produce `401`.

Available scopes:

| Scope | Allows |
| --- | --- |
| `status.read` | Agent status, protocol/diagnostics, adapter heartbeat |
| `configuration.read` | Desired state and configuration acknowledgement |
| `inventory.read` | Installed Application inventory |
| `inventory.write` | External installed-version reports |
| `updates.read` | Current update and attempt history |
| `updates.install` | Starting or completing an update |
| `telemetry.write` | Health, event, and log submission |
| `events.subscribe` | Adapter connection to the SignalR status hub |
| `uploads.write` | Create, list, inspect, cancel, and retry the adapter's file uploads |
| `access.read` | Read the Asset's currently authorized downstream users |

### REST endpoints

All request/response property names use JSON camel case.

| Method and path | Credential/scope | Purpose |
| --- | --- | --- |
| `POST /local/v1/adapter/register` | Bootstrap | Register name, version, capabilities and scopes; returns ID and one-time token |
| `GET /local/v1/adapter` | Bootstrap | List adapters without plaintext tokens |
| `DELETE /local/v1/adapter/{adapterId}` | Bootstrap | Revoke one adapter token |
| `POST /local/v1/adapter/heartbeat` | `status.read` | Refresh adapter liveness |
| `POST /local/v1/adapter/configuration/acknowledge` | `configuration.read` | Acknowledge the exact current desired-state revision |
| `GET /local/v1/status` | `status.read` | Connection, synchronization, adapter and update summary |
| `GET /local/v1/protocol` | `status.read` | Protocol version, scopes and limits |
| `GET /local/v1/diagnostics` | `status.read` | Protocol plus adapter counts and durable queue pressure |
| `GET /local/v1/desired-state` | `configuration.read` | Product, Platforms, Applications, matching Releases and revision |
| `GET /local/v1/applications` | `inventory.read` | Durable installed/reported Application observations |
| `GET /local/v1/authorized-users` | `access.read` | Effective unexpired Asset authorization roster |
| `POST /local/v1/applications/report` | `inventory.write` | Report a version installed outside the Agent |
| `GET /local/v1/updates/current` | `updates.read` | Current update state/progress |
| `GET /local/v1/updates/history` | `updates.read` | Up to 100 durable update attempts |
| `POST /local/v1/applications/{applicationId}/install` | `updates.install` | Download and install/handoff the latest matching Release |
| `POST /local/v1/applications/{applicationId}/install-completion` | `updates.install` | Complete an `AwaitingAdapter` installation |
| `POST /local/v1/health` | `telemetry.write` | Queue structured health payload |
| `POST /local/v1/events` | `telemetry.write` | Queue structured operational event/error |
| `POST /local/v1/logs` | `telemetry.write` | Redact and queue structured diagnostic log |
| `GET /local/v1/uploads/configuration` | `uploads.write` | Allowed roots, inbox, categories and limits |
| `POST /local/v1/uploads` | `uploads.write` | Inspect an allowlisted file and queue an upload |
| `GET /local/v1/uploads` | `uploads.write` | List this adapter's durable upload sessions |
| `GET /local/v1/uploads/{uploadId}` | `uploads.write` | Read state, progress, and error |
| `DELETE /local/v1/uploads/{uploadId}` | `uploads.write` | Request durable cancellation |
| `POST /local/v1/uploads/{uploadId}/retry` | `uploads.write` | Retry a failed session |

Registration request:

```json
{
  "name": "Customer System Manager",
  "version": "2.4.0",
  "capabilities": ["application-inventory", "adapter-installation"],
  "scopes": ["status.read", "configuration.read", "inventory.write", "updates.install"]
}
```

External inventory report:

```json
{
  "applicationId": "00000000-0000-0000-0000-000000000000",
  "softwareReleaseId": null,
  "version": "1.2.3"
}
```

Health/event/log submissions share this envelope. Allowed severities are `Healthy`, `Degraded`, or
`Unhealthy` for health; `Info`, `Warning`, `Error`, or `Critical` for events; and `Trace`, `Debug`,
`Info`, `Warning`, `Error`, or `Critical` for logs.

```json
{
  "schema": "com.customer.instrument-health/1.0",
  "severity": "Healthy",
  "payload": { "acquisitionService": "Running", "temperatureC": 41.2 }
}
```

Adapter installation completion:

```json
{ "succeeded": true, "message": "Installed and verified by Customer System Manager." }
```

File upload creation:

```json
{
  "localPath": "/var/lib/fleetcomb-agent/upload-inbox/example.scan",
  "category": "scan",
  "schema": "com.customer.scan-file/1.0",
  "contentType": "application/octet-stream",
  "metadata": { "inspectionId": "INS-10042" },
  "capturedAt": "2026-08-01T18:00:00Z"
}
```

### SignalR events

The hub route is:

```text
/hubs/status
```

The Razor UI authenticates with its administrator cookie. An adapter authenticates with its bearer
token as SignalR's `access_token` and must have `events.subscribe`. Subscribe to the
`StatusChanged` event. Its current payload is one change-category string:

- `synchronization`
- `desired-state`
- `inventory`
- `update`
- `local-integration`
- `upload`
- `authorized-users`

Treat the notification as invalidation: call the appropriate REST GET endpoint to obtain the latest
durable state. Do not assume every progress transition is delivered; reconnect and read current
state. Rich producer-event content on this socket remains pending.

### Limits and compatibility

- REST/JSON protocol version: `1.0`.
- Telemetry request/payload limit: 64 KiB.
- Pending producer-message limit: 10,000.
- Maximum opaque file size: 100 GiB; transfer chunk size: 4 MiB.
- The local file store retains the latest 500 upload sessions.
- Producer messages per cloud heartbeat: 100.
- Registered capabilities: at most 100 strings.
- Local controller rate limit: 300 requests per minute per Agent process in the current increment.
- Adapter heartbeat is considered offline after 15 seconds.
- API additions should be backward compatible within v1. Breaking contract changes require
  `/local/v2` and a capability-negotiation transition.

## Development and testing

```bash
dotnet restore
dotnet build

export FLEETCOMB_AGENT_DATA_DIR="$PWD/agent-state"
dotnet run --project src/FleetComb.Agent.Api -- \
  enroll --server http://localhost:5000 --code 'FC1-...'
dotnet run --project src/FleetComb.Agent.Api
```

When the data-directory variable is omitted during Linux development, state uses
`~/.local/share/FleetComb/Agent`. The supplied systemd unit explicitly uses
`/var/lib/fleetcomb-agent` under its dedicated service user.

The web UI listens on `http://0.0.0.0:5137` by default. Open
`http://INSTRUMENT-IP:5137` from a laptop. Web enrollment creates the local administrator; CLI
enrollment redirects the first browser visit to password setup. Configure a different bind using
`AgentWeb__Urls`.

Uploads default to `DATA_DIRECTORY/upload-inbox`. Configure additional absolute roots as follows:

```json
{
  "AgentUploads": {
    "AllowedRoots": ["/data/scans", "/data/projects"]
  }
}
```

Environment configuration uses keys such as
`AgentUploads__AllowedRoots__0=/data/scans`. A configured list replaces the default inbox-only
allowlist; include the inbox explicitly if both are required.

Run the example adapter with the bootstrap token:

```bash
dotnet run --project tools/FleetComb.SystemManagerSimulator -- \
  --token 'BOOTSTRAP-TOKEN' --watch
```

The simulator exchanges the bootstrap token for a scoped adapter token and uses the latter for all
subsequent calls. Additional options:

```bash
# Report an externally installed version.
dotnet run --project tools/FleetComb.SystemManagerSimulator -- \
  --token 'BOOTSTRAP-TOKEN' --application 'APPLICATION-ID' --version '1.2.3'

# Exercise adapter update handoff with a safe ZIP Release.
dotnet run --project tools/FleetComb.SystemManagerSimulator -- \
  --token 'BOOTSTRAP-TOKEN' --simulate-update 'APPLICATION-ID' --watch

# Queue an allowlisted opaque file and watch it complete.
dotnet run --project tools/FleetComb.SystemManagerSimulator -- \
  --token 'BOOTSTRAP-TOKEN' --upload '/allowed/path/example.scan'
```

For the update test, the Asset Product Model must be compatible with an active Platform containing
an active Application and a Published Release matching Agent OS/architecture. Successful adapter
completion updates local inventory and FleetComb on the next heartbeat.

Run verification:

```bash
dotnet test FleetComb.Agent.slnx
```

## Service installation

- Linux: review `packaging/linux/fleetcomb-agent.service`, publish the Agent, install it under the
  dedicated service account, and ensure the data directory is writable only by that identity.
- Windows: publish the Agent and run `packaging/windows/Install-FleetCombAgent.ps1` from an elevated
  PowerShell prompt. Restrict the data directory ACL to the service identity.
- Production LAN deployments must configure a Kestrel HTTPS certificate and network access rules.
  Never expose the administrator password, bootstrap token, adapter tokens, or local API over an
  untrusted plaintext network.

## Where to resume

Resume Agent work in this order: trusted release manifests/signatures, the privileged platform
installer, Agent self-update, production deployment security, and final real-machine validation.
Do not describe standard EXE/DEB installation as production-ready before all five areas are closed.

FleetComb upload browsing/download/reassembly and format-specific processing are separate
FleetComb application features; the generic Agent transfer is implemented. Keep this README and the
FleetComb `docs/Features/Agent-Features.MD` ledger updated in the same change whenever a feature
moves between pending, prototype, and implemented.

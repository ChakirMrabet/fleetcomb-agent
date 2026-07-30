# FleetComb Agent

Cross-platform FleetComb equipment Agent. The first slice supports:

- Windows x64 and Debian/Ubuntu Linux x64 from one .NET codebase;
- one-time Asset enrollment;
- locally generated ECDSA P-256 identity;
- signed heartbeat requests;
- hostname, operating-system, architecture, Agent version, and protocol reporting;
- Windows Service and systemd hosting.
- desired Product/Software Platform/Application synchronization;
- a bearer-protected local API for customer System Managers;
- durable Agent-installed and externally reported Application inventory;
- streamed update downloads with length and SHA-256 verification;
- standard `.deb` and Windows `.exe` installation plus customer-adapter handoff.
- a LAN-accessible Razor Pages UI with enrollment, local-administrator authentication, status,
  desired software, inventory, and update progress.

## Architecture

The Agent uses the same inward dependency direction as the FleetComb backend:

```text
FleetComb.Agent.Domain
        ↑
FleetComb.Agent.Application
        ↑
Infrastructure.Cloud / Infrastructure.Persistence / Infrastructure.Updates
        ↑
FleetComb.Agent.Api
        ↓
FleetComb.Agent.Ui
```

- **Domain** owns Agent registration, desired software, inventory, and update state.
- **Application** owns enrollment, synchronization, status, adapter, and update use cases. Every
  external dependency is represented by a dedicated interface under `Application/Abstractions`.
- **Infrastructure** implements FleetComb cloud communication, durable files, platform identity,
  and standard installers.
- **API** is the single executable and composition root. Thin controllers host the customer and
  UI endpoints; `Program.cs` only assembles services and the HTTP pipeline.
- **UI** is a Razor class library containing pages and bundled offline assets.

Architecture tests prevent inward projects from referencing API, UI, or Infrastructure.

## Development

```bash
dotnet restore
dotnet build

export FLEETCOMB_AGENT_DATA_DIR="$PWD/agent-state"
dotnet run --project src/FleetComb.Agent.Api -- \
  enroll --server http://localhost:5000 --code 'FC1-...'
dotnet run --project src/FleetComb.Agent.Api
```

When `FLEETCOMB_AGENT_DATA_DIR` is omitted during Linux development, state is stored under the
current user's local application-data directory (`~/.local/share/FleetComb/Agent`). The supplied
systemd unit explicitly uses `/var/lib/fleetcomb-agent` and runs under the dedicated service user.
Set the environment variable before enrollment whenever you want an isolated development state.

The web interface listens on `http://0.0.0.0:5137` in development so a laptop on the instrument
network can open `http://INSTRUMENT-IP:5137`. First-time web enrollment creates a separate local
administrator password. CLI enrollment redirects the first browser visit to password setup.

An authenticated **Reset enrollment** page moves local credentials and state into
`reset-backups/TIMESTAMP` and returns the instrument to enrollment. Generate a **Replace Agent**
code in FleetComb before enrolling again; its successful claim invalidates the previous cloud
identity.

Set `AgentWeb__Urls` to bind a specific instrument interface. Production LAN deployments must use
HTTPS, configured through normal ASP.NET Core Kestrel certificate settings; do not send the local
administrator password over an untrusted plaintext network.

Enrollment also creates a separate local API token. Customer software sends the token as
`Authorization: Bearer TOKEN`.

For an existing enrollment, retrieve or create its local token with the same data-directory
environment used by the service:

```bash
dotnet run --project src/FleetComb.Agent.Api -- local-token
```

Useful endpoints:

- `GET /local/v1/status`
- `GET /local/v1/desired-state`
- `GET /local/v1/applications`
- `POST /local/v1/applications/report`
- `GET /local/v1/updates/current`
- `POST /local/v1/adapter/register`
- `POST /local/v1/adapter/heartbeat`
- `POST /local/v1/applications/{applicationId}/install`
- `POST /local/v1/applications/{applicationId}/install-completion`

Run the example customer System Manager:

```bash
dotnet run --project tools/FleetComb.SystemManagerSimulator -- \
  --token 'LOCAL-API-TOKEN' --watch
```

The simulator registers itself as a local integration and sends a heartbeat every five seconds.
When an integration has registered, the Agent UI shows its own reported name and connection state.
The integration section is hidden on installations that do not use one. The same page shows
FleetComb synchronization, its last success or error, and live update progress.

To report software installed outside the Agent:

```bash
dotnet run --project tools/FleetComb.SystemManagerSimulator -- \
  --token 'LOCAL-API-TOKEN' \
  --application 'APPLICATION-ID' \
  --version '1.2.3'
```

To request installation of the latest matching release:

```bash
dotnet run --project tools/FleetComb.SystemManagerSimulator -- \
  --token 'LOCAL-API-TOKEN' \
  --simulate-update 'APPLICATION-ID'
```

`.pkg` and `.zip` releases enter `AwaitingAdapter`. A customer adapter performs its specialized
installation and posts the result to `install-completion`. Successful standard and adapter-driven
installations are stored as `AgentInstalled` and synchronized back to FleetComb.

For a safe end-to-end development test:

1. In FleetComb, link the Asset's Product Model to an active Software Platform.
2. Add an active Application and publish a release matching the Agent's operating system and
   architecture. Use a small `.zip` or `.pkg` artifact so the simulator, rather than the operating
   system package manager, handles installation.
3. Enroll and start the Agent, retrieve its local token, and open the Agent UI.
4. Run the simulator with `--simulate-update APPLICATION-ID --watch`.
5. Confirm the UI changes from downloading to waiting for the System Manager and then completed.
   The installed Application version appears in the local inventory and is reported to FleetComb
   on the next synchronization.

Use `--base-url http://INSTRUMENT-IP:5137` when the simulator is running on another computer on
the instrument network. In production, protect both the UI and local API with HTTPS and network
access controls.

The enrollment code is created from the Asset page in FleetComb and expires after 15 minutes.
The private key remains on the Agent machine. The fallback file key store restricts Unix key files
to the owning account. Production installers must restrict the Windows data directory to the
dedicated service identity.

## Service installation

- Linux: review and install `packaging/linux/fleetcomb-agent.service`.
- Windows: publish the Agent, then run
  `packaging/windows/Install-FleetCombAgent.ps1` from an elevated PowerShell prompt.

macOS is not an initial supported package, but platform detection and shared Agent behavior contain
no Windows-only assumptions.

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

## Development

```bash
dotnet restore
dotnet build

export FLEETCOMB_AGENT_DATA_DIR="$PWD/agent-state"
dotnet run --project src/FleetComb.Agent -- \
  enroll --server http://localhost:5000 --code 'FC1-...'
dotnet run --project src/FleetComb.Agent
```

Enrollment prints the local API token once. The local API listens only on
`http://127.0.0.1:5137/local/v1`. Customer software sends the token as
`Authorization: Bearer TOKEN`.

For an existing enrollment, retrieve or create its local token with the same data-directory
environment used by the service:

```bash
dotnet run --project src/FleetComb.Agent -- local-token
```

Useful endpoints:

- `GET /local/v1/desired-state`
- `GET /local/v1/applications`
- `POST /local/v1/applications/report`
- `GET /local/v1/updates/current`
- `POST /local/v1/applications/{applicationId}/install`
- `POST /local/v1/applications/{applicationId}/install-completion`

Run the example customer System Manager:

```bash
dotnet run --project tools/FleetComb.SystemManagerSimulator -- \
  --token 'LOCAL-API-TOKEN' --watch
```

To report software installed outside the Agent:

```bash
dotnet run --project tools/FleetComb.SystemManagerSimulator -- \
  --token 'LOCAL-API-TOKEN' \
  --application 'APPLICATION-ID' \
  --version '1.2.3'
```

To request installation of the latest matching release:

```bash
curl -X POST \
  -H "Authorization: Bearer LOCAL-API-TOKEN" \
  http://127.0.0.1:5137/local/v1/applications/APPLICATION-ID/install
```

`.pkg` and `.zip` releases enter `AwaitingAdapter`. A customer adapter performs its specialized
installation and posts the result to `install-completion`. Successful standard and adapter-driven
installations are stored as `AgentInstalled` and synchronized back to FleetComb.

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

# FleetComb Agent Instructions

- Read `README.md` before substantial work and keep it current after every material Agent change.
- Keep the Agent generic for different customer System Managers; vendor-specific orchestration
  belongs in adapters.
- Preserve the software model: Applications own Releases; Modules are licensed functionality;
  desired state, Entitlements, reported installations, and Agent-installed versions are distinct.
- Preserve inward dependencies: Domain <- Application <- Infrastructure; API is the composition
  host and UI must not reference Infrastructure.
- Define external dependencies in `FleetComb.Agent.Application/Abstractions` and implement them in
  the appropriate Infrastructure project.
- Use action-named MediatR commands/queries with one request, validator when needed, and handler per
  file. Keep controllers, Razor Pages, workers, and `Program.cs` thin.
- Keep the LAN UI usable from another authorized computer; do not assume localhost, an attached
  display, or a keyboard.
- Keep `/local/v1` generic, versioned, scope-authorized, documented, and backward compatible unless
  a protocol change is explicit.
- Treat identity, desired state, inventory, adapters, updates, telemetry, and uploads as durable.
  Define truthful restart recovery and make retries idempotent and bounded.
- Never log or persist plaintext adapter tokens beyond their one-time return. Preserve key, request
  signing, replay prevention, redaction, path, and package-verification protections.
- Support Windows and Linux explicitly; do not claim macOS packaging/installation support until
  tested. Keep direct EXE/DEB execution labeled prototype-only.
- Format touched C# with `dotnet format`; format Razor, CSS, and JavaScript with their configured
  formatter or the surrounding file style. Do not format unrelated files.

Use `$implement-fleetcomb-agent-feature` for the detailed architecture, protocol, update,
formatting, testing, and README workflow.

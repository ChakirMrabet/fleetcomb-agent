---
name: implement-fleetcomb-agent-feature
description: Implement, extend, or repair a FleetComb Agent capability using the Agent's Clean Architecture, MediatR CQRS, local Razor Pages UI, scoped customer adapter REST/SignalR API, durable file persistence, signed FleetComb cloud protocol, cross-platform Windows/Linux behavior, secure software-update flow, tests, and README handoff. Use for enrollment, synchronization, inventory, adapters, telemetry, uploads, updates, service hosting, local UI/API, or Agent security work.
---

# Implement a FleetComb Agent Feature

## Establish the boundary

1. Read `AGENTS.md` and the complete relevant sections of `README.md`.
2. Trace the existing feature through Domain state, Application command/query and abstractions,
   Infrastructure implementations, controller/Razor/worker entry point, persistence, notifications,
   tests, and documentation.
3. Identify whether the behavior belongs to the generic Agent, FleetComb cloud, or a customer
   adapter. Keep vendor orchestration in the adapter while preserving a useful generic protocol.
4. Identify Windows/Linux differences, privilege requirements, network exposure, durable state,
   restart behavior, authentication/scope, idempotency, and compatibility impact before editing.

## Implement inward

1. Define durable state and invariants in Domain without outward dependencies.
2. Define the use case as one action-named command/query file in Application, with one request,
   validator when needed, and handler.
3. Add every external capability as an Application abstraction. Implement persistence, cloud,
   platform, and update behavior in the appropriate Infrastructure project.
4. Invoke the use case through MediatR from a thin controller, Razor handler, hosted worker, or CLI
   adapter. Do not register business endpoints inline in `Program.cs`.
5. Publish state changes through the existing notification abstraction so the local UI and scoped
   adapter stream remain current.

## Preserve protocol and durability

- Keep `/local/v1` contracts generic, versioned, scope-protected, and backward compatible unless a
  deliberate protocol-version change is documented.
- Do not let the bootstrap token submit adapter-owned inventory or telemetry. Store only token
  hashes and keep secrets out of logs, diagnostics, responses, and backups.
- Persist state before acknowledging operations that must survive a crash. Define explicit restart
  recovery for every active/intermediate state.
- Keep retries idempotent, queues bounded, uploads resumable, acknowledgements exact, and source
  file/update artifact identity stable.
- Preserve signed cloud request canonicalization and replay protection when changing cloud calls.

## Handle updates defensively

1. Resolve only the desired Application's compatible published Release for the current OS and
   architecture.
2. Stream without unbounded buffering; verify declared length, SHA-256, package type, extension,
   recognizable header, and required trust metadata.
3. Choose built-in installation only for an explicitly supported safe contract; otherwise enter the
   durable adapter handoff state.
4. Track every transition and successful installed version. Make interruption, timeout, reboot,
   failure, and recovery-required outcomes truthful.
5. Do not present prototype EXE/DEB execution as production-ready.

## Verify on the real boundaries

- Add focused tests for validation/handler behavior, persistence/restart, auth scopes, protocol
  serialization, OS branching, concurrency, retry, cancellation, and architecture as applicable.
- Format touched C# files with
  `dotnet format FleetComb.Agent.slnx --no-restore --include <touched C# paths>`. Format touched
  Razor, CSS, and JavaScript with their configured formatter or the surrounding file style. Inspect
  the diff so unrelated files are not rewritten.
- Run focused tests, then `dotnet build FleetComb.Agent.slnx` and
  `dotnet test FleetComb.Agent.slnx` for substantial work.
- Use the System Manager simulator or a small authenticated REST/SignalR flow when no customer
  adapter exists.
- Update the README's implemented/prototype/pending status, endpoint tables, core implementation,
  configuration, and testing instructions before completion. Update FleetComb cloud protocol docs
  when the cross-repository contract changes.

# Project S Production UI Pause Note

Recorded: 2026-09-01 (Asia/Seoul)
Status: paused at user request

## Completed in the working tree

- `UnitProductionQueue` exposes production-queue state, enqueue failure reasons, and a non-mutating enqueue validation API.
- The RTS HUD shows the active production progress, queued/pending counts, pending unit names, unit costs, concise enqueue failure feedback, and the current rally point.
- The player command controller accepts the HUD Rally command and applies the next valid world click as the selected production building's rally point.
- The production queue still sends completed units to the configured rally point.
- PlayMode coverage was added for insufficient-resource rejection and queue ordering/progression.

## Verification completed

- `dotnet build Project_S.sln` completed successfully with 0 warnings and 0 errors.
- `dotnet test ProjectS.PlayModeTests.csproj --no-build --verbosity normal` returned exit code 0, but did not display Unity PlayMode discovery or an execution summary.
- Unity batch-mode PlayMode execution did not create a result XML. A pre-existing Unity process may have held the project; it was intentionally left running rather than terminated.

## Pending request after resume

Implement the MVP production-cancellation loop without adding Supply/population mechanics:

1. Add a cancellation API to `UnitProductionQueue` for both active and pending production.
2. Refund 100 percent of the cancelled production's `ResourceAmount` through `PlayerResourceWallet.Add`.
3. Add HUD controls to cancel the active production and/or selected pending queue entry.
4. Extend tests for queue removal, full refund, no unintended cost changes on rejected enqueue, and rally movement after production.

## Primary files in scope

- `Assets/05.Scripts/Buildings/UnitProductionQueue.cs`
- `Assets/05.Scripts/UI/RtsGameHud.cs`
- `Assets/05.Scripts/Units/PlayerUnitCommandController.cs`
- `Assets/05.Scripts/Units/UnitCommandTypes.cs`
- `Assets/05.Scripts/Tests/PlayMode/UnitPathAgentMovementTests.cs`

## Cautions for resuming

- The working tree contains unrelated user changes. Preserve them.
- Keep the existing rally-point behavior intact.
- Do not introduce Supply/population rules in this task; at most, leave a later TODO.
- Do not commit or push unless explicitly requested.

# Verification Status

This file tracks live-verification status for every trigger's underlying memory
read against the actual game (`DarkSoulsRemastered.exe`). A trigger firing
correctly in code review is not the same as a trigger firing correctly against
real game memory — see `DsrPointers` for the provenance notes on each read,
and the project's CANDIDATE-vs-verified distinction in general.

Until a row says VERIFIED, treat its detection method as unconfirmed, even if
tests pass and the app runs without errors.

| Trigger        | Detection method                                              | Status       | Verified on (date / game version) | Notes |
|-----------------|----------------------------------------------------------------|--------------|------------------------------------|-------|
| outOfStamina    | Stamina (DsrPointers, CANDIDATE offset) crosses > 0 to <= 0    | NOT VERIFIED |                                     |       |
| tookDamage      | HP (SoulMemory) decreases, stays > 0                           | NOT VERIFIED |                                     |       |
| dexIncrease     | Dexterity attribute (SoulMemory) increases                     | NOT VERIFIED |                                     |       |
| death           | HP (SoulMemory) crosses > 0 to <= 0                             | NOT VERIFIED |                                     |       |
| emptyEstus      | Animation ID (CANDIDATE) + EstusCount == 0                     | NOT VERIFIED |                                     |       |
| runningJump     | Animation ID (CANDIDATE, not yet discovered)                   | NOT VERIFIED |                                     |       |
| gotParried      | Animation ID (CANDIDATE, not yet discovered)                   | NOT VERIFIED |                                     |       |
| failedParry     | Animation ID pair: parryAttempt without parrySuccess in window | NOT VERIFIED |                                     |       |
| hitWall         | Animation ID (CANDIDATE, not yet discovered; may be infeasible)| NOT VERIFIED |                                     |       |

## How to verify a row

1. Run `dotnet run --project src/DSLaughTrack -- --status` with the game running
   and a character loaded. Confirm `HP(DsrPointers)` equals `HP(SoulMemory)` —
   this cross-checks the shared player-instance pointer chain.
2. For animation-based triggers, use `--diff` (see README.md "How discovery
   works") to find and confirm the animation ID in `animation_ids.json`, then
   trigger the in-game action and confirm the app logs the expected trigger
   firing (use `--monitor` to watch field changes live).
3. Update this table: set Status to `VERIFIED`, fill in the date and the game
   version tested against, and note anything relevant (e.g. game patch,
   caveats, false positives observed).

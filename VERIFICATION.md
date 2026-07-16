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
| outOfStamina    | Stamina (DsrPointers, offset VERIFIED) crosses > 0 to <= 0     | VERIFIED     | 2026-07-15 / Steam latest (exe FileVersion 1.0.0.0) | Fired on live depletion; 10s cooldown correctly suppressed a second depletion 6s later. Stamina goes negative internally (observed -36); `<= 0` handles it. |
| tookDamage      | HP (SoulMemory) decreases, stays > 0                           | VERIFIED     | 2026-07-15 / Steam latest          | 4 live fires; hits inside 8s cooldown correctly silent; did NOT fire on killing blow or during respawn HP refill; menus produced no false fires. |
| dexIncrease     | Dexterity attribute (SoulMemory) increases                     | VERIFIED     | 2026-07-15 / Steam latest          | Fired exactly on level-up confirm (39→40); opening level-up screen without confirming fired nothing. |
| death           | HP (SoulMemory) crosses > 0 to <= 0                             | VERIFIED     | 2026-07-15 / Steam latest          | Fired on death; respawn (player unload → reload, HP 0→full) fully suppressed. |
| emptyEstus      | Anim 7588 + EstusCount == 0                                    | VERIFIED     | 2026-07-16 / Steam latest          | Fired on empty-flask attempt; 5 normal drinks correctly silent. 7588 observed only while EstusCount==0 (8 occurrences). |
| runningJump     | Anim 900                                                       | VERIFIED     | 2026-07-16 / Steam latest          | 4 live fires; captured 6 clean repetitions across two sessions; rolls (710) and backsteps (711) never triggered it. |
| gotParried      | Animation ID (not yet captured)                                | NOT DISCOVERED |                                   | No parry-capable enemy reachable in session (fresh NG+). Ships disabled; capture instructions in animation_ids.json + README. |
| failedParry     | Anim 485102 without parrySuccess in window                     | EXPERIMENTAL (disabled) | 2026-07-16 / Steam latest | Verified negative result: successful parry plays the SAME anim as a whiff (3 vs 3 confirmed), so success is indistinguishable — if enabled, fires on every parry attempt. |
| hitWall         | Anims 254150 (1H) / 253150 (2H) — bounce = attack family + 150 | VERIFIED (enabled by default) | 2026-07-16 / Steam latest | 3+3 clean captures + 5-rep same-session control; live beeps on walls. Shield check: 6 attacks vs raised shields produced NO bounce anim (and no beep in earlier enabled session) — wall-specific vs shields tested. Remaining caveats: moveset-relative IDs (other weapons may need more values); greatshield-class deflections untested; illusory walls indistinguishable (out of scope). |

## Session A evidence (2026-07-15)

- `--status` cross-check: `HP(DsrPointers) == HP(SoulMemory) == 1320` — validates the ported
  WorldChrManImp→playerIns chain against SoulMemory's independent resolution.
- Stamina offset (playerIns+0x3f8, derived from DSR-Gadget ChrData1 layout): max 140 matched
  the in-game status screen; drained/regenerated in real time; **promoted from CANDIDATE to VERIFIED**.
- Animation-ID chain (playerIns→0x68→0x48→+0x80, cross-project CANDIDATE from Task 9):
  behaves exactly like an animation ID — idle -1, roll 710, attack combo 254000/254001/254002,
  estus drink 7585→7586→7587. Distinct, stable, action-specific, reverts at idle.
  **Chain confirmed live; individual trigger anim IDs still require discovery (session B).**
- EstusCount (SoulMemory GetInventory, EstusFlask.Quantity): tracked a drink (9→8) and a
  bonfire refill (8→10) exactly. One unreproduced pre-session discrepancy (HUD 10 vs read 9
  at first attach) never recurred across two watched events; treat as observer error unless seen again.

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

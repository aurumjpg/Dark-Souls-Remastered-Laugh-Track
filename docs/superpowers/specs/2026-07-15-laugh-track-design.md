# Dark Souls Laugh Track — Design Spec (v1)

Date: 2026-07-15
Status: Approved by user (offline-only v1; all multiplayer triggers cut)

## Goal

A real, working, configurable PC mod for Dark Souls Remastered (Steam) that plays
sitcom-style audience laughter when the player fails in specific ways. v1 is
offline/single-player only. No online/multiplayer triggers are included.

## Non-goals / exclusions (v1)

- Trigger "player is invaded" — multiplayer, cut from v1.
- Trigger "another player performs Point Down at the player" — multiplayer, cut from v1.
- No anti-cheat bypassing, stealth injection, save manipulation, gameplay advantages,
  or detection evasion of any kind.
- No writes to game memory, no code injection, no game-file modification.

## Architecture

A standalone C#/.NET 8 Windows console app (`DSLaughTrack.exe`) that runs alongside
the game — the same safety class as a speedrun autosplitter:

- Attaches to `DarkSoulsRemastered.exe` read-only via `ReadProcessMemory`.
- Polls game state ~30 Hz into immutable `GameState` snapshots.
- Independent trigger modules edge-detect events from (previous, current) snapshots.
- Plays laughter through Windows audio (NAudio). Audio is user-supplied.

### Components

1. **Memory layer**
   - `SoulMemory` NuGet package (from the SoulSplitter autosplitter project) for:
     process attach, screen state (InGame/Loading/MainMenu), player attributes
     (Dexterity), inventory (Estus count), player-loaded state.
   - `DsrPointers` module for values SoulMemory does not expose: player HP,
     max HP, stamina, current animation ID. Pointer chains/AOBs are ported from
     the open-source DSR-Gadget project (https://github.com/JKAnderson/DSR-Gadget),
     with a provenance comment on every offset (source file + what it reads).
   - **Rule: no memory address, offset, animation ID, or flag may appear in code
     without documented provenance (open-source citation or empirical discovery
     record in `animation_ids.json`).**

2. **Snapshot poller** — reads all values each tick. A failed read marks that
   field "unavailable" in the snapshot instead of throwing.

3. **Trigger engine** — one class per trigger implementing
   `ITrigger { string Name; bool ShouldFire(GameState prev, GameState curr); }`.
   Triggers are edge-detected (fire once, re-arm on condition reset). A trigger
   whose required fields are unavailable self-disables with one clear log line.

4. **Laugh player** — per-trigger sound list (random pick per fire), per-trigger
   volume × master volume, per-trigger cooldown + global cooldown.

5. **Config** — `config.json` next to the exe, hot-reloaded on file change.
   `sounds/<triggerName>/` folders for user-supplied .wav/.mp3 files.

6. **Logging** — console + rotating file log (`logs/`). Levels: info/debug.
   `--monitor` mode live-logs raw values (HP, stamina, anim ID, estus, Dex)
   for discovery and verification.

## Triggers (9 in v1)

| Trigger key        | Event                              | Detection                                                    | Ship status |
|--------------------|------------------------------------|--------------------------------------------------------------|-------------|
| `outOfStamina`     | Stamina fully depleted             | stamina ≤ 0 edge; re-arm when stamina recovers above threshold | Verified tier |
| `tookDamage`       | Player takes any damage            | HP decreased while in-game and not loading                   | Verified tier |
| `dexIncrease`      | Dexterity raised on level-up       | Dex attribute value increased                                | Verified tier |
| `death`            | Player dies                        | HP reaches 0 edge                                            | Verified tier |
| `emptyEstus`       | Drinks from empty flask            | drink/empty-flask animation ID AND estus count == 0          | Needs discovery |
| `runningJump`      | Performs a running jump            | jump animation ID                                            | Needs discovery |
| `gotParried`       | Player is parried                  | parry-stagger animation ID                                   | Needs discovery |
| `failedParry`      | Parry attempt that fails           | parry animation ID + no success indicator within window      | Experimental (heuristic) |
| `hitWall`          | Attack bounces off a real wall     | wall-bounce animation ID, if distinct from other bounces     | Experimental |

Known limitation, documented in README: distinguishing illusory from normal walls is
likely infeasible from animation state alone; `hitWall` (if it works at all) will fire
on any wall bounce. If no distinct wall-bounce state is discoverable, `hitWall` ships
as a documented stub (disabled, logged).

### Discovery workflow ("needs discovery" values)

1. Run `DSLaughTrack.exe --monitor` while the game runs.
2. User performs the action (gets parried, jumps, drinks empty flask, ...).
3. Monitor logs animation ID transitions with timestamps.
4. Captured IDs are recorded in `animation_ids.json` with: value, action,
   capture date, game version, and how it was reproduced.
5. Trigger code reads IDs only from that file. Undiscovered ID → trigger
   disabled with a log line ("hitWall: no verified animation ID, disabled").

## Configuration

```json
{
  "masterVolume": 0.8,
  "globalCooldownSeconds": 2.0,
  "pollHz": 30,
  "logLevel": "info",
  "triggers": {
    "death":        { "enabled": true, "volume": 1.0, "cooldownSeconds": 5,  "sounds": [] },
    "tookDamage":   { "enabled": true, "volume": 0.6, "cooldownSeconds": 8,  "sounds": [] }
  }
}
```

- Empty/omitted `sounds` array → play a random file from `sounds/<triggerKey>/`.
- Named files are resolved relative to `sounds/`.
- Missing folder/files → warning logged, trigger self-disables (no crash).
- Unknown config keys → warning, ignored. Malformed JSON → keep last good config, log error.

## Safe failure

- Game not running → wait/retry loop, quiet.
- Game exits → detach cleanly, return to wait loop.
- AOB/pointer resolution fails (e.g. game patch) → only dependent triggers disable,
  each with a specific log line; others keep working.
- All triggers suppressed during loading screens and menus (screen-state check).

## Testing

1. **Unit tests** (no game required): trigger engine fed synthetic `GameState`
   sequences — fire, re-arm, cooldown, unavailable-field self-disable, config
   parsing, sound resolution.
2. **Live verification**: per-trigger checklist — user performs action, log shows
   fire, laugh plays. Results recorded in `VERIFICATION.md` as
   Verified / Experimental / Not implemented, with date and game version.

## Deliverables

```
DSLaughTrack/
  src/DSLaughTrack/         main app
  src/DSLaughTrack.Tests/   unit tests
  config.json
  sounds/<triggerKey>/      user drops audio here (README explains)
  animation_ids.json        discovered values + provenance
  README.md                 setup, config reference, safety & licensing notes
  VERIFICATION.md           per-trigger verification status
```

## Licensing note

SoulMemory/SoulSplitter is GPLv3; this project will be GPLv3 to comply. DSR-Gadget
offsets are cited per-offset. No copyrighted laugh audio is bundled.

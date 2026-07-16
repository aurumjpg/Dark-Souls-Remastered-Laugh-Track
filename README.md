# Dark Souls Laugh Track

A companion app for Dark Souls: Remastered that plays a sound clip of your
choosing when funny/embarrassing/dramatic things happen to your character —
running out of stamina, getting hit, getting parried, dying, and so on.
Think a laugh track for your own deaths.

## Safety statement

- **Read-only.** This app only *reads* the game process's memory to observe
  HP, stamina, animation state, etc. It never writes to game memory, injects
  code, or hooks any game function. `ProcessMemory` (see
  `src/DSLaughTrack/Memory/ProcessMemory.cs`) opens the process handle with
  `PROCESS_VM_READ | PROCESS_QUERY_INFORMATION` only — there is no write
  capability anywhere in this codebase.
- **External process.** It runs as its own separate `.exe`, entirely outside
  the game. It does not modify any game files.
- **Offline, single-player use only.** This tool is intended for offline
  single-player play. Any external process reading a game's memory can be
  flagged by anti-cheat, so **do not run this while connected to Dark Souls
  Remastered's online services** (matchmaking, invasions, message boards,
  etc.). Play offline (disconnect from the internet, or use the game's
  offline mode) before starting this app.
- Nothing here bypasses DRM, cheats, or gives any competitive advantage — it
  only listens for events to play a sound.

## Install

Requires the .NET 8 SDK on Windows.

**Run from source:**

```
dotnet run --project src/DSLaughTrack
```

**Or publish a standalone build:**

```
dotnet publish src/DSLaughTrack -c Release -o publish
```

Then run `publish/DSLaughTrack.exe`. `config.json` and `animation_ids.json`
are copied next to the executable automatically (see the `<None Include>`
entries in `DSLaughTrack.csproj`); only `.wav`/`.mp3` files inside `sounds/`
are copied, so on a fresh clone (which ships no audio) create
`sounds/<triggerName>/` folders next to the `.exe` and drop your files there —
or put your audio in the source tree's `sounds/` folders before publishing.
Edit the copies next to the `.exe`, not the ones in the source tree, once
you've published.

Start Dark Souls: Remastered (offline) and load into a save; the app polls
for the game process automatically and starts reacting once it detects one.

## Config reference (`config.json`)

| Key | Type | Meaning |
|-----|------|---------|
| `masterVolume` | number 0–1 | Global volume multiplier, applied on top of each trigger's own `volume`. |
| `globalCooldownSeconds` | number | Minimum time between *any* two trigger sounds firing, regardless of trigger. Default 0 (off) — overlap is instead prevented by the no-overlap gate: only one sound plays at a time, fires during playback are skipped (not queued), and the next event after the sound finishes plays normally. |
| `pollHz` | integer | How many times per second the app reads game state while the game is running. |
| `logLevel` | `"info"` or `"debug"` | Log verbosity. `debug` adds low-level read-failure diagnostics. |
| `triggers.<name>.enabled` | bool | Whether this trigger can fire at all. |
| `triggers.<name>.volume` | number 0–1 | Per-trigger volume multiplier (combined with `masterVolume`). |
| `triggers.<name>.cooldownSeconds` | number | Minimum time between two firings of *this specific* trigger. Default 0 (off) — every event fires, and the no-overlap gate alone paces the audio. Raise it if a trigger feels too chatty. |
| `triggers.<name>.interrupt` | bool | When true, this trigger's sound *stops* whatever is currently playing and takes over, instead of being skipped by the no-overlap gate. Ships enabled for `death` only — so death music always plays, even if a laugh from the killing combo is mid-playback. |
| `triggers.<name>.sounds` | array of strings | Explicit sound file names (relative to `sounds/`) to pick from. If empty, the app instead picks a random `.wav`/`.mp3` from `sounds/<name>/`. |

The 9 trigger names are: `outOfStamina`, `tookDamage`, `dexIncrease`,
`death`, `emptyEstus`, `runningJump`, `gotParried`, `failedParry`, `hitWall`.

Trigger status after the 2026-07-15 live verification sessions (full evidence
in `VERIFICATION.md`):

- **Verified & enabled by default:** `outOfStamina`, `tookDamage`,
  `dexIncrease`, `death`, `runningJump`, `emptyEstus`, `hitWall`.
- **`hitWall` caveats:** verified firing on wall bounces (one- and two-handed
  captured) and verified NOT firing on attacks against the raised shields
  tested; but the bounce animation is moveset-relative (other weapons may
  need extra IDs added to `animation_ids.json`) and greatshield-class
  deflections were never observed, so those remain untested.
- **`failedParry` — experimental, disabled by default.** Discovery proved a
  successful parry plays the *same* player animation as a whiff, so this
  trigger cannot tell them apart: if enabled, it laughs at every parry
  attempt (which you may find fitting; enable at your own comedic risk).
- **`gotParried` — not yet captured.** No parry-capable enemy was reachable
  during the discovery session. To activate it: run `--monitor`, get parried
  3+ times, confirm the same animation ID each time, and add it to
  `animation_ids.json` with provenance.
- Distinguishing illusory walls from normal walls is **out of scope** —
  `hitWall` cannot tell them apart (documented limitation).

Config is hot-reloaded: edit `config.json` while the app is running and it
picks up the change within about a second (checked by file write time). If
the file is malformed JSON, the app logs an error and keeps using the last
good config instead of crashing.

## Sounds

No audio ships with this repo — see `sounds/README.txt`. Drop your own
`.wav`/`.mp3` files into the folder matching the trigger name, or list exact
file names in that trigger's `sounds` array in `config.json`.

Any trigger without files of its own falls back to the shared
`sounds/default/` folder. That makes per-trigger overrides easy: put one
laugh file in `sounds/default/` and, say, dramatic music in `sounds/death/`
— death plays its own sound, every other trigger plays the shared laugh.

## How discovery works

Some triggers (stamina, animation-based events) rely on memory offsets that
aren't exposed by the public SoulMemory API and had to be derived from other
open-source Dark Souls tools (see the provenance comments in
`src/DSLaughTrack/Memory/DsrPointers.cs`). Offsets sourced this way are
marked `CANDIDATE` until confirmed against the live game.

Two CLI modes exist to help confirm or discover offsets:

- `dotnet run --project src/DSLaughTrack -- --status` — one-shot dump of
  every value the app can currently read (HP, stamina, dexterity, estus,
  animation ID), including a cross-check of HP read via SoulMemory against
  HP read via our own pointer chain (`DsrPointers`). Run this with the game
  running and a character loaded to sanity-check that reads are working.
- `dotnet run --project src/DSLaughTrack -- --diff [hexOffset]` — interactive
  struct-diff tool. With no argument, it snapshots a 0x2000-byte block
  starting at the player instance, waits for you to press Enter, has you
  perform some in-game action, then snapshots again and prints every 4-byte
  offset that changed. With a hex offset argument, it instead follows the
  pointer at `PlayerIns + offset` and diffs *that* block — used to explore
  sub-structures such as the animation state. This is how new animation IDs
  get discovered and recorded (with their provenance) in
  `animation_ids.json`.
- `dotnet run --project src/DSLaughTrack -- --monitor` — normal run mode,
  but also logs every observed field change live, useful for watching what
  values do during specific in-game actions.

Any animation ID found this way must be recorded in `animation_ids.json`
with its provenance, and the corresponding row in `VERIFICATION.md` updated
before it's trusted for triggers.

## Troubleshooting

**"game not found" / app just sits there waiting:**
The app polls for `DarkSoulsRemastered.exe` every 2 seconds while the game
isn't running. Make sure the game is actually running under that process
name (not a renamed executable) and that this app has permission to open a
read handle to it (some security software blocks cross-process memory reads
— try running as the same user, or check antivirus logs).

**"extended pointers UNAVAILABLE" in the log, or stamina/animation triggers
never fire:**
This means the `WorldChrManImp` AOB (address-of-bytes) signature scan
failed — likely because the installed game version doesn't match the one
this scan pattern was built against. Basic triggers (HP/dexterity-based:
`outOfStamina` won't work in this case, but `tookDamage`, `death`,
`dexIncrease` still will, since those use SoulMemory's own API rather than
our pointer chain) will keep working; stamina- and animation-based triggers
will not until the pattern is updated for your game version.

**`HP(DsrPointers)` doesn't match `HP(SoulMemory)` in `--status`:**
This should never happen since both read from the same offset via the same
struct — if it does, something is wrong with the pointer chain resolution
for your game version; please report it rather than trusting stamina/animation
reads.

**Sounds don't play:**
Check `config.json`'s `sounds` array for the trigger, or that
`sounds/<triggerName>/` actually contains `.wav`/`.mp3` files. The log will
say `"fired, but no audio files found"` when a trigger fires with nothing to
play.

## License

This project is licensed under the **GNU General Public License v3.0**
(GPLv3) — see `LICENSE`.

It depends on the [SoulMemory](https://www.nuget.org/packages/SoulMemory)
NuGet package (part of the [SoulSplitter](https://github.com/FrankvdStam/SoulSplitter)
project), which is itself GPLv3-licensed. Because this project links against
SoulMemory, distributing built binaries of this project is subject to
GPLv3's copyleft terms, consistent with the license under which SoulMemory
is provided.

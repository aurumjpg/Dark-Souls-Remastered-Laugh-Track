# Dark Souls Laugh Track Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** A standalone Windows console app that reads Dark Souls Remastered game state read-only and plays user-supplied sitcom laughter on 9 offline fail-events, per the approved spec at `docs/superpowers/specs/2026-07-15-laugh-track-design.md`.

**Architecture:** External companion app (autosplitter safety class). SoulMemory (NuGet) handles process attach + documented reads (HP, attributes, inventory); a small cited pointer module adds stamina and (after discovery) animation ID. A 30 Hz poller produces immutable `GameState` snapshots; per-trigger classes edge-detect events; NAudio plays laughs with volumes + cooldowns from hot-reloaded `config.json`.

**Tech Stack:** .NET 8 (`net8.0-windows`), C#, SoulMemory 1.8.5 (pinned), NAudio 2.2.1 (pinned), xUnit.

## Global Constraints

- READ-ONLY memory access. Never call `WriteProcessMemory`; never call SoulMemory's write APIs (`WriteItemLotParam`, `WriteWeaponDescription`). No injection, no game-file changes, no save manipulation.
- Offline single-player only. No multiplayer-related code in v1.
- **Every game-specific constant (offset, AOB, animation ID) must carry a provenance comment**: either a citation (project + file) or a pointer to its discovery record in `animation_ids.json`. A constant without provenance is a task failure.
- Sentinel rule: any `FILL_FROM_SOURCE` marker introduced during porting must be replaced with the fetched value before the task's commit. Grep for it before committing.
- Dependencies pinned: `SoulMemory 1.8.5`, `NAudio 2.2.1`. TFM: `net8.0-windows` (both projects).
- License: GPLv3 (because SoulMemory is GPLv3). No copyrighted laugh audio committed to the repo.
- Triggers whose required data is unavailable must self-disable with one clear log line — never crash, never guess.
- Work happens on branch `feature/laugh-track` (worktree per superpowers:using-git-worktrees).

## File Structure

```
DSLaughTrack.sln
src/DSLaughTrack/DSLaughTrack.csproj
src/DSLaughTrack/Program.cs                  main loop, CLI modes (--monitor, --status, --diff)
src/DSLaughTrack/GameState.cs                immutable snapshot record
src/DSLaughTrack/AnimationIds.cs             loads discovered IDs from animation_ids.json
src/DSLaughTrack/Logging/Log.cs              console + per-run file log
src/DSLaughTrack/Config/AppConfig.cs         config model + loader
src/DSLaughTrack/Triggers/ITrigger.cs
src/DSLaughTrack/Triggers/BasicTriggers.cs   outOfStamina, tookDamage, death, dexIncrease
src/DSLaughTrack/Triggers/AnimationTrigger.cs
src/DSLaughTrack/Triggers/FailedParryTrigger.cs
src/DSLaughTrack/Triggers/TriggerFactory.cs
src/DSLaughTrack/TriggerEngine.cs            cooldowns, arming, suppression
src/DSLaughTrack/Audio/LaughPlayer.cs        NAudio playback + sound resolution
src/DSLaughTrack/Memory/AobScanner.cs        byte-pattern search (pure, testable)
src/DSLaughTrack/Memory/ProcessMemory.cs     ReadProcessMemory P/Invoke wrapper (read-only)
src/DSLaughTrack/Memory/DsrPointers.cs       cited pointer chains (stamina, anim)
src/DSLaughTrack/DsrGameStateSource.cs       SoulMemory + DsrPointers → GameState
src/DSLaughTrack.Tests/DSLaughTrack.Tests.csproj
src/DSLaughTrack.Tests/*Tests.cs
config.json                                  default config (all verified triggers on)
animation_ids.json                           discovered animation IDs + provenance
sounds/<triggerKey>/                         user-supplied audio (folders + README)
README.md
VERIFICATION.md
LICENSE                                      GPLv3
```

Trigger keys (used for config, sound folders, logs): `outOfStamina`, `tookDamage`, `dexIncrease`, `death`, `emptyEstus`, `runningJump`, `gotParried`, `failedParry`, `hitWall`.

---

### Task 1: Solution scaffold

**Files:**
- Create: `DSLaughTrack.sln`, `src/DSLaughTrack/DSLaughTrack.csproj`, `src/DSLaughTrack.Tests/DSLaughTrack.Tests.csproj`, `.gitignore`, `LICENSE`

**Interfaces:**
- Produces: buildable solution; namespaces rooted at `DSLaughTrack`.

- [ ] **Step 1: Create projects**

```bash
dotnet new sln -n DSLaughTrack
dotnet new console -o src/DSLaughTrack -n DSLaughTrack -f net8.0
dotnet new xunit -o src/DSLaughTrack.Tests -n DSLaughTrack.Tests -f net8.0
dotnet sln add src/DSLaughTrack src/DSLaughTrack.Tests
dotnet add src/DSLaughTrack.Tests reference src/DSLaughTrack
dotnet add src/DSLaughTrack package SoulMemory --version 1.8.5
dotnet add src/DSLaughTrack package NAudio --version 2.2.1
dotnet new gitignore
```

- [ ] **Step 2: Set TFM to net8.0-windows in both csproj files**

In both `.csproj`, change `<TargetFramework>net8.0</TargetFramework>` to `<TargetFramework>net8.0-windows</TargetFramework>` and add `<Nullable>enable</Nullable>` (keep it if the template already set it).

- [ ] **Step 3: Add GPLv3 LICENSE**

Download the GPLv3 text into `LICENSE`:
```bash
curl -s https://www.gnu.org/licenses/gpl-3.0.txt -o LICENSE
```

- [ ] **Step 4: Verify build and test run**

Run: `dotnet test`
Expected: build succeeds; the template's placeholder test passes (1 passed).

- [ ] **Step 5: Commit**

```bash
git add -A
git commit -m "chore: scaffold solution with SoulMemory and NAudio pinned"
```

---

### Task 2: Logging and config

**Files:**
- Create: `src/DSLaughTrack/Logging/Log.cs`, `src/DSLaughTrack/Config/AppConfig.cs`
- Test: `src/DSLaughTrack.Tests/ConfigTests.cs`
- Delete: the xunit template's placeholder test file (`UnitTest1.cs`)

**Interfaces:**
- Produces:
  - `Log(string? filePath = null)`, methods `Debug/Info/Warn/Error(string)`, property `LogLevel MinLevel`.
  - `AppConfig` with `double MasterVolume=0.8`, `double GlobalCooldownSeconds=2.0`, `int PollHz=30`, `string LogLevel="info"`, `Dictionary<string,TriggerConfig> Triggers`; `TriggerConfig For(string key)` (returns stored or default); `static AppConfig Load(string path)` (missing file → defaults; malformed JSON → throws `JsonException`).
  - `TriggerConfig` with `bool Enabled=true`, `double Volume=1.0`, `double CooldownSeconds=5.0`, `List<string> Sounds=[]`.

- [ ] **Step 1: Write failing tests**

```csharp
// src/DSLaughTrack.Tests/ConfigTests.cs
using System.Text.Json;
using DSLaughTrack.Config;
using Xunit;

namespace DSLaughTrack.Tests;

public class ConfigTests
{
    [Fact]
    public void Load_MissingFile_ReturnsDefaults()
    {
        var cfg = AppConfig.Load(Path.Combine(Path.GetTempPath(), Guid.NewGuid() + ".json"));
        Assert.Equal(0.8, cfg.MasterVolume);
        Assert.Equal(30, cfg.PollHz);
        Assert.True(cfg.For("death").Enabled);
        Assert.Equal(5.0, cfg.For("death").CooldownSeconds);
    }

    [Fact]
    public void Load_ParsesTriggersCaseInsensitively()
    {
        var path = Path.GetTempFileName();
        File.WriteAllText(path, """
            { "masterVolume": 0.5,
              "triggers": { "death": { "enabled": false, "volume": 0.9, "cooldownSeconds": 10, "sounds": ["a.wav"] } } }
            """);
        var cfg = AppConfig.Load(path);
        Assert.Equal(0.5, cfg.MasterVolume);
        Assert.False(cfg.For("death").Enabled);
        Assert.Equal(10, cfg.For("death").CooldownSeconds);
        Assert.Single(cfg.For("death").Sounds);
        Assert.True(cfg.For("unknownKey").Enabled); // default for unconfigured trigger
    }

    [Fact]
    public void Load_MalformedJson_Throws()
    {
        var path = Path.GetTempFileName();
        File.WriteAllText(path, "{ not json");
        Assert.Throws<JsonException>(() => AppConfig.Load(path));
    }
}
```

- [ ] **Step 2: Run tests, expect failure**

Run: `dotnet test`
Expected: FAIL — `AppConfig` does not exist (compile error).

- [ ] **Step 3: Implement Log and AppConfig**

```csharp
// src/DSLaughTrack/Logging/Log.cs
namespace DSLaughTrack.Logging;

public enum LogLevel { Debug, Info, Warn, Error }

public sealed class Log
{
    private readonly object _lock = new();
    private readonly string? _filePath;
    public LogLevel MinLevel { get; set; } = LogLevel.Info;

    public Log(string? filePath = null)
    {
        _filePath = filePath;
        if (_filePath is not null)
            Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(_filePath))!);
    }

    public void Debug(string msg) => Write(LogLevel.Debug, msg);
    public void Info(string msg) => Write(LogLevel.Info, msg);
    public void Warn(string msg) => Write(LogLevel.Warn, msg);
    public void Error(string msg) => Write(LogLevel.Error, msg);

    private void Write(LogLevel level, string msg)
    {
        if (level < MinLevel) return;
        var line = $"{DateTime.Now:HH:mm:ss.fff} [{level.ToString().ToUpperInvariant(),-5}] {msg}";
        lock (_lock)
        {
            Console.WriteLine(line);
            if (_filePath is not null) File.AppendAllText(_filePath, line + Environment.NewLine);
        }
    }
}
```

```csharp
// src/DSLaughTrack/Config/AppConfig.cs
using System.Text.Json;

namespace DSLaughTrack.Config;

public sealed class TriggerConfig
{
    public bool Enabled { get; set; } = true;
    public double Volume { get; set; } = 1.0;
    public double CooldownSeconds { get; set; } = 5.0;
    public List<string> Sounds { get; set; } = new();
}

public sealed class AppConfig
{
    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
    };

    public double MasterVolume { get; set; } = 0.8;
    public double GlobalCooldownSeconds { get; set; } = 2.0;
    public int PollHz { get; set; } = 30;
    public string LogLevel { get; set; } = "info";
    public Dictionary<string, TriggerConfig> Triggers { get; set; } = new(StringComparer.OrdinalIgnoreCase);

    public TriggerConfig For(string key) =>
        Triggers.TryGetValue(key, out var t) ? t : new TriggerConfig();

    public static AppConfig Load(string path)
    {
        if (!File.Exists(path)) return new AppConfig();
        var cfg = JsonSerializer.Deserialize<AppConfig>(File.ReadAllText(path), JsonOpts);
        return cfg ?? new AppConfig();
    }
}
```

Also delete `src/DSLaughTrack.Tests/UnitTest1.cs`.

Note: the deserialized `Triggers` dictionary must remain case-insensitive. `System.Text.Json` replaces the dictionary instance on deserialize, so normalize in `For()` if the case-insensitive test fails — acceptable fallback: look up with `Triggers.FirstOrDefault(kv => string.Equals(kv.Key, key, StringComparison.OrdinalIgnoreCase))`. Keep whichever makes the test pass, simplest first.

- [ ] **Step 4: Run tests, expect pass**

Run: `dotnet test`
Expected: PASS (3 tests).

- [ ] **Step 5: Commit**

```bash
git add -A
git commit -m "feat: logging and hot-reloadable config model"
```

---

### Task 3: GameState and AnimationIds

**Files:**
- Create: `src/DSLaughTrack/GameState.cs`, `src/DSLaughTrack/AnimationIds.cs`
- Test: `src/DSLaughTrack.Tests/AnimationIdsTests.cs`

**Interfaces:**
- Produces:
  - `GameState` record: `GameState(DateTimeOffset Timestamp, bool ProcessAttached, bool InGame, int? Hp, int? MaxHp, int? Stamina, int? Dexterity, int? EstusCount, int? AnimationId)` plus `static GameState Detached(DateTimeOffset ts)` (all fields null/false).
  - `AnimationIds` with `int? Get(string key)` and `static AnimationIds Load(string path, Log log)`; also `AnimationIds(Dictionary<string,int>)` for tests. Missing/invalid file → empty set + warning, never throws.

- [ ] **Step 1: Write failing tests**

```csharp
// src/DSLaughTrack.Tests/AnimationIdsTests.cs
using DSLaughTrack;
using DSLaughTrack.Logging;
using Xunit;

namespace DSLaughTrack.Tests;

public class AnimationIdsTests
{
    [Fact]
    public void Load_MissingFile_ReturnsEmpty()
    {
        var ids = AnimationIds.Load(Path.Combine(Path.GetTempPath(), Guid.NewGuid() + ".json"), new Log());
        Assert.Null(ids.Get("runningJump"));
    }

    [Fact]
    public void Load_ReadsValuesAndIgnoresEntriesWithoutValue()
    {
        var path = Path.GetTempFileName();
        File.WriteAllText(path, """
            { "ids": {
                "runningJump": { "value": 1234, "capturedOn": "2026-07-15", "gameVersion": "1.03.1", "method": "monitor mode", "notes": "sprint+jump" },
                "hitWall": { "notes": "not yet discovered" }
            } }
            """);
        var ids = AnimationIds.Load(path, new Log());
        Assert.Equal(1234, ids.Get("runningJump"));
        Assert.Null(ids.Get("hitWall"));
    }
}
```

- [ ] **Step 2: Run tests, expect compile failure**

Run: `dotnet test`
Expected: FAIL — types missing.

- [ ] **Step 3: Implement**

```csharp
// src/DSLaughTrack/GameState.cs
namespace DSLaughTrack;

public sealed record GameState(
    DateTimeOffset Timestamp,
    bool ProcessAttached,
    bool InGame,
    int? Hp,
    int? MaxHp,
    int? Stamina,
    int? Dexterity,
    int? EstusCount,
    int? AnimationId)
{
    public static GameState Detached(DateTimeOffset ts) =>
        new(ts, false, false, null, null, null, null, null, null);
}
```

```csharp
// src/DSLaughTrack/AnimationIds.cs
using System.Text.Json;
using DSLaughTrack.Logging;

namespace DSLaughTrack;

/// Discovered animation IDs. Values come ONLY from animation_ids.json, which records
/// provenance (capture date, game version, method) for every entry. See VERIFICATION.md.
public sealed class AnimationIds
{
    private readonly Dictionary<string, int> _ids;

    public AnimationIds(Dictionary<string, int> ids) =>
        _ids = new Dictionary<string, int>(ids, StringComparer.OrdinalIgnoreCase);

    public int? Get(string key) => _ids.TryGetValue(key, out var v) ? v : null;

    public static AnimationIds Load(string path, Log log)
    {
        var result = new Dictionary<string, int>();
        if (!File.Exists(path))
        {
            log.Warn($"animation ids file not found ({path}); all animation-based triggers will be disabled.");
            return new AnimationIds(result);
        }
        try
        {
            using var doc = JsonDocument.Parse(File.ReadAllText(path));
            if (doc.RootElement.TryGetProperty("ids", out var ids))
                foreach (var prop in ids.EnumerateObject())
                    if (prop.Value.TryGetProperty("value", out var v) && v.TryGetInt32(out var id))
                        result[prop.Name] = id;
        }
        catch (JsonException ex)
        {
            log.Error($"animation ids file is malformed, ignoring it: {ex.Message}");
        }
        return new AnimationIds(result);
    }
}
```

- [ ] **Step 4: Run tests, expect pass**

Run: `dotnet test`
Expected: PASS (5 tests total).

- [ ] **Step 5: Commit**

```bash
git add -A
git commit -m "feat: GameState snapshot and provenance-backed AnimationIds loader"
```

---

### Task 4: Basic triggers (stamina, damage, death, dex)

**Files:**
- Create: `src/DSLaughTrack/Triggers/ITrigger.cs`, `src/DSLaughTrack/Triggers/BasicTriggers.cs`
- Test: `src/DSLaughTrack.Tests/BasicTriggerTests.cs`

**Interfaces:**
- Produces:
  - `ITrigger { string Key { get; } bool ShouldFire(GameState prev, GameState curr); }`
  - `OutOfStaminaTrigger` (key `outOfStamina`), `TookDamageTrigger` (`tookDamage`), `DeathTrigger` (`death`), `DexIncreaseTrigger` (`dexIncrease`).

Semantics (edge-detected; engine guarantees both states are `InGame` before calling):
- `outOfStamina`: `prev.Stamina > 0 && curr.Stamina <= 0`.
- `tookDamage`: `curr.Hp < prev.Hp && curr.Hp > 0` (death is its own trigger).
- `death`: `prev.Hp > 0 && curr.Hp <= 0`.
- `dexIncrease`: `curr.Dexterity > prev.Dexterity`.
- All return false when a required field is null in either state.

- [ ] **Step 1: Write failing tests**

```csharp
// src/DSLaughTrack.Tests/BasicTriggerTests.cs
using DSLaughTrack;
using DSLaughTrack.Triggers;
using Xunit;

namespace DSLaughTrack.Tests;

public static class States
{
    public static GameState S(int? hp = 100, int? maxHp = 100, int? stam = 50, int? dex = 10,
        int? estus = 5, int? anim = 0, bool inGame = true, double t = 0) =>
        new(DateTimeOffset.UnixEpoch.AddSeconds(t), true, inGame, hp, maxHp, stam, dex, estus, anim);
}

public class BasicTriggerTests
{
    [Fact]
    public void OutOfStamina_FiresOnCrossingZero_Once()
    {
        var t = new OutOfStaminaTrigger();
        Assert.True(t.ShouldFire(States.S(stam: 12), States.S(stam: 0)));
        Assert.False(t.ShouldFire(States.S(stam: 0), States.S(stam: 0)));   // stays empty: no refire
        Assert.False(t.ShouldFire(States.S(stam: 0), States.S(stam: 30)));  // recovering
        Assert.False(t.ShouldFire(States.S(stam: null), States.S(stam: 0))); // unavailable
    }

    [Fact]
    public void TookDamage_FiresOnHpDrop_NotOnDeath()
    {
        var t = new TookDamageTrigger();
        Assert.True(t.ShouldFire(States.S(hp: 100), States.S(hp: 60)));
        Assert.False(t.ShouldFire(States.S(hp: 60), States.S(hp: 60)));
        Assert.False(t.ShouldFire(States.S(hp: 60), States.S(hp: 100)));  // healing
        Assert.False(t.ShouldFire(States.S(hp: 60), States.S(hp: 0)));    // death, not damage
    }

    [Fact]
    public void Death_FiresWhenHpReachesZero()
    {
        var t = new DeathTrigger();
        Assert.True(t.ShouldFire(States.S(hp: 60), States.S(hp: 0)));
        Assert.False(t.ShouldFire(States.S(hp: 0), States.S(hp: 0)));
        Assert.False(t.ShouldFire(States.S(hp: 0), States.S(hp: 100))); // respawn
    }

    [Fact]
    public void DexIncrease_FiresOnlyOnIncrease()
    {
        var t = new DexIncreaseTrigger();
        Assert.True(t.ShouldFire(States.S(dex: 10), States.S(dex: 11)));
        Assert.True(t.ShouldFire(States.S(dex: 10), States.S(dex: 15))); // multi-point level-up
        Assert.False(t.ShouldFire(States.S(dex: 11), States.S(dex: 11)));
        Assert.False(t.ShouldFire(States.S(dex: null), States.S(dex: 11)));
    }
}
```

- [ ] **Step 2: Run tests, expect compile failure**

Run: `dotnet test`
Expected: FAIL — trigger types missing.

- [ ] **Step 3: Implement**

```csharp
// src/DSLaughTrack/Triggers/ITrigger.cs
namespace DSLaughTrack.Triggers;

public interface ITrigger
{
    string Key { get; }
    /// Called only when both prev and curr are InGame. Must be side-effect free
    /// except for the trigger's own internal arming state.
    bool ShouldFire(GameState prev, GameState curr);
}
```

```csharp
// src/DSLaughTrack/Triggers/BasicTriggers.cs
namespace DSLaughTrack.Triggers;

public sealed class OutOfStaminaTrigger : ITrigger
{
    public string Key => "outOfStamina";
    public bool ShouldFire(GameState prev, GameState curr) =>
        prev.Stamina is > 0 && curr.Stamina is <= 0;
}

public sealed class TookDamageTrigger : ITrigger
{
    public string Key => "tookDamage";
    public bool ShouldFire(GameState prev, GameState curr) =>
        prev.Hp is int before && curr.Hp is int after && after < before && after > 0;
}

public sealed class DeathTrigger : ITrigger
{
    public string Key => "death";
    public bool ShouldFire(GameState prev, GameState curr) =>
        prev.Hp is > 0 && curr.Hp is <= 0;
}

public sealed class DexIncreaseTrigger : ITrigger
{
    public string Key => "dexIncrease";
    public bool ShouldFire(GameState prev, GameState curr) =>
        prev.Dexterity is int before && curr.Dexterity is int after && after > before;
}
```

- [ ] **Step 4: Run tests, expect pass**

Run: `dotnet test`
Expected: PASS.

- [ ] **Step 5: Commit**

```bash
git add -A
git commit -m "feat: stamina/damage/death/dex triggers with edge detection"
```

---

### Task 5: Animation triggers, failed parry, factory

**Files:**
- Create: `src/DSLaughTrack/Triggers/AnimationTrigger.cs`, `src/DSLaughTrack/Triggers/FailedParryTrigger.cs`, `src/DSLaughTrack/Triggers/TriggerFactory.cs`
- Test: `src/DSLaughTrack.Tests/AnimationTriggerTests.cs`

**Interfaces:**
- Consumes: `ITrigger`, `GameState`, `AnimationIds`, `Log`.
- Produces:
  - `AnimationTrigger(string key, int targetAnimId, Func<GameState,bool>? extraCondition = null)`.
  - `FailedParryTrigger(int parryAnimId, int? parrySuccessAnimId, TimeSpan window)` (key `failedParry`).
  - `static IReadOnlyList<ITrigger> TriggerFactory.Build(AnimationIds ids, Log log)` — builds all 9; animation-dependent triggers whose ID is missing are **not** built and a `Warn` line names each (`"<key>: no verified animation ID in animation_ids.json — trigger disabled"`). `failedParry` needs id key `parryAttempt` (and optionally `parrySuccess`); `emptyEstus` uses id key `emptyEstus` plus `EstusCount == 0`; `hitWall` uses id key `hitWall`; `runningJump`/`gotParried` use their own keys.

- [ ] **Step 1: Write failing tests**

```csharp
// src/DSLaughTrack.Tests/AnimationTriggerTests.cs
using DSLaughTrack;
using DSLaughTrack.Logging;
using DSLaughTrack.Triggers;
using Xunit;

namespace DSLaughTrack.Tests;

public class AnimationTriggerTests
{
    [Fact]
    public void FiresOnEnteringTargetAnimation_Once()
    {
        var t = new AnimationTrigger("runningJump", 777);
        Assert.True(t.ShouldFire(States.S(anim: 0), States.S(anim: 777)));
        Assert.False(t.ShouldFire(States.S(anim: 777), States.S(anim: 777)));
        Assert.False(t.ShouldFire(States.S(anim: 777), States.S(anim: 0)));
        Assert.False(t.ShouldFire(States.S(anim: null), States.S(anim: 777)));
    }

    [Fact]
    public void ExtraCondition_Gates()
    {
        var t = new AnimationTrigger("emptyEstus", 555, s => s.EstusCount == 0);
        Assert.True(t.ShouldFire(States.S(anim: 0, estus: 0), States.S(anim: 555, estus: 0)));
        Assert.False(t.ShouldFire(States.S(anim: 0, estus: 3), States.S(anim: 555, estus: 3)));
    }

    [Fact]
    public void FailedParry_FiresAfterWindowWithoutSuccess()
    {
        var t = new FailedParryTrigger(900, 901, TimeSpan.FromMilliseconds(800));
        Assert.False(t.ShouldFire(States.S(anim: 0, t: 0.00), States.S(anim: 900, t: 0.03)));
        Assert.False(t.ShouldFire(States.S(anim: 900, t: 0.03), States.S(anim: 900, t: 0.50)));
        Assert.True(t.ShouldFire(States.S(anim: 900, t: 0.50), States.S(anim: 0, t: 0.90)));
        // no refire after it has fired
        Assert.False(t.ShouldFire(States.S(anim: 0, t: 0.90), States.S(anim: 0, t: 1.50)));
    }

    [Fact]
    public void FailedParry_SuppressedBySuccessAnimation()
    {
        var t = new FailedParryTrigger(900, 901, TimeSpan.FromMilliseconds(800));
        Assert.False(t.ShouldFire(States.S(anim: 0, t: 0.00), States.S(anim: 900, t: 0.03)));
        Assert.False(t.ShouldFire(States.S(anim: 900, t: 0.03), States.S(anim: 901, t: 0.40)));
        Assert.False(t.ShouldFire(States.S(anim: 901, t: 0.40), States.S(anim: 0, t: 1.50)));
    }

    [Fact]
    public void Factory_DisablesTriggersWithoutIds()
    {
        var ids = new AnimationIds(new() { ["runningJump"] = 777 });
        var triggers = TriggerFactory.Build(ids, new Log());
        var keys = triggers.Select(t => t.Key).ToHashSet();
        Assert.Contains("outOfStamina", keys);
        Assert.Contains("tookDamage", keys);
        Assert.Contains("death", keys);
        Assert.Contains("dexIncrease", keys);
        Assert.Contains("runningJump", keys);
        Assert.DoesNotContain("gotParried", keys);
        Assert.DoesNotContain("emptyEstus", keys);
        Assert.DoesNotContain("failedParry", keys);
        Assert.DoesNotContain("hitWall", keys);
    }
}
```

- [ ] **Step 2: Run tests, expect compile failure**

Run: `dotnet test`
Expected: FAIL — types missing.

- [ ] **Step 3: Implement**

```csharp
// src/DSLaughTrack/Triggers/AnimationTrigger.cs
namespace DSLaughTrack.Triggers;

public sealed class AnimationTrigger : ITrigger
{
    public string Key { get; }
    private readonly int _target;
    private readonly Func<GameState, bool>? _extra;

    public AnimationTrigger(string key, int targetAnimId, Func<GameState, bool>? extraCondition = null)
    {
        Key = key;
        _target = targetAnimId;
        _extra = extraCondition;
    }

    public bool ShouldFire(GameState prev, GameState curr) =>
        curr.AnimationId == _target && prev.AnimationId != _target && prev.AnimationId is not null
        && (_extra?.Invoke(curr) ?? true);
}
```

```csharp
// src/DSLaughTrack/Triggers/FailedParryTrigger.cs
namespace DSLaughTrack.Triggers;

/// EXPERIMENTAL: fires when the parry animation played and no success indicator
/// was observed within the window. If parrySuccessAnimId is unknown (null), every
/// parry attempt fires — documented limitation, ships disabled by default.
public sealed class FailedParryTrigger : ITrigger
{
    public string Key => "failedParry";
    private readonly int _parryAnim;
    private readonly int? _successAnim;
    private readonly TimeSpan _window;
    private DateTimeOffset? _pendingSince;

    public FailedParryTrigger(int parryAnimId, int? parrySuccessAnimId, TimeSpan window)
    {
        _parryAnim = parryAnimId;
        _successAnim = parrySuccessAnimId;
        _window = window;
    }

    public bool ShouldFire(GameState prev, GameState curr)
    {
        if (curr.AnimationId is null) { _pendingSince = null; return false; }
        if (curr.AnimationId == _parryAnim && prev.AnimationId != _parryAnim)
            _pendingSince = curr.Timestamp;
        if (_pendingSince is null) return false;
        if (_successAnim is not null && curr.AnimationId == _successAnim)
        {
            _pendingSince = null;
            return false;
        }
        if (curr.Timestamp - _pendingSince >= _window)
        {
            _pendingSince = null;
            return true;
        }
        return false;
    }
}
```

```csharp
// src/DSLaughTrack/Triggers/TriggerFactory.cs
using DSLaughTrack.Logging;

namespace DSLaughTrack.Triggers;

public static class TriggerFactory
{
    public static IReadOnlyList<ITrigger> Build(AnimationIds ids, Log log)
    {
        var list = new List<ITrigger>
        {
            new OutOfStaminaTrigger(),
            new TookDamageTrigger(),
            new DeathTrigger(),
            new DexIncreaseTrigger(),
        };

        AddAnim(list, ids, log, "runningJump");
        AddAnim(list, ids, log, "gotParried");

        if (ids.Get("emptyEstus") is int estusAnim)
            list.Add(new AnimationTrigger("emptyEstus", estusAnim, s => s.EstusCount == 0));
        else
            LogDisabled(log, "emptyEstus");

        if (ids.Get("hitWall") is int wallAnim)
            list.Add(new AnimationTrigger("hitWall", wallAnim));
        else
            LogDisabled(log, "hitWall");

        if (ids.Get("parryAttempt") is int parryAnim)
            list.Add(new FailedParryTrigger(parryAnim, ids.Get("parrySuccess"), TimeSpan.FromMilliseconds(800)));
        else
            LogDisabled(log, "failedParry");

        return list;
    }

    private static void AddAnim(List<ITrigger> list, AnimationIds ids, Log log, string key)
    {
        if (ids.Get(key) is int anim) list.Add(new AnimationTrigger(key, anim));
        else LogDisabled(log, key);
    }

    private static void LogDisabled(Log log, string key) =>
        log.Warn($"{key}: no verified animation ID in animation_ids.json — trigger disabled");
}
```

- [ ] **Step 4: Run tests, expect pass**

Run: `dotnet test`
Expected: PASS.

- [ ] **Step 5: Commit**

```bash
git add -A
git commit -m "feat: animation-based triggers, experimental failed-parry, trigger factory"
```

---

### Task 6: TriggerEngine (cooldowns + suppression)

**Files:**
- Create: `src/DSLaughTrack/TriggerEngine.cs`
- Test: `src/DSLaughTrack.Tests/TriggerEngineTests.cs`

**Interfaces:**
- Consumes: `ITrigger`, `AppConfig`, `GameState`.
- Produces: `TriggerEngine(IReadOnlyList<ITrigger> triggers, Func<AppConfig> config)` with `IReadOnlyList<string> Tick(GameState prev, GameState curr)` returning fired trigger keys after all gating. Time source is `curr.Timestamp` (no wall clock — deterministic tests).

Gating order per tick: both states `InGame` → per trigger: config `Enabled` → `ShouldFire` → global cooldown → per-trigger cooldown. Firing updates both cooldown clocks.

- [ ] **Step 1: Write failing tests**

```csharp
// src/DSLaughTrack.Tests/TriggerEngineTests.cs
using DSLaughTrack;
using DSLaughTrack.Config;
using DSLaughTrack.Triggers;
using Xunit;

namespace DSLaughTrack.Tests;

file sealed class AlwaysFire : ITrigger
{
    public AlwaysFire(string key) => Key = key;
    public string Key { get; }
    public bool ShouldFire(GameState prev, GameState curr) => true;
}

public class TriggerEngineTests
{
    private static AppConfig Cfg(double global = 0, double perTrigger = 0, bool enabled = true) => new()
    {
        GlobalCooldownSeconds = global,
        Triggers = { ["a"] = new TriggerConfig { Enabled = enabled, CooldownSeconds = perTrigger },
                     ["b"] = new TriggerConfig { CooldownSeconds = perTrigger } }
    };

    [Fact]
    public void FiresAndRespectsPerTriggerCooldown()
    {
        var cfg = Cfg(global: 0, perTrigger: 5);
        var engine = new TriggerEngine(new[] { new AlwaysFire("a") }, () => cfg);
        Assert.Equal(new[] { "a" }, engine.Tick(States.S(t: 0), States.S(t: 0.1)));
        Assert.Empty(engine.Tick(States.S(t: 0.1), States.S(t: 3)));   // inside cooldown
        Assert.Equal(new[] { "a" }, engine.Tick(States.S(t: 3), States.S(t: 5.2)));
    }

    [Fact]
    public void GlobalCooldown_SuppressesOtherTriggers()
    {
        var cfg = Cfg(global: 2, perTrigger: 0);
        var engine = new TriggerEngine(new ITrigger[] { new AlwaysFire("a"), new AlwaysFire("b") }, () => cfg);
        // first tick: "a" fires, then global cooldown blocks "b" in the same tick
        Assert.Equal(new[] { "a" }, engine.Tick(States.S(t: 0), States.S(t: 0.1)));
        Assert.Empty(engine.Tick(States.S(t: 0.1), States.S(t: 1)));
        Assert.Equal(new[] { "a" }, engine.Tick(States.S(t: 1), States.S(t: 2.5)));
    }

    [Fact]
    public void DisabledTrigger_NeverFires()
    {
        var cfg = Cfg(enabled: false);
        var engine = new TriggerEngine(new[] { new AlwaysFire("a") }, () => cfg);
        Assert.Empty(engine.Tick(States.S(t: 0), States.S(t: 0.1)));
    }

    [Fact]
    public void NotInGame_SuppressesEverything()
    {
        var engine = new TriggerEngine(new[] { new AlwaysFire("a") }, () => Cfg());
        Assert.Empty(engine.Tick(States.S(inGame: false), States.S()));
        Assert.Empty(engine.Tick(States.S(), States.S(inGame: false)));
    }
}
```

- [ ] **Step 2: Run tests, expect compile failure**

Run: `dotnet test`
Expected: FAIL — `TriggerEngine` missing.

- [ ] **Step 3: Implement**

```csharp
// src/DSLaughTrack/TriggerEngine.cs
using DSLaughTrack.Config;
using DSLaughTrack.Triggers;

namespace DSLaughTrack;

public sealed class TriggerEngine
{
    private readonly IReadOnlyList<ITrigger> _triggers;
    private readonly Func<AppConfig> _config;
    private readonly Dictionary<string, DateTimeOffset> _lastFire = new();
    private DateTimeOffset _lastGlobalFire = DateTimeOffset.MinValue;

    public TriggerEngine(IReadOnlyList<ITrigger> triggers, Func<AppConfig> config)
    {
        _triggers = triggers;
        _config = config;
    }

    public IReadOnlyList<string> Tick(GameState prev, GameState curr)
    {
        var fired = new List<string>();
        if (!prev.InGame || !curr.InGame) return fired;
        var cfg = _config();
        foreach (var trigger in _triggers)
        {
            var tc = cfg.For(trigger.Key);
            if (!tc.Enabled) continue;
            if (!trigger.ShouldFire(prev, curr)) continue;
            var now = curr.Timestamp;
            if (now - _lastGlobalFire < TimeSpan.FromSeconds(cfg.GlobalCooldownSeconds)) continue;
            if (_lastFire.TryGetValue(trigger.Key, out var last) &&
                now - last < TimeSpan.FromSeconds(tc.CooldownSeconds)) continue;
            _lastFire[trigger.Key] = now;
            _lastGlobalFire = now;
            fired.Add(trigger.Key);
        }
        return fired;
    }
}
```

- [ ] **Step 4: Run tests, expect pass**

Run: `dotnet test`
Expected: PASS.

- [ ] **Step 5: Commit**

```bash
git add -A
git commit -m "feat: trigger engine with per-trigger and global cooldowns"
```

---

### Task 7: LaughPlayer (audio)

**Files:**
- Create: `src/DSLaughTrack/Audio/LaughPlayer.cs`
- Test: `src/DSLaughTrack.Tests/LaughPlayerTests.cs`

**Interfaces:**
- Consumes: `AppConfig`, `TriggerConfig`, `Log`.
- Produces: `LaughPlayer(string soundsRoot, Func<AppConfig> config, Log log)` with `void Play(string triggerKey)` (fire-and-forget, never throws) and `internal static string? ResolveSound(string soundsRoot, string triggerKey, TriggerConfig cfg, Random rng)`.
- Resolution rules: if `cfg.Sounds` non-empty → those paths relative to `soundsRoot`, keeping only existing files; else all `.wav`/`.mp3` files in `soundsRoot/<triggerKey>/`; random pick; none → null.

Add `InternalsVisibleTo` for the test project in `src/DSLaughTrack/DSLaughTrack.csproj`:

```xml
<ItemGroup>
  <InternalsVisibleTo Include="DSLaughTrack.Tests" />
</ItemGroup>
```

- [ ] **Step 1: Write failing tests**

```csharp
// src/DSLaughTrack.Tests/LaughPlayerTests.cs
using DSLaughTrack.Audio;
using DSLaughTrack.Config;
using Xunit;

namespace DSLaughTrack.Tests;

public class LaughPlayerTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "dslt-" + Guid.NewGuid());
    public LaughPlayerTests() => Directory.CreateDirectory(_root);
    public void Dispose() => Directory.Delete(_root, recursive: true);

    private string Touch(string relPath)
    {
        var full = Path.Combine(_root, relPath);
        Directory.CreateDirectory(Path.GetDirectoryName(full)!);
        File.WriteAllBytes(full, new byte[] { 0 });
        return full;
    }

    [Fact]
    public void ExplicitList_UsedWhenPresent_MissingFilesSkipped()
    {
        var existing = Touch("big_laugh.wav");
        var cfg = new TriggerConfig { Sounds = { "big_laugh.wav", "missing.wav" } };
        var resolved = LaughPlayer.ResolveSound(_root, "death", cfg, new Random(1));
        Assert.Equal(existing, resolved);
    }

    [Fact]
    public void FolderFallback_PicksOnlyAudioFiles()
    {
        Touch(Path.Combine("death", "a.wav"));
        Touch(Path.Combine("death", "b.MP3"));
        Touch(Path.Combine("death", "notes.txt"));
        var picks = Enumerable.Range(0, 20)
            .Select(i => LaughPlayer.ResolveSound(_root, "death", new TriggerConfig(), new Random(i)))
            .Where(p => p is not null)
            .Select(p => Path.GetFileName(p)!)
            .ToHashSet();
        Assert.Subset(new HashSet<string> { "a.wav", "b.MP3" }, picks);
        Assert.NotEmpty(picks);
    }

    [Fact]
    public void NoSounds_ReturnsNull()
    {
        Assert.Null(LaughPlayer.ResolveSound(_root, "death", new TriggerConfig(), new Random(1)));
    }
}
```

- [ ] **Step 2: Run tests, expect compile failure**

Run: `dotnet test`
Expected: FAIL — `LaughPlayer` missing.

- [ ] **Step 3: Implement**

```csharp
// src/DSLaughTrack/Audio/LaughPlayer.cs
using DSLaughTrack.Config;
using DSLaughTrack.Logging;
using NAudio.Wave;

namespace DSLaughTrack.Audio;

public sealed class LaughPlayer
{
    private static readonly string[] Extensions = { ".wav", ".mp3" };
    private readonly string _soundsRoot;
    private readonly Func<AppConfig> _config;
    private readonly Log _log;
    private readonly Random _rng = new();

    public LaughPlayer(string soundsRoot, Func<AppConfig> config, Log log)
    {
        _soundsRoot = soundsRoot;
        _config = config;
        _log = log;
    }

    public void Play(string triggerKey)
    {
        try
        {
            var cfg = _config();
            var tc = cfg.For(triggerKey);
            var file = ResolveSound(_soundsRoot, triggerKey, tc, _rng);
            if (file is null)
            {
                _log.Warn($"{triggerKey}: fired, but no audio files found (looked in {Path.Combine(_soundsRoot, triggerKey)}) — nothing played");
                return;
            }
            var volume = (float)Math.Clamp(cfg.MasterVolume * tc.Volume, 0.0, 1.0);
            var reader = new AudioFileReader(file) { Volume = volume };
            var output = new WaveOutEvent();
            output.PlaybackStopped += (_, _) => { output.Dispose(); reader.Dispose(); };
            output.Init(reader);
            output.Play();
            _log.Info($"LAUGH [{triggerKey}] {Path.GetFileName(file)} (vol {volume:0.00})");
        }
        catch (Exception ex)
        {
            _log.Error($"{triggerKey}: audio playback failed: {ex.Message}");
        }
    }

    internal static string? ResolveSound(string soundsRoot, string triggerKey, TriggerConfig cfg, Random rng)
    {
        List<string> candidates;
        if (cfg.Sounds.Count > 0)
        {
            candidates = cfg.Sounds
                .Select(s => Path.Combine(soundsRoot, s))
                .Where(File.Exists)
                .ToList();
        }
        else
        {
            var dir = Path.Combine(soundsRoot, triggerKey);
            candidates = Directory.Exists(dir)
                ? Directory.EnumerateFiles(dir)
                    .Where(f => Extensions.Contains(Path.GetExtension(f), StringComparer.OrdinalIgnoreCase))
                    .ToList()
                : new List<string>();
        }
        return candidates.Count == 0 ? null : candidates[rng.Next(candidates.Count)];
    }
}
```

- [ ] **Step 4: Run tests, expect pass**

Run: `dotnet test`
Expected: PASS.

- [ ] **Step 5: Commit**

```bash
git add -A
git commit -m "feat: NAudio laugh player with per-trigger sound resolution and volume"
```

---

### Task 8: AobScanner and ProcessMemory

**Files:**
- Create: `src/DSLaughTrack/Memory/AobScanner.cs`, `src/DSLaughTrack/Memory/ProcessMemory.cs`
- Test: `src/DSLaughTrack.Tests/AobScannerTests.cs`

**Interfaces:**
- Produces:
  - `static int AobScanner.Find(byte[] haystack, string pattern)` — pattern like `"48 8b 0d ? ? ? ? 0f"`; returns first index or -1.
  - `ProcessMemory : IDisposable` — `static ProcessMemory? Attach(int pid)` (null if open fails); `long BaseAddress`; `byte[]? ReadBytes(long address, int count)`; `int? ReadInt32(long address)`; `long? ReadInt64(long address)`; `byte[]? ReadMainModule()`. Opens the process with `PROCESS_VM_READ | PROCESS_QUERY_INFORMATION` **only** — read-only by construction.

- [ ] **Step 1: Write failing tests (scanner only — ProcessMemory is exercised live in Task 12)**

```csharp
// src/DSLaughTrack.Tests/AobScannerTests.cs
using DSLaughTrack.Memory;
using Xunit;

namespace DSLaughTrack.Tests;

public class AobScannerTests
{
    private static readonly byte[] Hay = { 0x00, 0x48, 0x8B, 0x0D, 0xAA, 0xBB, 0xCC, 0xDD, 0x0F, 0x28 };

    [Fact]
    public void ExactMatch_ReturnsIndex() =>
        Assert.Equal(1, AobScanner.Find(Hay, "48 8b 0d aa bb cc dd"));

    [Fact]
    public void Wildcards_Match() =>
        Assert.Equal(1, AobScanner.Find(Hay, "48 8b 0d ? ? ? ? 0f"));

    [Fact]
    public void NoMatch_ReturnsMinusOne() =>
        Assert.Equal(-1, AobScanner.Find(Hay, "ff ff ff"));

    [Fact]
    public void MatchAtEnd_Found() =>
        Assert.Equal(8, AobScanner.Find(Hay, "0f 28"));
}
```

- [ ] **Step 2: Run tests, expect compile failure**

Run: `dotnet test`
Expected: FAIL.

- [ ] **Step 3: Implement**

```csharp
// src/DSLaughTrack/Memory/AobScanner.cs
namespace DSLaughTrack.Memory;

public static class AobScanner
{
    public static int Find(byte[] haystack, string pattern)
    {
        var parts = pattern.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var needle = new int[parts.Length];
        for (var i = 0; i < parts.Length; i++)
            needle[i] = parts[i] is "?" or "??" ? -1 : Convert.ToInt32(parts[i], 16);

        for (var i = 0; i <= haystack.Length - needle.Length; i++)
        {
            var match = true;
            for (var j = 0; j < needle.Length; j++)
            {
                if (needle[j] != -1 && haystack[i + j] != needle[j]) { match = false; break; }
            }
            if (match) return i;
        }
        return -1;
    }
}
```

```csharp
// src/DSLaughTrack/Memory/ProcessMemory.cs
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace DSLaughTrack.Memory;

/// Read-only process memory access. Opened with VM_READ|QUERY_INFORMATION only;
/// this class has no write capability by construction.
public sealed class ProcessMemory : IDisposable
{
    private const uint ProcessVmRead = 0x0010;
    private const uint ProcessQueryInformation = 0x0400;

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern IntPtr OpenProcess(uint access, bool inheritHandle, int pid);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool ReadProcessMemory(IntPtr handle, IntPtr address, byte[] buffer, int size, out IntPtr bytesRead);

    [DllImport("kernel32.dll")]
    private static extern bool CloseHandle(IntPtr handle);

    private readonly IntPtr _handle;
    public long BaseAddress { get; }
    public int MainModuleSize { get; }

    private ProcessMemory(IntPtr handle, long baseAddress, int mainModuleSize)
    {
        _handle = handle;
        BaseAddress = baseAddress;
        MainModuleSize = mainModuleSize;
    }

    public static ProcessMemory? Attach(int pid)
    {
        try
        {
            using var process = Process.GetProcessById(pid);
            var module = process.MainModule;
            if (module is null) return null;
            var handle = OpenProcess(ProcessVmRead | ProcessQueryInformation, false, pid);
            if (handle == IntPtr.Zero) return null;
            return new ProcessMemory(handle, module.BaseAddress.ToInt64(), module.ModuleMemorySize);
        }
        catch
        {
            return null;
        }
    }

    public byte[]? ReadBytes(long address, int count)
    {
        var buffer = new byte[count];
        if (!ReadProcessMemory(_handle, new IntPtr(address), buffer, count, out var read) ||
            read.ToInt64() != count)
            return null;
        return buffer;
    }

    public int? ReadInt32(long address)
    {
        var b = ReadBytes(address, 4);
        return b is null ? null : BitConverter.ToInt32(b, 0);
    }

    public long? ReadInt64(long address)
    {
        var b = ReadBytes(address, 8);
        return b is null ? null : BitConverter.ToInt64(b, 0);
    }

    public byte[]? ReadMainModule() => ReadBytes(BaseAddress, MainModuleSize);

    public void Dispose()
    {
        if (_handle != IntPtr.Zero) CloseHandle(_handle);
    }
}
```

- [ ] **Step 4: Run tests, expect pass**

Run: `dotnet test`
Expected: PASS.

- [ ] **Step 5: Commit**

```bash
git add -A
git commit -m "feat: AOB scanner and read-only process memory wrapper"
```

---

### Task 9: DsrPointers — cited pointer chains

This task ports pointer chains from open source. **Nothing may be typed from memory or this plan's summaries — every constant is copied from the fetched source files and cited.**

**Files:**
- Create: `src/DSLaughTrack/Memory/DsrPointers.cs`
- Test: `src/DSLaughTrack.Tests/DsrPointersTests.cs` (pattern-format sanity only; live validation is Task 12)

**Interfaces:**
- Consumes: `ProcessMemory`, `AobScanner`, `Log`.
- Produces: `DsrPointers` with:
  - `bool TryResolve(ProcessMemory mem, Log log)` — scans the main module once, resolves base pointers; false (with specific log lines) on failure.
  - `int? ReadHp(ProcessMemory mem)` — used ONLY for cross-checking against SoulMemory's `GetPlayerHealth()` in `--status` (Task 12).
  - `int? ReadStamina(ProcessMemory mem)`, `int? ReadMaxStamina(ProcessMemory mem)`.
  - `long? PlayerInsAddress(ProcessMemory mem)` — exposed for the `--diff` discovery tool.
  - `int? ReadAnimationId(ProcessMemory mem)` — returns null until Step 2's investigation lands a cited or discovered chain; the method must exist so `DsrGameStateSource` compiles.

- [ ] **Step 1: Fetch the authoritative sources**

Fetch and save locally for reference (do not commit):
- `https://raw.githubusercontent.com/FrankvdStam/SoulSplitter/<tag-for-1.8.5>/src/SoulMemory/DarkSouls1/Remastered.cs` — find the release tag matching NuGet 1.8.5 via the repo's releases page. From it, copy verbatim: the `WorldChrManImp` AOB pattern, how the relative address is resolved (instruction offsets), and the full chain from `WorldChrManImp` to `_playerIns` including the version-dependent `_playerCtrlOffset` values and health offset.
- `https://raw.githubusercontent.com/JKAnderson/DSR-Gadget/master/DSR-Gadget/DSROffsets.cs` — copy verbatim the `ChrData1` enum values (`Health`, `MaxHealth`, `Stamina`, `MaxStamina`) and any `ChrData1Boost` offsets, plus `WorldChrBaseAOB` and its resolution.

Known from research (verify against fetched source, then cite file+line in code comments):
- SoulMemory `WorldChrManImp` pattern: `48 8b 0d ? ? ? ? 0f 28 f1 48 85 c9 74 ? 48 89 7c`, resolved at instruction offsets (3, 7).
- SoulMemory reads player health at `_playerIns + 0x3e8`; `_playerCtrlOffset` is `0x68` (V1.03+) / `0x48` (V1.01).
- DSR-Gadget `ChrData1`: `Health = 0x3D8`, `MaxHealth = 0x3DC`, `Stamina = 0x3E8`, `MaxStamina = 0x3EC` — i.e. **stamina = health + 0x10, max stamina = health + 0x14 within the same struct**. Therefore the stamina candidate is `_playerIns + 0x3f8` (health 0x3e8 + 0x10). This is a *derived candidate*: mark it `// CANDIDATE — verified live in Task 12` until Task 12 confirms it.

- [ ] **Step 2: Investigate animation-ID source**

In order, stop at the first hit:
1. Search the fetched SoulSplitter repo (all of `src/SoulMemory/DarkSouls1/`) for animation reads (`grep -ri "anim" …`). PTDE support or DS1 splits may read a player animation ID; if a Remastered chain exists, port it verbatim with citation.
2. Read the full fetched `DSROffsets.cs` + `DSRHook.cs` for an animation-ID (not AnimSpeed) field reachable from a cited chain (`ChrMapData → ChrAnimData` region). Port only what is actually in the source.
3. If neither source provides one, `ReadAnimationId` stays `return null;` with a comment: `// No open-source pointer found (searched SoulSplitter <tag>, DSR-Gadget master on <date>). Discovery via --diff (Task 13).` Animation triggers then stay disabled until Task 13 discovers a chain empirically — that is an acceptable, documented outcome for this task.

- [ ] **Step 3: Write the sanity test**

```csharp
// src/DSLaughTrack.Tests/DsrPointersTests.cs
using DSLaughTrack.Memory;
using Xunit;

namespace DSLaughTrack.Tests;

public class DsrPointersTests
{
    [Fact]
    public void AobPatterns_AreParseable()
    {
        // Every registered pattern must be scannable (valid hex / wildcards).
        foreach (var pattern in DsrPointers.AllPatterns)
            Assert.Equal(-1, AobScanner.Find(new byte[8], pattern)); // no throw, no match on empty
    }
}
```

- [ ] **Step 4: Implement DsrPointers**

Shape (constants filled from Step 1's fetched sources — replace every `FILL_FROM_SOURCE` before committing; `grep -rn FILL_FROM_SOURCE src/` must return nothing):

```csharp
// src/DSLaughTrack/Memory/DsrPointers.cs
using DSLaughTrack.Logging;

namespace DSLaughTrack.Memory;

/// Pointer chains for values SoulMemory's public API does not expose.
/// PROVENANCE RULE: every constant below cites the open-source file it was copied
/// from, or the discovery record in animation_ids.json / VERIFICATION.md.
public sealed class DsrPointers
{
    // Source: SoulSplitter <tag>/src/SoulMemory/DarkSouls1/Remastered.cs ("WorldChrManImp" TreeBuilder entry)
    private const string WorldChrManImpAob = "48 8b 0d ? ? ? ? 0f 28 f1 48 85 c9 74 ? 48 89 7c";
    // Source: same file — relative address resolution offsets for this pattern.
    private const int AobAddressOffset = 3;
    private const int AobInstructionLength = 7;
    // Source: SoulSplitter <tag> Remastered.cs — player pointer chain offsets (copy verbatim).
    private const int PlayerCtrlOffset = FILL_FROM_SOURCE;
    // Source: SoulSplitter <tag> Remastered.cs GetPlayerHealth reads _playerIns + 0x3e8.
    private const int HealthOffset = 0x3e8;
    // CANDIDATE — derived: DSR-Gadget DSROffsets.cs ChrData1 has Stamina = Health + 0x10,
    // MaxStamina = Health + 0x14. Verified live in Task 12 (VERIFICATION.md).
    private const int StaminaOffset = HealthOffset + 0x10;
    private const int MaxStaminaOffset = HealthOffset + 0x14;

    public static readonly string[] AllPatterns = { WorldChrManImpAob };

    private long _worldChrManImp;

    public bool TryResolve(ProcessMemory mem, Log log)
    {
        var module = mem.ReadMainModule();
        if (module is null) { log.Error("DsrPointers: could not read main module for AOB scan"); return false; }
        var idx = AobScanner.Find(module, WorldChrManImpAob);
        if (idx < 0) { log.Error("DsrPointers: WorldChrManImp AOB not found — game version may be unsupported; stamina/animation triggers disabled"); return false; }
        var instructionAddress = mem.BaseAddress + idx;
        var relative = mem.ReadInt32(instructionAddress + AobAddressOffset);
        if (relative is null) { log.Error("DsrPointers: failed to resolve WorldChrManImp relative address"); return false; }
        _worldChrManImp = instructionAddress + relative.Value + AobInstructionLength;
        log.Debug($"DsrPointers: WorldChrManImp at 0x{_worldChrManImp:X}");
        return true;
    }

    public long? PlayerInsAddress(ProcessMemory mem)
    {
        if (_worldChrManImp == 0) return null;
        // Chain copied verbatim from SoulSplitter <tag> Remastered.cs InitPointers (_playerIns).
        var chrMan = mem.ReadInt64(_worldChrManImp);
        if (chrMan is null or 0) return null;
        var playerIns = mem.ReadInt64(chrMan.Value + PlayerCtrlOffset);
        if (playerIns is null or 0) return null;
        return playerIns.Value;
    }

    public int? ReadHp(ProcessMemory mem) => ReadPlayerInt(mem, HealthOffset);
    public int? ReadStamina(ProcessMemory mem) => ReadPlayerInt(mem, StaminaOffset);
    public int? ReadMaxStamina(ProcessMemory mem) => ReadPlayerInt(mem, MaxStaminaOffset);

    public int? ReadAnimationId(ProcessMemory mem)
    {
        // Filled by Step 2 investigation if a cited source exists, else by Task 13 discovery.
        return null;
    }

    private int? ReadPlayerInt(ProcessMemory mem, int offset)
    {
        var player = PlayerInsAddress(mem);
        return player is null ? null : mem.ReadInt32(player.Value + offset);
    }
}
```

**Important:** the `PlayerInsAddress` chain above is a *shape*. The real chain (how many derefs, which offsets, version handling) must match the fetched `Remastered.cs` exactly. If SoulMemory's chain differs from this two-step shape, rewrite the method to match the source — the source wins over this plan.

- [ ] **Step 5: Run tests, expect pass; grep for sentinel**

Run: `dotnet test` → PASS.
Run: `grep -rn FILL_FROM_SOURCE src/` → no output.

- [ ] **Step 6: Commit**

```bash
git add -A
git commit -m "feat: cited DSR pointer chains for stamina (candidate) and HP cross-check"
```

---

### Task 10: DsrGameStateSource

**Files:**
- Create: `src/DSLaughTrack/IGameStateSource.cs`, `src/DSLaughTrack/DsrGameStateSource.cs`

**Interfaces:**
- Consumes: SoulMemory `Remastered` (`TryRefresh()`, `IsPlayerLoaded()`, `GetPlayerHealth()`, `GetAttribute(Attribute.Dexterity)`, `GetInventory()`, `GetProcess()`), `ProcessMemory`, `DsrPointers`.
- Produces:
  - `IGameStateSource { GameState Read(); }`
  - `DsrGameStateSource(Log log) : IGameStateSource, IDisposable` — attaches lazily; exposes `Remastered Game` and `ProcessMemory? Memory`, `DsrPointers Pointers` (for `--status`/`--diff`).

Behavior:
- Each `Read()`: `TryRefresh()`; on error → `GameState.Detached(now)` (and drop our `ProcessMemory` if the process died).
- On first successful refresh (or new PID): `ProcessMemory.Attach(pid)` + `Pointers.TryResolve` once; a failed resolve logs once and leaves stamina/anim null thereafter (basic triggers still work — HP comes from SoulMemory, not DsrPointers).
- `InGame` = refresh ok && `IsPlayerLoaded()`.
- Wrap each SoulMemory read in try/catch → null field on exception (log at Debug, once per field per attach to avoid spam).
- Estus count: `GetInventory()` is comparatively expensive — cache and refresh at most 4×/second. Find the Estus Flask entry by inspecting SoulMemory's `Item`/`ItemReader` API at implementation time (the DS1 `Item` model in the pinned package defines how items are identified — use its own Estus Flask definition if present; otherwise match by the item name string SoulMemory produces). Cite what you used in a comment. The count must reflect *current uses remaining*, verify in Task 12 by drinking one charge. If the API can't distinguish uses-remaining, set EstusCount = null and log that `emptyEstus` is unavailable — do not approximate.

- [ ] **Step 1: Implement (no unit test — this class is a thin adapter over live APIs; it is exercised by `--status` in Task 12)**

```csharp
// src/DSLaughTrack/IGameStateSource.cs
namespace DSLaughTrack;

public interface IGameStateSource
{
    GameState Read();
}
```

```csharp
// src/DSLaughTrack/DsrGameStateSource.cs
using DSLaughTrack.Logging;
using DSLaughTrack.Memory;
using SoulMemory.DarkSouls1;

namespace DSLaughTrack;

public sealed class DsrGameStateSource : IGameStateSource, IDisposable
{
    private readonly Log _log;
    private ProcessMemory? _memory;
    private int _attachedPid;
    private bool _pointersOk;
    private int? _cachedEstus;
    private DateTimeOffset _estusReadAt = DateTimeOffset.MinValue;
    private readonly HashSet<string> _warned = new();

    public Remastered Game { get; } = new();
    public DsrPointers Pointers { get; } = new();
    public ProcessMemory? Memory => _memory;

    public DsrGameStateSource(Log log) => _log = log;

    public GameState Read()
    {
        var now = DateTimeOffset.UtcNow;
        var refresh = Game.TryRefresh();
        if (refresh.IsErr)
        {
            Detach();
            return GameState.Detached(now);
        }
        EnsureAttached();

        var loaded = Try(() => Game.IsPlayerLoaded(), "IsPlayerLoaded") ?? false;
        if (!loaded)
            return new GameState(now, true, false, null, null, null, null, null, null);

        var hp = Try(() => (int?)Game.GetPlayerHealth(), "GetPlayerHealth");
        var dex = Try(() => (int?)Game.GetAttribute(SoulMemory.DarkSouls1.Attribute.Dexterity), "GetAttribute(Dex)");
        int? stamina = null, maxStamina = null, anim = null;
        if (_pointersOk && _memory is not null)
        {
            stamina = Pointers.ReadStamina(_memory);
            maxStamina = Pointers.ReadMaxStamina(_memory);
            anim = Pointers.ReadAnimationId(_memory);
        }
        var estus = ReadEstusCached(now);

        // MaxHp stays null: unused by triggers, no cited source needed for v1.
        return new GameState(now, true, true, hp, null, stamina, dex, estus, anim);
    }

    private void EnsureAttached()
    {
        var process = Game.GetProcess();
        if (process is null) return;
        if (_memory is not null && process.Id == _attachedPid) return;
        Detach();
        _memory = ProcessMemory.Attach(process.Id);
        _attachedPid = process.Id;
        if (_memory is null)
        {
            _log.Error("could not open read-only handle to game process; stamina/animation triggers unavailable");
            return;
        }
        _pointersOk = Pointers.TryResolve(_memory, _log);
        _log.Info($"attached to DarkSoulsRemastered.exe (pid {process.Id}); extended pointers {(_pointersOk ? "resolved" : "UNAVAILABLE")}");
    }

    private void Detach()
    {
        _memory?.Dispose();
        _memory = null;
        _attachedPid = 0;
        _pointersOk = false;
        _cachedEstus = null;
        _warned.Clear();
    }

    private int? ReadEstusCached(DateTimeOffset now)
    {
        if (now - _estusReadAt < TimeSpan.FromMilliseconds(250)) return _cachedEstus;
        _estusReadAt = now;
        _cachedEstus = Try(ReadEstus, "GetInventory(estus)");
        return _cachedEstus;
    }

    private int? ReadEstus()
    {
        // Implementation note: identify the Estus Flask via the pinned SoulMemory 1.8.5
        // DS1 Item model — inspect SoulMemory.DarkSouls1.Item and GetInventory() results
        // at implementation time and cite the mechanism here. Must return USES REMAINING
        // (verified in Task 12) or null if that is not determinable.
        var inventory = Game.GetInventory();
        throw new NotImplementedException("replace with pinned-API-verified lookup during implementation");
    }

    private T? Try<T>(Func<T?> read, string what)
    {
        try { return read(); }
        catch (Exception ex)
        {
            if (_warned.Add(what)) _log.Debug($"{what} failed: {ex.Message}");
            return default;
        }
    }

    public void Dispose()
    {
        Detach();
        Game.GetProcess()?.Dispose();
    }
}
```

**Adjust to reality:** exact SoulMemory names (`ResultErr.IsErr`, `Attribute` enum member, `Item` shape, whether `GameState` needs `MaxHp` init syntax fixed) must be corrected against the actual pinned package during implementation — compile errors here mean *fix against the package*, not guess. Replace the `NotImplementedException` before commit (sentinel rule applies).

- [ ] **Step 2: Build**

Run: `dotnet build`
Expected: success, zero warnings about missing members (all names verified against the pinned package).

- [ ] **Step 3: Commit**

```bash
git add -A
git commit -m "feat: live game-state source bridging SoulMemory and cited pointers"
```

---

### Task 11: Program main loop, CLI modes, default assets

**Files:**
- Create/Modify: `src/DSLaughTrack/Program.cs`
- Create: `config.json`, `animation_ids.json`, `sounds/README.txt`, `README.md` (initial), `VERIFICATION.md` (initial, all rows "NOT VERIFIED")

**Interfaces:**
- Consumes: everything above.
- Produces: `DSLaughTrack.exe` with modes:
  - default: run the laugh track loop
  - `--monitor`: also print every observed field change (for discovery/verification)
  - `--status`: one-shot dump of all reads incl. HP cross-check (`SoulMemory HP` vs `DsrPointers HP`), then exit
  - `--diff <hexOffset?>`: interactive struct-diff for discovery (Task 13); no arg = diff over PlayerIns block `0x0..0x2000`; with arg = follow the pointer at `PlayerIns+offset` and diff that block

- [ ] **Step 1: Implement Program.cs**

```csharp
// src/DSLaughTrack/Program.cs
using DSLaughTrack;
using DSLaughTrack.Audio;
using DSLaughTrack.Config;
using DSLaughTrack.Logging;
using DSLaughTrack.Triggers;

var baseDir = AppContext.BaseDirectory;
var configPath = Path.Combine(baseDir, "config.json");
var animPath = Path.Combine(baseDir, "animation_ids.json");
var soundsRoot = Path.Combine(baseDir, "sounds");
var logPath = Path.Combine(baseDir, "logs", $"dslaughtrack-{DateTime.Now:yyyyMMdd-HHmmss}.log");

var log = new Log(logPath);
var config = SafeLoadConfig(configPath, new AppConfig(), log);
log.MinLevel = config.LogLevel.Equals("debug", StringComparison.OrdinalIgnoreCase) ? LogLevel.Debug : LogLevel.Info;
var configWriteTime = FileTime(configPath);

log.Info("Dark Souls Laugh Track — offline single-player companion app. READ-ONLY: never writes game memory.");

var mode = args.FirstOrDefault() ?? "";
using var source = new DsrGameStateSource(log);

if (mode == "--status") { RunStatus(source, log); return; }
if (mode == "--diff") { RunDiff(source, log, args.Skip(1).FirstOrDefault()); return; }
var monitor = mode == "--monitor";

var ids = AnimationIds.Load(animPath, log);
var triggers = TriggerFactory.Build(ids, log);
var engine = new TriggerEngine(triggers, () => config);
var player = new LaughPlayer(soundsRoot, () => config, log);

log.Info($"{triggers.Count} trigger(s) active: {string.Join(", ", triggers.Select(t => t.Key))}");

var prev = GameState.Detached(DateTimeOffset.UtcNow);
var wasAttached = false;
while (true)
{
    var curr = source.Read();

    if (curr.ProcessAttached && !wasAttached) log.Info("game detected");
    if (!curr.ProcessAttached && wasAttached) log.Info("game exited — waiting for it to start again");
    wasAttached = curr.ProcessAttached;

    if (monitor) PrintChanges(prev, curr, log);

    foreach (var key in engine.Tick(prev, curr))
        player.Play(key);

    prev = curr;

    // config hot reload (checked ~1x/sec)
    if (DateTimeOffset.UtcNow.Second != _lastReloadSecond)
    {
        _lastReloadSecond = DateTimeOffset.UtcNow.Second;
        var wt = FileTime(configPath);
        if (wt != configWriteTime)
        {
            configWriteTime = wt;
            config = SafeLoadConfig(configPath, config, log);
            log.Info("config.json reloaded");
        }
    }

    Thread.Sleep(curr.ProcessAttached ? Math.Max(5, 1000 / Math.Max(1, config.PollHz)) : 2000);
}

static AppConfig SafeLoadConfig(string path, AppConfig lastGood, Log log)
{
    try { return AppConfig.Load(path); }
    catch (Exception ex)
    {
        log.Error($"config.json invalid, keeping previous settings: {ex.Message}");
        return lastGood;
    }
}

static DateTime FileTime(string path) =>
    File.Exists(path) ? File.GetLastWriteTimeUtc(path) : DateTime.MinValue;

static void PrintChanges(GameState prev, GameState curr, Log log)
{
    void Diff<T>(string name, T? a, T? b) where T : struct, IEquatable<T>
    {
        if (!Nullable.Equals(a, b)) log.Info($"MONITOR {name}: {Fmt(a)} -> {Fmt(b)}");
    }
    static string Fmt<T>(T? v) where T : struct => v is null ? "n/a" : v.ToString()!;
    if (prev.InGame != curr.InGame) log.Info($"MONITOR InGame: {prev.InGame} -> {curr.InGame}");
    Diff("HP", prev.Hp, curr.Hp);
    Diff("Stamina", prev.Stamina, curr.Stamina);
    Diff("Dex", prev.Dexterity, curr.Dexterity);
    Diff("Estus", prev.EstusCount, curr.EstusCount);
    Diff("Anim", prev.AnimationId, curr.AnimationId);
}
```

Also implement in the same file (local static functions or a small `DiagnosticModes` class in `Program.cs`):

```csharp
static void RunStatus(DsrGameStateSource source, Log log)
{
    var state = source.Read();
    log.Info($"attached={state.ProcessAttached} inGame={state.InGame}");
    log.Info($"HP(SoulMemory)={state.Hp?.ToString() ?? "n/a"}");
    if (source.Memory is { } mem)
    {
        log.Info($"HP(DsrPointers)={source.Pointers.ReadHp(mem)?.ToString() ?? "n/a"}  <- must equal HP(SoulMemory)");
        log.Info($"Stamina={source.Pointers.ReadStamina(mem)?.ToString() ?? "n/a"} / Max={source.Pointers.ReadMaxStamina(mem)?.ToString() ?? "n/a"}  (CANDIDATE until verified)");
        log.Info($"AnimationId={source.Pointers.ReadAnimationId(mem)?.ToString() ?? "n/a (no verified pointer)"}");
    }
    log.Info($"Dex={state.Dexterity?.ToString() ?? "n/a"}  Estus={state.EstusCount?.ToString() ?? "n/a"}");
}

static void RunDiff(DsrGameStateSource source, Log log, string? hexOffset)
{
    // Discovery tool: snapshot a block of int32s, user performs an action, snapshot again,
    // print changed offsets. Used in Task 13 to find the animation-ID field empirically.
    source.Read();
    if (source.Memory is not { } mem || source.Pointers.PlayerInsAddress(mem) is not { } player)
    {
        log.Error("game/player not available — start the game and load a character first");
        return;
    }
    long blockBase = player;
    if (hexOffset is not null)
    {
        var follow = mem.ReadInt64(player + Convert.ToInt32(hexOffset, 16));
        if (follow is null or 0) { log.Error("pointer at that offset is null"); return; }
        blockBase = follow.Value;
    }
    const int blockSize = 0x2000;
    log.Info($"diffing 0x{blockSize:X} bytes at 0x{blockBase:X}. Press Enter to take snapshot A...");
    Console.ReadLine();
    var a = mem.ReadBytes(blockBase, blockSize);
    log.Info("Snapshot A taken. Perform the action, then press Enter for snapshot B...");
    Console.ReadLine();
    var b = mem.ReadBytes(blockBase, blockSize);
    if (a is null || b is null) { log.Error("read failed"); return; }
    for (var off = 0; off + 4 <= blockSize; off += 4)
    {
        var va = BitConverter.ToInt32(a, off);
        var vb = BitConverter.ToInt32(b, off);
        if (va != vb) log.Info($"  +0x{off:X4}: {va} -> {vb}");
    }
    log.Info("diff complete");
}
```

(`_lastReloadSecond` is a local `int` declared with the other loop state; adjust top-level-statement syntax as needed to compile — e.g. plain local variable, not a field.)

- [ ] **Step 2: Create default assets**

`config.json` (repo root; copied next to exe on publish — add `<None Include>` copy-to-output entries in the csproj for `config.json`, `animation_ids.json`, and the `sounds/` folder, `PreserveNewest`):

```json
{
  "masterVolume": 0.8,
  "globalCooldownSeconds": 2.0,
  "pollHz": 30,
  "logLevel": "info",
  "triggers": {
    "outOfStamina": { "enabled": true,  "volume": 0.7, "cooldownSeconds": 10, "sounds": [] },
    "tookDamage":   { "enabled": true,  "volume": 0.5, "cooldownSeconds": 8,  "sounds": [] },
    "dexIncrease":  { "enabled": true,  "volume": 0.8, "cooldownSeconds": 3,  "sounds": [] },
    "death":        { "enabled": true,  "volume": 1.0, "cooldownSeconds": 5,  "sounds": [] },
    "emptyEstus":   { "enabled": true,  "volume": 0.8, "cooldownSeconds": 5,  "sounds": [] },
    "runningJump":  { "enabled": true,  "volume": 0.7, "cooldownSeconds": 6,  "sounds": [] },
    "gotParried":   { "enabled": true,  "volume": 0.9, "cooldownSeconds": 5,  "sounds": [] },
    "failedParry":  { "enabled": false, "volume": 0.7, "cooldownSeconds": 5,  "sounds": [] },
    "hitWall":      { "enabled": false, "volume": 0.6, "cooldownSeconds": 10, "sounds": [] }
  }
}
```

(`failedParry` and `hitWall` ship disabled — experimental per spec.)

`animation_ids.json` (no invented values — empty until discovery):

```json
{
  "_comment": "Discovered animation IDs. Every entry MUST record provenance. Entries without a 'value' are documented gaps, not usable IDs.",
  "ids": {
    "runningJump":  { "notes": "not yet discovered — run Task 13 discovery session" },
    "gotParried":   { "notes": "not yet discovered" },
    "parryAttempt": { "notes": "not yet discovered" },
    "parrySuccess": { "notes": "not yet discovered" },
    "emptyEstus":   { "notes": "not yet discovered" },
    "hitWall":      { "notes": "not yet discovered; may be infeasible (illusory-wall distinction is out of scope)" }
  }
}
```

`sounds/README.txt`: explain the 9 folder names, that users drop `.wav`/`.mp3` files in, and that no audio ships with the repo. Create the 9 empty folders with `.gitkeep` files.

`VERIFICATION.md` initial content — a table of all 9 triggers with columns: Trigger, Detection method, Status (`NOT VERIFIED` initially for all), Verified on (date/game version), Notes.

`README.md` initial content: what the mod is, safety statement (external read-only tool, offline use, no injection/writes; not intended for online play), install (dotnet publish or `dotnet run`), config reference (every key in config.json), how discovery works, troubleshooting (game not found, pointers unresolved), license (GPLv3, SoulMemory dependency).

- [ ] **Step 3: Build and smoke-run without game**

Run: `dotnet run --project src/DSLaughTrack`
Expected: starts, logs the read-only banner, logs trigger list (4 basic active; 5 animation triggers disabled with named warnings), then quietly waits for the game (2 s poll). Ctrl+C to exit.

- [ ] **Step 4: Run full test suite**

Run: `dotnet test`
Expected: all tests pass.

- [ ] **Step 5: Commit**

```bash
git add -A
git commit -m "feat: main loop with monitor/status/diff modes and default assets"
```

---

### Task 12: Live verification session A (game required, user participates)

**Files:**
- Modify: `VERIFICATION.md`, possibly `src/DSLaughTrack/Memory/DsrPointers.cs` (if the stamina candidate is wrong)

This task needs the game running and the user at the controls. Coordinate with the user; the checklist is executed together.

- [ ] **Step 1: `--status` cross-checks (user: launch DSR, load a character, stand at a bonfire)**

Run: `dotnet run --project src/DSLaughTrack -- --status`
Verify and record in VERIFICATION.md:
1. `HP(DsrPointers) == HP(SoulMemory)` — validates our WorldChrManImp→player chain against the library's independent chain.
2. `Stamina/MaxStamina` plausible: matches the visible green bar; user checks MaxStamina against a community endurance table value for their character's Endurance stat.
3. `Dex` matches the equip screen. `Estus` matches the HUD count; user drinks one charge, re-run `--status`, count decreased by exactly 1.

If check 1 or 2 fails: the candidate offsets are wrong → do NOT ship them; set the failing reads to return null with a comment recording the failed verification, disable dependent triggers, note in VERIFICATION.md. (Then debug via superpowers:systematic-debugging as a follow-up — but never leave unverified values live.)

- [ ] **Step 2: Verify the four basic triggers live (user plays; app in default mode with test sounds)**

Put any short `.wav` in each of `sounds/outOfStamina`, `sounds/tookDamage`, `sounds/dexIncrease`, `sounds/death`. User: sprint until stamina hits zero; take a hit; level Dex at a bonfire (also confirms no false fire from merely opening menus); die (also confirm `tookDamage` did NOT fire on the killing blow, and nothing fires during the respawn load).
Expected: one laugh + one `LAUGH [...]` log line per event, cooldowns respected.

- [ ] **Step 3: Record results**

Update `VERIFICATION.md` rows for `outOfStamina`, `tookDamage`, `dexIncrease`, `death` to `VERIFIED` with date + game version (user reads version from the game's title screen). Record the stamina-offset verification outcome explicitly (promote from CANDIDATE, or record failure).

- [ ] **Step 4: Commit**

```bash
git add -A
git commit -m "test: live verification of basic triggers and pointer cross-checks"
```

---

### Task 13: Discovery session B — animation IDs (game required, user participates)

**Files:**
- Modify: `animation_ids.json`, `VERIFICATION.md`, `README.md`, possibly `src/DSLaughTrack/Memory/DsrPointers.cs` (`ReadAnimationId`)

- [ ] **Step 1: Find the animation-ID field (only if Task 9 Step 2 found no cited source)**

With the game running and character loaded:
1. Run `dotnet run --project src/DSLaughTrack -- --diff` — snapshot A while idle, user does a roll, snapshot B. Note offsets that changed.
2. Repeat with different actions (roll vs jump vs attack). A field that takes a distinct stable value per action type and reverts when idle is the animation-ID candidate. If nothing in the PlayerIns block behaves like that, repeat via `--diff <offset>` for pointer-valued offsets observed in the block (start with offsets whose values look like addresses).
3. When found, implement `ReadAnimationId` with the discovered chain and a provenance comment: `// DISCOVERED empirically on <date>, game version <v>, method: --diff sessions; see VERIFICATION.md`. Re-verify with `--monitor`: anim value changes on every action, stable at idle.
4. If no candidate is found after a reasonable session (~30 min), stop: record the negative result in VERIFICATION.md, leave animation triggers disabled, and ship v1 with the four verified triggers. Do not force it.

- [ ] **Step 2: Capture per-trigger animation IDs (user performs, `--monitor` records)**

For each action, user performs it 3+ times; the ID is accepted only if identical every time:
- running jump (sprint + jump) → `runningJump`
- get parried by the Undead Burg spear hollow or any parrying enemy → `gotParried`
- parry attempt in the air (no enemy) → `parryAttempt`
- successful parry → `parrySuccess` (record whether it's distinguishable from a whiff; if not, note that `failedParry` cannot distinguish and stays experimental)
- drink with empty flask → `emptyEstus` (verify: with a full flask the drinking anim differs or the `EstusCount==0` gate blocks it)
- attack a wall → `hitWall` (record whether the bounce anim differs from hitting an enemy shield; if identical or absent, record `hitWall` as infeasible)

Enter each confirmed ID in `animation_ids.json` with value, capturedOn, gameVersion, method, notes.

- [ ] **Step 3: Verify animation triggers end-to-end**

Add test sounds to the corresponding folders; restart the app; user re-performs each action.
Expected: laugh + log line for each enabled trigger; `emptyEstus` does NOT fire when drinking with charges remaining; update VERIFICATION.md per trigger: `VERIFIED` / `EXPERIMENTAL` / `INFEASIBLE (documented)`.

- [ ] **Step 4: Finalize docs**

README.md: update the trigger table to final statuses; document known limitations (illusory walls, failedParry heuristic); add the discovery-session how-to so users on other game versions can re-capture IDs.

- [ ] **Step 5: Final full test run and commit**

Run: `dotnet test` → all pass. Run: `grep -rn FILL_FROM_SOURCE src/` → nothing.

```bash
git add -A
git commit -m "feat: discovered animation IDs with provenance; final verification statuses"
```

---

## Plan Self-Review (completed)

- **Spec coverage:** all 9 v1 triggers have tasks (4 basic in Task 4/12, animation-based in Tasks 5/13); config/hot-reload (2, 11), audio + volumes + cooldowns (6, 7), logging (2), monitor mode (11), safe failure (9, 10, 11), discovery workflow (11 `--diff`, 13), docs + VERIFICATION.md (11, 12, 13), GPLv3 (1). Online triggers correctly absent.
- **Placeholder scan:** the only sentinels are the explicitly-governed `FILL_FROM_SOURCE`/`NotImplementedException` porting markers, each with a grep gate before commit and instructions for where the real value comes from.
- **Type consistency:** `GameState` shape, `States.S(...)` helper, `ITrigger`, `TriggerEngine.Tick`, `AppConfig.For`, `LaughPlayer.ResolveSound`, `DsrPointers` member names checked for consistency across tasks. One known adjust-to-reality zone (SoulMemory API names) is explicitly flagged in Task 10.

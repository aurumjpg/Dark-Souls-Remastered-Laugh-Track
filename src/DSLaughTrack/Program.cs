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
var lastReloadSecond = -1;
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
    if (DateTimeOffset.UtcNow.Second != lastReloadSecond)
    {
        lastReloadSecond = DateTimeOffset.UtcNow.Second;
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

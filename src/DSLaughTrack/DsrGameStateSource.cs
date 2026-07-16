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

        var loaded = Try<bool>(() => Game.IsPlayerLoaded(), "IsPlayerLoaded") ?? false;
        if (!loaded)
            return new GameState(now, true, false, null, null, null, null, null, null);

        var hp = Try(() => (int?)Game.GetPlayerHealth(), "GetPlayerHealth");
        var dex = Try(() => (int?)Game.GetAttribute(SoulMemory.DarkSouls1.Attribute.Dexterity), "GetAttribute(Dex)");
        int? stamina = null, anim = null;
        if (_pointersOk && _memory is not null)
        {
            stamina = Pointers.ReadStamina(_memory);
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
        // Estus mechanism verified by reading SoulSplitter tag 1.8.5
        // src/SoulMemory/DarkSouls1/ItemReader.cs GetCurrentInventoryItems() directly:
        // for Consumables with raw item id in [200, 215], the Estus Flask gets special-cased
        // (id 200/201 = level 0 empty/full, 202/203 = level 1 empty/full, etc. — odd id = full):
        //     instance.Quantity = item % 2 == 0 ? 0 : quantity;
        // with the comment "If the flask is not empty, the amount of charges is stored in the
        // quantity field." So Item.Quantity for the ItemType.EstusFlask entry returned by
        // GetInventory() IS uses-remaining (0 when empty), not a stack count — no approximation
        // needed. If the flask isn't in the inventory yet (not picked up), FirstOrDefault
        // returns null and we correctly report EstusCount = null rather than 0.
        var flask = Game.GetInventory().FirstOrDefault(i => i.ItemType == ItemType.EstusFlask);
        return flask?.Quantity;
    }

    private T? Try<T>(Func<T?> read, string what) where T : struct
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

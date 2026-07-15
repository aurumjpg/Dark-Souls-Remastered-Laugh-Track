using DSLaughTrack.Logging;

namespace DSLaughTrack.Memory;

/// Pointer chains for values SoulMemory's public API does not expose.
/// PROVENANCE RULE: every constant below cites the open-source file it was copied from,
/// or is marked CANDIDATE with its derivation and the task that verifies it live.
public sealed class DsrPointers
{
    // Source: SoulSplitter tag 1.8.5, src/SoulMemory/DarkSouls1/Remastered.cs, GetTreeBuilder():
    //   .ScanRelative("WorldChrManImp", "48 8b 0d ? ? ? ? 0f 28 f1 48 85 c9 74 ? 48 89 7c", 3, 7)
    private const string WorldChrManImpAob = "48 8b 0d ? ? ? ? 0f 28 f1 48 85 c9 74 ? 48 89 7c";

    // Source: same call — ScanRelative(name, pattern, addressOffset, instructionSize) = (3, 7).
    // Resolution formula verified against src/SoulMemory/Memory/MemoryScanner.cs TryScanRelative:
    //   result = baseAddress + scanResult + relativeAddress + instructionSize
    private const int AobAddressOffset = 3;
    private const int AobInstructionLength = 7;

    // Source: Remastered.cs GetTreeBuilder(), same WorldChrManImp node:
    //   .AddPointer(_playerIns, 0, 0x68)
    // Per SoulMemory src/SoulMemory/Memory/Pointer.cs ResolveOffsets(): every offset except the
    // LAST one in a Pointer's Offsets list is dereferenced before the next offset is added. So
    // _playerIns (offsets [0, 0x68]) resolves as: deref the scanned slot once (offset 0) to get
    // the WorldChrManImp instance, then "instance + 0x68" is _playerIns's own base address.
    // GetPlayerHealth() = _playerIns.ReadInt32(0x3e8) then appends 0x3e8, which makes the
    // earlier 0x68 offset no longer last, so it IS dereferenced at read time: the fully expanded
    // read is *(*(scannedSlot) + 0x68) + 0x3e8 (final add, no deref).
    //
    // NOTE: this 0x68 is a literal in the AddPointer(...) call above, distinct from the
    // separately-declared, version-dependent field `_playerCtrlOffset` (0x68 default / 0x48 for
    // V101, Remastered.cs lines 74, 149, 154). Remastered.cs uses `_playerCtrlOffset` only for
    // the *_playerPos* chain (.AddPointer(_playerPos, 0, 0x68, _playerCtrlOffset, 0x28)), one
    // dereference further than _playerIns. We don't need _playerPos, so the offset from the
    // WorldChrManImp instance to _playerIns is the fixed 0x68 below for all DSR versions, per
    // source — not version-dependent.
    private const int PlayerInsOffset = 0x68;

    // Source: Remastered.cs — public int GetPlayerHealth() => _playerIns.ReadInt32(0x3e8);
    private const int HealthOffset = 0x3e8;

    // CANDIDATE — derived, not copied verbatim from a single line: DSR-Gadget DSROffsets.cs
    // enum ChrData1 { ..., Health = 0x3D8, MaxHealth = 0x3DC, Stamina = 0x3E8, MaxStamina = 0x3EC,
    // ... } puts Stamina at Health+0x10 and MaxStamina at Health+0x14 within the same struct.
    // Applying that relative layout to SoulMemory's HealthOffset (0x3e8) gives these candidates.
    // (Cross-check: DSR-Gadget's Health read is ChrData1.Health + Offsets.ChrData1Boost2, and
    // ChrData1Boost2 = 0x10 for the "version > 2" / 1.03 game version in DSROffsets.GetOffsets(),
    // giving an effective Health offset of 0x3D8+0x10 = 0x3E8 — matching SoulMemory's constant
    // exactly for modern game versions, which supports treating DSR-Gadget's ChrData1 struct and
    // SoulMemory's _playerIns as the same struct.) Verified live in Task 12 (VERIFICATION.md)
    // before being trusted for triggers.
    private const int StaminaOffset = HealthOffset + 0x10;
    private const int MaxStaminaOffset = HealthOffset + 0x14;

    // CANDIDATE — animation ID chain. SoulSplitter tag 1.8.5 src/SoulMemory/DarkSouls1/ has no
    // animation-ID read (grepped Remastered.cs and Ptde.cs for "anim": only unrelated
    // substring hits in symbol names like "WorldChrManImp"). Chain instead reconstructed from
    // DSR-Gadget (github.com/JKAnderson/DSR-Gadget, master @ 4c34636694ead6c4812d223ea506d66f343b0d0b,
    // fetched 2026-07-15):
    //   DSROffsets.cs:  enum WorldChrBase { ChrData1 = 0x68, DeathCam = 0x70 }
    //   DSRHook.cs:     ChrData1 = CreateChildPointer(WorldChrBase, (int)DSROffsets.WorldChrBase.ChrData1);
    //                   CurrentAnimStruct = CreateChildPointer(ChrData1, 0x68, 0x48);
    //                   public int CurrentAnim => CurrentAnimStruct.ReadInt32(0x80);
    // PropertyHook semantics (github.com/JKAnderson/PropertyHook, master @
    // 0f50507e9116b241bdaba5911d1fc004aeb7664f, fetched 2026-07-15), PHPointer/PHPointer.cs
    // Resolve(): EVERY entry in a PHPointer's Offsets[] is dereferenced (unlike SoulMemory's
    // Pointer, which skips the last one); only the final Read*(offset) argument is added without
    // a further dereference. So CurrentAnim = *(int32)( *( *(ChrData1) + 0x68 ) + 0x48 + 0x80 ).
    // ChrData1 itself resolves as (WorldChrBase deref'd once) + 0x68 (deref'd again) — the same
    // two-dereference shape as SoulMemory's _playerIns chain above, which is independent
    // cross-project support that both land on the same player-instance struct.
    // CANDIDATE ASSUMPTION (unverified until live-tested): DSR-Gadget's "WorldChrBase" AOB
    // ("48 8B 05 ? ? ? ? 48 8B 48 68 ...") is a different scan pattern from SoulMemory's
    // "WorldChrManImp" AOB above, but both are assumed to resolve to the same underlying global
    // pointer slot, so this chain reuses our already-scanned _worldChrManImp / PlayerInsAddress()
    // instead of re-scanning DSR-Gadget's separate pattern. Verified live in Task 12/13
    // (VERIFICATION.md); until then ReadAnimationId's result should not be trusted for triggers
    // without that confirmation.
    private const int AnimStructOffset1 = 0x68;
    private const int AnimStructOffset2 = 0x48;
    private const int AnimationIdOffset = 0x80;

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
        // Chain copied verbatim (in shape) from SoulSplitter tag 1.8.5 Remastered.cs GetTreeBuilder,
        // WorldChrManImp node: .AddPointer(_playerIns, 0, 0x68) — see PlayerInsOffset comment above.
        var worldChrManImp = mem.ReadInt64(_worldChrManImp);
        if (worldChrManImp is null or 0) return null;
        var playerIns = mem.ReadInt64(worldChrManImp.Value + PlayerInsOffset);
        if (playerIns is null or 0) return null;
        return playerIns.Value;
    }

    public int? ReadHp(ProcessMemory mem) => ReadPlayerInt(mem, HealthOffset);
    public int? ReadStamina(ProcessMemory mem) => ReadPlayerInt(mem, StaminaOffset);
    public int? ReadMaxStamina(ProcessMemory mem) => ReadPlayerInt(mem, MaxStaminaOffset);

    public int? ReadAnimationId(ProcessMemory mem)
    {
        // See the AnimStructOffset1/2 / AnimationIdOffset provenance comment above (DSR-Gadget +
        // PropertyHook derived, CANDIDATE pending live verification).
        var playerIns = PlayerInsAddress(mem);
        if (playerIns is null) return null;
        var animPtr1 = mem.ReadInt64(playerIns.Value + AnimStructOffset1);
        if (animPtr1 is null or 0) return null;
        var animPtr2 = mem.ReadInt64(animPtr1.Value + AnimStructOffset2);
        if (animPtr2 is null or 0) return null;
        return mem.ReadInt32(animPtr2.Value + AnimationIdOffset);
    }

    private int? ReadPlayerInt(ProcessMemory mem, int offset)
    {
        var player = PlayerInsAddress(mem);
        return player is null ? null : mem.ReadInt32(player.Value + offset);
    }
}

Dark Souls Laugh Track — sound files
=====================================

No audio ships with this repository. Drop your own .wav or .mp3 files into
the folder matching the trigger you want to hear, and the app plays a
random file from that folder when the trigger fires.

If a trigger's own folder is empty, the app falls back to the shared
default/ folder. So the simplest setup is: one laugh file in default/,
plus special files only where you want them (e.g. dramatic music in
death/ only).

Folders (one per trigger, names match config.json's "triggers" keys):

  default/       - shared fallback for any trigger without its own files
  outOfStamina/  - stamina bar hits zero
  tookDamage/    - player takes damage and survives
  dexIncrease/   - Dexterity attribute increases (level up)
  death/         - player HP hits zero
  emptyEstus/    - Estus Flask used while already empty (animation-based; discovered, enabled by default)
  runningJump/   - running jump animation plays (animation-based; discovered, enabled by default)
  gotParried/    - player gets parried (animation-based; not yet captured, see animation_ids.json)
  failedParry/   - parry attempted but missed (animation-based; disabled by default, experimental)
  hitWall/       - attack bounces off a wall (animation-based; enabled by default; see VERIFICATION.md caveats)

Alternatively, list explicit file names in a trigger's "sounds" array in
config.json (relative to this sounds/ folder) instead of relying on the
folder scan.

Supported formats: .wav, .mp3

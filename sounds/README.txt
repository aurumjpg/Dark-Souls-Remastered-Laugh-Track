Dark Souls Laugh Track — sound files
=====================================

No audio ships with this repository. Drop your own .wav or .mp3 files into
the folder matching the trigger you want to hear, and the app plays a
random file from that folder when the trigger fires.

Folders (one per trigger, names match config.json's "triggers" keys):

  outOfStamina/  - stamina bar hits zero
  tookDamage/    - player takes damage and survives
  dexIncrease/   - Dexterity attribute increases (level up)
  death/         - player HP hits zero
  emptyEstus/    - Estus Flask used while already empty (animation-based; disabled until discovered)
  runningJump/   - running jump animation plays (animation-based; disabled until discovered)
  gotParried/    - player gets parried (animation-based; disabled until discovered)
  failedParry/   - parry attempted but missed (animation-based; disabled by default, experimental)
  hitWall/       - player runs into a wall (animation-based; disabled by default, experimental)

Alternatively, list explicit file names in a trigger's "sounds" array in
config.json (relative to this sounds/ folder) instead of relying on the
folder scan.

Supported formats: .wav, .mp3

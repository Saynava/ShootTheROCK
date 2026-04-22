# Shoot the ROCK Scene State Snapshot

- Saved local time: 2026-04-22 23:14:33
- Scene: Assets/Scenes/SampleScene.unity
- Unity version: 6000.4.0f1
- Purpose: compact but inspectable current-state snapshot for agent follow-up work
- Detailed canonical diff file: `Assets/_Game/State/ShootTheRockSceneStateSnapshot.flat.txt`
- Human-readable change report: `Assets/_Game/State/ShootTheRockSceneStateChanges.md`
- Project summary: `Assets/_Game/State/ShootTheRockProjectStateSummary.md`
- Restore snapshot used by Restore: `Assets/_Game/State/ShootTheRockRestoreSnapshot.json`

## Snapshot coverage
- Tracked roots: 4
- GameObjects captured: 120
- Components captured: 369
- Serialized property lines captured: 4332
- Total flat records: 5301

## Tracked roots
- `Main Camera`
- `Global Light 2D`
- `ShootTheRockScene`
- `PrototypeMarkers`

## What this is good for
- seeing the current scene / inspector state without opening full scene YAML
- letting git show exact property-level diffs between checkpoints
- providing the saved Restore baseline used for rollback after bad changes

## Limits
- this is a tracked-root snapshot, not the whole project serialized byte-for-byte
- runtime-only non-serialized state does not persist here
- some Unity internal noise properties are intentionally skipped for readability

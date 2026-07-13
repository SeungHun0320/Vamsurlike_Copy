# Performance Optimization Log

## Enemy Swarm Optimization

Context: when enemy count approached roughly 100, frame drops became severe during gameplay. The first pass reduced constant per-enemy server work and network transform traffic. The second pass reduced hit-feedback RPC bursts and allocation-heavy skill queries.

Saved commits:

- `bf03337 Optimize large enemy swarm performance`
- `bdf8e22 Reduce enemy hit feedback overhead`

## Pass 1: Large Enemy Swarm

Files:

- `Assets/Scripts/Enemy/EnemyAI.cs`
- `Assets/Scripts/Network/NetworkVisibilityController.cs`
- `Assets/Prefabs/Enemies/Enemy_A.prefab`
- `Assets/Prefabs/Enemies/Enemy B.prefab`
- `Assets/Prefabs/Enemies/Enemy C.prefab`
- `Assets/Prefabs/Enemies/Enemy D.prefab`
- `Assets/Prefabs/Enemies/Missile Boss.prefab`

Changes:

- Reduced `NavMeshAgent.SetDestination()` frequency.
- Staggered enemy target refresh and path refresh timers with random offsets.
- Disabled `NavMeshAgent.autoRepath` for enemies.
- Replaced repeated attack-state allocation with a shared attack state.
- Changed range checks from `Vector3.Distance()` to squared-distance checks.
- Staggered `NetworkVisibilityController` refresh timers so spawned enemies do not all refresh visibility on the same frame.
- Relaxed enemy `NetworkTransform` thresholds:
  - `UseUnreliableDeltas: 1`
  - `PositionThreshold: 0.05`
  - `RotAngleThreshold: 3`
  - `ScaleThreshold: 0.05`

Expected effect:

- Lower server CPU cost from enemy pathfinding.
- Fewer synchronized transform updates for small enemy movements.
- Fewer periodic visibility-check spikes when many enemies are alive.

Tradeoff:

- Enemy transform sync is slightly less precise, but acceptable for swarm enemies.
- Path updates are less immediate, but should still track players smoothly enough.

## Pass 2: Hit Feedback and Skill Query Cost

Files:

- `Assets/Scripts/Enemy/EnemyAI.cs`
- `Assets/Scripts/Enemy/EnemyNetworkBase.cs`
- `Assets/Scripts/Skills/SkillAreaDamage.cs`
- `Assets/Scripts/Skills/PierceShotgunNetworkSkill.cs`
- `Assets/Scripts/Skills/PiercingBoomerangNetworkSkill.cs`

Changes:

- Throttled enemy animator speed updates:
  - `Animator.SetFloat("Speed")` no longer runs every frame.
  - Speed is updated every `0.1s` and only when the value changes meaningfully.
- Merged enemy hit feedback into one ClientRpc:
  - Damage text
  - Hit spark
  - Hit flash
  - Crit camera shake
- Added per-enemy hit feedback throttling:
  - Normal hit feedback minimum interval: `0.08s`
  - Critical hits and death hits still force feedback.
- Cached hit-flash renderers to avoid repeated `GetComponentsInChildren<Renderer>()`.
- Replaced allocating `Physics.OverlapSphere()` calls with `Physics.OverlapSphereNonAlloc()` in:
  - `SkillAreaDamage`
  - `PierceShotgunNetworkSkill`
  - `PiercingBoomerangNetworkSkill`

Expected effect:

- Fewer ClientRpc calls when many enemies are hit at once.
- Less client-side VFX/text spam during large-area damage.
- Less GC pressure from repeated physics overlap arrays.
- Lower `NetworkAnimator` parameter update pressure.

Tradeoff:

- Very rapid repeated hits may not show every single damage feedback effect.
- Damage calculation itself is unchanged; only visual/audio feedback is throttled.

## Verification

Verified after changes:

- `dotnet build Vamsurlike.sln`
  - Result: error 0
  - Existing warning: no restorable projects found in the Unity solution.
- Unity main editor console
  - Result: error 0

## Remaining Bottleneck Candidates

If frame drops still happen at very high enemy counts, check these next:

- Remove or replace enemy `NetworkAnimator` on simple swarm enemies.
- Limit floating damage text globally per frame.
- Add LOD-style animation culling for off-screen or far enemies.
- Reduce enemy mesh/material complexity or combine repeated renderer parts.
- Add stronger enemy visibility culling or interest management.
- Profile server-only pathfinding cost versus client-side rendering cost separately.
- Consider using simpler movement for fodder enemies instead of full `NavMeshAgent`.


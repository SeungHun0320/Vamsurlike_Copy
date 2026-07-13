# Balance DPS Report

Generated: 2026-07-13

## Assumptions

- Defense, crit, passive multipliers, skill haste, projectile travel miss, overkill, and target uptime are excluded.
- `Single DPS` is rough DPS against one target.
- `Total/Area DPS` is theoretical summed DPS when every projectile/orbital/tick can hit useful targets.
- AoE, piercing, shotgun, and cone skills can exceed listed per-target DPS when multiple enemies are inside the area.
- Piercing boomerang return-stack amplification is excluded from base DPS because it depends on outbound hit count.

## Common Skills

| Skill | Type | Lv | Cooldown | Damage | Key Params | Single DPS | Total/Area DPS | Notes |
|---|---:|---:|---:|---:|---|---:|---:|---|
| Basic Projectile | Projectile | 1 | 0.8 | 16 |  | 20.0 | 20.0 | damage/cooldown |
| Basic Projectile | Projectile | 2 | 0.75 | 16 |  | 21.3 | 21.3 | damage/cooldown |
| Basic Projectile | Projectile | 3 | 0.7 | 16 |  | 22.9 | 22.9 | damage/cooldown |
| Pierce Projectile | Projectile | 1 | 1.6 | 12 |  | 7.50 | 7.50 | damage/cooldown |
| Pierce Projectile | Projectile | 2 | 1.52 | 15 |  | 9.87 | 9.87 | damage/cooldown |
| Damage Aura | AreaAura | 1 | 1.28 | 8 | tick 0.8 | 10.0 | 10.0 | damage/tickInterval |
| Damage Aura | AreaAura | 2 | 1.04 | 10 | tick 0.65 | 15.4 | 15.4 | damage/tickInterval |
| Orbital Blades | Orbital | 1 | 8 | 8 | orb 3, hitCd 0.2 | 40.0 | 120 | damage*orbitalDamageMultiplier*orbitalCount/orbitalHitCooldown |
| Orbital Blades | Orbital | 2 | 8 | 10 | orb 4, hitCd 0.2 | 50.0 | 200 | damage*orbitalDamageMultiplier*orbitalCount/orbitalHitCooldown |
| Orbital Blades | Orbital | 3 | 8 | 12 | orb 5, hitCd 0.2 | 60.0 | 300 | damage*orbitalDamageMultiplier*orbitalCount/orbitalHitCooldown |
| Bullet Storm | Ultimate | 1 | 24 | 22 | proj 15 | 0.92 | 55.0 | damage*waveCount*projectileCount/cooldown |
| 수류탄 | Grenade | 1 | 6.4 | 30 |  | 4.69 | 4.69 | damage/cooldown |
| 수류탄 | Grenade | 2 | 4.8 | 42 |  | 8.75 | 8.75 | damage/cooldown |
| 수류탄 | Grenade | 3 | 4 | 55 |  | 13.8 | 13.8 | damage/cooldown |
| 기관총 | ScatterShot | 1 | 5.6 | 7.5 | bullets 8 | 1.34 | 10.7 | damage*scatterBulletCount/cooldown; burst 1.5s |
| 기관총 | ScatterShot | 2 | 4.8 | 9 | bullets 10 | 1.88 | 18.8 | damage*scatterBulletCount/cooldown; burst 1.8s |
| 기관총 | ScatterShot | 3 | 3.52 | 11 | bullets 12 | 3.13 | 37.5 | damage*scatterBulletCount/cooldown; burst 2.2s |
| 망치 | Melee | 1 | 3.2 | 30 |  | 9.38 | 9.38 | damage/cooldown |
| 망치 | Melee | 2 | 2.88 | 45 |  | 15.6 | 15.6 | damage/cooldown |
| 망치 | Melee | 3 | 2.4 | 60 |  | 25.0 | 25.0 | damage/cooldown |
| 샷건 | Shotgun | 1 | 1.5 | 20 | proj 3, solo 1, shared 0.7 | 13.3 | 9.33 | single: damage*solo/cooldown, multi per-target: damage*shared/cooldown |
| 샷건 | Shotgun | 2 | 1.3 | 20 | proj 3, solo 1, shared 0.7 | 15.4 | 10.8 | single: damage*solo/cooldown, multi per-target: damage*shared/cooldown |

## Combine Skills

| Skill | Type | Lv | Cooldown | Damage | Key Params | Single DPS | Total/Area DPS | Notes |
|---|---:|---:|---:|---:|---|---:|---:|---|
| 클러스터 수류탄 | ClusterGrenade | 1 | 6.4 | 20.4 | cluster 5x0.5 | 3.19 | 11.2 | (damage + damage*clusterDamageRatio*clusterCount)/cooldown |
| 충격 궤도 | OrbitalGrenade | 1 | 3.2 | 13 | orb 5, hitCd 1 | 13.0 | 65.0 | damage*orbitalDamageMultiplier*orbitalCount/orbitalHitCooldown |
| 블랙홀 | BlackHole | 1 | 12.8 | 9.6 | tick 0.4 | 24.0 | 24.0 | damage/tickInterval |
| 관통 산탄 | PierceShotgun | 1 | 2 | 20 |  | 10.0 | 10.0 | per target: damage/cooldown; total scales by enemies in cone |
| 대지분쇄자 | Earthshatter | 1 | 2.24 | 65 | after 3x0.75 | 29.0 | 94.3 | (damage + damage*aftershockDamageRatio*aftershockCount)/cooldown |
| 질풍확산탄 | GaleSpread | 1 | 3.2 | 9 | bullets 22 | 2.81 | 61.9 | damage*scatterBulletCount/cooldown; burst 0.8s |
| 왕복 관통창 | PiercingBoomerang | 1 | 1.6 | 19.8 |  | 12.4 | 12.4 | base damage/cooldown; return-stack amplification excluded; +22% per outbound stack on return |
| 생명흡수탄 | LifeDrainBolt | 1 | 1.5 | 19.8 | orb 3, hitCd 0.2, orbMul 0.7 | 69.3 | 208 | damage*orbitalDamageMultiplier*orbitalCount/orbitalHitCooldown; lifesteal 0.25 |

## 15 HP Fodder Time-To-Kill Snapshot

| Skill | Type | Lv | Damage/Hit | Hits To Kill 15 HP |
|---|---:|---:|---:|---:|
| Basic Projectile | Projectile | 1 | 16.0 | 1 |
| Basic Projectile | Projectile | 2 | 16.0 | 1 |
| Basic Projectile | Projectile | 3 | 16.0 | 1 |
| Pierce Projectile | Projectile | 1 | 12.0 | 2 |
| Pierce Projectile | Projectile | 2 | 15.0 | 1 |
| Damage Aura | AreaAura | 1 | 8.00 | 2 |
| Damage Aura | AreaAura | 2 | 10.0 | 2 |
| Orbital Blades | Orbital | 1 | 8.00 | 2 |
| Orbital Blades | Orbital | 2 | 10.0 | 2 |
| Orbital Blades | Orbital | 3 | 12.0 | 2 |
| Bullet Storm | Ultimate | 1 | 22.0 | 1 |
| 수류탄 | Grenade | 1 | 30.0 | 1 |
| 수류탄 | Grenade | 2 | 42.0 | 1 |
| 수류탄 | Grenade | 3 | 55.0 | 1 |
| 기관총 | ScatterShot | 1 | 7.50 | 2 |
| 기관총 | ScatterShot | 2 | 9.00 | 2 |
| 기관총 | ScatterShot | 3 | 11.0 | 2 |
| 망치 | Melee | 1 | 30.0 | 1 |
| 망치 | Melee | 2 | 45.0 | 1 |
| 망치 | Melee | 3 | 60.0 | 1 |
| 샷건 | Shotgun | 1 | 20.0 | 1 |
| 샷건 | Shotgun | 2 | 20.0 | 1 |

## Quick Ranking By Max Level Total/Area DPS

| Rank | Skill | Type | Category | Max Lv | Single DPS | Total/Area DPS |
|---:|---|---:|---:|---:|---:|---:|
| 1 | Orbital Blades | Orbital | Common | 3 | 60.0 | 300 |
| 2 | 생명흡수탄 | LifeDrainBolt | Combine | 1 | 69.3 | 208 |
| 3 | 대지분쇄자 | Earthshatter | Combine | 1 | 29.0 | 94.3 |
| 4 | 충격 궤도 | OrbitalGrenade | Combine | 1 | 13.0 | 65.0 |
| 5 | 질풍확산탄 | GaleSpread | Combine | 1 | 2.81 | 61.9 |
| 6 | Bullet Storm | Ultimate | Common | 1 | 0.92 | 55.0 |
| 7 | 기관총 | ScatterShot | Common | 3 | 3.13 | 37.5 |
| 8 | 망치 | Melee | Common | 3 | 25.0 | 25.0 |
| 9 | 블랙홀 | BlackHole | Combine | 1 | 24.0 | 24.0 |
| 10 | Basic Projectile | Projectile | Common | 3 | 22.9 | 22.9 |
| 11 | Damage Aura | AreaAura | Common | 2 | 15.4 | 15.4 |
| 12 | 수류탄 | Grenade | Common | 3 | 13.8 | 13.8 |
| 13 | 왕복 관통창 | PiercingBoomerang | Combine | 1 | 12.4 | 12.4 |
| 14 | 클러스터 수류탄 | ClusterGrenade | Combine | 1 | 3.19 | 11.2 |
| 15 | 샷건 | Shotgun | Common | 2 | 15.4 | 10.8 |
| 16 | 관통 산탄 | PierceShotgun | Combine | 1 | 10.0 | 10.0 |
| 17 | Pierce Projectile | Projectile | Common | 2 | 9.87 | 9.87 |

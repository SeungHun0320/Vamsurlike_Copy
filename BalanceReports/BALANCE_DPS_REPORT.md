# 밸런스 DPS 리포트

생성일: 2026-07-10

계산 기준: ScriptableObject/CSV 원본 데이터 기반의 1차 이론값. 실제 명중률, 타겟 수, 오버킬, 이동, 넉백, 치명타, 패시브 배율, 플레이어 수 보정은 제외했다.

## 산출 파일

- `BALANCE_SKILL_DPS.csv`: 스킬 레벨별 이론 DPS
- `BALANCE_ENEMY_TABLE.csv`: 몬스터 기본 스탯과 시간별 HP
- `BALANCE_TTK_MATRIX.csv`: 만렙 스킬 기준 몬스터 처치 시간
- `BALANCE_WAVE_BUDGET.csv`: 웨이브별 총 HP/XP 예산

## 만렙 스킬 DPS 순위

| Rank | Skill | Type | Lv | DPS_Est | BurstPotential | Note |
| --- | --- | --- | --- | --- | --- | --- |
| 1 | 충격 궤도 | OrbitalGrenade | 1 | 504.0 | 504.0 | orbital grenade sustained, ideal contact |
| 2 | 생명흡수탄 | LifeDrainBolt | 1 | 123.8 | 99.0 | life drain bolt: projectile + orbiting satellites rough potential |
| 3 | 대지분쇄자 | Earthshatter | 1 | 94.3 | 211.3 | earthshatter: main + aftershock if same target overlaps |
| 4 | Orbital Blades | Orbital | 3 | 90.0 | 90.0 | orbital sustained per second, ideal contact |
| 5 | 질풍확산탄 | GaleSpread | 1 | 61.9 | 198.0 | gale spread: haste bonus not included |
| 6 | Basic Projectile | Projectile | 5 | 45.0 | 39.6 | projectile: includes pierce potential |
| 7 | Pierce Projectile | Projectile | 2 | 43.4 | 66.0 | projectile: includes pierce potential |
| 8 | Bullet Storm | Ultimate | 1 | 42.0 | 1008.0 | ultimate: full burst averaged over cooldown |
| 9 | 왕복 관통창 | PiercingBoomerang | 1 | 24.8 | 39.6 | boomerang: outbound+return, stack amp not included |
| 10 | 망치 | Melee | 3 | 20.8 | 50.0 | melee box main hit |
| 11 | 기관총 | ScatterShot | 3 | 18.4 | 64.8 | scatter: assumes every bullet hits |
| 12 | 수류탄 | Grenade | 3 | 12.0 | 48.0 | grenade main splash, single target |
| 13 | 클러스터 수류탄 | ClusterGrenade | 1 | 11.2 | 71.4 | cluster: main + sub grenade damage potential |
| 14 | 블랙홀 | BlackHole | 1 | 8.2 | 153.6 | black hole ticks averaged over cooldown+duration |
| 15 | Damage Aura | AreaAura | 2 | 7.1 | 32.4 | aura: one target inside area |
| 16 | 샷건 | Shotgun | 2 | 4.1 | 7.2 | shotgun: current code nearest 1 target |
| 17 | 관통 산탄 | PierceShotgun | 1 | 2.9 | 13.8 | pierce shotgun: per target in cone |

## 몬스터 기준표

| EnemyId | Name | BaseHP | MoveSpeed | AttackPower | AtkInterval | DPS_ToPlayer | XP | HP@0m | HP@3m | HP@6m | HP@9m |
| --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- |
| BossData_A | Boss | 1000.0 | 3.0 | 40.0 | 1.20 | 33.3 | 500 | 1000.0 | 1300.0 | 1750.0 | 1750.0 |
| EnemyData_A | Enemy | 50.0 | 7.0 | 10.0 | 1.00 | 10.0 | 10 | 50.0 | 65.0 | 87.5 | 87.5 |
| EnemyData_B | Scout | 30.0 | 10.0 | 8.0 | 0.80 | 10.0 | 8 | 30.0 | 39.0 | 52.5 | 52.5 |
| EnemyData_C | Brute | 150.0 | 3.0 | 20.0 | 2.00 | 10.0 | 25 | 150.0 | 195.0 | 262.5 | 262.5 |

## 웨이브 예산

| Wave | Duration | Loop | Action | EnemyId | Count | SpawnInterval | SpawnRatePerSec | TotalBaseHP | TotalHP@6m | XPTotal |
| --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- |
| 0 | 20 | FALSE |  | EnemyData_A | 12 | 0.35 | 2.86 | 600.0 | 1050.0 | 120 |
| 1 | 25 | FALSE |  | EnemyData_A | 14 | 0.25 | 4.00 | 700.0 | 1225.0 | 140 |
| 1 | 25 | FALSE |  | EnemyData_B | 10 | 0.25 | 4.00 | 300.0 | 525.0 | 80 |
| 2 | 22 | TRUE |  | EnemyData_B | 16 | 0.2 | 5.00 | 480.0 | 840.0 | 128 |
| 2 | 22 | TRUE |  | EnemyData_C | 10 | 0.35 | 2.86 | 1500.0 | 2625.0 | 250 |
| 3 | 35 | FALSE | SpawnEliteRing | EnemyData_C | 16 | 0 | instant | 2400.0 | 4200.0 | 400 |

## 해석 팁

- `DPS_Est`가 높아도 광역/관통 전제면 실제 단일 보스 DPS와 다를 수 있다.
- 초반 일반몹 TTK가 1초 미만이면 스폰량을 늘리거나 HP를 올려도 된다.
- 웨이브 `TotalHP@6m`이 플레이어 총 DPS보다 너무 낮으면 몬스터가 화면에 쌓이지 않는다.
- 조합 스킬은 즉시 Lv.Max라서 `BALANCE_TTK_MATRIX.csv`의 만렙 기준을 우선 보면 된다.

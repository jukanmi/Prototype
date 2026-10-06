# Stage 리팩토링 계획 (캔버스 #9)

작성 2026-09-06 · 대상 `Assets/Scripts/Stage/` — 30 파일 3,912 줄

> **이 문서의 Arena 계열은 지난 이야기다.** `ArenaDirector` · `ArenaRound` ·
> `ArenaRoundCatalog`는 이후 통합으로 전부 사라졌고, 아레나는 자리가 붙은 조우 한 종류가 됐다.
> 아래 숫자와 표는 2026-09-06 시점의 실측이므로 그대로 둔다 —
> 지금 상태는 [Stage_Encounter_Unification_Plan.md](Stage_Encounter_Unification_Plan.md)를 본다.
상위 문서: [Refactor_Master_Plan.md](Refactor_Master_Plan.md)
전제: **코드량 감소가 목표.** 프리팹·에디터 수작업이 늘어도 상관없다.
프리팹/`.asset`에 붙는 MonoBehaviour·ScriptableObject는 유니티 MonoScript 규칙상
**자기 이름의 파일이 필요하다.**

---

> **파일 수 최다 그룹.** 30개인데 평균 130줄이다. 다만 **5개 계열로 이미
> 깔끔하게 갈려 있고**, 프리팹에 물린 건 6개뿐이다.

## 0. 현황 — 5개 계열

| 계열 | 파일 | 줄 | 구성 |
|---|---:|---:|---|
| **Arena** | 5 | 842 | `ArenaDirector`(323,프리팹4) · `ArenaSpawnPlanner`(203) · `ArenaRound`(113) · `ArenaRoundCatalog`(109) · `ArenaGate`(94,프리팹4) |
| **Spawn/Wave** | 6 | 923 | `EnemySpawnService`(190,프리팹2) · `StageWaveCatalog`(195) · `WaveSpawnPlanner`(168) · `WaveDefinition`(140) · `SpawnEntry`(116) · `EnemySpawnGuard`(114) |
| **Stage** | 5 | 757 | `StageDirector`(330,프리팹1) · `StageRunner`(224,프리팹1) · `StageBounds`(98,프리팹1) · `StageSection`(82) · `StageProgressSource`(23) |
| **Entrance** | 5 | 643 | `EntrancePlayer`(187) · `EntranceGuard`(135) · `EntranceDirector`(132) · `EntranceRules`(105) · `EntranceSpec`(84) |
| **Ground** | 3 | 267 | `GroundRules`(94) · `GroundPlate`(88) · `GroundRegistry`(85) |
| 기타 | 6 | 480 | `AttackTokenPool`(132) · `SpawnTelegraph`(93) · `CameraFrameRules`(81) · `BoundsBlend`(75) · `EnemyLayers`(50) · `RoundClearRules`(49) |

**프리팹에 물린 6개** — `ArenaDirector`(4) · `ArenaGate`(4) · `EnemySpawnService`(2) ·
`StageDirector`(1) · `StageRunner`(1) · `StageBounds`(1). 파일명 고정.

**조립 코드**: `EnemySpawnService` 43줄뿐. UI가 아니라 **적 스폰**이므로
프리팹 이관 대상이 아니다.

## 1. 역할 분리가 이미 옳다

> `StageDirector` — 스테이지 한 판의 **웨이브 진행**. 표(`StageWaveCatalog`)를 읽어
> 때가 되면 적을 소환하고, 다 잡히면 다음 웨이브를 연다.
> **승패는 여기서 판정하지 않는다** — 그건 `BattleSceneController`.

> `ArenaDirector` — 아레나 하나에서 도는 **라운드 사이클**.
> `진입 → 락 → 예고 → 스폰 → 전투 → 클리어 판정 → 정비 → 언락`

**웨이브형(Stage)과 아레나형(Arena)이 다른 진행 방식**이라 둘로 갈린 것이다.
합치면 안 된다.

씬 배치가 이를 확인해 준다 (에디터 실측):

```
StageDirector    Stage_01 · 03 · 04          (웨이브형 3씬)
ArenaDirector    Stage_02 · 05               (아레나형 2씬)
ArenaGate · StageRunner · StageBounds        Stage_02 · 05 — 아레나 전용
EnemySpawnService                            Stage_01~05 전부 — 공용
```

**겹치는 씬이 하나도 없다.**

## 2. 병합 — 30 → 15

계열별로 묶는다. 프리팹 고정 6개는 각자 파일을 유지한다.

```
Stage/
├ ArenaDirector.cs       (프리팹 4 — 고정)                              323
├ ArenaGate.cs           (프리팹 4 — 고정)                               94
├ Arena.cs               ArenaSpawnPlanner + ArenaRound
│                        + ArenaRoundCatalog                            425
├ StageDirector.cs       (프리팹 1 — 고정)                              330
├ StageRunner.cs         (프리팹 1 — 고정)                              224
├ StageBounds.cs         StageBounds(프리팹1 고정) + BoundsBlend         173
├ Stage.cs               StageSection + StageProgressSource
│                        + CameraFrameRules                             186
├ EnemySpawnService.cs   (프리팹 2 — 고정)                              190
├ Wave.cs                StageWaveCatalog + WaveSpawnPlanner
│                        + WaveDefinition + SpawnEntry                  619
├ Spawn.cs               EnemySpawnGuard + SpawnTelegraph
│                        + EnemyLayers + AttackTokenPool                389
├ Entrance.cs            EntrancePlayer + EntranceGuard
│                        + EntranceDirector + EntranceRules
│                        + EntranceSpec                                 643
├ Ground.cs              GroundRules + GroundPlate + GroundRegistry      267
└ RoundClearRules.cs                                                     49
                                                          합계      ~3,912
```

**13 파일.** (표에 13줄 — "30 → 13")

**병합 근거**

- **Entrance 5개 → 1개**: `EntranceDirector`가 `EntranceSpec`을 읽어
  `EntrancePlayer`를 돌리고 `EntranceGuard`가 막는다. 하나의 진입 연출 파이프라인이다.
  프리팹 참조 0.
- **Ground 3개 → 1개**: `GroundRegistry`가 `GroundPlate`를 모으고 `GroundRules`가
  판정한다. 프리팹 참조 0.
- **Wave 4개 → 1개**: 표(`StageWaveCatalog`) · 계획(`WaveSpawnPlanner`) ·
  정의(`WaveDefinition`) · 항목(`SpawnEntry`)이 한 세트.
- **Arena 3개 → 1개**: 프리팹 고정인 `ArenaDirector`·`ArenaGate`를 뺀 나머지.

> `Entrance.cs`가 643줄, `Wave.cs`가 619줄이 된다. 계열이 커서 그렇다.
> **더 잘게 두고 싶으면 Entrance/Wave만 2개씩으로 나눈다 → 30 → 15.**

## 3. 회수할 코드 — 없다

조립 코드가 `EnemySpawnService` 43줄뿐이고 그건 적 스폰이다(UI 아님).
죽은 타입도 없다 — `SpawnTelegraph`는 소비자가 `ArenaDirector` 1개지만 정상이다.

```
Stage/   30 파일 3,912 줄  →  13 파일 ~3,912 줄   (0)
```

**파일 수만 준다.** 이 그룹은 코드 감소 대상이 아니다.

## 4. 우선순위 — 낮다

줄이 안 줄고, 프리팹 고정 6개 때문에 반쯤밖에 못 합친다.
[Refactor_Master_Plan.md](Refactor_Master_Plan.md) 6단계 이후에 한다.

## 검증

- [ ] 웨이브 스테이지(1·3·4) 진행 — 소환 → 처치 → 다음 웨이브
- [ ] 아레나 스테이지(2·5보스) 라운드 사이클 8단계 전부
- [ ] 진입 연출 (`EntrancePlayer` · `EntranceGuard`)
- [ ] 바닥 판정 · 발판 등록 (`GroundRegistry`)
- [ ] 카메라 프레이밍 · 경계 블렌드 (`CameraFrameRules` · `BoundsBlend`)
- [ ] 프리팹 6개가 `Missing (Mono Script)`가 아닌가

# 리팩토링 마스터 계획

작성 2026-09-06 · `Assets/Scripts` 181파일 31,695줄 + `Assets/Editor` 118파일 26,042줄

캔버스 `docs/Architecture_Map.canvas` 그룹 번호 순 하위 문서:

**[Builder](Builder_Refactor_Plan.md)**(1순위) ·
[#1 Battle](Battle_Refactor_Plan.md) · [#2 UI](UI_Refactor_Plan.md) ·
[#3 Tactic](Tactic_Refactor_Plan.md) · [#4 Entities](Entities_Refactor_Plan.md) ·
[#5 Core](Core_Refactor_Plan.md) · [#6 Skill](Skill_Refactor_Plan.md) ·
[#7 AI](AI_Refactor_Plan.md) · [#8 Characters](Characters_Refactor_Plan.md) ·
[#9 Stage](Stage_Refactor_Plan.md) · [#10 Progression](Progression_Refactor_Plan.md) ·
[#11 Party](Party_Refactor_Plan.md) · [#12 Input](Input_Refactor_Plan.md) ·
[#13 Vfx](Vfx_Refactor_Plan.md) · [#14 yg](yg_Refactor_Plan.md) ·
[#15 StateMachine](StateMachine_Refactor_Plan.md)

---

## 0. 전제

**목표는 코드량 감소다.** 프리팹을 쓰든 에디터 수작업을 늘리든 상관없다 —
복잡도를 코드에서 에셋으로 옮기는 게 이득이다.

프리팹은 **다시 만든다.**

### 지켜야 하는 유니티 제약

유니티는 `.cs` 파일 1개당 MonoScript 1개를 만들고, **파일명 == 클래스명**일 때만
만든다. 프리팹·씬·`.asset`은 그 MonoScript를 guid로 문다.

> **프리팹/`.asset`에 붙는 MonoBehaviour·ScriptableObject는 자기 이름의 파일이
> 필요하다.** 파일을 합쳐 `.cs`가 사라지면 `Missing (Mono Script)`가 되고
> 인스펙터 값도 날아간다. 프리팹을 새로 만들어도 마찬가지다.

이 제약이 **프리팹화와 파일 병합을 양립 불가능하게** 만든다. UI를 프리팹으로
되돌리면 그 MonoBehaviour들은 전부 파일명이 고정된다. 코드량이 목표이므로
**프리팹화를 택하고 병합을 포기한다.**

### asmdef

`.asmdef`가 **0개** — 전부 `Assembly-CSharp` 단일 어셈블리.
네임스페이스는 컴파일에 영향이 없다(순수 이름표). `Assets/Editor`만 유니티
특수 폴더로 분리 컴파일되며, 런타임 코드가 참조할 수 없다.

---

## 1. 줄이 줄어드는 곳은 네 군데뿐

```
빌더 삭제           6,511   (73%)
UI 조립 → 프리팹    1,792   (20%)
DebugComboHUD 삭제    286   ( 3%)
Core 죽은 코드         33
Skill 병합 오버헤드    -11
────────────────────────────
                    8,988
```

나머지 11개 그룹은 **줄 감소 0**이다. 파일 수만 준다.

---

## 2. 빌더 — 1순위 (9,089줄 중 6,511 삭제)

상세: **[Builder_Refactor_Plan.md](Builder_Refactor_Plan.md)**

```
빌더 전체   26 파일 9,089 줄
  삭제      18 파일 6,511 줄  (71.6%)
  유지       8 파일 2,578 줄  (28.4%)
```

### 유지 — 아트 파이프라인은 멱등 재실행 도구다

**새 아트(png · fbx)가 앞으로 더 들어온다.** 아트 작업은 들어올 때마다 반복되므로
**여기서만 코드가 더 싼 자리다.** 한 번 하고 끝나는 씬·프리팹 배치와 성격이 다르다.

| 빌더 | 줄 | 손으로 하면 나는 사고 |
|---|---:|---|
| `ArtImportBuilder` | 597 | 피벗이 발밑이 아니면 발이 바닥에 안 닿는다 (+ 공용 헬퍼 제공) |
| `PlayerArtBuilder` | 493 | Auto Tight Trim으로 발이 위아래로 들썩인다 |
| `EffectImportBuilder` | 433 | 시트 세로 9칸이 색 변형이라 프레임에 색이 섞인다 |
| `BossArtImportBuilder` | 366 | 멱등 슬라이스 + 컨트롤러 |
| `VfxClipBuilder` | 142 | `_10`이 `_2` 앞에 오는 정렬 사고 |
| `BasicComboBuilder` | 76 | (프리팹 튜닝값을 안 건드리는 모범) |
| `SkillTableBuilder` | 396 | 스킬 저작 도구 (`MenuItem` 6개) |
| `SkillCatalogBuilder` | 75 | 스킬 추가 시 카탈로그 재생성 |

### 삭제 — 6,511줄

| 분류 | 개수 | 줄 |
|---|---:|---:|
| 씬/프리팹 생성기 (산출물 전부 커밋됨) | 14 | 5,296 |
| 완료된 마이그레이션 `PartyMigrationBuilder` | 1 | 528 |
| 목적 소멸 `AnimationBuilder` (플레이스홀더) | 1 | 337 |
| 1회성 자산 `InputActionsBuilder`·`KeyBindingPresetBuilder` | 2 | 350 |

산출물이 전부 커밋돼 있다 — **씬 12 · 프리팹 20 · SO 54 · anim 78 · controller 7.**

**안전성 검증 완료** — 유지 빌더가 삭제 빌더를 참조하는 3건은 전부 주석이다
(실제 코드 의존 0). `Assets/Editor`는 분리 어셈블리라 런타임 영향도 없다.

---

## 3. UI 조립 코드 — 프리팹으로 회수 (1,792줄)

프로젝트 전체에서 `Build*`/`Create*` 메서드 본문을 브레이스 매칭으로 실측.
**UI를 그리는 MonoBehaviour만** 집계했다 — `CardOfferRules.Build`(카드 후보 생성) ·
`PartyLoadout.CreateRuntime`(데이터 생성) · `PartyAssembler`(프리팹 인스턴스화) ·
`EnemySpawnService`(적 스폰) 같은 오탐은 제외.

| 순위 | 파일 | 그룹 | 회수 |
|---:|---|---|---:|
| 1 | DeckInspectorUI | #2 | 276 |
| 2 | ComboBoardUI | #2 | 250 |
| 3 | PartySelectUI | #14 | 226 |
| 4 | RebindUI | #2 | 225 |
| 5 | DeckBuilderUI | #10 | 156 |
| 6 | PartyHealthHUD | #2 | 110 |
| 7 | LevelUpSession | #10 | 88 |
| 8 | SkillCutinUI | #2 | 71 |
| 9 | RecentHitEnemyHUD | #2 | 68 |
| 10 | CardOfferView | #10 | 65 |
| 11 | BattleRestartUI | #14 | 55 |
| 12 | SimpleUI | #14 | 51 |
| 13 | ComboDamageHUD | #2 | 47 |
| 14 | StageResultUI | #14 | 45 |
| 15 | UiFactory · BulletTimeGaugeWidget | #2 | 59 |
| | **합계** | | **1,792** |

그룹별: **#2 Battle/UI 1,106 · #14 yg 377 · #10 Progression 309**

### "씬 배선 0"은 기술 제약이 아니다

코드로 UI를 짓기로 한 이유가 주석에 남아 있다:

> `ComboBoardUI.cs:10` — 씬/프리팹 연결 없이 코드로만 만든다 —
> **BulletTimeController가 붙은 GameObject에 이 컴포넌트만 추가하면 동작한다.**

**배선 실수를 피하려는 편의 규범이다.** 프리팹이 못 하는 일이 있어서가 아니다.
프리팹으로 되돌리면 그 편의를 잃고 코드 1,792줄을 얻는다.

비용은 `[SerializeField]` 참조가 늘어나는 것 — 배선이 끊기면 런타임
`NullReferenceException`이 난다. 코드량과 교환하는 것이므로 의도된 비용이다.

> `Vfx`(#13)의 75줄은 제외했다 — 런타임에 개수가 정해지는 월드 마커라
> 프리팹화해도 인스턴스화 코드가 남아 회수량이 20~30줄뿐이다.

---

## 4. 그룹별 결과

| # | 그룹 | 파일 전→후 | 줄 전→후 | 감소 |
|---:|---|---|---|---:|
| — | **빌더** | 26 → 8 | 9,089 → 2,578 | **-6,511** |
| #1 | Battle | 17 → 7 | 3,295 → 3,009 | -286 |
| #2 | Battle/UI | 12 → 8 | 3,867 → 2,735 | **-1,132** |
| #3 | Battle/Tactic | 3 → 0 (#1 흡수) | 239 → 0 | 0 |
| #4 | Entities | 13 → 11 | 3,898 → 3,888 | -10 |
| #5 | Core | 14 → 11 | 1,438 → 1,405 | -33 |
| #6 | Skill | 9 → 6 | 1,861 → 1,850 | -11 |
| #7 | AI | 10 → 6 | 1,453 → 1,453 | 0 |
| #8 | Characters | 4 → 4 | 510 → 510 | 0 |
| #9 | Stage | 30 → 13 | 3,912 → 3,912 | 0 |
| #10 | Deck+Progression | 15 → 6 | 2,298 → 1,989 | **-309** |
| #11 | Party | 8 → 7 | 1,183 → 1,183 | 0 |
| #12 | Input+Control | 11 → 8 | 1,816 → 1,816 | 0 |
| #13 | Vfx | 20 → 7 | 2,676 → 2,676 | 0 |
| #14 | yg | 14 → 0 (해체) | 2,836 → 1,933 | **-903** |
| #15 | StateMachine+Stats | 4 → 2 (+Core 1) | 777 → 777 | 0 |

```
파일   299 → 194   (-105)
줄   57,737 → 48,749   (-8,988, -15.6%)
```

---

## 5. 실행 순서

| 순 | 작업 | 감소 | 위험 | 프리팹 |
|---:|---|---:|---|---|
| 1 | 미커밋 변경 커밋 | — | — | — |
| 2 | 빌더 삭제 1단계 | -5,824 | 낮음 | 무관 |
| 3 | `DebugComboHUD` 삭제 | -286 | 낮음 | 무관 |
| 4 | `Tactic/` 병합 (#3) | 0 | 낮음 | 무관 |
| 5 | `Core` 죽은 코드 (#5) | -33 | 낮음 | 무관 |
| 6 | `AnimationBuilder` 삭제 | -337 | 낮음 | 무관 |
| 7 | `Core/UiKit.cs` 통합 | -70 | 중간 | 무관 |
| 8 | **UI 프리팹화 15개** | **-1,792** | 높음 | **재작성** |
| 9 | `yg/` 해체 + 네임스페이스 | 0 | 중간 | 이동만 |
| 10 | `Vfx` 병합 (20→7) | 0 | 낮음 | 무관 |
| 11 | 나머지 그룹 병합 | 0 | 중간 | 일부 고정 |

**2~7번은 프리팹과 무관해서 지금 바로 가능하다** — 합계 **-6,550줄**,
전체 목표의 73%가 프리팹을 하나도 안 건드리고 나온다.

8번(UI 프리팹화)이 나머지 20%이고, 여기만 프리팹 재작성이 필요하다.

### 0단계 — 선행 정리 (필수)

미커밋 변경이 쌓여 있다:

```
M  Assets/Data/Resources/Party/Party_Tan.asset · Party_War.asset
M  Assets/Data/Skills/SK_TK연격.asset · SK_WR일섬.asset
M  Assets/Scenes/Boot.unity
M  Assets/Scenes/Level/Stage_01..05.unity
?? docs/Architecture_Map.canvas · Variable_Reference_Graph.canvas
```

빌더 삭제는 `.cs` + `.cs.meta` 쌍 삭제라 diff가 크다. 섞이면 못 읽는다.

---

## 6. 판단이 필요한 것

| 항목                  | 위치                                      | 내용                                                                                                              |
| ------------------- | --------------------------------------- | --------------------------------------------------------------------------------------------------------------- |
| 안 쓰는 Effect 4종      | [#6 Skill](Skill_Refactor_Plan.md) 2절   | `ShieldEffect`·`DamageCutEffect`·`TauntEffect`·`LifestealEffect` — `.asset` 사용 0. **죽은 코드가 아니라 안 쓴 팔레트.** 유지 권장 |
| `BattleLogSettings` | [#5 Core](Core_Refactor_Plan.md) 1-2    | 씬 12·프리팹 20 어디에도 안 붙음. 삭제 vs 씬에 붙여 살리기                                                                          |
| `AI/Brains` SO 5개   | [#7 AI](AI_Refactor_Plan.md) 2절         | `.asset`이 타입명을 무는지 작업 전 확인 필요                                                                                   |
| 입력 자산 빌더 2개         | [Builder](Builder_Refactor_Plan.md) 3-4 | `InputActionsBuilder`·`KeyBindingPresetBuilder` 350줄. 확신도 낮음                                                    |
| sortingOrder 충돌     | [#2 UI](UI_Refactor_Plan.md) 2-3        | `RebindUI`·`StageResultUI` 둘 다 200                                                                              |

---

## 7. 범위 밖

**테스트 90파일 17,143줄** — 재작성의 유일한 안전망이다. 작업이 끝난 뒤
죽은 것만 정리한다. 셋업 중복도 심하지 않다 (최대 `new GameObject` 9회,
`AttackRangePreviewTests`).

빌더 삭제 시 대응 테스트를 같이 지운다 — `BossPrefabBuilderTests.cs`(586줄)가
대표적이다.

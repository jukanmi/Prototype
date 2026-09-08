# 에디터 빌더 정리 계획 — 1순위

작성 2026-09-06 · 대상 `Assets/Editor/*Builder*.cs` (26개) + `Scripts/yg/Editor/FlowSceneBuilder.cs`
상위 문서: [Refactor_Master_Plan.md](Refactor_Master_Plan.md)

**전제 확정: 새 아트(png · fbx)가 앞으로 더 들어온다.**

---

## 0. 요약

```
빌더 전체    26 파일  9,089 줄
  삭제       18 파일  6,511 줄   (71.6%)
  유지        8 파일  2,578 줄   (28.4%)
```

---

## 1. 판단 기준 — 1회용인가, 멱등 재실행 도구인가

씬·프리팹 생성기는 **한 번 돌리고 끝난 스캐폴딩**이다. 산출물이 커밋돼 있고
프리팹을 다시 만들 거면 에디터에서 손으로 만드는 게 싸다.

반면 **아트 파이프라인 계열은 멱등 재실행 도구다.** 주석이 직접 말한다:

> `ArtImportBuilder` — 격자 · 피벗 · PPU를 코드가 들고 있으면 **다시 돌리면
> 복구된다. 여러 번 돌려도 같은 결과가 나온다.**

> `BossArtImportBuilder` — 여러 번 돌려도 같은 결과가 나온다.

그리고 손으로 하면 반드시 틀리는 문제를 각자 하나씩 풀고 있다:

| 빌더 | 손으로 하면 나는 사고 |
|---|---|
| `ArtImportBuilder` | 캐릭터 피벗이 발밑이 아니면 `BeltScrollView` 오프셋과 어긋나 **발이 바닥에 안 닿는다** |
| `PlayerArtBuilder` | 유니티 Auto Tight Trim이 프레임마다 알파 여백을 다르게 잘라 **발이 위아래로 들썩인다** |
| `EffectImportBuilder` | 시트 세로 9칸이 **같은 애니의 색 변형**이라 통째로 슬라이스하면 프레임 순서에 색이 섞인다 |
| `VfxClipBuilder` | 문자열 정렬로 **`_10`이 `_2` 앞에 온다** |

**아트 파이프라인에서는 코드가 더 싼 자리다** — 에셋 작업이 **아트가 들어올
때마다 반복**되기 때문이다. 한 번 하고 마는 씬·프리팹 배치와 성격이 다르다.

---

## 2. 유지 — 8개 2,578줄

### 2-1. 아트 파이프라인 (2,107줄)

| 파일                        |   줄 | 근거                                              |
| ------------------------- | --: | ----------------------------------------------- |
| `ArtImportBuilder.cs`     | 597 | 멱등. **공용 헬퍼 제공자** — 아래 2-3 참조                   |
| `PlayerArtBuilder.cs`     | 493 | 낱장 PNG 피벗 보정. Ally 파이프라인과 원본 형식이 다르다            |
| `EffectImportBuilder.cs`  | 433 | 64×64 격자에서 **쓰는 행만** 잘라낸다                       |
| `BossArtImportBuilder.cs` | 366 | 멱등. 보스 시트 슬라이스 + `BossAnimator.controller`      |
| `VfxClipBuilder.cs`       | 142 | 우클릭 단건 도구(`Selection.` 기반, 스캔 호출 7곳). 프레임 수치 정렬 |
| `BasicComboBuilder.cs`    |  76 | 평타 3연타 클립. **프리팹을 안 건드린다** — 2-2 참조             |

### 2-2. `BasicComboBuilder`가 이미 옳은 원칙을 지킨다

> **프리팹은 안 건드린다.** 단계 표(`BasicAttackStage`)는 Player · Ally 프리팹에
> 이미 들어가 있고, **거기가 원본이다** — 손으로 튜닝한 값을 생성기가 되돌리면
> 인스펙터에서 고친 손맛이 메뉴 한 번에 조용히 사라진다.

이게 남기는 빌더가 지켜야 할 선이다: **클립·스프라이트 같은 아트 산출물은 굽고,
프리팹의 튜닝 값은 손대지 않는다.** 유지하는 6개가 이 선을 지키는지 확인할 것.

### 2-3. `ArtImportBuilder`는 공용 API 제공자 — 먼저 지우면 안 된다

```
public  static void BuildAll()
internal static Sprite[]       LoadSprites(string assetPath)
internal static AnimationClip  BuildSpriteClip(ClipSpec spec)
internal static AnimationClip  SaveClip(AnimationClip clip, string name)
internal static Color          RoleTint(Role role)
```

실제 호출 지점:

```
BasicComboBuilder.cs:70-72   ArtImportBuilder.BuildSpriteClip(new ArtImportBuilder.ClipSpec(...))  ← 유지 빌더
AllyPrefabBuilder.cs:116     ArtImportBuilder.RoleTint(member.role)                                ← 삭제 대상
```

`AllyPrefabBuilder`를 지우는 건 문제없다(호출하는 쪽이 사라짐).
**`ArtImportBuilder` 자체는 `BasicComboBuilder`가 쓰므로 유지.**

### 2-4. 데이터/SO 저작 도구 (471줄)

| 파일 | 줄 | 근거 |
|---|---:|---|
| `SkillTableBuilder.cs` | 396 | `MenuItem` 6개. 스킬 저작 도구 — 최근 커밋이 계속 스킬 추가(`feat:일섬 구현`) |
| `SkillCatalogBuilder.cs` | 75 | 스킬 추가할 때마다 카탈로그 재생성 |

---

## 3. 삭제 — 18개 6,511줄

### 3-1. 씬 / 프리팹 생성기 (14개 5,296줄)

산출물이 전부 커밋돼 있다 — **씬 12 · 프리팹 20 · SO 54 · anim 78 · controller 7.**
프리팹을 다시 만들 거면 에디터에서 손으로 만드는 게 빌더를 고쳐 돌리는 것보다 싸다.
아트와 달리 **한 번 하고 끝나는 작업**이다.

| 파일                            |   줄 | 산출물                            |
| ----------------------------- | --: | ------------------------------ |
| `SceneLayoutBuilder.cs`       | 700 | 프리팹+씬                          |
| `ArenaSceneBuilder.cs`        | 697 | 프리팹+씬+임포트설정                    |
| `BossPrefabBuilder.cs`        | 604 | SO+프리팹+애니메이션                   |
| `FlowSceneBuilder.cs`         | 526 | 씬+임포트설정 (`Scripts/yg/Editor/`) |
| `RiggedAllyBuilder.cs`        | 523 | 프리팹+애니메이션                      |
| `EnemyPrefabBuilder.cs`       | 458 | SO+프리팹                         |
| `TrainingSceneBuilder.cs`     | 416 | SO+프리팹+씬+임포트설정                 |
| `StageSceneBuilder.cs`        | 239 | 프리팹+씬+임포트설정                    |
| `AllyPrefabBuilder.cs`        | 229 | 프리팹                            |
| `StageWaveSceneBuilder.cs`    | 221 | 씬+임포트설정                        |
| `ProjectileBuilder.cs`        | 217 | 프리팹+씬                          |
| `BattleInputBuilder.cs`       | 185 | 프리팹                            |
| `SkillHitboxPrefabBuilder.cs` | 158 | 프리팹                            |
| `GroundPlateBuilder.cs`       | 123 | 프리팹+씬                          |

### 3-2. 완료된 마이그레이션 (1개 528줄)

| 파일 | 줄 | 근거 |
|---|---:|---|
| `PartyMigrationBuilder.cs` | 528 | 메뉴명 `파티 - 1단계: 씬에서 표 추출`. 결과가 `Data/Resources/Party/*.asset`으로 남았다 |

### 3-3. 목적이 소멸한 것 (1개 337줄)

| 파일                    |   줄 | 근거                                                             |
| --------------------- | --: | -------------------------------------------------------------- |
| `AnimationBuilder.cs` | 337 | 주석: *"**아트가 없는 동안** 쓰는 플레이스홀더 애니메이션."* 아트가 들어왔고 더 들어온다 → 목적 소멸 |

> 새 아트가 들어오므로 플레이스홀더는 더 이상 필요 없다.

### 3-4. 1회성 자산 생성 (2개 350줄)

| 파일 | 줄 | 근거 |
|---|---:|---|
| `InputActionsBuilder.cs` | 200 | `입력 - 액션 자산 **다시** 만들기`. 자산 커밋됨, 입력 스킴이 바뀔 일은 드물다 |
| `KeyBindingPresetBuilder.cs` | 150 | `입력 - **예시** 키 프리셋 만들기`. 예시 |

> 이 둘만 확신도가 낮다. 남겨도 350줄이니 **판단이 서지 않으면 3단계로 미룬다.**

---

## 4. 삭제 안전성 — 검증 완료

### 4-1. 유지 빌더가 삭제 빌더를 참조하는 3건 — 전부 주석이다

```
ArtImportBuilder.cs:43    /// <see cref="AnimationBuilder"/>와 같은 경로여야 한다.
ArtImportBuilder.cs:345   // 클립이 안 건드리는 값을 (AnimationBuilder와 같은 이유).
ArtImportBuilder.cs:500   /// (AllyPrefabBuilder가 칠한다), 여기서 표에 색을 …
ArtImportBuilder.cs:548   /// 직업 색. AllyPrefabBuilder도 같은 값을 써야 …
BossArtImportBuilder.cs:69 /// <see cref="BossPrefabBuilder"/>가 그 값을 쥐고 있다.
```

**실제 코드 의존은 0.** 다만 `<see cref="...">`는 대상 타입이 사라지면
컴파일 경고(CS1574)를 낸다 — 삭제와 같은 커밋에서 주석 문구를 고친다.

### 4-2. 런타임 영향 없음

`Assets/Editor`는 유니티 특수 폴더라 `Assembly-CSharp-Editor`로 분리 컴파일된다.
런타임 코드(`Assembly-CSharp`)는 **참조가 불가능**하다. 빌더를 지워도 게임 코드는
영향받지 않는다.

`Scripts/yg/Editor/FlowSceneBuilder.cs`도 `Editor/` 하위라 같다.

### 4-3. 테스트 영향 — 지우지 말고 상수만 인라인한다

삭제 대상 빌더를 참조하는 테스트 9개(2,155줄)를 전수 조사했다.
**하나도 삭제 대상이 아니다.**

| 테스트 | 줄 | 참조 방식 | 처리 |
|---|---:|---|---|
| `BossPrefabBuilderTests.cs` | 586 | `BossPrefabBuilder.*` 상수 8회 | 상수 인라인 |
| `AttackRangePreviewTests.cs` | 405 | **주석만** | 그대로 |
| `PartyPrefabTests.cs` | 240 | `BattleInputBuilder.PrefabPath`·`PartyRootName` | 상수 인라인 |
| `EnemyPrefabBuilderTests.cs` | 229 | 빌더 호출 2회 | 상수 인라인 |
| `PushToWallTests.cs` | 196 | `BossPrefabBuilder.GuardDamage` | 상수 인라인 |
| `SceneLayoutRoomTests.cs` | 191 | 빌더 호출 10회 | 상수 인라인 |
| `BossGuardPrefabTests.cs` | 112 | `BossPrefabBuilder.MaxGuard` | 상수 인라인 |
| `EntityVisualSetupTests.cs` | 104 | `EnemyPrefabBuilder.TryGetVariantColor` | 표 인라인 |
| `SkillHitboxPrefabTests.cs` | 92 | 빌더 호출 2회 | 상수 인라인 |

빌더는 이 테스트들에게 **기대값의 단일 출처**였을 뿐이다.
`BossPrefabBuilderTests` 주석이 정체를 밝힌다:

> **보스 애셋이 실제로 쓸 수 있는 상태로 나오는지 검증한다.**
> 프리팹 배선은 컴파일로 안 잡히는 실수가 가장 많이 나는 곳이다.

**프리팹을 손으로 다시 만들 거면 이 테스트들은 오히려 더 중요해진다** —
손 배선 실수를 잡는 유일한 그물이다.

> 상수를 인라인하면 테스트 쪽이 **몇 줄 늘어난다**(빌더가 계산해 주던 값을
> 직접 써야 한다). 전체 감소량에서 100~200줄 정도 상계한다.

---

## 5. 단계

각 단계 = 별도 커밋.

### 0단계 — 선행

미커밋 변경 정리 (씬 5 · 에셋 4 · docs 캔버스).
빌더 삭제는 `.cs` + `.cs.meta` 삭제라 diff가 크다. 섞이면 못 읽는다.

### 1단계 — 씬/프리팹 생성기 + 마이그레이션 삭제 (5,824줄)

3-1 + 3-2. 15개 파일.

- `.cs` + `.cs.meta` 쌍으로 삭제
- `BossPrefabBuilderTests.cs` 등 대응 테스트 동반 삭제 (4-3에서 확인한 것)
- `ArtImportBuilder` · `BossArtImportBuilder`의 주석 참조 5건 수정 (4-1)

> **[Battle_Refactor_Plan.md](Battle_Refactor_Plan.md) 2-1과 겹친다** — `SceneLayoutBuilder:643-673` ·
> `BattleInputBuilder:143`이 `DebugComboHUD`를 배선한다. 여기서 파일을 통째로
> 지우면 그 항목은 저절로 없어진다. **빌더 삭제를 먼저 하면 그쪽 작업이 줄어든다.**

### 2단계 — `AnimationBuilder` 삭제 (337줄)

3-3. 플레이스홀더 목적 소멸. `ArtImportBuilder:43` · `:345` 주석 수정 동반.

> 1단계와 분리한다 — 아트 계획이 바뀌면 이 커밋만 되돌리면 된다.

### 3단계 — 입력 자산 빌더 (350줄, 선택)

3-4. 확신도가 낮은 둘. 급하지 않다.

### 그 외 — 하지 않는다

아트 파이프라인 6개 + 저작 도구 2개는 유지. **새 아트가 들어올 때마다 필요하다.**

---

## 6. 결과

```
빌더        26 파일 9,089 줄  →  8 파일 2,578 줄   (-6,511)
  1단계                                            -5,824
  2단계                                              -337
  3단계 (선택)                                       -350
```

프로젝트 전체 누계 — [Refactor_Master_Plan.md](Refactor_Master_Plan.md) 4절:

```
빌더 삭제                        6,511
UI 조립 → 프리팹                1,792
DebugComboHUD 삭제                286
Core 죽은 코드                     33
Skill 병합 오버헤드               -11
────────────────────────────────────
                                8,988   /  57,737  →  -15.6%
```

**빌더가 전체 감소량의 73%다.**

---

## 7. 남기는 빌더에 붙일 규칙

유지하는 8개가 지켜야 할 선. `BasicComboBuilder`가 이미 지키고 있는 것을 명문화한다.

1. **아트 산출물(스프라이트 · 클립 · 컨트롤러)만 굽는다.**
2. **프리팹의 튜닝 값은 건드리지 않는다** — 인스펙터에서 손으로 고친 값이
   메뉴 한 번에 사라지면 안 된다.
3. **멱등해야 한다** — 여러 번 돌려도 같은 결과.

### `ArtImportBuilder.RigAlly`가 선에 걸쳐 있다 — 확인 완료

`ArtImportBuilder.cs:441-490`이 `Ally.prefab`을 **직접 쓴다**
(`PrefabUtility.LoadPrefabContents` → `SaveAsPrefabAsset`).

쓰는 값:

| 대상 | 값 | 성격 |
|---|---|---|
| `Animator` | `runtimeAnimatorController` · `applyRootMotion` · `cullingMode` | 아트 배선 — OK |
| `SpriteRenderer` | `sprite` · `flipX` · `localScale` · `color` | 아트 배선 — OK |
| `BeltScrollView` | `spriteOffsetY = 0f` · `flipToFacing = true` · `facingRenderer` | **튜닝값** — 위험 |

앞의 둘은 규칙 1(아트 산출물)에 해당한다. **`BeltScrollView`의
`spriteOffsetY`·`flipToFacing`은 인스펙터에서 조정할 수 있는 값**이라,
누가 손으로 고친 뒤 이 메뉴를 돌리면 조용히 되돌아간다.

주석이 이유를 달아 두긴 했다 — *"피벗이 발밑이라 더 올릴 필요가 없다."*
새 아트가 같은 피벗 규약을 따르면 문제없지만, 규약이 다른 아트가 들어오면
여기가 사고 지점이다.

**처리**: 삭제하지 않는다. 대신 `BeltScrollView` 세 줄에
"인스펙터 값을 덮어쓴다"는 경고 주석을 붙인다.

---

## 검증

빌더 삭제는 런타임 코드에 영향이 없으므로(4-2) 씬 재생 검증은 불필요하다.
대신 에디터에서 확인:

- [ ] 유니티 콘솔에 컴파일 에러 · CS1574 경고가 없는가
- [ ] 남긴 메뉴가 뜨는가 — `Prototype/아트 - 전부 임포트 + 배선`,
      `Prototype/이펙트 - Effect 팩 배선`, `Prototype/보스 - 아트 임포트 + 애니메이터`,
      `Prototype/평타 - Player 3연타 클립 굽기`, `Prototype/평타 - 3연타 클립 굽기`,
      `Assets/Prototype/선택한 시트 → VfxClip`(우클릭),
      `Prototype/스킬 - 빈 에셋 하나 만들기`, `Tools/Prototype/스킬 카탈로그 굽기`
- [ ] 남긴 아트 메뉴를 **실제로 한 번 돌려서** 산출물이 이전과 같은지
      (멱등성 확인 — 이게 유지 근거였다)
- [ ] `.meta` 고아가 남지 않았는가 (콘솔 경고)
- [ ] 삭제한 빌더에 대응하는 테스트가 남아 컴파일을 깨지 않는가

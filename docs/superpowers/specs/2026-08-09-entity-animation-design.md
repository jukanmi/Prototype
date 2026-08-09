# 주인공·적 스프라이트 애니메이션 — 동료 셋업 공유

날짜: 2026-08-09
브랜치: feat/khb-enemy

## 목적

지금 화면에서 **동료만 그림이 움직인다**(정확히는, 움직이도록 만들어졌다).
주인공과 적은 사각형이 꿀렁이는 플레이스홀더다.

[Assets/Art/Character/Ally/](../../../Assets/Art/Character/Ally/)에 이미 9장의 스프라이트 시트가 있고,
`Ally_*.anim` 11개가 그걸 프레임 단위로 돌린다. **같은 시트를 주인공·적에게도 물려
색 틴트로만 구분한다.** 캐릭터별 아트가 준비되면 그때 클립만 갈아끼운다.

작업 도중 **애니메이션이 지금 전혀 재생되지 않는다**는 사실을 발견했다.
그래서 이 스펙은 버그 수정 두 건을 선행 단계로 포함한다.

## 범위

| 항목 | 결정 |
|---|---|
| 아트 소스 | Ally 시트 재사용. 주인공·적 전용 시트는 만들지 않는다 |
| 캐릭터 구분 | `SpriteRenderer.color` RGB 틴트 |
| 컨트롤러 | `EntityAnimator.controller` **한 벌로 통합** |
| 대상 프리팹 | Player, Ally, enemy, Enemy_Melee, Enemy_Charger, Enemy_Ranged |

**비범위**:
- 주인공·적 전용 스프라이트 시트 제작
- 캐릭터별 `AnimatorOverrideController` 에셋 — 전용 아트가 생길 때 도입한다.
  지금 만들면 전부 같은 클립을 가리키는 빈 껍데기다.
- Animation Event 기반 판정 — [EntityAnimator.cs](../../../Assets/Scripts/Entities/EntityAnimator.cs)의
  주석대로 타이밍의 주인은 코드다. 바꾸지 않는다.
- 스킬 컷인 연출 — 별도 스펙(`2026-08-09-skill-cutin-design.md`)

## 전제 — 버그 두 건

### ① 모든 클립의 바인딩 경로가 죽어 있다

커밋 `7099ac1`이 프리팹 계층에 `View` 노드를 끼워 넣었다.

```
이전:  Ally ─ Attack, Shadow, Sprite
이후:  Ally ─ Attack, Shadow, View ─ Sprite
```

Animator는 루트에 있고, 유니티의 커브 바인딩은 **Animator 기준 상대 경로**다.
26개 `.anim` 전부가 아직 `path: Sprite`를 가리키므로 — 실제 경로는 `View/Sprite` —
스프라이트 교체·스케일·알파 커브가 전부 무효다. 프리팹에 직렬화된 첫 프레임에서 멈춰 있다.

`.anim` 파일에는 경로가 두 군데 적힌다. 사람이 읽는 문자열(`path: Sprite`)과
런타임이 쓰는 `m_ClipBindingConstant`의 해시(`path: 850496168`)다.
해시는 CRC32로 확인했다:

| 문자열 | CRC32 |
|---|---|
| `Sprite` | 850496168 |
| `View/Sprite` | 3701061421 |

26개 클립이 **`Sprite` 한 경로만** 바인딩하므로 (`grep`으로 확인: 문자열 123건, 해시 54건, 다른 경로 0건)
기계적 일괄 치환으로 안전하게 고칠 수 있다.

### ② 동료의 스킬 클립 교체가 죽어 있다

`EntityAnimator.SwapSkillClip`은 `overrides["Skill_Placeholder"]`로 클립을 꽂는다
([EntityAnimator.cs:17](../../../Assets/Scripts/Entities/EntityAnimator.cs#L17)).
`AnimatorOverrideController`의 인덱서 키는 **원본 클립의 이름**이다.

- `EntityAnimator.controller`의 `Skill` 스테이트 모션 = `Skill_Placeholder` → 키 일치 ✅
- `AllyAnimator.controller`의 `Skill` 스테이트 모션 = `Ally_Skill` → 키 불일치 ❌

동료는 어떤 스킬을 써도 `Ally_Skill` 클립만 돈다.
컨트롤러를 한 벌로 통합하면 이 버그가 함께 사라진다.

## 구성

네 단계. 앞 단계가 뒤 단계의 전제이므로 순서를 지킨다.

### 1단계 — 바인딩 경로 복구

`Assets/Data/Animation/*.anim` 26개에 두 가지 치환:

```
path: Sprite      → path: View/Sprite
path: 850496168   → path: 3701061421
```

`.anim`은 순수 텍스트 YAML이고 치환 대상 문자열이 다른 문맥에 나타나지 않으므로
스크립트 치환이 `AnimationUtility` 왕복보다 단순하고 결과가 같다.

**회귀 방지**: 이 실수는 계층을 건드릴 때마다 재발한다. EditMode 테스트로 고정한다 —
각 프리팹의 Animator에서 컨트롤러를 타고 내려가 모든 클립의 모든 바인딩 경로를 모은 뒤,
`prefabRoot.transform.Find(binding.path)`가 널이 아닌지 검사한다.

### 2단계 — 컨트롤러 통합

`EntityAnimator.controller`의 스테이트 모션을 `Ally_*` 클립으로 교체한다.
스테이트 집합이 양쪽 컨트롤러에서 동일하므로 잃는 스테이트가 없다.

| 스테이트 | 이전 클립 | 이후 클립 |
|---|---|---|
| Idle | `Idle` | `Ally_Idle` |
| Move | `Move` | `Ally_Move` |
| Attack | `Attack` | `Ally_Attack` |
| AerialAttack | `AerialAttack` | `Ally_AerialAttack` |
| Hit | `Hit` | `Ally_Hit` |
| AerialHit | `AerialHit` | `Ally_AerialHit` |
| Down | `Down` | `Ally_Down` |
| Getup | `Getup` | `Ally_Getup` |
| Dead | `Dead` | `Ally_Dead` |
| Jump | `Jump` | `Ally_Jump` |
| **Skill** | `Skill_Placeholder` | **`Skill_Placeholder` (유지)** |

`Skill`만 예외인 이유는 전제 ②다. 여기를 `Ally_Skill`로 바꾸면 스킬 클립 교체가 다시 깨진다.

이어서:
- `Ally.prefab`의 Animator 컨트롤러를 `AllyAnimator.controller` → `EntityAnimator.controller`
- `AllyAnimator.controller` 삭제
- 미사용이 된 플레이스홀더 클립 10개 삭제
  (`Idle` `Move` `Attack` `AerialAttack` `Hit` `AerialHit` `Down` `Getup` `Dead` `Jump`)
- **남기는 것**: `Skill_Placeholder`(오버라이드 슬롯), `SK_*` 4개(스킬 클립),
  `Ally_Skill`(`SkillData.animation`에 꽂을 후보)

Player와 enemy 프리팹은 이미 `EntityAnimator.controller`를 참조하므로 손대지 않는다.

### 3단계 — 프리팹 설정

`Enemy_Melee` · `Enemy_Charger` · `Enemy_Ranged`는 `EnemyPrefabBuilder` 메뉴가 `enemy.prefab`을
복제해 찍어내는 프리팹이다. 따라서 **`enemy.prefab`에 넣은 것은 재빌드 때 자동으로 상속된다** —
Animator도, `BeltScrollView` 필드도. 변종별로 달라지는 값(틴트)만 빌더 코드가 따로 넣어줘야 한다.
다만 메뉴를 실행하지 않고 작업하므로, 세 프리팹 애셋도 같은 결과가 되도록 직접 맞춘다.

**Animator가 없는 3종** — `Enemy_Melee`, `Enemy_Charger`, `Enemy_Ranged`:
루트에 `Animator`(Controller = `EntityAnimator.controller`) + `EntityAnimator` 추가,
`EntityAnimator.animator` 필드를 그 Animator에 연결한다.
세 프리팹 모두 루트에 `Prototype.Enemy`(→ `Entity`)를 가지므로 `[RequireComponent(typeof(Entity))]`를 만족한다.

**Ally를 제외한 5종 공통** — Ally 기준값으로 맞춘다:

| 필드 | 현재 | 변경 | 이유 |
|---|---|---|---|
| `BeltScrollView.spriteOffsetY` | 0.5 | 0 | Ally 시트의 피벗 기준 |
| `BeltScrollView.flipToFacing` | 0 | 1 | 시트에 좌우 방향이 없다 |
| `BeltScrollView.facingRenderer` | 없음 | Sprite의 SpriteRenderer | 뒤집을 대상 |
| Sprite의 `m_Sprite` | 사각형 | Ally `_Idle` 첫 프레임 | 에디터 프리뷰용. 런타임엔 클립이 덮는다 |

**틴트** — 6종 전부 대상. Sprite의 `m_Color` RGB만 바꾸고 알파는 건드리지 않는다(클립 소유).

색값은 새로 짓지 않는다. 적 3종은 [EnemyPrefabBuilder](../../../Assets/Editor/EnemyPrefabBuilder.cs)의
`Variants` 표에 이미 정해져 있다 — 지금은 머티리얼에만 반영되고 스프라이트에는 안 닿는다.
그 값을 그대로 SpriteRenderer로 옮긴다.

| 프리팹 | RGB | 출처 |
|---|---|---|
| Player | `1.00, 1.00, 1.00` | 원본 (가장 밝게) |
| Ally | `0.55, 1.00, 0.65` | 연두 — 적 3색·상태색 어느 것과도 안 겹친다 |
| enemy (기준) | `0.90, 0.30, 0.28` | Melee와 동일. 씬의 더미 적 |
| Enemy_Melee | `0.90, 0.30, 0.28` | 빌더 `고블린` |
| Enemy_Ranged | `0.35, 0.62, 1.00` | 빌더 `궁수 고블린` |
| Enemy_Charger | `1.00, 0.65, 0.20` | 빌더 `돌진 멧돼지` |

**빌더도 함께 고친다** — `BuildVariant`가 `Sprite` 자식의 `SpriteRenderer.color`에 `v.color`를 넣도록
몇 줄 추가한다. 그러지 않으면 메뉴를 다시 누르는 순간 세 프리팹의 틴트가 `enemy.prefab` 값으로 되돌아간다
(변종은 기준 프리팹의 복제본이다). 캡슐 메쉬(`BuildBody`)는 이 스펙에서 건드리지 않는다 — 별건이다.

이 틴트는 기존 설계와 충돌하지 않는다. [EnemyStateTint](../../../Assets/Scripts/Vfx/EnemyStateTint.cs)가
Awake에 `body.color`를 `BaseColor`로 붙잡고, [CombatStateVisuals.Tint](../../../Assets/Scripts/Vfx/CombatStateVisuals.cs)가
상태색을 80%만 섞어 고유색의 흔적을 남긴다 — **프리팹 색이 곧 "적 종류 고유색"이라는 것이 원래 의도다.**
알파도 `baseColor.a`를 그대로 되돌려주므로 사망 페이드 커브와 싸우지 않는다.

`Player`·`Ally`에는 `EnemyStateTint`가 붙지 않으므로(`Enemy`가 스스로 붙인다) 프리팹 색이 그대로 유지된다.

### 4단계 — 배선 검증 테스트

`Assets/Editor/Tests/`(네임스페이스 `Prototype.Tests`, NUnit):

1. **바인딩 경로** — 1단계의 회귀 방지 테스트. 6개 프리팹 × 컨트롤러의 전 클립 경로가 해석되는가.
2. **Animator 보유** — 6개 프리팹 전부 `Animator` + `EntityAnimator`를 갖고,
   컨트롤러가 `EntityAnimator.controller`인가.
3. **스테이트 이름 대응** — `EntityAnimator.StateToClip`이 만드는 이름(= `IState` 구현 클래스명에서
   `State` 접미사를 뗀 것)이 컨트롤러의 스테이트로 전부 존재하는가.
   현재 `EntityState` 파생 10종(`Idle` `Move` `Jump` `Attack` `AerialAttack` `Hit` `AerialHit`
   `Down` `Getup` `Dead`) + `SkillState` 계열이 매핑하는 `Skill` = 11개로 컨트롤러와 1:1이다.
   `Play`가 `HasState` 검사로 조용히 넘어가므로 이 불일치는 런타임에 드러나지 않는다.
4. **스킬 슬롯** — `Skill` 스테이트의 모션 이름이 `EntityAnimator.SkillSlotClip`과 같은가. 전제 ②의 회귀 방지.

## 눈으로 확인할 것

테스트로 못 잡는 항목. 씬을 재생해서 본다.

- 동료·주인공·적이 **실제로 애니메이션한다** (1단계가 먹혔는지 — 가장 먼저)
- 발이 바닥(`groundY`)에 닿는다. `spriteOffsetY` 0이 새 시트에 맞는가
- 이동 방향에 따라 스프라이트가 뒤집힌다
- 그림자(`shadowBaseScale` 0.9 × 0.35)가 새 실루엣 발밑에 맞는가
- `Attack` 자식의 `CapsuleCollider`(height 2, center 0,0,1) 위치가 새 스프라이트 기준으로 어긋나지 않는가
- 적 3종의 틴트가 난전에서 서로 구분된다. 상태 틴트(경직 흰색·공중 하늘색·넉백 주황)와도 헷갈리지 않는가.
  두 조합이 특히 위험하다 — Enemy_Charger 주황(`1, 0.65, 0.20`) × 넉백 주황(`1, 0.549, 0.259`),
  Enemy_Ranged 파랑(`0.35, 0.62, 1`) × 공중 하늘색(`0.349, 0.761, 1`).
  `TintStrength`가 0.8이라 궁수가 공중에 뜨면 원래 색과 거의 같아진다. 안 보이면 라벨로 충분한지,
  아니면 궁수 고유색을 옮길지 판단한다 (별건으로 뺀다)
- 사망 시 페이드가 끝까지 간다 (`Ally_Dead`의 알파 커브 × `Entity.despawnFade`)
- 스킬 사용 시 스킬별 클립이 나온다 (전제 ②가 풀렸는지)

## 결정 로그

**왜 Animator를 `View`로 옮기지 않고 클립 경로를 고쳤나.**
Animator를 `View`에 두면 `path: Sprite`가 다시 맞아떨어져 프리팹 6개만 고치면 된다.
그러나 그러면 루트·`Shadow`·`Attack`을 영영 애니메이션할 수 없다.
클립 경로 치환은 해시가 CRC32로 확인돼 기계적으로 안전하고, Animator를 루트에 두는 관례도 지킨다.

**왜 컨트롤러를 캐릭터별로 복사하지 않았나.**
지금은 6종이 완전히 같은 스테이트 집합·같은 클립을 쓴다. 복사본은 여섯 벌의 동일한 파일이고,
스테이트를 하나 추가할 때마다 여섯 번 고쳐야 한다.
캐릭터별로 다른 클립이 필요해지는 시점에 `AnimatorOverrideController` 에셋을 도입하는 편이
컨트롤러 복사보다 유지 비용이 낮다 — 스테이트 머신은 여전히 한 벌이기 때문이다.

**왜 플레이스홀더 클립 10개를 지우나.**
어느 컨트롤러도 참조하지 않게 되어 되살릴 경로가 없다. 남겨두면 Animation 창에서
`Idle`과 `Ally_Idle` 중 무엇을 편집해야 하는지 매번 헷갈린다.

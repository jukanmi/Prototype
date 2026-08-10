# 적 상태 표시 — 색 변화 + 머리 위 라벨

날짜: 2026-08-07
브랜치: feat/khb-enemy

## 목적

콤보 중에 **어떤 적이 내 스킬에 걸렸는지** 한눈에 보이게 한다.

지금은 적이 경직·공중·다운 상태로 넘어가도 화면에 그 사실이 드러나지 않는다.
[RecentHitEnemyHUD](../../../Assets/Scripts/Battle/UI/RecentHitEnemyHUD.cs)가 최근 피격 적 한 명의
체력을 하단에 보여주지만, 광역기로 여럿을 띄웠을 때 "누가 떠 있고 누가 멀쩡한지"는 알 수 없다.

적의 **몸 색**과 **머리 위 라벨** 두 경로로 상태를 이중 부호화한다.
색만으로는 색약자가 구분하지 못하고, 글자만으로는 난전에서 안 읽힌다.

## 범위

| 항목 | 결정 |
|---|---|
| 표시할 상태 | `CombatState`만 |
| 표시 조건 | `Neutral`·`Dead` 제외한 나머지 상태일 때만 |
| 적용 대상 | `Faction.Enemy`만 |

**비범위** (별건):
- 스킬 디버프(둔화·도발·피해감소) 표시 — 지속 상태를 저장하는 시스템이 아예 없다.
  [Effects.cs](../../../Assets/Scripts/Skill/Effects.cs)는 스탯을 바꾸고 타이머로 되돌릴 뿐이라
  "지금 둔화 중"을 물어볼 데가 없다. 만들려면 `StatusEffect` 레지스트리 신설이 선행돼야 한다.
- 아군 상태 표시
- 깊이감 개선(뒷벽·깊이 스케일) — 별도 스펙
- `EnemyPrefabBuilder` 변종 프리팹 복구 — 별도 스펙 (아래 "전제" 참조)

## 전제 — 적 프리팹이 두 가지 형태로 존재한다

SampleScene에는 구조가 다른 두 종류의 적이 섞여 있다.

| 프리팹 | 씬 인스턴스 | 몸 렌더러 위치 | BeltScrollView |
|---|---|---|---|
| `enemy.prefab` | 4 | `Sprite` 자식 | 있음 |
| `Enemy_Ranged.prefab` | 1 | 루트 | 없음 |
| `Enemy_Charger.prefab` | 1 | 루트 | 없음 |

변종 프리팹이 낡은 기준 프리팹에서 찍혀 Sprite·Shadow 자식을 잃은 상태다(별도 스펙에서 고친다).
이 스펙은 **두 형태 모두에서 동작해야 한다** — 몸 렌더러를 찾는 규칙이 그래서 필요하다.

## 구성

세 조각으로 나눈다. 색은 이벤트 구동, 라벨은 프레임 구동으로 성격이 다르므로 한 컴포넌트에 묶지 않는다.

```
CombatStateVisuals   (static, 순수)   ← 색·글자·표시여부의 단일 출처
      ↑                    ↑
EnemyStateTint      EnemyStateLabel
(적마다 1개)         (씬에 1개)
몸 SpriteRenderer    TextMesh 풀
```

세 파일 모두 `Assets/Scripts/Vfx/` 에 둔다.
[ChargeGauge](../../../Assets/Scripts/Vfx/ChargeGauge.cs)·[RangeIndicator](../../../Assets/Scripts/Vfx/RangeIndicator.cs)와
같은 성격의 표현 레이어이고, `EnemyStateLabel`은 ChargeGauge와 같은 GameObject에 붙는다.

### 1. `CombatStateVisuals` — Assets/Scripts/Vfx/CombatStateVisuals.cs

상태 → 표현의 매핑을 담은 static 클래스. 유니티 객체를 만지지 않는 순수 함수라 EditMode 테스트 대상이다.

```csharp
public static class CombatStateVisuals
{
    /// <summary>기본 색을 상태색 쪽으로 끌어당기는 정도. 1이면 완전히 덮는다.</summary>
    public const float TintStrength = 0.8f;

    public static bool  ShouldShow(CombatState s);
    public static string Label(CombatState s);      // 표시 대상이 아니면 빈 문자열
    public static Color StateColor(CombatState s);  // 표시 대상이 아니면 Color.white
    public static Color Tint(Color baseColor, CombatState s);
}
```

매핑표:

| 상태 | 라벨 | 색 | 표시 |
|---|---|---|---|
| `Neutral` | — | — | 아니오 |
| `LightHit` | 경직 | `#FFFFFF` | 예 |
| `AerialHit` | 공중 | `#59C2FF` | 예 |
| `Knockback` | 넉백 | `#FF8C42` | 예 |
| `WallBound` | 벽꽂 | `#E24AFF` | 예 |
| `Down` | 다운 | `#7A7A85` | 예 |
| `Getup` | 기상 | `#FFD166` | 예 |
| `Dead` | — | — | 아니오 |

`Tint(baseColor, s)`의 규칙:
- 표시 대상이 아니면 `baseColor`를 그대로 돌려준다.
- 표시 대상이면 `Color.Lerp(baseColor, StateColor(s), TintStrength)`.
- **알파는 항상 `baseColor.a`를 유지한다.** 사망 페이드가 알파를 쓰므로 여기서 덮으면 안 된다.

적 고유색(고블린 빨강 / 궁수 파랑 / 멧돼지 주황)을 완전히 덮지 않고 0.8로 섞는 이유는
상태는 확실히 보이되 "무슨 적이었는지"의 흔적을 남기기 위해서다.

### 2. `EnemyStateTint` — Assets/Scripts/Vfx/EnemyStateTint.cs

적 하나에 붙어 몸 색을 바꾼다. `Update`가 없다 — 상태가 바뀔 때만 일한다.

```csharp
public class EnemyStateTint : MonoBehaviour
{
    [SerializeField] private SpriteRenderer body;   // 비우면 스스로 찾는다
    public Color BaseColor { get; }                 // 테스트가 읽는다
    public SpriteRenderer Body { get; }             // 테스트가 읽는다
}
```

**몸 렌더러 해석 규칙** (위에서부터 먼저 맞는 것):
1. 인스펙터에서 `body`가 지정돼 있으면 그것.
2. `BeltScrollView`가 있으면 그 컴포넌트의 `sprite` 자식에 붙은 `SpriteRenderer`.
3. 없으면 루트의 `GetComponent<SpriteRenderer>()`.

`BeltScrollView.sprite`는 private `[SerializeField]`라 접근할 수 없다.
`public Transform SpriteRoot => sprite;` 를 추가한다.

**그림자 렌더러는 절대 고르지 않는다.** 2·3번 규칙은 그림자를 지나치므로
`GetComponentInChildren<SpriteRenderer>()`는 쓰지 않는다 — 자식 순서에 따라 그림자를 집을 수 있다.

동작:
- `Awake` — 렌더러를 해석하고 `BaseColor`에 현재 색을 저장한다.
- `OnEnable` / `OnDisable` — `Combat.OnCombatStateChanged` 구독·해제.
- 상태 변경 시 — `body.color = CombatStateVisuals.Tint(BaseColor, next)`.
  `Neutral`·`Dead`로 돌아오면 같은 식이 `BaseColor`를 돌려주므로 원복은 자동이다.

**부착 방법:** `Enemy.Awake`에서 없으면 붙인다.

```csharp
if (GetComponent<EnemyStateTint>() == null) gameObject.AddComponent<EnemyStateTint>();
```

프리팹 두 형태를 각각 배선하지 않아도 되고, 씬에 이미 놓인 인스턴스도 자동으로 얻는다.
[ChargeGauge](../../../Assets/Scripts/Vfx/ChargeGauge.cs)가 세운 "씬 배선 0" 원칙과 같은 방향이다.

### 3. `EnemyStateLabel` — Assets/Scripts/Vfx/EnemyStateLabel.cs

씬에 하나 존재하며 모든 적의 머리 위 라벨을 그린다.
[ChargeGauge](../../../Assets/Scripts/Vfx/ChargeGauge.cs)와 같은 GameObject에 붙는다.

구조를 ChargeGauge에서 그대로 가져온다 — 머리 위에 무언가를 띄우는 문제가 이미 그 파일에서 풀려 있다.

```csharp
public class EnemyStateLabel : MonoBehaviour
{
    [SerializeField] private float headOffset = 1.9f;   // ChargeGauge는 1.6 — 겹치지 않게
    [SerializeField] private int sortingOffset = 320;   // ChargeGauge는 300
    [SerializeField] private float characterSize = 0.06f;
    [SerializeField] private int fontSize = 48;
}
```

`LateUpdate`에서:
1. `BattleRegistry.Enemies`를 순회한다.
2. `e == null || e.Combat == null` 이거나 `!CombatStateVisuals.ShouldShow(e.Combat.CombatState)` 면 건너뛴다.
3. 풀에서 `TextMesh`를 꺼내 배치한다.
   - `text` = `CombatStateVisuals.Label(state)`
   - `color` = `CombatStateVisuals.StateColor(state)`
   - `position` = `BeltScroll.ToView(physics.GroundPosition, physics.Height + headOffset)`
   - `rotation` = `Quaternion.identity` (빌보드)
   - `sortingOrder` = `Mathf.RoundToInt(-ground.z * 100f) + sortingOffset`
4. 남는 렌더러는 `enabled = false`. 파괴하지 않는다.

`LateUpdate`인 이유는 BeltScrollView가 그 시점에 캐릭터 위치를 확정하기 때문이다.
`TimeControl`을 보지 않으므로 불릿타임 중에도 라벨이 보인다 — 조준하는 동안 상태가 보여야 한다.

폰트는 `Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf")`.
RecentHitEnemyHUD가 쓰는 것과 같은 폰트다.

**위치 계산은 반드시 `BeltScroll.ToView`를 거친다.** 루트 transform은 논리 좌표(x, 0, z)를 들고 있어
깊이가 화면 세로로 접히기 전 값이다. 그대로 쓰면 라벨이 발밑 훨씬 아래에 찍힌다.

## 엣지 케이스

| 상황 | 처리 |
|---|---|
| 적이 죽는다 | `Dead`는 표시 대상이 아니므로 라벨이 사라지고 색이 `BaseColor`로 돌아간다. 알파는 안 건드리므로 사망 페이드와 충돌하지 않는다. |
| 적이 파괴된다 | `EnemyStateTint`는 같이 사라진다. `EnemyStateLabel`은 매 프레임 목록을 다시 읽으므로 다음 프레임에 저절로 정리된다. |
| 슈퍼아머로 전이가 거부됨 | `SetCombatState`가 안 불리므로 색·라벨도 안 바뀐다. 데미지만 들어간 것이 화면에도 그대로 반영된다 — 의도한 동작이다. |
| 무적으로 공격을 흘림 | 같은 이유로 아무것도 안 바뀐다. |
| 불릿타임(시간 정지) | 라벨은 `LateUpdate` 구동이라 계속 보인다. 색은 이벤트 구동이라 마지막 상태를 유지한다. |
| `BeltScrollView` 없는 변종 프리팹 | 해석 규칙 3번이 루트 렌더러를 집는다. 라벨은 `BeltScroll.ToView`를 쓰므로 몸과 어긋날 수 있으나, 이는 변종 프리팹 자체의 결함이며 별도 스펙에서 고친다. |

## 테스트

`Assets/Editor/Tests/` 에 둔다.
asmdef로 만든 어셈블리는 `Assembly-CSharp`를 참조할 수 없어서 여기가 유일하게 동작하는 자리다
([RecentHitEnemyHUDTests](../../../Assets/Editor/Tests/RecentHitEnemyHUDTests.cs) 주석 참조).

### `CombatStateVisualsTests`

- `ShouldShow`가 `Neutral`·`Dead`에 `false`, 나머지 6개 상태에 `true`
- 표시 대상인 모든 상태에서 `Label`이 빈 문자열이 아니다
- `Tint(baseColor, Neutral)` == `baseColor`
- `Tint(baseColor, Dead)` == `baseColor`
- `Tint`의 알파가 항상 `baseColor.a`와 같다 (상태색 알파를 물려받지 않는다)
- `Tint(baseColor, AerialHit)`가 `baseColor`와 `StateColor(AerialHit)` 사이에 있다

### `EnemyStateTintTests`

상태는 `attacker.Attack(target, in hit)`으로 만든다 — RecentHitEnemyHUDTests와 같은 방식이다.
`Combat.SetCombatState`는 private이라 실제 타격 경로를 거쳐야 한다.

- 피격 전 `body.color` == `BaseColor`
- `LightHit`을 만드는 타격 후 `body.color` != `BaseColor`
- 색이 `CombatStateVisuals.Tint(BaseColor, LightHit)`과 일치
- 상태가 `Neutral`로 돌아오면 `body.color` == `BaseColor`
- 그림자 자식이 있는 형태에서 `Body`가 그림자 렌더러가 **아니다**
- `Enemy.Awake`가 `EnemyStateTint`를 자동으로 붙인다

### `EnemyStateLabel`

렌더링 배치라 EditMode 가치가 낮다. 위치 계산만 static 순수 함수로 뽑아 검증한다:

```csharp
public static Vector3 LabelPosition(Vector3 ground, float height, float headOffset);
```

- `DepthToScreen`가 0.9일 때 `z = 2`인 적의 라벨 Y가 `height + headOffset + 1.8`이다
- `height`가 커지면 라벨도 같이 올라간다

## 구현 순서

1. `CombatStateVisuals` + 테스트
2. `BeltScrollView.SpriteRoot` 공개
3. `EnemyStateTint` + 테스트, `Enemy.Awake` 자동 부착
4. `EnemyStateLabel` + 위치 계산 테스트, ChargeGauge와 같은 자리에 부착

1~3까지만 해도 색 변화는 동작한다. 4는 그 위에 얹는다.

## 열린 항목

`characterSize`·`fontSize`·`headOffset`은 화면에서 보고 맞춰야 하는 값이다.
위 숫자는 ChargeGauge 값에서 추정한 출발점이며, 실행해서 조정한다.

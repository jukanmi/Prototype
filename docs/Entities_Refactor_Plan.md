# Entities 리팩토링 계획 (캔버스 #4)

작성 2026-09-06 · 대상 `Assets/Scripts/Entities/` — 13 파일 3898 줄
자매 문서: [Battle_Refactor_Plan.md](Battle_Refactor_Plan.md) ·
[UI_Refactor_Plan.md](UI_Refactor_Plan.md) · [Tactic_Refactor_Plan.md](Tactic_Refactor_Plan.md)

> ## 이 그룹은 병합 여지가 없다
>
> Entities는 **캐릭터 프리팹 12~13개**에 붙어 있고, 그 프리팹들은 스프라이트 ·
> 애니메이터 · 콜라이더를 들고 있다. 프리팹을 다시 만들더라도 이 컴포넌트들은
> 계속 프리팹에 붙으므로, 유니티 MonoScript 규칙상 **자기 이름의 파일이 필요하다.**
>
> **조립 코드도 0줄이다** — UI가 아니라 전투 로직이라 프리팹 이관으로 회수할
> 코드가 없다.
>
> 결과: **병합 여지가 13 → 11 뿐이다.**

---

## 0. 현황

| 파일 | 줄 | 종류 | 프리팹/씬 | 외부 소비자 | 테스트 |
|---|---:|---|---:|---:|---:|
| Combat.cs | 1055 | MB, `IHittable`,`IDamageable` | **12** | **46** | 19 |
| Entity.cs | 626 | MonoBehaviour | **0** | 40 | 15 |
| Physics.cs | 625 | MonoBehaviour | **12** | 29 | 18 |
| EntityAnimator.cs | 284 | MonoBehaviour | **12** | 4 | 3 |
| Attack.cs | 227 | MonoBehaviour | **13** | 23 | 21 |
| StatusEffects.cs | 214 | 순수 class + struct | **0** | 5 | 2 |
| Projectile.cs | 209 | MonoBehaviour | **1** | 4 | 3 |
| BeltScrollView.cs | 204 | MonoBehaviour | **12** | 13 | 6 |
| BasicAttackProfile.cs | 197 | abstract MonoBehaviour | **0** | 0 | 0 |
| BeltScroll.cs | 130 | static class | **0** | 15 | 7 |
| Pilotable.cs | 57 | MonoBehaviour | **6** | 2 | 1 |
| AllyBasicAttack.cs | 51 | `: BasicAttackProfile` | **6** | 0 | — |
| EnemyBasicAttack.cs | 19 | `: BasicAttackProfile` | **6** | 0 | — |

**Entities는 코드베이스의 심장이다.** `Scripts/` 181파일 중 46개가 `Combat`을,
40개가 `Entity`를, 29개가 `Physics`를 본다. 여기를 건드리면 절반이 흔들린다.

프리팹 수는 에디터 실측값이다 ([Prefab_Snapshot.md](Prefab_Snapshot.md) 2절).

대신 안전망도 두껍다 — `Attack` 21 · `Combat` 19 · `Physics` 18 · `Entity` 15
테스트 파일.

## 1. 의존 그래프

```
Entity ──┬─→ Physics ──→ Combat            [RequireComponent] 사슬:
 (뼈대)   ├─→ Combat  ──┬─→ Attack           Physics → Rigidbody
         ├─→ Attack    ├─→ Physics          Combat  → Physics
         ├─→ EntityAnimator                 Entity  → Physics + Combat
         ├─→ Projectile                     Attack  → Collider
         └─→ BasicAttackProfile ──┬─→ AllyBasicAttack
                                  └─→ EnemyBasicAttack

Combat ──→ StatusEffects        ("Combat 하나당 한 벌" — 소유 명확)
Combat ──→ Pilotable

Projectile ──→ Attack · Combat · Physics · BeltScroll · BeltScrollView
BeltScrollView ──→ BeltScroll · Entity · Physics

상속:  Player · Enemy · Ally  ──→ Entity     (Characters/, 캔버스 #8)
```

---

## 2. 파일명이 고정되는 이유

`Attack.cs`가 붙은 13개 프리팹 전수 (에디터 실측):

```
적 6      enemy · Enemy_Boss · Enemy_Charger · Enemy_Dummy · Enemy_Melee · Enemy_Ranged
아군 6    Player/Player · Player/Ally · Ally_Arc · Ally_Tan · Ally_War · Ally_Wiz
투사체 1  Projectile.prefab
```

`Combat` · `Physics` · `EntityAnimator` · `BeltScrollView`는 같은 12개
(적 6 + 아군 6)에 붙는다. `Attack`만 `Projectile.prefab`이 더해져 13개다.

`Enemy_Dummy.prefab` 하나만 봐도 `m_Sprite` · `Animator` · `SpriteRenderer` ·
`m_Mesh` 참조가 13곳이다. **아트 에셋 · 애니메이터 컨트롤러 · 콜라이더 배치는
코드 생성 대상이 아니다.**

[UI_Refactor_Plan.md](UI_Refactor_Plan.md) 3절의 유니티 MonoScript 규칙이
그대로 걸린다: **프리팹에 붙는 MonoBehaviour는 자기 이름의 `.cs` 파일을 가져야 한다.**
프리팹을 새로 만들어도 마찬가지다.

→ 9개 파일(`Combat` · `Physics` · `EntityAnimator` · `Attack` · `Projectile` ·
`BeltScrollView` · `Pilotable` · `AllyBasicAttack` · `EnemyBasicAttack`)은
**파일 유지가 강제된다.**

### 프리팹 참조가 0인 4개

| 파일 | 왜 0인가 | 병합 가능? |
|---|---|---|
| `Entity.cs` | `Player`·`Enemy`·`Ally`가 상속하고 **파생만** 프리팹에 붙는다 | ○ |
| `BasicAttackProfile.cs` | `abstract` — 파생 2개만 프리팹에 붙는다 | ○ |
| `StatusEffects.cs` | 순수 class, `Combat`이 필드로 소유 | ○ |
| `BeltScroll.cs` | `static class` | ○ |

---

## 3. 목표 배치 — 13 → 11

```
Entities/
├ Combat.cs           Combat + StatusEffects              1055+214 → ~1264
├ Entity.cs           그대로                                        626
├ Physics.cs          그대로                                        625
├ BeltScrollView.cs   BeltScrollView + BeltScroll          204+130 →  ~329
├ EntityAnimator.cs   그대로                                        284
├ Attack.cs           그대로                                        227
├ Projectile.cs       그대로                                        209
├ BasicAttackProfile.cs  그대로                                     197
├ Pilotable.cs        그대로                                         57
├ AllyBasicAttack.cs  그대로                                         51
└ EnemyBasicAttack.cs 그대로                                         19
                                                        합계    ~3888
```

### 병합 2건의 근거

**`StatusEffects` → `Combat.cs`**
자기 주석이 소유 관계를 명시한다:

> 한 캐릭터에 걸린 지속시간 있는 상태의 목록. **`Combat` 하나당 한 벌.**

외부 소비자 5파일은 전부 `combat.StatusEffects` 경유. 순수 class라 프리팹 무관.
`Combat.cs`가 1264줄이 되지만 이미 8개 섹션으로 나뉘어 있어 한 섹션이 늘 뿐이다.

**`BeltScroll` → `BeltScrollView.cs`**
같은 개념(벨트스크롤 2.5D 좌표계)의 규칙(static)과 표현(MonoBehaviour)이다.
`BeltScrollView` 주석이 `BeltScroll`을 직접 가리킨다:

> 깊이를 화면에 보여 주는 일은 기울어진 카메라가 한다(`BeltScroll`).

`BeltScroll`은 `static class`라 `BeltScrollView.cs` 안에 그대로 들어간다.
`BeltScrollView`가 프리팹 15곳에 물려 있으므로 **파일명은 `BeltScrollView.cs` 유지.**

---

## 4. 합치지 않는 것과 그 이유

**`Combat.cs` 1055줄 — 쪼개지도 더 합치지도 않는다**
이미 8개 섹션으로 정리돼 있고 전부 다른 일을 한다:

```
:119  가드          :434  맞는 쪽            :912  물리 이벤트 반응
:318  디버프        :735  가드·가드브레이크   :1043 외부 조작 (ISkillEffect)
:398  때리는 쪽     :827  대시 패링
```

주석이 존재 이유를 말한다 — *"타격·피격·상태 전이의 단일 창구.
온힛 트리거·흡혈·콤보 카운트를 여기 한 곳에서 처리한다(결정 로그 ②)."*
**의도적으로 한 곳에 모은 것이다.** 소비자 46파일이 이 창구를 본다.

**`BasicAttackProfile` → `Entity.cs` 병합 안 함**
파일 참조는 0이라 기술적으로는 가능하다. 하지만 주석이 **Entity에서 뺀 이유**를
명시적으로 기록하고 있다:

> **왜 `Entity`에서 뺐나.** 예전에는 이 값이 전부 Entity에 있었고, 그래서 적
> 프리팹 인스펙터에도 연타 단계 칸이 떴다. (…) **채울 수 있다는 것 자체가 저작
> 실수의 통로**였다 — 값이 새면 테스트(`BasicComboPrefabTests.EnemiesStaySingleHit`)
> 로만 잡혔다.

파일만 합치고 컴포넌트는 분리 유지라 **동작은 안 바뀐다.** 그래도 되돌린 것처럼
읽히고, 다음 사람이 필드를 Entity로 옮기는 통로가 다시 열린다. **197줄 아끼자고
낼 위험이 아니다.**

**`Physics` · `Attack` · `EntityAnimator` 등 9개**
프리팹이 막는다 (2절).

---

## 5. 단계

각 단계 = 별도 커밋.

### 1단계 — `BeltScroll` → `BeltScrollView.cs`

- `BeltScroll.cs` 본문을 `BeltScrollView.cs`의 `namespace` 안으로 이동
- `BeltScroll.cs` + `.cs.meta` 삭제
- **`BeltScrollView.cs` 파일명 유지** (프리팹 15곳)
- 네임스페이스 · `using` 확인 후 이동

가장 안전하다 — `BeltScroll`은 `static class`, 테스트 7파일이 받쳐 준다.

### 2단계 — `StatusEffects` → `Combat.cs`

- `StatusEffects` + 중첩 `struct Entry`를 `Combat.cs`의 `namespace` 안으로 이동
- `StatusEffects.cs` + `.cs.meta` 삭제
- **`Combat.cs` 파일명 유지** (프리팹 15곳)

테스트 2파일뿐이라 1단계보다 얇다. 씬 재생 검증 비중이 크다.

### 그 외 — 하지 않는다

`Entities/`는 여기까지다. 남은 9파일은 프리팹이 잠갔고, `Combat.cs`는 의도적
집중이라 건드릴 이유가 없다.

---

## 6. 결과

```
Entities/   13 파일 3898 줄  →  11 파일 ~3888 줄
프리팹 배선  16곳             →  16곳 (변동 없음)
```

**줄도 파일도 거의 안 준다.** 실패가 아니라 측정 결과다 —
Entities는 캐릭터 프리팹 12~13개와 소비자 46파일에 물려 있고, 조립 코드가 0줄이다.

누적:

| 캔버스 | 그룹 | 전 | 후 |
|---|---|---|---|
| #1 | Battle | 17 파일 3,295 | **7** 파일 3,009 |
| #2 | Battle/UI | 12 파일 3,867 | **8** 파일 2,831 |
| #3 | Battle/Tactic | (#1에 흡수) | — |
| #4 | Entities | 13 파일 3898 | 11 파일 3888 |

---

## 7. 다음 그룹으로 넘길 것

- **`Entity.cs`(626, 프리팹 0)** — 합칠 이웃이 `Entities/` 안에 없다. `Player` ·
  `Enemy` · `Ally`가 상속하므로 **캔버스 #8 `Characters/`(4파일 506줄)** 를 볼 때
  같이 판단한다. 다만 `Player`·`Enemy`·`Ally`는 프리팹에 붙으므로 각자 파일이
  필요하고, `Entity`만 그 폴더로 가는 게 이득인지는 그때 따진다.
- **`BeltScroll` 소비자 15파일** — `Vfx/`(캔버스 #13)가 다수. #13 볼 때 확인.
- `Core/BasicAttackCombo.cs`의 `BasicComboRules` — `BasicAttackProfile` ·
  `ComboMeter` 양쪽이 참조. 캔버스 #5 `Core` 볼 때 판단.

---

## 검증 — 씬 재생해서 눈으로 확인할 것

테스트가 두껍다(`Attack` 21 · `Combat` 19 · `Physics` 18). 테스트로 못 잡는 것만.

**1단계 후 (`BeltScroll` 병합)**
- [ ] 캐릭터 발이 바닥에 닿아 보이는가 (빌보드 + 화면 위 보정)
- [ ] 깊이(Z)로 이동할 때 가로로 새지 않는가
- [ ] 스프라이트 앞뒤 정렬이 깊이 순인가
- [ ] 투사체가 벨트스크롤 좌표를 그대로 따르는가 (`Projectile` → `BeltScroll`)

**2단계 후 (`StatusEffects` 병합)**
- [ ] 스턴 · 빙결 걸렸을 때 지속시간 바가 실제 해제 시점과 맞는가
      (주석: *"타이머를 두 벌 굴리면 화면과 실제가 반드시 어긋난다"*)
- [ ] 보호막 지속시간이 화면에 뜨는가
- [ ] 상태이상 중 사망 · 교대 시 정리되는가
- [ ] 프리팹 인스펙터에서 `Combat` · `BeltScrollView`가
      `Missing (Mono Script)`가 아닌가 — 파일명 유지 확인

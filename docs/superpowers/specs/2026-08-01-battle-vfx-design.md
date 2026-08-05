# 전투 이펙트 (BattleVfx) — 설계

- 날짜: 2026-08-01
- 브랜치: `feat/khb-enemy`
- 범위: 휘두름 · 타격 · 불릿타임 시전 범위 3종. 아트 없이 절차적으로 그리고, 스킬별로 색과 크기를 다르게 줄 수 있게 한다.

## 1. 배경 · 문제

전투에 시각 연출이 하나도 없었다. 있던 건 [`Attack`](../../../Assets/Scripts/Entities/Attack.cs)의
`marker` — 히트박스가 켜진 동안 스프라이트를 보여 주는 **디버그 표시**뿐이다. 판정은 다 돌지만
때리는 게 보이지 않으니 타격감을 평가할 수가 없다.

### 이름 충돌

`Effect`라는 이름은 이미 게임플레이 쪽이 점유하고 있다.

| 기존 | 하는 일 |
|---|---|
| [`ISkillEffect`](../../../Assets/Scripts/Skill/ISkillEffect.cs) | 끌어당기기 · 띄우기 · 보호막 같은 **판정 효과** |
| [`EffectRunner`](../../../Assets/Scripts/Skill/EffectRunner.cs) | 지속시간이 끝나면 되돌리는 스케줄러 |

여기에 시각 연출을 `Effect`로 끼워 넣으면 "PullEffect가 이펙트고 SlashEffect도 이펙트"가 되어
읽는 사람이 매번 어느 쪽인지 따져야 한다. 시각 연출은 **`Vfx`** 로 분리한다.

### 제약 3개

1. **아트가 0장이다.** `arts/`에 캐릭터 시트 39장과 UI 9장이 있지만 슬래시·임팩트·먼지는 한 장도 없다.
2. **2.5D 벨트스크롤이다.** 논리 좌표는 3D(X 좌우 / Z 깊이 / Y 높이)인데 카메라를 기울이지 않으므로
   [`BeltScroll`](../../../Assets/Scripts/Entities/BeltScroll.cs)이 Z를 화면 세로로 접는다
   (`screenY = Y + Z · DepthToScreen`). 이 변환을 안 거친 연출은 캐릭터와 다른 공간에 뜬다.
3. **`Time.timeScale`을 쓰지 않는다**(결정 로그 ⑥). 불릿타임은
   [`TimeControl.Scale`](../../../Assets/Scripts/Core/TimeControl.cs)만 0으로 만든다.
   `Time.deltaTime`으로 도는 연출은 시간이 멈춰도 혼자 재생된다.

**목표**: 아트 없이 지금 당장 타격감을 볼 수 있게 하고, 나중에 스프라이트가 들어와도 호출부를
안 건드리게 한다. 스킬이 각자 다른 색을 낼 수 있어야 한다.

**비목표**: 파티클 시스템 도입, 히트스톱·카메라 셰이크, 사망·피격 연출, 스프라이트 시트 임포트.

## 2. 아키텍처

```
Attack (히트박스)                          ← 유일한 훅 지점
  ├ Begin(hit, vfx)  → BattleVfx.Swing
  └ TryHit(collider) → BattleVfx.Impact
                              │
                     BattleVfx  진입점 · 전역색 · Enabled 스위치
                              │
                     VfxRunner  자동 생성 싱글턴 · 풀
                          ├─ VfxLine        Arc / Star / Ring
                          └─ RangeIndicator ← TargetSelector를 읽는다

SkillData.vfx : SkillVfx ─→ SkillState.FireMelee ─→ Attack.Begin(hit, vfx)
                        └─→ SkillState.LaunchProjectile ─→ Projectile.Launch ─→ Attack.Begin(hit, vfx)
```

### 신규 — `Assets/Scripts/Vfx/`

| 파일 | 하는 일 | 안 하는 일 |
|---|---|---|
| [`BattleVfx.cs`](../../../Assets/Scripts/Vfx/BattleVfx.cs) | `Swing()` · `Impact()` 진입점, 전역 색, 머티리얼 생성. `VfxRunner`가 풀 관리 | 좌표 변환, 그리기 |
| [`VfxLine.cs`](../../../Assets/Scripts/Vfx/VfxLine.cs) | 선 하나의 수명 · 모양 · 좌표 접기 | 언제 터질지 판단 |
| [`RangeIndicator.cs`](../../../Assets/Scripts/Vfx/RangeIndicator.cs) | 조준 범위 타원 + 커서 마름모 | 조준 입력 처리 (`TargetSelector` 소관) |
| [`SkillVfx.cs`](../../../Assets/Scripts/Vfx/SkillVfx.cs) | 스킬 한 개의 색 · 크기 값 묶음 | 로직 |

### 수정

| 파일 | 변경 |
|---|---|
| [`Entities/Attack.cs`](../../../Assets/Scripts/Entities/Attack.cs) | `Begin` 오버로드 추가, `style` 보관, `EmitSwing`/`EmitImpact`, 인스펙터 토글 2개 |
| [`Entities/Projectile.cs`](../../../Assets/Scripts/Entities/Projectile.cs) | `Launch`에 옵션 인자 `in SkillVfx vfx = default` |
| [`Skill/SkillData.cs`](../../../Assets/Scripts/Skill/SkillData.cs) | 연출 섹션에 `public SkillVfx vfx` |
| [`Skill/SkillState.cs`](../../../Assets/Scripts/Skill/SkillState.cs) | `FireMelee` · `LaunchProjectile`이 `data.vfx`를 실어 보냄 |

## 3. 결정 근거

### 훅을 `Attack`에 둔 이유

처음 후보는 [`EntityStates.AttackState.Tick`](../../../Assets/Scripts/StateMachine/States/EntityStates.cs)의
`windup` 시점이었다. 거기 걸면 **평타만** 덮인다. 스킬은
`SkillState.FireMelee`가 따로 히트박스를 켜고, 공중공격은 `AerialAttackState.Enter`가 켠다.

세 경로가 전부 지나는 목이 `Attack.Begin` 하나다. 여기 한 곳에 걸면:

- 평타 · 공중공격 · 스킬 · 스킬 투사체가 전부 자동으로 덮인다
- 다단히트는 `SkillState.FireNextHit`이 매 타마다 `Begin`을 부르므로 타마다 터진다
- `EntityStates.cs`를 안 건드린다 — 상태머신은 판정 타이밍의 주인이고, 연출은 그 아래 계층이다

적중 쪽도 같다. `Attack.TryHit`이 유일한 적중 지점이고, 이미
`public event Action<Combat> OnHit`이 있다. 이벤트를 구독하는 대신 `TryHit` 안에서 직접 부른 이유는
`OnHit`이 [`Projectile.HandleHit`](../../../Assets/Scripts/Entities/Projectile.cs)의 관통 카운트용으로
쓰이고 있어서, 여기에 연출을 얹으면 두 관심사가 한 이벤트에 섞이기 때문이다.

### LineRenderer로 그린 이유

아트가 0장이다. 선택지는 셋이었다.

| 방법 | 문제 |
|---|---|
| 스프라이트 시트 | 없다. 만들거나 사와야 한다 |
| ParticleSystem | 결국 텍스처가 필요하고, 프리팹을 씬에 배선해야 한다 |
| **LineRenderer** | 텍스처 없이 정점 색만으로 그려진다 |

`VfxLine` 하나가 세 모양을 다 낸다 — `Arc`(부채꼴 궤적), `Star`(뾰족한 고리), `Ring`(퍼지는 원).
LineRenderer는 폴리라인 하나뿐이라 흩어진 파편을 못 그리지만, **바깥 반경과 안쪽 반경을 번갈아
찍으면** 한 줄로도 별 모양이 나온다(`DrawStar`). 파편처럼 읽힌다.

나중에 시트가 들어오면 `VfxLine`만 갈아끼우면 된다. `Attack`은 좌표와 크기만 넘기므로 그대로다.

### 논리 좌표로 받고 `VfxLine` 안에서만 접는 이유

`BeltScroll.ToView`를 호출부가 부르게 두면, 이미 접힌 좌표를 다시 넘기는 실수가 반드시 난다.
두 번 접히면 이펙트가 화면 위로 튄다. 조준점에서 한 번 겪은 문제다 —
`TargetSelector`가 `CursorPoint`(논리)와 `CursorViewPoint`(화면)를 굳이 둘로 나눠 들고 있는 이유가 그거다.

그래서 `BattleVfx.Swing/Impact`는 **논리 좌표만** 받고, `ToView` 호출은 `VfxLine.PointAt` 한 군데에 가둔다.

부수 효과가 하나 있는데 유용하다. 논리 XZ 평면의 원을 접으면 화면에서 `DepthToScreen`
비율의 **타원**이 된다 — 바닥에 누운 원으로 읽힌다. 별도 계산이 필요 없다.

### 정렬 공식을 `BeltScrollView`와 맞춘 이유

[`BeltScrollView.ApplySorting`](../../../Assets/Scripts/Entities/BeltScrollView.cs)이
`sortingOrder = -z * 100`으로 캐릭터를 깊이 정렬한다. 이펙트가 다른 공식을 쓰면 멀리 있는
적의 이펙트가 가까운 캐릭터 앞에 뜬다.

같은 기준선 위에서 오프셋만 다르게 준다.

| 대상 | 오프셋 | 이유 |
|---|---|---|
| 휘두름 · 타격 | `+50` | 같은 깊이의 캐릭터보다 앞 |
| 범위 원 | `-20` | 바닥에 깔린 표시. 발을 가리면 안 된다 |
| 조준 커서 | `+200` | 원이 커져도 중심은 항상 보여야 한다 |

### 시계를 나눈 이유

| 대상 | 시계 | 근거 |
|---|---|---|
| `VfxLine` | `TimeControl.DeltaTime` | 불릿타임에 멈춰야 한다. `Projectile.Update`와 같은 선택 |
| `RangeIndicator` 깜빡임 | `TimeControl.UnscaledDeltaTime` | 시간이 멈춘 동안 조준하는 것이므로 멈추면 안 된다. `TargetSelector.MoveByKeyboard`와 같은 선택 |

### 크기를 히트박스 `bounds`에서 뽑은 이유

휘두름 반경을 상수로 두면 판정을 키웠을 때 "보이는 것 ≠ 맞는 것"이 된다. `Attack.EmitSwing`은
콜라이더의 실제 월드 bounds에서 위치와 반경을 그대로 가져온다.

```csharp
Bounds b = box.bounds;   // box.enabled = true 뒤라야 유효하다
BattleVfx.Swing(new Vector3(b.center.x, 0f, b.center.z), b.center.y,
                facing, Mathf.Max(b.extents.x, b.extents.z), in style);
```

호출 순서가 중요하다 — `box.enabled = true` **다음에** 불러야 bounds가 산다.

### `SkillVfx`의 `default`가 "전역색"인 이유

`Assets/Data/Skills/`에 `SkillData` 에셋이 27개 있다. 색 필드를 그냥 추가하면 기존 에셋은 전부
`Color(0,0,0,0)`으로 역직렬화되어 이펙트가 투명해지거나 검게 나온다. 27개를 손으로 고치는
마이그레이션이 생긴다.

그래서 구조체에 `bool custom`을 두고, **꺼져 있으면 전역 색으로 떨어지게** 했다.
`default(SkillVfx)`는 `custom == false`이므로 자동으로 안전한 쪽이다. 마이그레이션이 0이 된다.

`scale`도 같은 이유로 `Scale => scale > 0f ? scale : 1f` 창구를 거친다 — 인스펙터에서 안 채운
0이 이펙트를 지우지 않게.

### `Attack`이 `style`을 필드로 붙든 이유

휘두름은 `Begin` 시점, 타격은 `TryHit` 시점이다. 그 사이에 몇 프레임이 있다. 지역 변수로는 못 넘긴다.

더 중요한 건 **히트박스가 공유된다**는 점이다.

```csharp
public Attack SkillAttack => skillAttack != null ? skillAttack : basicAttack;
```

스킬 전용 히트박스가 없으면 평타 히트박스를 재사용하고, 있어도 그 캐릭터의 모든 스킬이 같은
것 하나를 돌려쓴다. 그래서 `Begin`마다 색을 **다시** 실어야 한다. 안 그러면 직전에 쓴 스킬 색이 남는다.
`SkillState.FireMelee`가 매번 `data.vfx`를 넘기는 이유다.

### 투사체에서 휘두름을 자동으로 끈 이유

`Projectile.Launch`도 `hitbox.Begin`을 부른다. 훅이 `Begin`에 있으니 화살에도 검 궤적이 그려진다.

프리팹마다 체크박스를 끄게 하면 새 투사체를 만들 때마다 잊는다. `Awake`에서 한 번 판정해서
자동으로 뺀다.

```csharp
isProjectile = GetComponent<Projectile>() != null;
```

색은 그대로 전달된다 — 궤적만 빠지고 **적중 이펙트에는 스킬 색이 적용된다**.

### 자동 생성 싱글턴인 이유

씬에 프리팹을 놓고 배선하게 하면 씬이 늘 때마다 빠뜨린다. `VfxRunner.Instance`가 첫 호출에
`[BattleVfx]` 오브젝트를 만들고 `DontDestroyOnLoad`로 올린다.
[`EffectRunner`](../../../Assets/Scripts/Skill/EffectRunner.cs)가 이미 쓰는 방식이라 새 패턴이 아니다.

`RangeIndicator`도 여기서 같이 붙인다. 씬 배선이 0이 된다.

에디터 정지 중에 오브젝트가 생겨 씬에 쓰레기가 남는 걸 막으려고
`quitting || !Application.isPlaying`이면 `null`을 돌려주고, 호출부는 그때 조용히 넘어간다.

### 오버로드로 넓힌 이유

기존 호출부를 한 줄도 안 고치기 위해서다.

```csharp
public void Begin(in HitData data) => Begin(in data, default);           // 기존 호출부 그대로
public void Begin(in HitData data, in SkillVfx vfx) { ... }              // 스킬 경로
```

```csharp
public void Launch(..., int layer, in SkillVfx vfx = default)            // 옵션 인자
```

덕분에 `EntityStates`(평타 · 공중공격)와 `Entity.FireBasicProjectile`은 수정이 0줄이다.

## 4. 스킬별 설정

`SK_**.asset` → **연출** 섹션.

| 필드 | 설명 |
|---|---|
| `animation` | 시전자 본체 모션. **이펙트가 아니다** — `EntityAnimator`가 `Skill_Placeholder` 슬롯 클립을 갈아끼운다 |
| `vfx.custom` | 끄면 전역색. 켜야 아래 값이 먹는다 |
| `vfx.swing` | 궤적 색 |
| `vfx.impact` | 파편 색 |
| `vfx.shock` | 충격파 색 |
| `vfx.scale` | 이펙트 크기 배율. **판정 크기는 안 건드린다** |

전역 기본값과 조절 지점.

| 위치 | 값 |
|---|---|
| `BattleVfx.Enabled` | 전역 on/off (성능 비교 · 캡처용) |
| `BattleVfx.SwingColor` / `ImpactColor` / `ShockColor` | 전역 색 |
| `Attack` 인스펙터 | `swingVfx` / `impactVfx` 개별 토글 |
| `RangeIndicator` 인스펙터 | `okColor` / `whiffColor` / `lineWidth` / `pulseSpeed` |
| `VfxLine` 상수 | `ArcSpan 110°`, `ArcTrail 55°`, 세그먼트 수 |
| `BattleVfx` 상수 | `SwingDuration 0.16s`, `StarDuration 0.18s`, `RingDuration 0.22s` |

## 5. 알려진 한계 — 검토 대상

우선순위 순.

### 5.1 `vfx.scale`이 차징과 연동되지 않는다

[`ChargeSkillState`](../../../Assets/Scripts/Skill/ChargeSkillState.cs)가 `SkillContext.RadiusScale`로
광역 판정을 최대 `maxChargeRadiusMul`(기본 1.6)배까지 키운다. 이펙트는 그대로다.
최대까지 모아도 연출이 안 커지므로 차징 보상이 안 읽힌다.

고치려면 `ModifyHit` 근처에서 배율을 `SkillVfx`에 실어 보내는 훅이 하나 더 필요하다.

### 5.2 셰이더 스트립 위험

`Shader.Find("Sprites/Default")`는 런타임 조회다. 이 셰이더를 참조하는 머티리얼 에셋이
프로젝트에 없으면 빌드에서 스트립될 수 있다. 에디터 플레이는 영향 없다.

→ Project Settings → Graphics → Always Included Shaders에 `Sprites/Default` 등록.

현재 파이프라인은 빌트인이다(`GraphicsSettings.m_CustomRenderPipeline: {fileID: 0}`).
URP 패키지는 설치돼 있지만 할당돼 있지 않다. **URP로 전환하면 이 부분을 다시 봐야 한다.**

### 5.3 조준 표시가 `GroundPoint`에만 있다

`RangeIndicator`는 `TargetingType.GroundPoint`일 때만 그린다. `EnemyUnit`(대상 지정)과
`Direction`(방향 지정)은 아무 표시가 없다. `TargetSelector.HoveredUnit`이 이미 있으므로
대상 하이라이트는 붙일 수 있다.

### 5.4 기존 Gizmo와 중복

`TargetSelector.OnDrawGizmosSelected`가 같은 원을 그린다. 에디터에서 그 오브젝트를 선택하면
두 개가 겹쳐 보인다. 지울지 남길지 결정 필요.

### 5.5 평타는 스킬별 색을 못 낸다

`Entity`에 `SkillVfx` 필드가 없어서 평타는 항상 전역색이다. 캐릭터별로 다르게 하려면
`Entity`에 필드를 추가하고 `AttackState`가 넘겨야 한다.

### 5.6 풀에 상한이 없다

`Prewarm 8`로 시작하고 모자라면 계속 새로 만든다. 반납만 하고 줄이지는 않는다.
난전에서 동시 이펙트가 많아지면 `VfxLine` 오브젝트가 누적된다. 프로토타입 규모에서는 문제없지만
상한이나 축소 정책이 없다는 건 기록해 둔다.

### 5.7 머티리얼이 2개 만들어진다

`VfxRunner`와 `RangeIndicator`가 각자 `BattleVfx.CreateMaterial()`을 부른다.
소유권을 단순하게 하려고 일부러 나눴지만, 하나로 공유해도 된다.

### 5.8 인접 제약 — `.anim` 클립으로 이동·회전 연출 불가

이펙트와 별개지만 같이 알아야 한다. `BeltScrollView.LateUpdate`가 매 프레임
`sprite.position`과 `sprite.rotation`을 덮어쓴다(빌보드).

```csharp
sprite.position = BeltScroll.ToView(ground, height + spriteOffsetY);
sprite.rotation = Quaternion.identity;
```

Animator는 LateUpdate 앞에서 돌기 때문에, 클립에 `Sprite`의 위치·회전 커브를 넣어도 **무시된다**.
기존 플레이스홀더 클립들이 `m_LocalScale`과 `m_Color.a`만 쓰는 이유가 이거다.
찌르기 전진 같은 모션은 클립으로 만들 수 없다.

## 6. 검증

### 컴파일

```
dotnet build Assembly-CSharp.csproj
```

현재 **오류 0개**. 경고 9개는 기존 Unity 패키지 참조 버전 충돌로, 이번 변경과 무관하다.

> `Assembly-CSharp.csproj`는 Unity가 생성하고 `.gitignore`에 걸려 있다.
> 새 파일이 목록에 없으면 `CS0246`이 난다 — Unity를 한 번 띄우면 갱신된다.

### 플레이 확인

| 확인 | 기대 |
|---|---|
| 평타 | 히트박스 켜지는 순간 앞쪽에 흰 부채꼴, 맞으면 대상 몸통에 노란 파편 + 주황 충격파 |
| 다단히트 스킬 | `hitDataList` 개수만큼 반복 |
| 원거리 스킬 | 발사 시 궤적 없음, 적중 시 이펙트 |
| E키 조준 (GroundPoint) | 바닥에 청록 타원 + 중앙 마름모, 깜빡임 |
| 반경 안에 적 0마리 | 타원이 빨강 (`TargetSelector.WillWhiff`) |
| 불릿타임 중 | 이펙트 정지, 조준 표시 깜빡임은 계속 |
| 스킬 `vfx.custom` on | 그 스킬만 지정 색 |

### 리뷰 포인트

1. 5.1 차징 연동 — 지금 붙일지, 차징 튜닝할 때 같이 볼지
2. 5.4 Gizmo 중복 — 지울지
3. `ArcSpan 110°` / `SwingDuration 0.16s` 체감
4. `Vfx` 네이밍 — `ISkillEffect`와의 구분이 실제로 읽히는지

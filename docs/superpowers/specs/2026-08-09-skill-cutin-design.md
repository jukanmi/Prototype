# 스킬 컷인 연출 설계

날짜: 2026-08-09

## 목표

콤보 실행 중 **각 슬롯의 스킬이 발동하기 직전**, 화면 왼쪽에 시전 동료의 얼굴과
스킬명이 잠깐 떴다 사라진다. 컷인이 도는 동안 전투는 완전히 멈춘다.

## 결정 사항

| 항목 | 결정 | 이유 |
|---|---|---|
| 적용 범위 | 콤보 슬롯만 (`ComboExecutor` 경로) | 연출 밀도가 높고 "결정타" 느낌이 산다. 라이브 고유기(ASDF)·실시간 카드(U키)는 자주 나가 시야를 가린다 |
| 시간 처리 | 완전 정지 후 발동 (`TimeControl.Scale = 0`) | 스킬명을 읽을 시간이 확실히 생기고 타이밍 계산이 깔끔하다 |
| 얼굴 소스 | `Ally.portrait` 필드 + 폴백 | 초상화 에셋이 아직 없다. 나중에 그림만 꽂으면 끝난다 |
| 빈도 | 매 슬롯마다 전부 | 슬롯마다 다른 스킬이므로 전부 보여 준다. 길이를 0.5s 안팎으로 짧게 잡아 리듬을 만든다 |
| 모양 | 왼쪽에서 슬라이드 인/아웃, 약 0.5s | |
| 배선 방식 | 인터페이스 삽입 (`ISkillCutin`) | Executor가 UI 구현을 모른다. 미배선이면 기존 동작 그대로. 테스트에서 페이크 주입 가능 |

## 아키텍처

### `ISkillCutin` — `Assets/Scripts/Battle/ISkillCutin.cs`

```csharp
public interface ISkillCutin
{
    /// <summary>컷인을 한 번 재생한다. 끝날 때까지 Executor가 기다린다.</summary>
    IEnumerator Play(Ally caster, SkillData data);

    /// <summary>재생을 즉시 중단하고 정지시킨 시간을 되돌린다.</summary>
    void Cancel();
}
```

`Cancel`이 인터페이스에 있는 이유: `ComboExecutor.Abort()`는 `StopCoroutine`으로 실행을
잘라 낸다. 잘린 코루틴은 `finally`가 돌지 않으므로 `TimeControl.Scale`이 0에 묶인 채
게임이 영구 정지한다. 복원 책임을 명시적인 진입점으로 노출한다.

### `SkillCutinUI : MonoBehaviour, ISkillCutin` — `Assets/Scripts/Battle/UI/SkillCutinUI.cs`

`RecentHitEnemyHUD`와 같은 방식으로 캔버스·패널을 코드로 짓는다(프리팹 없음).

- 캔버스: `ScreenSpaceOverlay`, `sortingOrder = 10`
  (`RecentHitEnemyHUD` 2 위, `DeckInspectorUI` 20 아래)
- `CanvasScaler`: `ScaleWithScreenSize`, 1920×1080, `matchWidthOrHeight = 1`
- 루트 패널은 평소 비활성. `Play` 동안만 켠다.

**레이아웃**

| 요소 | 앵커 / 피벗 | 크기 | 위치 |
|---|---|---|---|
| 초상화 패널 | `(0, 0.5)` / `(0, 0.5)` | 360×360 | 등장 x=+48, 대기 x=−400 |
| 초상화 이미지 | 패널 채움 (안쪽 여백 8) | — | `Ally.Portrait`, 없으면 Role 색 |
| 폴백 이름 라벨 | 패널 하단 | — | 초상화가 없을 때만 켠다 |
| 스킬명 라벨 | `(0, 0.5)` / `(0, 0.5)` | 640×72 | 등장 x=+300, 대기 x=−660 |

스킬명은 초상화 오른쪽 상단에 겹친다(y 오프셋 +90). 굵은 44pt, 흰색.

**Role 폴백 색** — 초상화가 비었을 때 쓰는 단색.

| Role | 색 |
|---|---|
| Tanker | `#3D6EA8` |
| Warrior | `#B0483C` |
| Archer | `#3F8F5B` |
| Wizard | `#7A4FA8` |

**타임라인** (전부 `TimeControl.UnscaledDeltaTime` 기준)

| 구간 | 길이 | 내용 |
|---|---|---|
| In | 0.15s | 초상화가 대기 위치 → 등장 위치. ease-out (`1-(1-t)²`) |
| Hold | 0.6s | 정지 |
| Out | 0.15s | 등장 위치 → 대기 위치. ease-in (`t²`) |

총 0.9s. 스킬명 라벨은 같은 타임라인을 **0.08s 늦게** 따라간다 — 두 요소가 층을 이뤄
들어오고 나간다. 지연 때문에 라벨의 Out은 초상화보다 0.08s 늦게 끝나므로,
전체 길이는 `0.9 + 0.08 = 0.98s`이다. Executor는 이 전체 길이를 기다린다.

네 값은 전부 `[SerializeField]`라 **재생 중 인스펙터에서 조절한다.** 처음에는
In 0.12 / Hold 0.25 / Out 0.12 / 지연 0.06(총 0.55s)으로 잡았는데 "읽기도 전에 사라진다"는
피드백이 나와 위 값으로 올렸다. 슬롯 4개면 약 4초가 정지 시간으로 들어가므로,
콤보가 늘어지면 Hold부터 내린다.

**진행률 계산은 순수 함수로 분리한다.**

```csharp
/// <summary>경과 시간 → 0(대기 위치) ~ 1(등장 위치) 사이의 보간 계수.</summary>
public static float SlideAmount(float elapsed, float delay);
```

코루틴 없이 단위 테스트할 수 있어야 한다.

**시간 정지**

```
Play 진입  → savedScale = TimeControl.Scale; TimeControl.Scale = 0
정상 종료  → TimeControl.Scale = savedScale
Cancel()   → 패널 숨기고 TimeControl.Scale = savedScale
```

이미 0인 상태에서 다시 들어와도 저장값이 0이므로 복원해도 0이다 — 중첩 호출에서
시간이 멋대로 되살아나지 않는다. `Play`가 도는 동안 재진입은 없다(Executor가 직렬 실행).

### `Ally` 변경

```csharp
[Tooltip("컷인에 뜨는 얼굴. 비우면 직업 색 박스로 대체된다.")]
[SerializeField] private Sprite portrait;

public Sprite Portrait => portrait;
```

### `ComboExecutor` 변경

```csharp
/// <summary>슬롯 실행 직전에 재생할 인트로. null이면 컷인 없이 곧바로 스킬로 간다.</summary>
public ISkillCutin Cutin { get; set; }

private void Awake() => Cutin ??= GetComponentInChildren<ISkillCutin>(true);
```

씬에서는 `SkillCutinUI`를 `ComboExecutor`와 같은 GameObject나 그 자식에 붙이면
자동 배선된다. 테스트는 세터로 페이크를 꽂는다.

**슬롯 루프 재구성** — 현재 유효성 검사(`data == null || caster == null || caster.Combat.IsDead`)가
`RunSlot` 내부에 있다. 컷인을 띄울지 판단하려면 루프 쪽에서 먼저 알아야 하므로
`CanRunSlot(slot)` 판정을 밖으로 끌어올린다.

```
while (queue.Count > 0)
    slot = queue.Dequeue()
    if (!CanRunSlot(slot))            → 경고 로그 후 OnSlotConsumed, continue
    yield return PlayCutin(slot)      → Cutin이 null이면 즉시 반환
    if (slot.Data.IsCharge)           → StartCharge, OnSlotConsumed, continue
    yield return RunSlot(slot)
    OnSlotConsumed(slot.card)
    slotGap 대기
```

`RunSlot` 안의 대상 재타겟 로직(죽은 대상 → 최근접 적)은 그대로 둔다.
컷인은 시전자와 스킬만 읽으므로 대상 확정 전에 띄워도 문제없다.
`RunSlot` 내부의 기존 유효성 검사도 그대로 남긴다 — `CanRunSlot`과 중복이지만
`RunSlot`이 단독으로도 안전해야 한다.

건너뛴 슬롯에서 `slotGap`을 기다리지 않는 건 현재 동작과 다르다(지금은 기다린다).
발동하지 않은 슬롯 때문에 콤보가 멈칫할 이유가 없으므로 의도적으로 바꾼다.

`ReleaseCharges`(차징 해제 시점)에는 컷인을 띄우지 않는다 — 시작 시 이미 한 번 나왔고,
같은 카드가 두 번 나오면 중복이다.

`Abort()`는 `StopCoroutine` 뒤에 `Cutin?.Cancel()`을 부른다.

## 데이터 흐름

```
ResolveState.Enter
  └ ComboExecutor.Execute(queue)
      └ Run 코루틴
          └ 슬롯마다:
              ├ PlayCutin  → SkillCutinUI.Play(caster, data)
              │                ├ Scale = 0
              │                ├ Duration(기본 0.98s) 동안 unscaled 슬라이드
              │                └ Scale 복원
              └ RunSlot    → caster.StateMachine.ForceChangeState(SkillState)
```

## 에러 처리

| 상황 | 동작 |
|---|---|
| `Cutin`이 null | 컷인 없이 기존 흐름 그대로 |
| `caster`가 null / 사망 | 슬롯 자체를 건너뛰므로 컷인도 안 뜬다 |
| `data`가 null | 위와 같다 |
| `portrait`가 null | Role 색 박스 + 동료 이름 라벨 |
| `skillName`이 빈 문자열 | 에셋 이름(`data.name`)으로 대체 |
| `Abort()` 중 컷인 재생 중 | `Cancel()`이 패널을 숨기고 `Scale` 복원 |
| 컷인 중 시전자 사망 | 시간이 멈춰 있어 발생하지 않는다. 발생하더라도 `RunSlot` 내부에 남겨 둔 사망 검사가 잡아 스킬을 내지 않는다 |

## 테스트

`Assets/Editor/Tests/`, 네임스페이스 `Prototype.Tests`, NUnit.

**`SkillCutinUITests`**
- `SlideAmount`: `elapsed = 0` → 0, In 종료 시점 → 1, Hold 구간 → 1, 전체 종료 → 0
- 지연(`delay`)이 붙으면 그만큼 뒤로 밀린다
- `Play` 코루틴을 수동 `MoveNext`: 진입 직후 `TimeControl.Scale == 0`, 소진 후 원래 값 복원
- 재생 도중 `Cancel()` → 즉시 `Scale` 복원, 패널 비활성

**`ComboExecutorCutinTests`** — `FakeCutin : ISkillCutin`(호출 기록만 남기는 스텁) 주입
- 4슬롯 실행 → `Play` 호출 4회, 각 슬롯의 `caster`/`data`가 순서대로 맞다
- `Play` 호출이 해당 동료의 상태 전환보다 **먼저** 일어난다
- `Cutin`이 null이어도 기존 실행 흐름이 그대로 돈다
- `data`가 null인 슬롯은 `Play`를 부르지 않는다
- `Abort()` → `FakeCutin.Cancel` 호출 기록이 남는다

## 눈으로 확인할 것 (씬 재생)

테스트로 잡히지 않는 항목:

1. 컷인이 뜬 동안 적·동료·투사체가 전부 멈추는가
2. 4슬롯 콤보에서 컷인이 4번 나올 때 템포가 늘어지지 않는가 (길면 Hold를 줄인다)
3. 초상화 폴백 색 박스가 화면 왼쪽에 잘리지 않고 붙는가 (16:9 외 해상도 포함)
4. 스킬명이 초상화를 가리지 않고 읽히는가
5. 콤보 도중 동료가 죽어 슬롯이 건너뛰어질 때 컷인이 유령처럼 남지 않는가

## 범위 밖

- 라이브 고유기(ASDF)·실시간 카드(U키) 컷인
- 컷인 전용 사운드·카메라 줌
- 실제 초상화 그림 제작
- 컷인 스킵 입력

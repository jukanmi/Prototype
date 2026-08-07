# 벨트스크롤 깊이감 — 뒷벽 + 깊이 스케일

날짜: 2026-08-07
브랜치: feat/khb-enemy

## 목적

**점프로 뜬 높이가 화면에서 읽히게** 한다.

지금 SampleScene은 어두운 사각형 바닥 하나가 전부다. 배경에 세로 기준선이 없어서
캐릭터가 위로 떠도 "떴다"가 아니라 "화면에서 위로 갔다"로만 보인다.
깊이(Z)로 뒤에 선 적과 앞에 선 적도 구분되지 않는다 —
[BeltScroll.ToView](../../../Assets/Scripts/Entities/BeltScroll.cs)가 Z를 화면 세로로 접기만 할 뿐
크기는 그대로 두기 때문이다. 결과적으로 "높이 Y"와 "깊이 Z"가 화면에서 같은 신호가 된다.

두 경로로 깊이를 부호화해 둘을 갈라놓는다:

| 신호 | 담당 | 읽히는 것 |
|---|---|---|
| 세로 위치 | 기존 `ToView` (Y + Z·0.9) | 높이 + 깊이 (합쳐져 있음) |
| **크기** | 신설 `ScaleAt(z)` | 깊이만 |
| **뒷벽 + 경계선** | 신설 `Room/BackWall`·`Room/Horizon` | 높이의 기준면 |

## 범위

| 항목 | 결정 |
|---|---|
| 깊이 스케일 | 캐릭터(`BeltScrollView`) + 투사체(`Projectile`) |
| 강도 | unit당 6% — z=-3에서 1.18, z=+3에서 0.82 (앞뒤 1.44배) |
| 뒷벽 | 뒷면 1장 + 바닥/벽 경계선 1줄. 측벽·바닥 그라데이션 없음 |
| 바닥 모양 | 사각형 유지 — X에는 원근을 먹이지 않는다 |
| 논리 좌표 | **불변**. 이동·히트박스·벽 판정 전부 그대로 |

**비범위** (별건):
- `VfxSprite` 깊이 스케일 — 자기 `transform.localScale`을 이펙트 곡선으로 구동하므로
  ([VfxSprite.cs:93](../../../Assets/Scripts/Vfx/VfxSprite.cs#L93)) 캐릭터와 같은 View 노드 분리가 선행돼야 한다.
- `ChargeGauge`·`RangeIndicator`·`EnemyStateLabel`의 **크기** — UI성 표시라 일정한 편이 읽기 좋다.
  다만 **머리 위 오프셋은 배율을 따라야 한다**(아래 참조).
- 측벽·바닥 그라데이션·타일 패턴 — 아트가 붙을 때.
- `EnemyPrefabBuilder` 변종 프리팹 복구 — 별도 스펙. 이 스펙은 그것과 독립적으로 동작해야 한다.

## 왜 X에는 원근을 안 먹이나

진짜 원근이면 바닥이 뒤로 갈수록 좁아지는 사다리꼴이 되고 `screenX = x · ScaleAt(z)`가 된다.
안 하는 이유:

1. **역변환이 깨진다.** [TargetSelector](../../../Assets/Scripts/Battle/TargetSelector.cs#L166)가
   `BeltScroll.ToGround`로 마우스를 논리 좌표로 되돌린다. X가 Z에 의존하면 역변환이 2변수 연립이 된다.
2. **히트박스가 어긋난다.** 판정은 3D 콜라이더가 논리 X에서 하는데 그림은 다른 X에 나온다.
3. **벽이 어긋난다.** 벽 판정은 모든 z에서 x ∈ [-6, 6]인 직사각형이다
   ([SceneLayoutBuilder.BuildRoom](../../../Assets/Editor/SceneLayoutBuilder.cs#L274)).
   그림만 좁아지면 뒤쪽에서 캐릭터가 바닥 밖으로 걸어 나간 것처럼 보인다.

크기만 줄이고 바닥은 사각형으로 두는 건 벨트스크롤의 표준 치트다. 깊이는 충분히 읽히고
로직과 화면이 X에서 1:1로 유지된다.

## 전제 — 애니 클립이 `Sprite`의 localScale을 이미 쓰고 있다

[AnimationBuilder](../../../Assets/Editor/AnimationBuilder.cs)가 구운 플레이스홀더 클립은
`Sprite` 자식의 **localScale과 알파만** 움직인다(위치·회전은 `BeltScrollView`가 매 프레임 덮어쓰므로
쓸 수 없다). Idle의 바운스, Jump의 스트레치, Hit의 셰이크가 전부 스케일이다.
아군이 실제 도트 시트를 받은 뒤에도 같은 컨트롤러를 쓴다
([ArtImportBuilder.RigAlly](../../../Assets/Editor/ArtImportBuilder.cs#L440)).

따라서 `BeltScrollView`가 `sprite.localScale`에 깊이 스케일을 써넣으면
Animator(Update)와 BeltScrollView(LateUpdate)가 매 프레임 서로 덮어써서
**스쿼시·스트레치가 통째로 죽는다**. 스케일 채널을 나눠 가질 노드가 하나 더 필요하다.

## 구성

### 1. `BeltScroll` — 스케일 규약

`DepthToScreen`과 같은 자리에 한 벌만 둔다. 투사체·테스트가 같은 함수를 봐야 한다.

```csharp
/// <summary>깊이 1당 줄어드는 비율. z=0이 기준 1.0.</summary>
public static float DepthScalePerUnit { get; set; } = 0.06f;

/// <summary>깊이에 따른 표시 배율. 논리 좌표에는 영향이 없다.</summary>
public static float ScaleAt(float z) => Mathf.Max(MinScale, 1f - z * DepthScalePerUnit);
```

`MinScale`(0.05f) 하한이 필요한 이유: 방 밖으로 밀려나거나 `DepthScalePerUnit`을 크게 올리면
1 - z·k가 0 이하로 내려가 스프라이트가 뒤집히거나 사라진다.

`DepthToScreen`과 같은 방식으로 `BeltScrollView`가 자기 직렬화 값으로 덮어쓴다.

### 2. `BeltScrollView` — 스케일 전용 노드 분리

```
현재:  root ─ Sprite   (SpriteRenderer, Animator가 localScale 구동)
            └ Shadow

변경:  root ─ View     ← BeltScrollView가 position · rotation · localScale
            │   └ Sprite  ← Animator가 localScale만 구동, localPosition 0
            └ Shadow   ← BeltScrollView가 position · localScale
```

`LateUpdate`에서:

```
float s = BeltScroll.ScaleAt(ground.z);

view.position   = BeltScroll.ToView(ground, height + spriteOffsetY * s);
view.localScale = new Vector3(s, s, 1f);
view.rotation   = Quaternion.identity;          // 빌보드 — 기존과 동일

shadow.position   = BeltScroll.ToView(ground);
shadow.localScale = shadowBaseScale * shrink * s;
```

**`spriteOffsetY * s`가 핵심이다.** 스케일은 View의 원점을 중심으로 걸린다.

- 발밑 피벗(아군 도트, `spriteOffsetY = 0`) → View 원점이 곧 발 위치. 발이 안 뜬다.
- 센터 피벗(플레이스홀더, `spriteOffsetY = 0.5`) → View 원점이 발 위 0.5·s,
  스프라이트가 원점 기준 ±0.5·s로 그려져 아랫변이 정확히 바닥에 닿는다.

`s`를 안 곱하면 뒤쪽 캐릭터의 발이 그림자 위로 뜬다.

그림자에도 `s`를 곱한다. 그림자는 바닥에 눕는 물체라 깊이 배율을 똑같이 받아야 한다.

**호환 처리**: `depthRoot`가 비어 있으면 기존 경로 그대로 — `sprite`에 직접 position을 쓰고
스케일은 적용하지 않는다. 배선 안 된 프리팹(변종 적 등)이 깨지지 않고, 프리팹을 하나씩 옮길 수 있다.

**flipX**는 그대로 `facingRenderer`(Sprite의 SpriteRenderer)에 건다. View 스케일 X를 음수로
뒤집는 방법은 쓰지 않는다 — Animator가 쓰는 Sprite localScale과 부호가 섞이면 추적이 어렵다.

**정렬**(`ApplySorting`)은 변경 없다. 깊이 스케일과 무관하다.

### 3. `Room/BackWall` · `Room/Horizon` — 높이의 기준면

바닥 윗변은 화면 y = `RoomHalfZ · DepthToScreen` = 3 × 0.9 = **2.7**.

| 오브젝트 | localPosition | localScale | 색 | sortingOrder |
|---|---|---|---|---|
| `BackWall` | (0, 2.7 + 2.0, 0) | (12, 4, 1) | `(0.10, 0.11, 0.14)` | -10001 |
| `Horizon` | (0, 2.7, 0) | (12, 0.06, 1) | `(0.32, 0.34, 0.40)` | -9999 |

- 벽 색은 바닥 `(0.16, 0.17, 0.20)`보다 어둡다 — 뒤로 물러나 보인다.
- `Horizon`은 밝은 가로선 한 줄. 이 선이 "바닥이 여기서 꺾인다"를 만든다.
  점프한 캐릭터가 이 선을 넘어 벽면 앞으로 올라가면 높이가 즉시 읽힌다.
- 정렬은 바닥(-10000)을 사이에 두고 벽이 뒤, 경계선이 앞이다.
  캐릭터는 최대 z=3에서도 -300이라 셋 모두보다 앞이다.

**카메라**: y를 `0.8 → 1.3`으로 올린다. orthographicSize 5 기준 현재 보이는 세로 범위는
-4.2 ~ 5.8인데 바닥 아랫변이 -2.7이라 화면 아래 1.5가 빈 공간이고 그만큼 벽이 잘린다.
1.3으로 올리면 -3.7 ~ 6.3 — 아래 여백이 1.0으로 줄고 벽 4 중 3.6이 보인다.
벽 윗변이 살짝 잘리는 건 의도다. 벽이 화면 위로 계속 이어지는 편이 자연스럽다.

### 4. 머리 위에 붙는 표시 — 오프셋에만 배율

[ChargeGauge](../../../Assets/Scripts/Vfx/ChargeGauge.cs#L75)와
`EnemyStateLabel`(작업 중)은 `BeltScroll.ToView(ground, height + headOffset)`으로
캐릭터 머리 위에 붙는다. 캐릭터가 뒤에서 0.82배로 줄면 머리도 그만큼 내려오는데
`headOffset`이 고정이면 표시가 머리 위로 붕 뜬다.

`headOffset * BeltScroll.ScaleAt(ground.z)`로 고친다. **위젯 자체의 크기는 안 건드린다** —
게이지·글자는 어느 깊이에서나 같은 크기로 읽혀야 한다. 앵커 높이만 따라간다.

### 5. `Projectile` — 같은 배율

[Projectile.cs:123](../../../Assets/Scripts/Entities/Projectile.cs#L123)의 sprite·shadow에도
`BeltScroll.ScaleAt(ground.z)`를 곱한다. Projectile은 Animator가 없어 스케일 충돌이 없다 —
노드 분리 없이 `localScale`에 직접 쓴다.

캐릭터만 줄고 화염구는 안 줄면 뒤쪽 적에게 날아가는 투사체가 눈에 띄게 어긋난다.

## 빌더 파급

전부 재실행으로 해결되는 에디터 빌더다. 순서가 있다.

| 파일 | 변경 |
|---|---|
| [SceneLayoutBuilder](../../../Assets/Editor/SceneLayoutBuilder.cs) | `RigBeltScrollView`가 `View` 노드를 만들고 기존 `Sprite`를 그 아래로 옮긴다. `depthRoot`·`depthScalePerUnit` 배선. `BuildRoomVisual`에 `BackWall`·`Horizon` 추가. 카메라 y 1.3 |
| [AnimationBuilder](../../../Assets/Editor/AnimationBuilder.cs) | `SpritePath` `"Sprite"` → `"View/Sprite"`. 클립 재빌드 |
| [ArtImportBuilder](../../../Assets/Editor/ArtImportBuilder.cs) | `SpritePath` 동일 갱신 |

`RigBeltScrollView`의 재부모화는 여러 번 돌려도 같은 결과여야 한다 —
`View`가 이미 있고 `Sprite`가 이미 그 아래면 아무것도 하지 않는다.

실행 순서: `씬 벨트스크롤 배치로 정리` → `애니메이션 - 플레이스홀더 굽기 + 배선`.
반대로 하면 클립이 옛 경로로 구워진다.

## 검증

`Assets/Editor/Tests/`에 EditMode 테스트. 기존 파일들과 같은 형식.

**`BeltScrollDepthTests`**
- `ScaleAt(0)` == 1.0
- `ScaleAt(3)` ≈ 0.82, `ScaleAt(-3)` ≈ 1.18 (`DepthScalePerUnit` = 0.06)
- 아주 큰 z에서도 `MinScale` 아래로 안 내려간다
- `ToGround(ToView(p))` 왕복이 여전히 일치한다 — 스케일이 좌표 변환에 안 샜다는 확인

**`BeltScrollViewDepthTests`** (씬에 임시 오브젝트를 만들어 `LateUpdate` 강제 호출)
- `depthRoot` 있음: `view.localScale` == s, `view.position.y`에 `spriteOffsetY · s`가 반영
- `depthRoot` 있음: **`sprite.localScale`이 안 바뀐다** — 애니 충돌 회귀 방지. 이 테스트가 핵심이다
- `depthRoot` 있음: `shadow.localScale`에 s가 곱해진다
- `depthRoot` 없음: 기존 동작 그대로 (`sprite.position`만 갱신, 스케일 불변)

**`SceneLayoutBuilderRoomTests`**
- `BuildRoomVisual` 후 `Room/BackWall`·`Room/Horizon`이 생기고 sortingOrder가 바닥을 사이에 둔다
- 2회 실행해도 오브젝트가 중복되지 않는다
- `RigBeltScrollView` 2회 실행 시 `View` 노드가 하나뿐이고 `Sprite`가 그 아래 그대로

**눈으로 확인** (테스트로 못 잡는 것):
- SampleScene 플레이 → 점프한 캐릭터가 `Horizon` 선을 넘어 벽면 앞으로 올라가는가
- 뒤쪽(z=+2.4) 적과 앞쪽(z=-2.4) 적의 크기 차이가 보이는가
- 뒤쪽 캐릭터의 발이 자기 그림자 위에 정확히 붙어 있는가 (`spriteOffsetY · s` 확인)
- Idle 바운스·Jump 스트레치가 살아 있는가 (애니 충돌 확인)

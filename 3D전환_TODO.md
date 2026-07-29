# 3D 좌표계 전환 TODO

> 작성 2026-07-28. Unity 6000.5.5f1 / URP 17.6 / 2D 템플릿으로 생성된 프로젝트.
> 이 문서는 새 Claude Code 세션이 Unity MCP로 실행할 작업 목록.

## 배경 한 줄

2.5D XZ축 벨트스크롤. 이동은 XZ 평면, 높이는 Y축 별도. 물리는 Unity 시뮬레이션을 쓰지 않고 **코드로 직접 속도 제어**. Unity에 맡기는 건 **충돌 판정(트리거)뿐**.

**렌더러는 안 바꿈.** Renderer2D 유지. 렌더러는 물리·충돌과 무관하고, 2D Light를 계속 쓸 수 있음. 필요한 건 좌표계와 판정 세팅뿐.

---

## 1. Transparency Sort — 제일 중요

`Project Settings > Graphics > Camera Settings`

| 항목 | 현재 | 목표 |
|---|---|---|
| Transparency Sort Mode | `Default` (0) | **`Custom Axis`** (3) |
| Transparency Sort Axis | `(0, 0, 1)` | 그대로 유지 ✓ |

**이유**: 기본값이면 카메라까지의 거리로 정렬함 → 카메라를 기울이면 **높이(Y)가 정렬에 섞임** → 점프한 캐릭터가 뒤에 서 있는 적보다 앞으로 튀어나옴. Custom Axis (0,0,1)로 두면 **깊이(Z)만** 보고 정렬 → 높이 무시. 벨트스크롤이 원하는 동작.

파일: `ProjectSettings/GraphicsSettings.asset` L50-51

---

## 2. 레이어 추가

`Project Settings > Tags and Layers`

| 번호 | 이름 | 용도 |
|---|---|---|
| 8 | `Ground` | 바닥 판정 |
| 9 | `Wall` | 벽 바운드 판정 |
| 10 | `PlayerHurtbox` | 아군 피격 판정 (Combat) |
| 11 | `EnemyHurtbox` | 적 피격 판정 |
| 12 | `PlayerHitbox` | 아군 공격 판정 (Attack) |
| 13 | `EnemyHitbox` | 적 공격 판정 |

파일: `ProjectSettings/TagManager.asset`

---

## 3. Physics(3D) 설정

`Project Settings > Physics`

- **Gravity = (0, 0, 0)** — 중력은 `Physics` 클래스가 `verticalVelocity`로 직접 계산 (`G = 2h / t²`). Unity 중력이 켜져 있으면 이중 적용됨.
- **Collision Matrix** — 아래 조합만 체크, 나머지 전부 해제:
  - `PlayerHitbox` ↔ `EnemyHurtbox`
  - `EnemyHitbox` ↔ `PlayerHurtbox`
  - Hurtbox끼리는 **끔** (캐릭터끼리 밀치지 않음. 위치는 코드가 결정)

파일: `ProjectSettings/DynamicsManager.asset`

모든 Rigidbody는 `isKinematic = true`, 콜라이더는 `isTrigger = true`. 판정만 쓰고 시뮬레이션은 안 씀.

---

## 4. 카메라 ✅ 완료 (2026-07-29) — 단, **기울이지 않기로 변경**

`Assets/Scenes/SampleScene.unity` — Main Camera

- Projection: **Orthographic**, Size 5
- Rotation: **(0, 0, 0) 유지** ← 아래 "왜 안 기울였나" 참조
- Position: `(0, 1.5, -10)` + `CameraFollow` 로 플레이어 X 추적

### ⚠️ 왜 안 기울였나

`BeltScrollView.cs`가 이미 `screenY = Y + Z·depthToScreen` 로 깊이를 화면 세로로 환산한다.
카메라까지 θ만큼 기울이면 **깊이가 두 번 적용**된다.

둘은 같은 공식의 두 구현이므로 **하나만 골라야 한다.** 코드로 이미 있는 `BeltScrollView` 쪽을 택했다:

| | 카메라 기울이기 | BeltScrollView (채택) |
|---|---|---|
| 깊이 환산 | 카메라 투영 | 스프라이트 Y 오프셋 |
| 빌보드 | 스프라이트를 θ만큼 돌려야 함 | 불필요 (정면 고정) |
| 그림자 | 별도 3D 배치 필요 | `shadow` 슬롯에 내장 |
| 정렬 | Transparency Sort Axis 의존 | `sortingOrder = -z·100` 직접 제어 |

`depthToScreen = 0.5` ≈ θ 26.6° — 아래 표의 25~35° 범위 안.

### 각도가 곧 기획서의 `(X, Z+Y)` 공식

카메라를 X축으로 θ만큼 기울이면 화면 세로 좌표는:

```
screenY = Y·cos(θ) + Z·sin(θ)
```

- **θ = 45°** → `Y`와 `Z` 가중치가 같음 → 기획서의 `(X, Z+Y)` **정확히 일치**
- **θ = 30°** → Z가 0.5배로 압축 → 깊이가 얕아 보임 (파이널 파이트류 클래식 벨트스크롤 느낌)
- **θ 작을수록** 정면에 가까움

θ는 **연출 취향**이고, 로직은 어느 쪽이든 진짜 3D 좌표를 씀. 콜라이더는 각도와 무관하게 정확.

### 스프라이트 빌보드

캐릭터 스프라이트의 rotation을 카메라 rotation과 동일하게 맞춤 (X = θ). 그래야 카메라 정면을 봄.

### 그림자

기획서대로 **바닥(X, Z)에 고정**. 실제 3D 그림자 대신 블롭 그림자 스프라이트를 `y = 0` 위치에 별도 오브젝트로 둠. 캐릭터가 점프해도 그림자는 안 따라 올라감 → 높이 가독성 확보.

---

## 5. 안 바꾸는 것 (의도적)

| 항목 | 유지 이유 |
|---|---|
| `EditorSettings.m_DefaultBehaviorMode: 1` (2D) | 텍스처 임포트 기본값이 Sprite라 편함. 3D 콜라이더 쓰는 데 지장 없음 |
| `Renderer2D.asset` | 렌더러는 물리와 무관. 2D Light 계속 사용 가능 |
| 2D 패키지들 (Aseprite, 2D Animation, PSD Importer) | 스프라이트 파이프라인에 그대로 필요 |
| `Physics2D` 모듈 | 안 쓰지만 제거 불필요 |

---

## 6. 완료 후 확인

- [ ] 캐릭터를 `y = 3`에 띄우고, 그보다 `z`가 큰(뒤쪽) 적을 배치 → 캐릭터가 적보다 **앞에** 그려지는지
- [ ] 두 오브젝트 트리거 겹침 → 높이 다르면 `OnTriggerEnter` 안 뜨는지
- [ ] Rigidbody에 중력 안 걸리는지 (가만히 두면 안 떨어짐)

---

## 관련 문서

- `클래스다이어그램.canvas` — 전체 구조 (Obsidian Canvas)
- `스킬테이블.md` — 콤보 덱 24종 + 도출된 클래스 변경점
- 상위 폴더: `기획 가지치기.pdf`, `프로토타입.pdf`

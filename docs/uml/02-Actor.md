# 1층 Actor — Entities

몸 하나: 위치 · 물리 · 피격 · 상태 · 애니메이션. **Kernel(0층)만 참조한다.**
위층이 끼어드는 자리는 전부 이 폴더의 인터페이스 파일이다 — 아래 다이어그램의 `<<interface>>` 6개.

## 클래스 다이어그램

점선 삼각형(`..|>`)은 **위층 클래스가 이 층의 인터페이스를 구현**하는 자리다.

> 다이어그램: [02-Actor.puml](02-Actor.puml)

## 시퀀스 — 타격 한 번

> 다이어그램: [02-Actor-Hit.puml](02-Actor-Hit.puml)

## 이번에 끊은 역방향

| 전 | 수단 | 결과 |
|---|---|---|
| `Attack` · `Combat.Impact` · `EffectUtil` → `BattleVfx.Impact/WallBounce` (Vfx) | **인터페이스 파일** `IBattleVfx` + `BattleVfxSink` | Vfx가 `BeforeSceneLoad` 에 구현을 꽂는다 (`BattleVfx.InstallSink`) |
| `Attack` · `Projectile` → `SkillVfx` 구조체 (Vfx) | 옮김 → `Core/Support/SkillVfx.cs` | 연출 *데이터*는 Kernel, 그리는 쪽만 Vfx |
| `SkillVfx` → `VfxClip` SO (Vfx) | 옮김 → `Core/Support/VfxClip.cs` (`.meta` 째) | GUID 유지 · 에셋 연결 그대로 |
| `Entity.Control` → `Control` (Agent) | **인터페이스 파일** `IControl` | `Control : MonoBehaviour, IControl` — `enabled` 는 물려받아 만족 |
| `EntityAnimator` → `SkillState`·`SkillData` (Skill), `AssistReady/MoveState` (Agent) | **인터페이스 파일** `IAnimatedState` | 상태가 `Animate(animator)` 로 스스로 재생. 애니메이터는 `PlayState`·`SetSkillClip`·`HoldSkillPose` 만 공개 |
| `Physics` · `Projectile` → `GroundRegistry` (Stage) | 옮김(`GroundRules.cs`) + **인터페이스 파일** `IGroundPlate` | `GroundPlate : MonoBehaviour, IGroundPlate` |
| `Projectile` → `EffectUtil` (Skill) | 옮김 → `Entities/Support/EffectUtil.cs` | 광역 판정은 투사체도 쓰는 Actor 기능 |
| `Entity` · `BasicAttackProfile` · `EffectUtil` → `is Enemy` (Agent) | `Faction == Faction.Enemy` 로 교체 | `Faction.Enemy` 를 돌려주는 건 `Enemy` 하나뿐이라 동치 |

### 동작이 같은지 — 주의해서 본 곳

- **파괴된 발판** — 인터페이스 참조는 유니티의 `== null` 오버로드를 안 탄다.
  `GroundRegistry.Fill` 에서 `p is UnityEngine.Object o && o == null` 로 따로 거른다.
- **스킬 클립 캐시** — 키를 `SkillData` 에서 `AnimationClip` 으로 바꿨다. 같은 클립이면 오버라이드를
  다시 안 쓴다는 뜻은 같고, `SkillData == null` 이면 클립을 안 건드리는 것도 `SkillState.Animate` 가 지킨다.
- **연출 없음 경로** — 예전에도 에디트 모드에선 `VfxRunner.Instance` 가 null이라 안 그렸다. 지금은 `Current` 가 null.

검증: `Assembly-CSharp` · `Assembly-CSharp-Editor` 컴파일 오류 0.
집계상 남은 `Entities → Control` (`Entity.Control` *속성 이름*) · `Entities → Battle` (`StatusEffects.Entry` 중첩 타입)은 오탐.

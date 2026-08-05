# Enemy Prefab Variants Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 기존 근접 적을 유지하면서 원거리·돌진 브레인을 구현하고, 실제로 사용할 수 있는 적 프리팹 3종과 관련 데이터·머티리얼 애셋을 생성한다.

**Architecture:** `EnemyBrainAsset`은 무상태 판단만 담당한다. 원거리 적은 병합된 공용 `Projectile`과 기존 `AttackState`를 재사용하고, 돌진의 예고·직선 이동·후딜은 인스턴스 컴포넌트 `EnemyChargeAction`이 실행한다. 별도 `EnemyPrefabBuilder`가 기존 `enemy.prefab`을 안전한 기준 프리팹으로 삼아 세 변형과 데이터 애셋을 반복 실행 가능한 방식으로 생성한다.

**Tech Stack:** Unity 6.5 (`6000.5.5f1`), C#, ScriptableObject, Unity Test Framework, UnityEditor PrefabUtility/AssetDatabase

## Global Constraints

- 기존 사용자 미커밋 변경과 `RecentHitEnemyHUD` 작업을 보존한다.
- `Assets/Editor/TestSceneBuilder.cs`는 수정하지 않고 별도 생성기를 만든다.
- 기존 `Assets/Prefabs/enemy.prefab`과 GUID를 삭제하거나 변경하지 않는다.
- 기존 `Assets/Scripts/Entities/Projectile.cs`와 `Assets/Prefabs/Projectile.prefab`을 재사용한다.
- 브레인 ScriptableObject에는 런타임 상태를 저장하지 않는다.
- 생산 코드는 해당 동작을 검증하는 실패 테스트를 먼저 확인한 뒤 작성한다.
- 자동 생성 애셋은 생성기를 다시 실행해도 중복되지 않고 같은 경로에서 갱신되어야 한다.

---

### Task 1: 원거리·돌진 판단 계약

**Files:**
- Create: `Assets/Editor/Tests/EnemyBrainVariantTests.cs`
- Create: `Assets/Scripts/AI/Brains/RangedBrainAsset.cs`
- Create: `Assets/Scripts/AI/Brains/ChargerBrainAsset.cs`
- Modify: `Assets/Scripts/AI/IEnemyBrain.cs`

**Interfaces:**
- Consumes: `EnemyBrainAsset.Decide(in EnemyBrainContext ctx)`
- Produces: `EnemyActionKind`, `EnemyIntent.Charge(Vector3)`, `EnemyBrainParams.preferredMinRange`, `EnemyBrainParams.specialRange`, `EnemyBrainContext.specialReady`

- [ ] **Step 1: 원거리 판단 실패 테스트 작성**

  실제 `RangedBrainAsset`을 생성해 타겟 존재 시 거리 3m는 반대 방향 이동, 4m와 7m는 준비 완료 시 `Command.Attack`, 8m는 정방향 이동을 기대한다. leash 밖과 공격 쿨 중에는 `Command.None`을 기대한다.

- [ ] **Step 2: 테스트가 기능 누락으로 실패하는지 확인**

  Run:

  ```powershell
  & 'C:\Program Files\Unity\Hub\Editor\6000.5.5f1\Editor\Unity.exe' -batchmode -nographics -projectPath 'C:\Users\onebe\Prototype' -runTests -testPlatform EditMode -testFilter 'Prototype.Tests.EnemyBrainVariantTests' -testResults 'C:\Users\onebe\Prototype\Logs\enemy-brain-red.xml' -logFile 'C:\Users\onebe\Prototype\Logs\enemy-brain-red.log'
  ```

  Expected: 컴파일 또는 테스트 FAIL. 원인은 `RangedBrainAsset`/새 계약이 아직 없기 때문이어야 한다.

- [ ] **Step 3: 원거리 브레인 최소 구현**

  `RangedBrainAsset.Decide`는 null/leash를 먼저 처리하고, `preferredMinRange` 미만에서는 `-dir` 이동, `attackRange` 이내에서는 준비된 경우 공격, 그 밖에서는 `dir` 이동을 반환한다.

- [ ] **Step 4: 돌진 판단 실패 테스트 작성**

  실제 `ChargerBrainAsset`에 대해 2m 이하는 일반 공격, 2m 초과 7m 이하는 특수 쿨 준비 시 `EnemyActionKind.Charge`, 특수 쿨 중 또는 7m 초과는 접근 이동을 기대한다.

- [ ] **Step 5: 돌진 브레인 최소 구현 후 판단 테스트 통과 확인**

  같은 Unity 명령을 `enemy-brain-green.xml`/`.log`로 실행한다. Expected: 대상 테스트 전부 PASS, 컴파일 오류 0.

### Task 2: 돌진 실행 수명 주기

**Files:**
- Create: `Assets/Editor/Tests/EnemyChargeSequenceTests.cs`
- Create: `Assets/Scripts/AI/EnemyChargeSequence.cs`
- Create: `Assets/Scripts/AI/EnemyChargeAction.cs`
- Modify: `Assets/Scripts/Control/EnemyControl.cs`
- Modify: `Assets/Scripts/Characters/EnemyData.cs`
- Modify: `Assets/Scripts/Characters/Enemy.cs`

**Interfaces:**
- Produces: `EnemyChargePhase { Idle, Telegraph, Charging, Recovery }`
- Produces: `EnemyChargeSequence.Begin()`, `Tick(float dt, Vector3 liveDirection)`, `HitOrWall()`, `Cancel()`
- Produces: `EnemyChargeAction.TryStart(Entity target)`, `Tick(float dt)`, `Cancel()`, `IsRunning`

- [ ] **Step 1: 돌진 단계 실패 테스트 작성**

  순수 `EnemyChargeSequence`를 대상으로 0.6초 전에는 Telegraph, 경계에서 방향 고정과 Charging, 0.55초 후 Recovery, 충돌 즉시 Recovery, 0.8초 후 Idle, Cancel 즉시 Idle을 각각 검증한다.

- [ ] **Step 2: RED 확인**

  Task 1과 같은 Unity 명령에 `-testFilter 'Prototype.Tests.EnemyChargeSequenceTests'`를 사용한다. Expected: `EnemyChargeSequence`가 없어 FAIL.

- [ ] **Step 3: 순수 단계 객체 최소 구현 후 GREEN 확인**

  시간 경계는 `>=`로 처리하고, Charging 진입 순간의 방향을 `LockedDirection`에 한 번만 저장한다. 테스트 전체 PASS를 확인한다.

- [ ] **Step 4: 실행 컴포넌트 연결**

  `EnemyChargeAction`은 Telegraph 동안 `Physics.Face`, Charging 동안 매 Tick `Physics.Dash`, 돌진 히트박스의 `Attack.OnHit` 및 `Physics.OnWallHit`에서 Recovery 진입, 종료/취소에서 `Attack.End`와 `Physics.ResetInertia`를 수행한다.

- [ ] **Step 5: EnemyControl에 특수 행동 게이트 연결**

  일반/특수 타이머를 분리한다. 실행 중에는 새 브레인 판단을 막고 실행기만 Tick한다. 피격·경직·사망·AI 정지 시 실행기를 Cancel한다. `TryStart` 성공 시에만 특수 쿨을 소비한다.

- [ ] **Step 6: 전체 EditMode 테스트 실행**

  Expected: 기존 `RecentHitEnemyHUDTests`를 포함해 모든 EditMode 테스트 PASS.

### Task 3: 원거리 설정 데이터 주입

**Files:**
- Create: `Assets/Editor/Tests/EnemyDataApplicationTests.cs`
- Modify: `Assets/Scripts/Characters/EnemyData.cs`
- Modify: `Assets/Scripts/Characters/Enemy.cs`
- Modify: `Assets/Scripts/Entities/Entity.cs`

**Interfaces:**
- Produces: `Entity.ConfigureBasicProjectile(Projectile prefab, float speed, float range, int pierce)`
- Consumes: `EnemyData.basicProjectile`, `projectileSpeed`, `projectileRange`, `projectilePierce`

- [ ] **Step 1: 설정 주입 실패 테스트 작성**

  실 Entity에 `ConfigureBasicProjectile`을 호출했을 때 `BasicIsRanged == true`이고 `BasicAttackReach == range * 0.8f`가 되는 동작을 검증한다. 0 이하 속도/사거리는 안전한 최소값으로 정규화되는 결과를 검증한다.

- [ ] **Step 2: RED 확인 후 최소 설정 API 구현**

  테스트가 API 부재로 실패하는지 확인한 뒤 필드 할당과 최소값 정규화만 구현한다.

- [ ] **Step 3: EnemyData 주입 연결 및 GREEN 확인**

  `Enemy.ApplyData()`가 투사체 프리팹이 있는 데이터에 한해서 설정 API를 호출한다. 전체 EditMode 테스트 PASS를 확인한다.

### Task 4: 적 3종 애셋 생성기

**Files:**
- Create: `Assets/Editor/Tests/EnemyPrefabBuilderTests.cs`
- Create: `Assets/Editor/EnemyPrefabBuilder.cs`
- Generate: `Assets/Data/Enemy/Brain_Ranged.asset`
- Generate: `Assets/Data/Enemy/Brain_Charger.asset`
- Generate: `Assets/Data/Enemy/Enemy_Melee.asset`
- Generate: `Assets/Data/Enemy/Enemy_Ranged.asset`
- Generate: `Assets/Data/Enemy/Enemy_Charger.asset`
- Generate: `Assets/Prefabs/Materials/M_Enemy_Melee.mat`
- Generate: `Assets/Prefabs/Materials/M_Enemy_Ranged.mat`
- Generate: `Assets/Prefabs/Materials/M_Enemy_Charger.mat`
- Generate: `Assets/Prefabs/Enemy_Melee.prefab`
- Generate: `Assets/Prefabs/Enemy_Ranged.prefab`
- Generate: `Assets/Prefabs/Enemy_Charger.prefab`

**Interfaces:**
- Produces: `Prototype.EditorTools.EnemyPrefabBuilder.Build()`

- [ ] **Step 1: 애셋 결과 실패 테스트 작성**

  `Build()` 실행 후 세 프리팹이 존재하고 각각 `Enemy`, `EnemyControl`, 올바른 `EnemyData`/브레인, 예상 Body primitive, 예상 색을 가지는지 검증한다. 원거리에는 `Projectile`, 돌진에는 `EnemyChargeAction`과 전용 히트박스가 연결돼야 한다.

- [ ] **Step 2: RED 확인**

  생성기 부재 또는 애셋 누락으로 테스트가 실패하는지 확인한다.

- [ ] **Step 3: 반복 실행 가능한 생성기 구현**

  기존 `enemy.prefab`을 임시 인스턴스로 열어 공통 전투/물리 구성을 보존한다. 브레인·데이터·머티리얼은 고정 경로에서 생성 또는 갱신하고, 변형 프리팹은 고정 경로로 저장한다. 기존 `enemy.prefab`은 수정하지 않는다.

- [ ] **Step 4: 실제 애셋 생성**

  Run:

  ```powershell
  & 'C:\Program Files\Unity\Hub\Editor\6000.5.5f1\Editor\Unity.exe' -batchmode -nographics -quit -projectPath 'C:\Users\onebe\Prototype' -executeMethod Prototype.EditorTools.EnemyPrefabBuilder.Build -logFile 'C:\Users\onebe\Prototype\Logs\enemy-prefab-build.log'
  ```

  Expected: exit 0, 세 프리팹과 데이터/브레인/머티리얼 애셋 생성.

- [ ] **Step 5: 생성기 테스트 GREEN 및 멱등성 확인**

  생성기를 두 번 실행한 뒤 애셋 수와 경로가 같고 참조가 유지되는지 확인한다. 전체 EditMode 테스트 PASS.

### Task 5: 최종 검증

**Files:**
- Verify only: all files above

- [ ] **Step 1: 컴파일 및 전체 EditMode 테스트**

  Unity 결과 XML에서 `failed="0"`과 모든 테스트 성공을 확인한다. 로그에서 `error CS`, `Scripts have compiler errors`, missing script를 검색해 0건임을 확인한다.

- [ ] **Step 2: 프리팹 직렬화 검증**

  세 프리팹의 missing script가 없고, 브레인/데이터/투사체/돌진 히트박스 참조가 null이 아닌지 에디터 테스트 결과로 확인한다.

- [ ] **Step 3: 변경 범위 검토**

  `git diff`로 사용자 HUD 변경이 보존됐고 `TestSceneBuilder.cs`에 새 변경을 추가하지 않았음을 확인한다. 우리 작업 파일만 구분해 보고한다.

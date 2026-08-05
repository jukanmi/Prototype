# Recent Hit Enemy HUD Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Show the single most recently damaged enemy's remaining health at the bottom of the combat screen.

**Architecture:** Add a focused runtime uGUI component that subscribes to enemy hit events and owns only one current target. The existing scene builder attaches that component to the battle controller, keeping hit resolution in `Combat` unchanged.

**Tech Stack:** Unity, C#, UnityEngine.UI, Unity Test Framework.

## Global Constraints

- Display exactly one recently hit enemy.
- Show name and HP bar only, at bottom center above the combo board.
- Replace the target on a newer valid allied hit; hide it on death or after 3 seconds.
- Ignore enemy attacks and hits rejected before damage is applied.
- Use a 1920×1080 `CanvasScaler` reference resolution.

---

### Task 1: Recent-hit target state and UI

**Files:**
- Create: `Assets/Scripts/Battle/UI/RecentHitEnemyHUD.cs`
- Modify: `Assets/Scripts/Entities/Combat.cs`
- Modify: `Assets/Editor/SceneLayoutBuilder.cs`
- Test: `Assets/Tests/EditMode/RecentHitEnemyHUDTests.cs`

**Interfaces:**
- Consumes: `Combat.OnHitLanded`, `Combat.OnDead`, `Entity.Faction`, and `Energy.CurValue`/`MaxValue`.
- Produces: `RecentHitEnemyHUD.Track(Combat attacker, Combat target)` and `RecentHitEnemyHUD.IsVisible` for tests.

- [ ] **Step 1: Write the failing test**

```csharp
[Test]
public void Track_AlliedEnemyTarget_MakesHudVisible()
{
    var hud = new GameObject().AddComponent<RecentHitEnemyHUD>();
    var attacker = CreateCombat(Faction.Ally);
    var target = CreateCombat(Faction.Enemy);

    hud.Track(attacker, target);

    Assert.That(hud.IsVisible, Is.True);
}
```

- [ ] **Step 2: Run the test to verify it fails**

Run the Unity EditMode test `RecentHitEnemyHUDTests.Track_AlliedEnemyTarget_MakesHudVisible`.

Expected: compile failure because `RecentHitEnemyHUD` does not exist.

- [ ] **Step 3: Write minimal implementation**

```csharp
public void Track(Combat attacker, Combat target)
{
    if (attacker?.Owner?.Faction != Faction.Ally ||
        target?.Owner?.Faction != Faction.Enemy || target.IsDead) return;
    currentTarget = target;
    expiresAt = Time.unscaledTime + 3f;
    Refresh();
}
```

Subscribe the HUD to each ally `Combat.OnHitLanded`. In `Combat.Attack`, invoke `OnHitLanded` only after `target.Hit` returns while the target remains alive or has taken the hit, so rejected invincible hits do not report as landed. Create a bottom-center overlay panel at `(0, 180)` with the enemy name and an `Image.fillAmount` bar. Unsubscribe a replaced target's `OnDead`, hide immediately when it dies, and hide once `Time.unscaledTime` reaches `expiresAt`.

- [ ] **Step 4: Run tests to verify they pass**

Run the Unity EditMode test suite containing `RecentHitEnemyHUDTests` and confirm the visibility, invalid-faction, replacement, expiry, and death cases pass.

- [ ] **Step 5: Commit**

```bash
git add Assets/Scripts/Battle/UI/RecentHitEnemyHUD.cs Assets/Scripts/Entities/Combat.cs Assets/Editor/SceneLayoutBuilder.cs Assets/Tests/EditMode/RecentHitEnemyHUDTests.cs
git commit -m "feat: show recently hit enemy health"
```

using System.Collections.Generic;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Prototype.Tests
{
    /// <summary>
    /// 스테이지 씬이 <b>파티를 들고 있지 않은지</b> 본다. 이 리팩터링의 회귀 방지 자리다.
    ///
    /// 이게 없으면 여섯 달 뒤에 누가 씬에 <c>Ally</c> 하나를 끌어다 놓고, 그 씬만
    /// 동료가 다섯 명이 되며, 증상은 "그 스테이지에서만 덱이 20장"으로 나온다.
    ///
    /// <b>씬을 열었다 닫는 테스트</b>라 다른 EditMode 테스트보다 느리다. 그 값은 한다 —
    /// 여기서 잡는 종류의 오류는 플레이로만 발견되고, 발견되는 곳이 원인에서 멀다.
    /// </summary>
    public class SceneCompositionTests
    {
        /// <summary>
        /// 검사 대상. <c>Skill_test</c>는 뺐다 — 동료 한 명으로 스킬을 실험하는 씬이라
        /// 파티 규약을 따르지 않는 것이 정상이다.
        /// </summary>
        private static readonly string[] StageScenes =
        {
            "Assets/Scenes/Level/Stage_01.unity",
            "Assets/Scenes/Level/Stage_02.unity",
            "Assets/Scenes/Level/Stage_03.unity",
            "Assets/Scenes/Level/Stage_04.unity",
            "Assets/Scenes/Level/Stage_05.unity",
            "Assets/Scenes/Level/Stage_Boss.unity",
            "Assets/Scenes/Level/Stage_Mini.unity",
            "Assets/Scenes/Level/Stage_Training.unity",
            "Assets/Scenes/SampleScene.unity",
        };

        private string reopen;

        [SetUp]
        public void SetUp() => reopen = SceneManager.GetActiveScene().path;

        [TearDown]
        public void TearDown()
        {
            // 테스트가 씬을 갈아치웠으므로 원래 자리로 돌려놓는다.
            if (!string.IsNullOrEmpty(reopen) && reopen != SceneManager.GetActiveScene().path)
                EditorSceneManager.OpenScene(reopen, OpenSceneMode.Single);
        }

        private static IEnumerable<string> Scenes()
        {
            foreach (string path in StageScenes)
                if (AssetDatabase.LoadAssetAtPath<SceneAsset>(path) != null)
                    yield return path;
        }

        private static Scene Open(string path) => EditorSceneManager.OpenScene(path, OpenSceneMode.Single);

        /// <summary>이 컴포넌트가 BattleInput 프리팹 안에 있는가.</summary>
        private static bool InsideHost(Component c)
            => c != null && c.GetComponentInParent<PartyAssembler>(true) != null;

        // ── 파티는 씬에 없어야 한다 ──────────────────────

        [Test]
        public void Stages_DoNotOwnPartyBodies()
        {
            foreach (string path in Scenes())
            {
                Open(path);

                foreach (Player p in Object.FindObjectsByType<Player>(
                             FindObjectsInactive.Include, FindObjectsSortMode.None))
                    Assert.That(InsideHost(p), Is.True,
                        $"{path} 에 씬 소유 Player '{p.name}' 가 남아 있다 — " +
                        "'Prototype ▸ 파티 - 2단계: 씬 마이그레이션'을 돌릴 것.");

                foreach (Ally a in Object.FindObjectsByType<Ally>(
                             FindObjectsInactive.Include, FindObjectsSortMode.None))
                    Assert.That(InsideHost(a), Is.True,
                        $"{path} 에 씬 소유 Ally '{a.name}' 가 남아 있다 — " +
                        "동료가 다섯 명이 되고 덱이 20장이 된다.");
            }
        }

        /// <summary>
        /// 덱이 두 벌 돌면 두 <see cref="BulletTimeController"/>가 같은
        /// <see cref="RunProgression"/>에 씨를 뿌리고 서로를 덮는다.
        /// </summary>
        [Test]
        public void Stages_HaveExactlyOneDeckOwner()
        {
            foreach (string path in Scenes())
            {
                Open(path);

                BulletTimeController[] found = Object.FindObjectsByType<BulletTimeController>(
                    FindObjectsInactive.Include, FindObjectsSortMode.None);

                Assert.That(found.Length, Is.EqualTo(1),
                    $"{path} 의 BulletTimeController 가 {found.Length}개다 — 정확히 1개여야 한다.");
                Assert.That(InsideHost(found[0]), Is.True,
                    $"{path} 의 BulletTimeController 가 BattleInput 밖에 있다 — " +
                    "씬의 CombatManager 인스턴스를 제거할 것.");
            }
        }

        // ── 호스트 ──────────────────────────────────────

        [Test]
        public void Stages_HaveExactlyOneHostAtOrigin()
        {
            foreach (string path in Scenes())
            {
                Open(path);

                PartyAssembler[] hosts = Object.FindObjectsByType<PartyAssembler>(
                    FindObjectsInactive.Include, FindObjectsSortMode.None);

                Assert.That(hosts.Length, Is.EqualTo(1),
                    $"{path} 의 BattleInput 이 {hosts.Length}개다.");

                Transform t = hosts[0].transform;

                Assert.That(t.position, Is.EqualTo(Vector3.zero),
                    $"{path} 의 BattleInput 이 원점에 있지 않다 ({t.position}) — " +
                    "출구 판정이 월드 x 를 읽으므로 스테이지가 안 넘어간다.");
                Assert.That(t.localScale, Is.EqualTo(Vector3.one),
                    $"{path} 의 BattleInput 배율이 1이 아니다 — 히트박스가 통째로 어긋난다.");
            }
        }

        // ── 스폰 자리 ───────────────────────────────────

        /// <summary>
        /// 자리가 없으면 <see cref="PartySpawnPoint.Fallback"/>으로 떨어진다. 게임은 돌지만
        /// 방마다 시작 지점이 다른 게 정상이므로, 없다는 것 자체가 마이그레이션 누락 신호다.
        /// </summary>
        [Test]
        public void Stages_HaveExactlyOneSpawnPoint()
        {
            foreach (string path in Scenes())
            {
                Open(path);

                PartySpawnPoint[] points = Object.FindObjectsByType<PartySpawnPoint>(
                    FindObjectsInactive.Include, FindObjectsSortMode.None);

                Assert.That(points.Length, Is.EqualTo(1),
                    $"{path} 의 PartySpawnPoint 가 {points.Length}개다 — 정확히 1개여야 한다.");
            }
        }
    }
}

using System.Collections.Generic;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using Prototype.EditorTools;

namespace Prototype.Tests
{
    /// <summary>
    /// <b>파티의 몸이 될 프리팹들</b>을 본다. 기본 <c>Player</c> · <c>Ally</c> 프리팹과,
    /// <see cref="PartyMemberData.prefab"/> · <see cref="PlayerData.prefab"/>이 가리키는
    /// 동료별 프리팹 전부가 대상이다.
    ///
    /// <b>왜 새로 필요한가.</b> 동료 넷이 <c>Ally.prefab</c> 하나를 공유하던 동안에는
    /// 이 검사가 <c>PartyPrefabTests</c>의 "BattleInput 안의 몸" 검사 하나로 충분했다.
    /// 몸이 넷으로 갈리면서 <b>틀릴 수 있는 자리도 넷으로 늘었고</b>, 증상은 전부
    /// "그 동료만 이상하다"라 어디를 봐야 할지가 화면에 안 나온다.
    /// </summary>
    public class PartyBodyPrefabTests
    {
        /// <summary>
        /// 검사 대상. 기본 프리팹 둘 + 표가 지정한 프리팹 전부.
        /// <c>(경로, 루트)</c>로 돌려준다 — 실패 메시지에 어느 에셋인지가 반드시 있어야 한다.
        /// </summary>
        private static IEnumerable<(string path, GameObject go)> Bodies()
        {
            var seen = new HashSet<string>();

            foreach (string path in new[] { PrefabLocator.PlayerPath, PrefabLocator.AllyPath })
            {
                var go = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (go != null && seen.Add(path)) yield return (path, go);
            }

            foreach (GameObject go in Custom<PartyMemberData>(m => m.prefab))
            {
                string path = AssetDatabase.GetAssetPath(go);
                if (seen.Add(path)) yield return (path, go);
            }

            foreach (GameObject go in Custom<PlayerData>(h => h.prefab))
            {
                string path = AssetDatabase.GetAssetPath(go);
                if (seen.Add(path)) yield return (path, go);
            }
        }

        private static IEnumerable<GameObject> Custom<T>(System.Func<T, GameObject> pick)
            where T : ScriptableObject
        {
            foreach (string guid in AssetDatabase.FindAssets($"t:{typeof(T).Name}"))
            {
                var data = AssetDatabase.LoadAssetAtPath<T>(AssetDatabase.GUIDToAssetPath(guid));
                if (data == null) continue;

                GameObject go = pick(data);
                if (go != null) yield return go;
            }
        }

        private static IEnumerable<(string path, T data)> Assets<T>() where T : ScriptableObject
        {
            foreach (string guid in AssetDatabase.FindAssets($"t:{typeof(T).Name}"))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                var data = AssetDatabase.LoadAssetAtPath<T>(path);
                if (data != null) yield return (path, data);
            }
        }

        // ── 표가 가리키는 프리팹 ────────────────────────

        /// <summary>
        /// 루트에 <see cref="Ally"/>가 없으면 <see cref="PartyAssembler"/>가 만든 몸을 그 자리에서
        /// 도로 버린다. 경고는 콘솔에 한 줄 뜨지만, 화면에서 보이는 건
        /// <b>"그 동료만 파티에 없다"</b>뿐이라 원인이 안 읽힌다.
        /// </summary>
        [Test]
        public void MemberPrefabs_HaveAllyAtRoot()
        {
            foreach ((string path, PartyMemberData data) in Assets<PartyMemberData>())
            {
                if (data.prefab == null) continue;   // 비면 기본 Ally 프리팹으로 떨어진다. 정상이다.

                Assert.That(data.prefab.GetComponent<Ally>(), Is.Not.Null,
                    $"{path} 의 prefab '{data.prefab.name}' 루트에 Ally 가 없다 — " +
                    "이 동료는 파티에서 통째로 빠진다.");
            }
        }

        [Test]
        public void HeroPrefabs_HavePlayerAtRoot()
        {
            foreach ((string path, PlayerData data) in Assets<PlayerData>())
            {
                if (data.prefab == null) continue;

                Assert.That(data.prefab.GetComponent<Player>(), Is.Not.Null,
                    $"{path} 의 prefab '{data.prefab.name}' 루트에 Player 가 없다 — " +
                    "태그 로스터가 통째로 안 만들어진다.");
            }
        }

        // ── 몸의 규약 ───────────────────────────────────

        /// <summary>
        /// 레이어가 틀리면 <see cref="Attack"/>이 충돌 매트릭스를 그대로 읽어
        /// <b>그 동료만 적을 통과한다</b> — 스킬이 나가고 이펙트도 뜨는데 데미지만 없다.
        ///
        /// 런타임에는 <see cref="AllyLayers"/>가 몸마다 다시 칠하므로 이건 <b>한 겹 더</b>다.
        /// 그래도 여기서 잡는 편이 낫다 — 프리팹을 씬에 직접 끌어다 놓고 시험하는 경로
        /// (스킬 실험 씬)에는 조립기가 없어서 칠할 사람이 없다.
        /// </summary>
        [Test]
        public void Bodies_UseAllyLayers()
        {
            int hurt = LayerMask.NameToLayer(AllyLayers.HurtboxLayer);
            int hit = LayerMask.NameToLayer(AllyLayers.HitboxLayer);

            Assert.That(hurt, Is.GreaterThanOrEqualTo(0),
                $"프로젝트에 '{AllyLayers.HurtboxLayer}' 레이어가 없다.");
            Assert.That(hit, Is.GreaterThanOrEqualTo(0),
                $"프로젝트에 '{AllyLayers.HitboxLayer}' 레이어가 없다.");

            foreach ((string path, GameObject go) in Bodies())
            {
                Assert.That(go.layer, Is.EqualTo(hurt),
                    $"{path} 의 몸통 레이어가 {AllyLayers.HurtboxLayer} 가 아니다.");

                foreach (Attack a in go.GetComponentsInChildren<Attack>(true))
                    Assert.That(a.gameObject.layer, Is.EqualTo(hit),
                        $"{path} 의 '{a.name}' 레이어가 {AllyLayers.HitboxLayer} 가 아니다.");
            }
        }

        /// <summary>
        /// 몸은 <b>배율 1</b>로 저장돼야 한다. 루트에 콜라이더(피격 범위)가 붙어 있고
        /// 그 치수를 <see cref="Entity.HurtboxSize"/>가 읽어 <b>스킬 사거리 배수</b>로 쓰기 때문이다
        /// (<c>SkillData.castRangeScale</c>) — 루트를 키우면 그 동료의 스킬만 사거리가 늘어난다.
        ///
        /// 체형을 키우고 싶으면 <c>View</c> · <c>Shadow</c> 쪽을 키운다.
        /// 그게 <see cref="PartyMemberData.bodyScale"/>이 루트를 안 건드리는 이유이기도 하다.
        /// </summary>
        [Test]
        public void Bodies_HaveIdentityRootScale()
        {
            foreach ((string path, GameObject go) in Bodies())
                Assert.That(go.transform.localScale, Is.EqualTo(Vector3.one),
                    $"{path} 의 루트 배율이 1이 아니다 ({go.transform.localScale}) — " +
                    "피격 범위와 스킬 사거리가 함께 어긋난다. View · Shadow 를 키울 것.");
        }

        /// <summary>
        /// 꺼진 채로 저장된 프리팹은 조립기가 <c>SetActive(true)</c>로 살려 주지만,
        /// 그건 안전망이지 저작 의도가 아니다. 여기서 한 번 짚어 준다 —
        /// 꺼진 프리팹은 씬에 직접 끌어다 놓았을 때 아무 일도 안 일어난다.
        /// </summary>
        [Test]
        public void Bodies_AreSavedActive()
        {
            foreach ((string path, GameObject go) in Bodies())
                Assert.That(go.activeSelf, Is.True,
                    $"{path} 가 꺼진 채로 저장돼 있다.");
        }
    }
}

using NUnit.Framework;
using Prototype.EditorTools;
using UnityEditor;
using UnityEngine;

namespace Prototype.Tests
{
    /// <summary>
    /// 6종이 같은 스프라이트 시트를 쓰므로 시각 설정도 같아야 한다.
    /// spriteOffsetY가 0이 아니면 발이 바닥에서 뜨고,
    /// flipToFacing이 꺼져 있으면 왼쪽으로 걸어도 오른쪽을 본다 — 시트에 좌향 프레임이 없다.
    /// </summary>
    public class EntityVisualSetupTests
    {
        private const string AllyIdleSheet = "Assets/Art/Character/Ally/_Idle.png";

        private static readonly string[] PrefabPaths =
        {
            "Assets/Prefabs/Player.prefab",
            "Assets/Prefabs/Ally.prefab",
            "Assets/Prefabs/enemy.prefab",
            "Assets/Prefabs/Enemy_Melee.prefab",
            "Assets/Prefabs/Enemy_Charger.prefab",
            "Assets/Prefabs/Enemy_Ranged.prefab",
        };

        private static SpriteRenderer BodyOf(GameObject root)
        {
            Transform sprite = root.transform.Find("View/Sprite");
            Assert.That(sprite, Is.Not.Null, $"{root.name}: View/Sprite가 없다");
            var sr = sprite.GetComponent<SpriteRenderer>();
            Assert.That(sr, Is.Not.Null, $"{root.name}: View/Sprite에 SpriteRenderer가 없다");
            return sr;
        }

        [TestCaseSource(nameof(PrefabPaths))]
        public void EveryEntity_UsesAllySheetAndFlipsToFacing(string prefabPath)
        {
            var root = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            Assert.That(root, Is.Not.Null, $"프리팹이 없다: {prefabPath}");

            SpriteRenderer body = BodyOf(root);
            Assert.That(AssetDatabase.GetAssetPath(body.sprite), Is.EqualTo(AllyIdleSheet),
                $"{root.name}: 몸 스프라이트가 동료 시트가 아니다");

            var view = root.GetComponent<BeltScrollView>();
            Assert.That(view, Is.Not.Null, $"{root.name}: BeltScrollView가 없다");

            var so = new SerializedObject(view);
            Assert.That(so.FindProperty("spriteOffsetY").floatValue, Is.EqualTo(0f).Within(0.0001f),
                $"{root.name}: spriteOffsetY가 0이 아니라 발이 뜬다");
            Assert.That(so.FindProperty("flipToFacing").boolValue, Is.True,
                $"{root.name}: flipToFacing이 꺼져 있다");
            Assert.That(so.FindProperty("facingRenderer").objectReferenceValue, Is.SameAs(body),
                $"{root.name}: facingRenderer가 몸 렌더러를 안 가리킨다");
        }

        /// <summary>
        /// 틴트는 캐릭터를 구별하는 유일한 수단이다. 두 종류가 같은 색이면 난전에서 못 가른다.
        /// 알파는 검사하지 않는다 — 사망 페이드 커브 소유다.
        /// </summary>
        [Test]
        public void EveryEntity_HasDistinctTint()
        {
            var seen = new System.Collections.Generic.Dictionary<Color, string>();

            foreach (string path in PrefabPaths)
            {
                if (path.EndsWith("enemy.prefab")) continue;   // 기준 더미. Melee와 같은 색이 맞다.

                var root = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                Color c = BodyOf(root).color;
                c.a = 1f;

                Assert.That(seen.ContainsKey(c), Is.False,
                    $"{root.name}과 {(seen.ContainsKey(c) ? seen[c] : "?")}의 틴트가 같다: {c}");
                seen[c] = root.name;
            }
        }

        /// <summary>
        /// 변종 프리팹은 EnemyPrefabBuilder 메뉴가 enemy.prefab을 복제해 만든다.
        /// 빌더가 스프라이트 색을 안 넣으면 메뉴를 누르는 순간 세 변종이 전부 기준 프리팹 색이 된다.
        /// 그래서 "빌더가 아는 색"과 "프리팹에 박힌 색"이 같은지 검사한다.
        /// </summary>
        [TestCase("Assets/Prefabs/Enemy_Melee.prefab")]
        [TestCase("Assets/Prefabs/Enemy_Charger.prefab")]
        [TestCase("Assets/Prefabs/Enemy_Ranged.prefab")]
        public void VariantTint_MatchesBuilderTable(string prefabPath)
        {
            var root = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            Assert.That(root, Is.Not.Null, $"프리팹이 없다: {prefabPath}");

            Assert.That(EnemyPrefabBuilder.TryGetVariantColor(root.name, out Color expected), Is.True,
                $"빌더 표에 {root.name}이 없다");

            Color actual = BodyOf(root).color;
            Assert.That(actual.r, Is.EqualTo(expected.r).Within(0.001f), $"{root.name} R");
            Assert.That(actual.g, Is.EqualTo(expected.g).Within(0.001f), $"{root.name} G");
            Assert.That(actual.b, Is.EqualTo(expected.b).Within(0.001f), $"{root.name} B");
        }
    }
}

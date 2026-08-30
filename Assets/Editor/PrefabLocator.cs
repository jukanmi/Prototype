using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace Prototype.EditorTools
{
    /// <summary>
    /// 핵심 프리팹을 <b>경로가 아니라 컴포넌트로</b> 찾는다.
    ///
    /// <b>왜 필요한가.</b> 빌더와 테스트 열 몇 곳이 <c>"Assets/Prefabs/Player.prefab"</c> 같은
    /// 문자열을 각자 들고 있었다. 프리팹을 폴더 하나로 정리하는 순간 그게 전부 끊겼는데,
    /// 증상이 제각각이라 원인이 하나라는 게 안 보였다 —
    /// 어떤 도구는 "못 찾았다"고 하고, 어떤 테스트는 "프리팹이 없다"고 하고,
    /// <c>BattleInputBuilder</c>는 <b>옛 경로에 새 프리팹을 만들어</b> 씬이 물고 있는 것과
    /// 조용히 갈라섰다.
    ///
    /// 루트에 붙은 컴포넌트는 프리팹을 어디로 옮겨도 안 바뀐다. 그걸 기준으로 삼는다.
    ///
    /// <b>루트만 본다.</b> <c>BattleInput</c>은 자식으로 <see cref="Player"/>와
    /// <see cref="Ally"/>를 품고 있어서, 자식까지 뒤지면 셋이 서로를 가리킨다.
    /// </summary>
    internal static class PrefabLocator
    {
        /// <summary>프리팹을 찾는 범위. 하위 폴더까지 훑는다.</summary>
        public const string SearchRoot = "Assets/Prefabs";

        /// <summary>못 찾았을 때 새로 만들 자리. <c>BattleInput</c>만 이 경우가 정상이다.</summary>
        private const string BattleInputFallback = SearchRoot + "/BattleInput.prefab";

        public static string PlayerPath => Locate<Player>("Player");
        public static string AllyPath => Locate<Ally>("Ally");
        public static string CombatManagerPath => Locate<BulletTimeController>("CombatManager");

        /// <summary>
        /// 전투 호스트. <b>있는 자리에 그대로 다시 굽기 위해</b> 찾는다 —
        /// 옛 경로에 새로 만들면 GUID가 달라져 씬 아홉 개가 계속 옛 것을 물고, 그 사실이
        /// 화면에 전혀 안 드러난다.
        ///
        /// <see cref="PartyAssembler"/>가 아니라 <see cref="PlayerInputController"/>로 찾는다 —
        /// 전자는 이 프리팹을 다시 구우면서 붙는 것이라 첫 빌드 때는 아직 없다.
        /// </summary>
        public static string BattleInputPath
        {
            get
            {
                string found = Find<PlayerInputController>();
                return found ?? BattleInputFallback;
            }
        }

        public static GameObject Player() => Load(PlayerPath);
        public static GameObject Ally() => Load(AllyPath);
        public static GameObject CombatManager() => Load(CombatManagerPath);

        // ── 탐색 ────────────────────────────────────────

        /// <summary>
        /// 못 찾으면 옛 자리를 돌려준다. 부르는 쪽이 "없다"를 스스로 처리하게 두는 편이
        /// 여기서 null을 흘리는 것보다 낫다 — 대부분의 호출부가 곧바로
        /// <c>LoadAssetAtPath</c>를 하고 null 검사를 이미 갖고 있다.
        /// </summary>
        private static string Locate<T>(string legacyName) where T : Component
            => Find<T>() ?? $"{SearchRoot}/{legacyName}.prefab";

        private static string Find<T>() where T : Component
        {
            var hits = new List<string>();

            foreach (string guid in AssetDatabase.FindAssets("t:Prefab", new[] { SearchRoot }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                var go = AssetDatabase.LoadAssetAtPath<GameObject>(path);

                // 루트만 본다. GetComponentInChildren을 쓰면 BattleInput이
                // 자기 안의 Player를 물고 Player 프리팹 행세를 한다.
                if (go != null && go.GetComponent<T>() != null) hits.Add(path);
            }

            if (hits.Count == 0) return null;

            if (hits.Count > 1)
                Debug.LogWarning(
                    $"[PrefabLocator] 루트에 {typeof(T).Name}을(를) 가진 프리팹이 {hits.Count}개다 — " +
                    $"'{hits[0]}'을(를) 쓴다. 나머지: {string.Join(", ", hits.GetRange(1, hits.Count - 1))}");

            return hits[0];
        }

        private static GameObject Load(string path) => AssetDatabase.LoadAssetAtPath<GameObject>(path);
    }
}

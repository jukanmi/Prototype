using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace Prototype.EditorTools
{
    /// <summary>
    /// 동료마다 <b>제 프리팹</b>을 찍어낸다. 공용 <c>Ally.prefab</c>을 기준으로 복제해
    /// 컴포넌트 구성 · 콜라이더 · 애니메이터를 물려받고, 몸 크기와 직업 색만 구워 넣는다.
    ///
    /// <b>왜 필요한가.</b> 지금까지 동료 넷은 <c>Ally.prefab</c> 하나를 공유하고
    /// <see cref="PartyMemberData.bodyScale"/> · <see cref="PartyMemberData.spriteTint"/>로만
    /// 갈렸다. 그 두 값은 런타임에 <see cref="PartyAssembler"/>가 얹는 것이라
    /// <b>에디터에서는 넷이 완전히 똑같아 보인다</b> — 씬 뷰에서 누가 탱커인지 알 수가 없고,
    /// 체형 · 콜라이더 · 애니메이션 계층이 다른 동료를 만들 자리도 없다.
    ///
    /// <b>표에서 프리팹으로 값을 옮긴다.</b> 구워 넣은 뒤 표의
    /// <c>bodyScale</c>을 1로, <c>spriteTint</c>를 흰색으로 되돌린다.
    /// 안 그러면 <see cref="PartyAssembler"/>가 프리팹 값 위에 <b>한 번 더</b> 곱하고 칠한다 —
    /// 0.94 배가 0.88 배가 되는 식이라 눈으로는 "좀 작아졌나" 정도로만 보인다.
    ///
    /// <b>기준값은 표가 아니라 <see cref="Scales"/>가 들고 있다.</b> 표를 1로 되돌리므로
    /// 표를 다시 읽으면 두 번째 실행부터 크기가 사라진다. 그래서 의도한 값은 여기 남긴다 —
    /// <see cref="EnemyPrefabBuilder"/>의 변종 표와 같은 이유다.
    ///
    /// 몇 번을 돌려도 같은 경로를 갱신할 뿐 애셋이 늘지 않는다.
    /// </summary>
    public static class AllyPrefabBuilder
    {
        /// <summary>동료별 프리팹이 사는 곳. 공용 <c>Ally.prefab</c>과 <b>다른 폴더</b>다.</summary>
        public const string PrefabFolder = "Assets/Prefabs/Player/Allies";

        public const string Prefix = "Ally_";

        /// <summary>
        /// 몸 크기. 지금 <see cref="PartyMemberData.bodyScale"/>에 들어 있는 값을 그대로 옮긴 것이다 —
        /// <b>화면이 지금과 똑같이 보이는 것</b>이 이 작업의 성공 조건이라 값을 손대지 않는다.
        /// 체형을 실제로 다시 잡는 건 프리팹이 생긴 다음의 일이고, 그때는 여기가 아니라
        /// 각 프리팹의 <c>View</c> · <c>Shadow</c>를 인스펙터에서 만지면 된다.
        ///
        /// 표에 없는 <c>memberId</c>는 1배로 만든다 — 동료를 늘려도 이 표를 안 고쳐도 된다.
        /// </summary>
        private static readonly Dictionary<string, float> Scales = new Dictionary<string, float>
        {
            { "Tan", 0.94f },
            { "War", 1.06f },
            { "Arc", 0.88f },
            { "Wiz", 1.12f },
        };

        [MenuItem("Prototype/파티 - 동료별 프리팹 만들기", priority = 31)]
        public static void Build()
        {
            string basePath = PrefabLocator.AllyPath;
            var basePrefab = AssetDatabase.LoadAssetAtPath<GameObject>(basePath);

            if (basePrefab == null)
            {
                Debug.LogError($"[AllyPrefabBuilder] 기준 프리팹이 없다: {basePath}");
                return;
            }

            if (basePrefab.GetComponent<Ally>() == null)
            {
                Debug.LogError($"[AllyPrefabBuilder] {basePath} 루트에 Ally 가 없다. " +
                               "PrefabLocator 가 엉뚱한 프리팹을 골랐다.");
                return;
            }

            PartyMemberData[] members = LoadMembers();
            if (members.Length == 0)
            {
                Debug.LogWarning("[AllyPrefabBuilder] PartyMemberData 가 하나도 없다.");
                return;
            }

            EnsureFolder(PrefabFolder);

            var made = new List<string>(members.Length);
            foreach (PartyMemberData member in members)
                made.Add(BuildOne(member, basePrefab));

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log($"[AllyPrefabBuilder] 동료 {made.Count}명 생성 완료 → {PrefabFolder}\n" +
                      string.Join("\n", made) + "\n\n" +
                      "표의 bodyScale · spriteTint 는 프리팹으로 옮겨졌으므로 1 · 흰색으로 되돌렸다. " +
                      "'Prototype ▸ 전투 - 입력 호스트 프리팹 만들기'는 다시 돌릴 필요가 없다 — " +
                      "기본 프리팹은 여전히 공용 Ally.prefab 이다.");
        }

        /// <summary>
        /// 이름 순으로 고정한다. <c>FindAssets</c>의 순서는 보장되지 않아서,
        /// 정렬하지 않으면 실행할 때마다 로그의 줄 순서가 바뀌어 무엇이 달라졌는지 안 읽힌다.
        /// </summary>
        private static PartyMemberData[] LoadMembers()
        {
            var list = new List<PartyMemberData>();

            foreach (string guid in AssetDatabase.FindAssets("t:" + nameof(PartyMemberData)))
            {
                var m = AssetDatabase.LoadAssetAtPath<PartyMemberData>(
                    AssetDatabase.GUIDToAssetPath(guid));
                if (m != null) list.Add(m);
            }

            list.Sort((a, b) => string.CompareOrdinal(a.name, b.name));
            return list.ToArray();
        }

        private static string BuildOne(PartyMemberData member, GameObject basePrefab)
        {
            string id = !string.IsNullOrEmpty(member.memberId) ? member.memberId : member.name;
            float scale = Scales.TryGetValue(id, out float s) ? s : 1f;
            Color tint = ArtImportBuilder.RoleTint(member.role);

            // 프리팹 애셋을 직접 편집하지 않는다. 사본을 조립해서 통째로 저장한다.
            var root = Object.Instantiate(basePrefab);
            root.name = Prefix + id;

            // 루트는 원점 · 무회전 · 배율 1. 루트 배율을 건드리면 피격 콜라이더가 함께 커지고,
            // 그 치수를 Entity.HurtboxSize 가 읽어 스킬 사거리 배수로 쓴다
            // (SkillData.castRangeScale) — 몸집 큰 동료의 스킬만 사거리가 늘어난다.
            root.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
            root.transform.localScale = Vector3.one;

            ApplyBodyScale(root.transform, scale);
            ApplyTint(root.transform, tint);
            AllyLayers.Apply(root);

            string path = $"{PrefabFolder}/{Prefix}{id}.prefab";
            GameObject saved = PrefabUtility.SaveAsPrefabAsset(root, path);
            Object.DestroyImmediate(root);

            Adopt(member, saved);

            return $"  {member.Label,-8} {RoleNames.Of(member.role),-5} 배율 {scale:0.##} · {path}";
        }

        /// <summary>
        /// 표를 새 몸에 물리고, <b>프리팹으로 옮겨 간 값을 표에서 비운다</b>.
        ///
        /// 비우지 않으면 <see cref="PartyAssembler"/>가 프리팹이 이미 들고 있는 크기와 색 위에
        /// 같은 값을 한 번 더 얹는다. 배율은 곱해지고(0.94 → 0.88), 색은 두 번 칠해져
        /// 도트가 묻힌다. 둘 다 에러가 아니라 "좀 이상한데" 로만 보인다.
        /// </summary>
        private static void Adopt(PartyMemberData member, GameObject prefab)
        {
            member.prefab = prefab;
            member.bodyScale = 1f;
            member.spriteTint = Color.white;

            EditorUtility.SetDirty(member);
        }

        // ── 외형 ────────────────────────────────────────

        /// <summary>
        /// <c>PartyAssembler.ApplyBodyScale</c>과 <b>같은 규칙</b>이어야 한다 —
        /// 여기서 굽는 것이 거기서 얹던 것과 다르면 프리팹을 꽂는 순간 크기가 바뀐다.
        /// 루트가 아니라 <c>View</c> · <c>Shadow</c>를 키우는 것이 그 규칙의 전부다.
        /// </summary>
        private static void ApplyBodyScale(Transform body, float scale)
        {
            if (Mathf.Approximately(scale, 1f) || scale <= 0f) return;

            Scale(body.Find("View"), scale);
            Scale(body.Find("Shadow"), scale);
        }

        private static void Scale(Transform t, float scale)
        {
            if (t != null) t.localScale *= scale;
        }

        /// <summary>
        /// 직업 색. <b>알파는 건드리지 않는다</b> — 등장 · 사망 연출이 알파를 애니메이션으로
        /// 굴리고(<c>Ally_Dead</c> 클립의 <c>m_Color.a</c>), 여기서 덮으면 그 연출이 첫 프레임에 잘린다.
        /// </summary>
        private static void ApplyTint(Transform body, Color tint)
        {
            Transform sprite = body.Find(PartyMemberData.SpritePath);
            var sr = sprite != null ? sprite.GetComponent<SpriteRenderer>() : null;

            if (sr == null)
            {
                Debug.LogWarning($"[AllyPrefabBuilder] {body.name} 에 " +
                                 $"'{PartyMemberData.SpritePath}' 가 없다 — 색을 못 칠했다.");
                return;
            }

            sr.color = new Color(tint.r, tint.g, tint.b, sr.color.a);
        }

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;

            string parent = System.IO.Path.GetDirectoryName(path).Replace('\\', '/');
            EnsureFolder(parent);

            AssetDatabase.CreateFolder(parent, System.IO.Path.GetFileName(path));
        }
    }
}

using UnityEditor;
using UnityEngine;

namespace Prototype.EditorTools
{
    /// <summary>
    /// <c>Ally.prefab</c> · <c>Player.prefab</c>에 <b>스킬 전용 히트박스</b>를 붙인다.
    ///
    /// 두 프리팹은 <c>skillAttack</c>이 비어 있어 <see cref="Entity.SkillAttack"/>이 평타 캡슐
    /// (r 0.5 · h 2)로 떨어진다. 그 캡슐은 1·2타가 밀어낸 거리(force 4 / impulseDamping 8 = 0.5유닛)
    /// 밖으로 대상을 내보내므로 <b>다단히트 후속타가 통째로 씹힌다</b>.
    ///
    /// <see cref="TestSceneBuilder"/>는 이미 2.4 × 2 × 2.4 박스를 만들어 물리고 있다 —
    /// 프리팹만 그 규약을 안 따라왔다. 여기서 맞춘다.
    ///
    /// 멱등이다. 이미 붙어 있으면 크기·연결만 확인하고 끝낸다.
    /// </summary>
    public static class SkillHitboxPrefabBuilder
    {
        /// <summary>재실행 때 이 이름으로 찾는다.</summary>
        internal const string SkillHitboxName = "SkillHitbox";

        // 평타 캡슐(r 0.5)보다 확실히 커야 한다. TestSceneBuilder의 동료 규약과 같은 값.
        internal static readonly Vector3 SkillHitboxSize = new Vector3(2.4f, 2f, 2.4f);
        /// <summary>정면은 로컬 +Z. 평타 히트박스(z +1)와 같은 자리에서 시작한다.</summary>
        internal static readonly Vector3 SkillHitboxPos = new Vector3(0f, 0f, 1f);

        /// <summary>경로가 아니라 컴포넌트로 찾는다 — 프리팹을 옮겨도 안 끊긴다.</summary>
        internal static string[] TargetPrefabs
            => new[] { PrefabLocator.AllyPath, PrefabLocator.PlayerPath };

        [MenuItem("Prototype/프리팹 - 스킬 히트박스 붙이기")]
        public static void Build()
        {
            int touched = 0;

            foreach (string path in TargetPrefabs)
                if (Apply(path)) touched++;

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log($"[SkillHitboxPrefabBuilder] {touched}/{TargetPrefabs.Length}개 프리팹을 갱신했다.");
        }

        /// <summary>프리팹 하나를 손본다. 바꿀 게 없으면 false — 애셋을 헛되이 더럽히지 않는다.</summary>
        internal static bool Apply(string path)
        {
            GameObject root = PrefabUtility.LoadPrefabContents(path);
            if (root == null)
            {
                Debug.LogWarning($"[SkillHitboxPrefabBuilder] {path}를 열지 못했다.");
                return false;
            }

            try
            {
                bool changed = EnsureSkillHitbox(root, out Attack hitbox);
                changed |= Link(root, hitbox);

                if (changed) PrefabUtility.SaveAsPrefabAsset(root, path);
                return changed;
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        /// <summary>규약대로 된 <see cref="Attack"/> 자식을 보장한다.</summary>
        private static bool EnsureSkillHitbox(GameObject root, out Attack hitbox)
        {
            bool changed = false;

            Transform child = root.transform.Find(SkillHitboxName);
            if (child == null)
            {
                var go = new GameObject(SkillHitboxName);
                go.transform.SetParent(root.transform, false);
                child = go.transform;
                changed = true;
            }

            GameObject node = child.gameObject;

            // 평타 히트박스와 같은 레이어라야 충돌 매트릭스가 맞는다(AllyHitbox).
            int layer = BasicHitboxLayer(root);
            if (node.layer != layer)
            {
                node.layer = layer;
                changed = true;
            }

            if (child.localPosition != SkillHitboxPos)
            {
                child.localPosition = SkillHitboxPos;
                changed = true;
            }

            var box = node.GetComponent<BoxCollider>();
            if (box == null)
            {
                box = node.AddComponent<BoxCollider>();
                changed = true;
            }

            if (!box.isTrigger)
            {
                box.isTrigger = true;
                changed = true;
            }

            if (box.size != SkillHitboxSize)
            {
                box.size = SkillHitboxSize;
                changed = true;
            }

            if (box.center != Vector3.zero)
            {
                box.center = Vector3.zero;
                changed = true;
            }

            hitbox = node.GetComponent<Attack>();
            if (hitbox == null)
            {
                hitbox = node.AddComponent<Attack>();
                changed = true;
            }

            var combat = root.GetComponent<Combat>();
            if (hitbox.Attacker != combat)
            {
                hitbox.Attacker = combat;
                changed = true;
            }

            return changed;
        }

        /// <summary>평타 히트박스가 쓰는 레이어. 없으면 루트 레이어로 떨어진다.</summary>
        private static int BasicHitboxLayer(GameObject root)
        {
            var entity = root.GetComponent<Entity>();
            Attack basic = entity != null ? entity.BasicAttack : null;

            return basic != null ? basic.gameObject.layer : root.layer;
        }

        /// <summary><c>skillAttack</c> 필드에 물린다. private 필드라 SerializedObject로 간다.</summary>
        private static bool Link(GameObject root, Attack hitbox)
        {
            var entity = root.GetComponent<Entity>();
            if (entity == null) return false;

            var so = new SerializedObject(entity);
            SerializedProperty prop = so.FindProperty("skillAttack");
            if (prop == null) return false;

            if (prop.objectReferenceValue == hitbox) return false;

            prop.objectReferenceValue = hitbox;
            so.ApplyModifiedPropertiesWithoutUndo();
            return true;
        }
    }
}

using UnityEditor;
using UnityEngine;

namespace Prototype.EditorTools
{
    /// <summary>
    /// 생성기들이 평타 값을 찌를 때 쓰는 한 창구.
    ///
    /// <b>왜 필요한가.</b> 평타가 <see cref="Entity"/>에서 <see cref="BasicAttackProfile"/>로
    /// 나가면서, <c>new SerializedObject(entity).FindProperty("basicAttack")</c> 하던 자리가
    /// 전부 <c>null</c>을 받게 됐다. 그런 코드는 컴파일도 되고 실행도 되다가
    /// <b>찌르는 순간에만</b> 터진다 — 여섯 군데가 같은 방식으로 깨져 있었다.
    /// 붙일 컴포넌트를 고르는 규칙도 한 곳에 있어야 생성기마다 갈라지지 않는다.
    ///
    /// 규칙은 하나다 — <see cref="Enemy"/>면 <see cref="EnemyBasicAttack"/>, 아니면
    /// <see cref="AllyBasicAttack"/>. <see cref="Entity"/>와 <b>같은 GameObject</b>에 붙인다
    /// (런타임이 <c>GetComponent</c>로 찾으므로 자식에 붙으면 조용히 안 잡힌다).
    /// </summary>
    internal static class BasicAttackProfiles
    {
        /// <summary>
        /// 이 몸의 평타 프로필. 없으면 진영에 맞는 것을 붙여서 돌려준다.
        /// <see cref="Entity"/>가 없으면 null — 부르는 쪽이 그냥 건너뛰면 된다.
        /// </summary>
        public static BasicAttackProfile GetOrAdd(GameObject root)
        {
            if (root == null) return null;

            var entity = root.GetComponent<Entity>();
            if (entity == null) return null;

            var profile = root.GetComponent<BasicAttackProfile>();
            if (profile != null) return profile;

            profile = entity is Enemy
                ? root.AddComponent<EnemyBasicAttack>()
                : (BasicAttackProfile)root.AddComponent<AllyBasicAttack>();

            EditorUtility.SetDirty(profile);
            return profile;
        }

        /// <summary>
        /// 평타 히트박스를 물린다. 값이 그대로면 아무것도 안 하고 false —
        /// 생성기가 "무엇을 바꿨나"를 세는 데 쓴다.
        /// </summary>
        public static bool SetHitbox(GameObject root, Attack hitbox)
            => SetReference(root, "basicAttack", hitbox);

        /// <summary>스킬 히트박스를 물린다. 비우면 평타 것을 재사용한다.</summary>
        public static bool SetSkillHitbox(GameObject root, Attack hitbox)
            => SetReference(root, "skillAttack", hitbox);

        /// <summary>선딜 · 판정끝 · 전체 길이를 한 번에 박는다.</summary>
        public static void SetTiming(GameObject root, float windup, float activeEnd, float total)
        {
            BasicAttackProfile profile = GetOrAdd(root);
            if (profile == null) return;

            var so = new SerializedObject(profile);
            so.FindProperty("windup").floatValue = windup;
            so.FindProperty("activeEnd").floatValue = activeEnd;
            so.FindProperty("total").floatValue = total;
            so.ApplyModifiedPropertiesWithoutUndo();

            EditorUtility.SetDirty(profile);
        }

        private static bool SetReference(GameObject root, string field, Object value)
        {
            BasicAttackProfile profile = GetOrAdd(root);
            if (profile == null) return false;

            var so = new SerializedObject(profile);
            SerializedProperty prop = so.FindProperty(field);
            if (prop == null) return false;

            if (prop.objectReferenceValue == value) return false;

            prop.objectReferenceValue = value;
            so.ApplyModifiedPropertiesWithoutUndo();

            EditorUtility.SetDirty(profile);
            return true;
        }
    }
}

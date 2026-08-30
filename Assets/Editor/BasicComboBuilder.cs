using UnityEditor;
using UnityEngine;

namespace Prototype.EditorTools
{
    /// <summary>
    /// 평타 3연타(약 · 중 · 강)에 필요한 클립을 굽고 프리팹에 단계를 채운다.
    ///
    /// <b><see cref="ArtImportBuilder"/>의 "전부 임포트" 메뉴를 쓰지 않는 이유</b>:
    /// 그쪽은 (1) 프로젝트 <b>바깥</b> 폴더에서 시트를 복사해 오고,
    /// (2) 이미 삭제된 <c>AllyAnimator.controller</c>를 다시 만들어 <c>Ally.prefab</c>을
    /// 거기로 옮겨 붙인다 — 지금 돌리면 애니메이터 배선이 갈라진다.
    /// 여기서는 <b>이미 프로젝트 안에 슬라이스돼 있는</b> 시트로 클립만 굽고,
    /// 컨트롤러는 손대지 않는다(단계별 클립은 런타임 오버라이드로 꽂힌다 —
    /// <see cref="EntityAnimator.PlayBasicAttackStage"/>).
    ///
    /// 멱등이다. 두 번 돌려도 클립 내용만 갱신되고 프리팹은 이미 채워져 있으면 그대로 둔다.
    /// </summary>
    public static class BasicComboBuilder
    {
        private const string AnimFolder = "Assets/Data/Animation";

        /// <summary>경로가 아니라 컴포넌트로 찾는다 — 프리팹을 옮겨도 안 끊긴다.</summary>
        internal static string[] TargetPrefabs
            => new[] { PrefabLocator.PlayerPath, PrefabLocator.AllyPath };

        /// <summary>단계 수. 에셋 시트가 3장이라 3타다.</summary>
        internal const int StageCount = 3;

        internal const string Stage1Clip = "Ally_Attack";    // 이미 있다(기본 평타)
        internal const string Stage2Clip = "Ally_Attack2";
        internal const string Stage3Clip = "Ally_Attack3";

        [MenuItem("Prototype/평타 - 3연타 클립 굽기 + 프리팹 배선")]
        public static void Build()
        {
            BakeClips();

            int touched = 0;
            foreach (string path in TargetPrefabs)
                if (Author(path)) touched++;

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log($"[BasicComboBuilder] 클립 2개 굽고 프리팹 {touched}/{TargetPrefabs.Length}개를 채웠다.");
        }

        /// <summary>
        /// 2·3타 클립을 굽는다. 1타는 기존 <c>Ally_Attack</c>을 그대로 쓴다.
        ///
        /// 3타 fps를 11로 낮춘 이유: 10프레임을 14fps로 구우면 0.71초라 3타 길이(0.70)와 거의 같아
        /// 재생 배율이 1이 된다. 11fps(0.91초)로 구워야 배율 1.3이 걸려 "짧고 세게 몰아친" 마무리가 된다.
        /// 최종 길이는 어차피 <see cref="EntityAnimator"/>가 맞추므로 여기 숫자는 프레임 분배 곡선만 정한다.
        /// </summary>
        private static void BakeClips()
        {
            ArtImportBuilder.BuildSpriteClip(new ArtImportBuilder.ClipSpec("Attack2", "_Attack2", 14f, false));
            ArtImportBuilder.BuildSpriteClip(new ArtImportBuilder.ClipSpec("Attack3", "_AttackCombo2hit", 11f, false));
        }

        /// <summary>프리팹 하나에 3단계를 채운다. 이미 채워져 있으면 false.</summary>
        internal static bool Author(string path)
        {
            GameObject root = PrefabUtility.LoadPrefabContents(path);
            if (root == null)
            {
                Debug.LogWarning($"[BasicComboBuilder] {path}를 열지 못했다.");
                return false;
            }

            try
            {
                var entity = root.GetComponent<Entity>();
                if (entity == null) return false;

                var so = new SerializedObject(entity);
                SerializedProperty stages = so.FindProperty("basicComboStages");
                if (stages == null) return false;

                // 손으로 튜닝한 값을 덮어쓰지 않는다.
                if (stages.arraySize == StageCount) return false;

                stages.arraySize = StageCount;

                // 약 — 짧고 가볍다. 완주 못 하면 손해가 되도록 1타를 기본보다 낮춘다.
                Fill(stages.GetArrayElementAtIndex(0), "약", Clip(Stage1Clip),
                     windup: 0.10f, activeEnd: 0.20f, cancelStart: 0.20f, total: 0.32f, damage: 0.9f);

                // 중
                Fill(stages.GetArrayElementAtIndex(1), "중", Clip(Stage2Clip),
                     windup: 0.12f, activeEnd: 0.24f, cancelStart: 0.24f, total: 0.40f, damage: 1.1f);

                // 강 — 마무리. 여기만 띄운다. cancelStart를 비워 캔슬을 막는다(후딜을 다 진다).
                SerializedProperty last = stages.GetArrayElementAtIndex(2);
                Fill(last, "강", Clip(Stage3Clip),
                     windup: 0.18f, activeEnd: 0.34f, cancelStart: 0f, total: 0.70f, damage: 1.8f);

                last.FindPropertyRelative("overrideReaction").boolValue = true;
                last.FindPropertyRelative("nextState").enumValueIndex = (int)CombatState.AerialHit;
                last.FindPropertyRelative("mode").enumValueIndex = (int)KnockbackMode.AwayFromCaster;
                last.FindPropertyRelative("pushDistance").floatValue = 0.5625f;
                last.FindPropertyRelative("airborneHeight").floatValue = 0.6f;
                last.FindPropertyRelative("hitStunDuration").floatValue = 0.5f;

                so.ApplyModifiedPropertiesWithoutUndo();
                PrefabUtility.SaveAsPrefabAsset(root, path);
                return true;
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        private static void Fill(SerializedProperty stage, string label, AnimationClip clip,
                                 float windup, float activeEnd, float cancelStart, float total, float damage)
        {
            stage.FindPropertyRelative("label").stringValue = label;
            stage.FindPropertyRelative("clip").objectReferenceValue = clip;
            stage.FindPropertyRelative("windup").floatValue = windup;
            stage.FindPropertyRelative("activeEnd").floatValue = activeEnd;
            stage.FindPropertyRelative("cancelStart").floatValue = cancelStart;
            stage.FindPropertyRelative("total").floatValue = total;
            stage.FindPropertyRelative("damageMultiplier").floatValue = damage;

            // 마무리 타만 덮어쓴다. 나머지는 basicHit 그대로다.
            stage.FindPropertyRelative("overrideReaction").boolValue = false;
            stage.FindPropertyRelative("pushDistance").floatValue = 0f;
            stage.FindPropertyRelative("airborneHeight").floatValue = 0f;
            stage.FindPropertyRelative("hitStunDuration").floatValue = 0f;
        }

        private static AnimationClip Clip(string name)
        {
            var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>($"{AnimFolder}/{name}.anim");
            if (clip == null)
                Debug.LogWarning($"[BasicComboBuilder] {name}.anim 이 없다. 그 타는 기본 평타 모션으로 떨어진다.");

            return clip;
        }
    }
}

using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace Prototype
{
    /// <summary>
    /// 레벨업 선택지의 모집단(<see cref="SkillCatalog"/>) 에셋을 프로젝트의 SkillData 전부로 굽는다.
    ///
    /// 에셋이 <b>없어도 게임은 돈다</b> — <see cref="SkillCatalog.Pool"/>이 파티 장착 카드로
    /// 폴백하기 때문이다. 다만 그 폴백은 "동료가 장착한 4장"만 보므로, 장착되지 않은 스킬까지
    /// 선택지에 띄우려면 이 표가 있어야 한다.
    /// </summary>
    public static class SkillCatalogBuilder
    {
        private const string Folder = "Assets/Data/Resources";
        private const string AssetPath = Folder + "/" + SkillCatalog.ResourcePath + ".asset";

        [MenuItem("Tools/Prototype/스킬 카탈로그 굽기")]
        public static void Build()
        {
            List<SkillData> skills = FindAllSkills();

            if (skills.Count == 0)
            {
                Debug.LogError("[SkillCatalogBuilder] SkillData 에셋을 하나도 못 찾았다. 굽지 않는다.");
                return;
            }

            if (!Directory.Exists(Folder)) Directory.CreateDirectory(Folder);

            var catalog = AssetDatabase.LoadAssetAtPath<SkillCatalog>(AssetPath);
            bool created = catalog == null;

            if (created)
            {
                catalog = ScriptableObject.CreateInstance<SkillCatalog>();
                AssetDatabase.CreateAsset(catalog, AssetPath);
            }

            var so = new SerializedObject(catalog);
            SerializedProperty list = so.FindProperty("skills");

            list.arraySize = skills.Count;
            for (int i = 0; i < skills.Count; i++)
                list.GetArrayElementAtIndex(i).objectReferenceValue = skills[i];

            so.ApplyModifiedProperties();

            EditorUtility.SetDirty(catalog);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log($"[SkillCatalogBuilder] {(created ? "생성" : "갱신")} — {AssetPath}에 스킬 {skills.Count}종", catalog);
        }

        /// <summary>이름순으로 세운다. 굽을 때마다 순서가 흔들리면 diff가 매번 지저분해진다.</summary>
        private static List<SkillData> FindAllSkills()
        {
            var skills = new List<SkillData>();

            foreach (string guid in AssetDatabase.FindAssets("t:SkillData"))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                var data = AssetDatabase.LoadAssetAtPath<SkillData>(path);
                if (data != null) skills.Add(data);
            }

            skills.Sort((a, b) => string.CompareOrdinal(a.name, b.name));
            return skills;
        }
    }
}

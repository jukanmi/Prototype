using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;

namespace Prototype.EditorTools
{
    /// <summary>
    /// 슬라이스한 스프라이트 시트 → <see cref="VfxClip"/> 에셋.
    ///
    /// 손으로 만들면 프레임 배열을 12~24칸 끌어다 넣어야 하고, 그 과정에서
    /// <c>_10</c>이 <c>_2</c> 앞에 오는 문자열 정렬 사고가 반드시 난다.
    /// 이름 끝의 숫자를 <b>수로</b> 비교해서 순서를 잡아 준다.
    /// </summary>
    public static class VfxClipBuilder
    {
        private const string ClipFolder = "Assets/Data/Vfx";

        // Resources 폴더는 Assets 아래 어디에 있어도 잡힌다. 이미 있는 것을 쓴다 —
        // 두 벌이 생기면 어느 쪽이 로드되는지 헷갈린다.
        private const string LibraryFolder = "Assets/Data/Resources";

        /// <summary>이름 끝의 연속된 숫자. Slash_2 · Slash_10 을 2 · 10으로 읽는다.</summary>
        private static readonly Regex TrailingNumber = new Regex(@"(\d+)\s*$");

        [MenuItem("Assets/Prototype/선택한 시트 → VfxClip", true)]
        private static bool ValidateFromSelection() => CollectSprites().Count > 0;

        [MenuItem("Assets/Prototype/선택한 시트 → VfxClip")]
        public static void FromSelection()
        {
            List<Sprite> sprites = CollectSprites();
            if (sprites.Count == 0)
            {
                Debug.LogWarning("[VfxClipBuilder] 슬라이스된 스프라이트가 선택되지 않았다. " +
                                 "Texture Type을 Sprite (2D and UI), Sprite Mode를 Multiple로 두고 슬라이스했는지 확인.");
                return;
            }

            EnsureFolder(ClipFolder);

            string baseName = Path.GetFileNameWithoutExtension(
                AssetDatabase.GetAssetPath(Selection.objects[0]));
            string path = AssetDatabase.GenerateUniqueAssetPath($"{ClipFolder}/VFX_{baseName}.asset");

            var clip = ScriptableObject.CreateInstance<VfxClip>();
            clip.frames = sprites.ToArray();

            AssetDatabase.CreateAsset(clip, path);
            AssetDatabase.SaveAssets();

            Selection.activeObject = clip;
            EditorGUIUtility.PingObject(clip);

            Debug.Log($"[VfxClipBuilder] {path} — 프레임 {sprites.Count}장, " +
                      $"{clip.fps}fps → {clip.Duration:0.###}s", clip);
        }

        /// <summary>
        /// 빈 라이브러리를 Resources에 만든다. <see cref="VfxLibrary.Get"/>이 여기서 찾는다.
        /// 칸을 비워 두면 그 이펙트만 기존 LineRenderer 연출로 떨어진다.
        /// </summary>
        [MenuItem("Prototype/이펙트 - VfxLibrary 만들기")]
        public static void CreateLibrary()
        {
            EnsureFolder(LibraryFolder);

            string path = $"{LibraryFolder}/{VfxLibrary.ResourcePath}.asset";
            var existing = AssetDatabase.LoadAssetAtPath<VfxLibrary>(path);
            if (existing != null)
            {
                Selection.activeObject = existing;
                EditorGUIUtility.PingObject(existing);
                Debug.Log($"[VfxClipBuilder] 이미 있다 — {path}", existing);
                return;
            }

            var lib = ScriptableObject.CreateInstance<VfxLibrary>();
            AssetDatabase.CreateAsset(lib, path);
            AssetDatabase.SaveAssets();

            Selection.activeObject = lib;
            EditorGUIUtility.PingObject(lib);
            Debug.Log($"[VfxClipBuilder] {path} 생성. 칸을 채우면 그 이펙트부터 시트로 바뀐다.", lib);
        }

        /// <summary>선택한 것에서 스프라이트를 모은다. 텍스처를 골랐으면 하위 슬라이스를 전부 가져온다.</summary>
        private static List<Sprite> CollectSprites()
        {
            var found = new List<Sprite>();
            if (Selection.objects == null) return found;

            foreach (Object o in Selection.objects)
            {
                if (o is Sprite s)
                {
                    found.Add(s);
                    continue;
                }

                if (o is Texture2D)
                {
                    string path = AssetDatabase.GetAssetPath(o);
                    foreach (Object sub in AssetDatabase.LoadAllAssetsAtPath(path))
                        if (sub is Sprite sliced) found.Add(sliced);
                }
            }

            found.Sort(CompareByTrailingNumber);
            return found;
        }

        /// <summary>끝 숫자를 수로 비교. 없으면 이름 그대로.</summary>
        private static int CompareByTrailingNumber(Sprite a, Sprite b)
        {
            Match ma = TrailingNumber.Match(a.name);
            Match mb = TrailingNumber.Match(b.name);

            if (ma.Success && mb.Success)
            {
                int na = int.Parse(ma.Groups[1].Value);
                int nb = int.Parse(mb.Groups[1].Value);
                if (na != nb) return na.CompareTo(nb);
            }

            return string.CompareOrdinal(a.name, b.name);
        }

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;

            string parent = Path.GetDirectoryName(path).Replace('\\', '/');
            string leaf = Path.GetFileName(path);

            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, leaf);
        }
    }
}

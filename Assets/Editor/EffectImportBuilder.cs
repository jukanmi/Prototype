using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEditor.U2D.Sprites;
using UnityEngine;
using static Prototype.EditorTools.ArtImportBuilder;

namespace Prototype.EditorTools
{
    /// <summary>
    /// <c>Assets/Art/Effect</c>의 픽셀 이펙트 팩을 <see cref="VfxClip"/>까지 한 번에 굽는다.
    ///
    /// 시트 한 장은 <b>64x64 격자</b>다. 가로가 프레임, 세로 9칸은 <b>같은 애니의 색 변형</b>이다.
    /// 그래서 통째로 슬라이스하면 프레임 순서에 색이 섞여 들어간다 —
    /// <see cref="VfxClipBuilder"/>가 이름 끝 숫자로만 정렬하기 때문이다.
    /// 여기서는 <b>쓰는 행만</b> 잘라내고, 그 행의 프레임만 클립으로 묶는다.
    ///
    /// 쓰지 않는 시트는 건드리지 않는다. 180장을 전부 9행으로 쪼개면
    /// 서브에셋이 만 개 단위로 불어난다.
    /// </summary>
    public static class EffectImportBuilder
    {
        private const string EffectRoot = "Assets/Art/Effect";
        private const string ClipFolder = "Assets/Data/Vfx";
        private const string SkillFolder = "Assets/Data/Skills";
        private const string LibraryPath = "Assets/Data/Resources/VfxLibrary.asset";

        /// <summary>격자 한 칸. 팩 전체가 이 크기다(높이 576 = 64 x 9행).</summary>
        private const int Cell = 64;

        /// <summary>색 변형 행 수. 시트 높이가 이와 다르면 건너뛴다 — 규격 밖 시트를 잘못 자르지 않게.</summary>
        private const int RowCount = 9;

        /// <summary>캐릭터 아트(<c>Rings.png</c>)와 같은 값. 다르면 이펙트만 크기가 튄다.</summary>
        private const float PixelsPerUnit = 48f;

        /// <summary>이 값 이하의 알파만 있는 칸은 빈 칸으로 본다. 끝쪽 빈 칸은 잘라낸다.</summary>
        private const byte AlphaFloor = 8;

        // ── 색 행 ────────────────────────────
        // 시트 위에서부터 0..8. 색은 팩 전체가 같은 순서다.
        private const int RowOrange = 0;
        private const int RowCyan = 2;
        private const int RowGreen = 3;
        private const int RowWhite = 5;
        private const int RowRed = 7;

        /// <summary>클립 하나의 설계도. 시트 · 행 · 재생 값.</summary>
        private struct ClipSpec
        {
            public string clipName;
            public string sheet;          // "Part 5/220"
            public int row;
            public float fps;
            public bool rotateToFacing;
            public bool useSkillColor;    // 흰 행을 호출부 색으로 물들일 때만 켠다
            public float fadeOut;
            public int sortingOffset;
        }

        /// <summary>
        /// 색은 <b>자리마다</b> 다르게 잡았다 —
        /// 타격은 주황(전역 <see cref="BattleVfx.ImpactColor"/>와 같은 결),
        /// 시전은 파랑(조준 표시와 같은 계열), 충격파·벽은 흰 행에 호출부 색을 곱한다.
        /// 스킬은 직업색을 따른다: 탱커 흰색 · 전사 붉은색 · 궁수 초록 · 마법사 청록.
        /// </summary>
        private static readonly ClipSpec[] Specs =
        {
            // ── 전역 (VfxLibrary) ────────────────────────────
            new ClipSpec { clipName = "VFX_Impact",  sheet = "Part 5/220",  row = RowOrange, fps = 24f, fadeOut = 0.3f,  sortingOffset = 50 },
            new ClipSpec { clipName = "VFX_Shock",   sheet = "Part 12/586", row = RowWhite,  fps = 26f, fadeOut = 0.3f,  sortingOffset = 40, useSkillColor = true },
            new ClipSpec { clipName = "VFX_Cast",    sheet = "Part 1/26",   row = RowCyan,   fps = 20f, fadeOut = 0.35f, sortingOffset = -50 },
            new ClipSpec { clipName = "VFX_Wall",    sheet = "Part 11/517", row = RowWhite,  fps = 24f, fadeOut = 0.25f, sortingOffset = 50, useSkillColor = true },

            // ── 직업별 시전 표시 ────────────────────────────
            // 바닥에 찍히는 자리라 캐릭터 뒤(-)로 깐다. 앞에 그리면 시전자를 가린다.
            new ClipSpec { clipName = "VFX_Cast_TK", sheet = "Part 1/26",   row = RowWhite, fps = 20f, fadeOut = 0.35f, sortingOffset = -50 },
            new ClipSpec { clipName = "VFX_Cast_WR", sheet = "Part 1/26",   row = RowRed,   fps = 20f, fadeOut = 0.35f, sortingOffset = -50 },
            new ClipSpec { clipName = "VFX_Cast_AR", sheet = "Part 14/673", row = RowGreen, fps = 22f, fadeOut = 0.3f,  sortingOffset = -50 },
            new ClipSpec { clipName = "VFX_Cast_WZ", sheet = "Part 4/184",  row = RowCyan,  fps = 20f, fadeOut = 0.35f, sortingOffset = -50 },

            // ── 탱커 (흰색) ────────────────────────────
            new ClipSpec { clipName = "VFX_TK_대지강타",     sheet = "Part 11/527", row = RowWhite, fps = 26f, fadeOut = 0.25f, sortingOffset = 50 },
            new ClipSpec { clipName = "VFX_TK_방패연타",     sheet = "Part 7/314",  row = RowWhite, fps = 30f, fadeOut = 0.2f,  sortingOffset = 50 },
            new ClipSpec { clipName = "VFX_TK_방패올려치기", sheet = "Part 8/378",  row = RowWhite, fps = 26f, fadeOut = 0.25f, sortingOffset = 50 },
            new ClipSpec { clipName = "VFX_TK_소용돌이베기", sheet = "Part 6/296",  row = RowWhite, fps = 24f, fadeOut = 0.3f,  sortingOffset = 50 },

            // ── 전사 (붉은색) ────────────────────────────
            // 돌진·베기 궤적은 방향이 있는 그림이라 시전 방향으로 돌린다.
            new ClipSpec { clipName = "VFX_WR_돌진베기",     sheet = "Part 8/375",  row = RowRed, fps = 26f, fadeOut = 0.25f, sortingOffset = 50, rotateToFacing = true },
            new ClipSpec { clipName = "VFX_WR_사슬감아치기", sheet = "Part 13/612", row = RowRed, fps = 22f, fadeOut = 0.3f,  sortingOffset = 50 },
            new ClipSpec { clipName = "VFX_WR_올려베기",     sheet = "Part 15/722", row = RowRed, fps = 26f, fadeOut = 0.25f, sortingOffset = 50 },
            new ClipSpec { clipName = "VFX_WR_회전강타",     sheet = "Part 1/16",   row = RowRed, fps = 24f, fadeOut = 0.3f,  sortingOffset = 50 },

            // ── 궁수 (초록) ────────────────────────────
            new ClipSpec { clipName = "VFX_AR_강력사격",     sheet = "Part 9/449",  row = RowGreen, fps = 24f, fadeOut = 0.25f, sortingOffset = 50 },
            new ClipSpec { clipName = "VFX_AR_그물사격",     sheet = "Part 4/185",  row = RowGreen, fps = 20f, fadeOut = 0.4f,  sortingOffset = 50 },
            new ClipSpec { clipName = "VFX_AR_상승화살",     sheet = "Part 8/386",  row = RowGreen, fps = 26f, fadeOut = 0.25f, sortingOffset = 50 },
            new ClipSpec { clipName = "VFX_AR_연속사격",     sheet = "Part 6/274",  row = RowGreen, fps = 30f, fadeOut = 0.2f,  sortingOffset = 50 },

            // ── 마법사 (청록) ────────────────────────────
            new ClipSpec { clipName = "VFX_WZ_마력탄연사",   sheet = "Part 2/77",   row = RowCyan, fps = 26f, fadeOut = 0.25f, sortingOffset = 50 },
            new ClipSpec { clipName = "VFX_WZ_융기",         sheet = "Part 11/529", row = RowCyan, fps = 24f, fadeOut = 0.25f, sortingOffset = 50 },
            new ClipSpec { clipName = "VFX_WZ_중력장",       sheet = "Part 4/186",  row = RowCyan, fps = 20f, fadeOut = 0.4f,  sortingOffset = 50 },
            new ClipSpec { clipName = "VFX_WZ_충격파",       sheet = "Part 10/475", row = RowCyan, fps = 26f, fadeOut = 0.3f,  sortingOffset = 50 },
        };

        /// <summary>스킬 에셋 이름 → (타격 클립, 시전 클립).</summary>
        private static readonly (string skill, string hit, string cast)[] SkillWiring =
        {
            ("SK_TK대지강타",     "VFX_TK_대지강타",     "VFX_Cast_TK"),
            ("SK_TK방패연타",     "VFX_TK_방패연타",     "VFX_Cast_TK"),
            ("SK_TK방패올려치기", "VFX_TK_방패올려치기", "VFX_Cast_TK"),
            ("SK_TK소용돌이베기", "VFX_TK_소용돌이베기", "VFX_Cast_TK"),

            ("SK_WR돌진베기",     "VFX_WR_돌진베기",     "VFX_Cast_WR"),
            ("SK_WR사슬감아치기", "VFX_WR_사슬감아치기", "VFX_Cast_WR"),
            ("SK_WR올려베기",     "VFX_WR_올려베기",     "VFX_Cast_WR"),
            ("SK_WR회전강타",     "VFX_WR_회전강타",     "VFX_Cast_WR"),

            ("SK_AR강력사격",     "VFX_AR_강력사격",     "VFX_Cast_AR"),
            ("SK_AR그물사격",     "VFX_AR_그물사격",     "VFX_Cast_AR"),
            ("SK_AR상승화살",     "VFX_AR_상승화살",     "VFX_Cast_AR"),
            ("SK_AR연속사격",     "VFX_AR_연속사격",     "VFX_Cast_AR"),

            ("SK_WZ마력탄연사",   "VFX_WZ_마력탄연사",   "VFX_Cast_WZ"),
            ("SK_WZ융기",         "VFX_WZ_융기",         "VFX_Cast_WZ"),
            ("SK_WZ중력장",       "VFX_WZ_중력장",       "VFX_Cast_WZ"),
            ("SK_WZ충격파",       "VFX_WZ_충격파",       "VFX_Cast_WZ"),
        };

        // ── 진입점 ────────────────────────────

        /// <summary>
        /// 표에 적힌 시트를 슬라이스하고, 클립을 굽고, 라이브러리와 스킬 16장에 물린다.
        /// 여러 번 눌러도 같은 결과다 — 클립 에셋은 이름으로 찾아 덮어쓴다.
        /// </summary>
        [MenuItem("Prototype/이펙트 - Effect 팩 배선 (시트 → 클립 → 스킬)")]
        public static void BuildAll()
        {
            try
            {
                AssetDatabase.StartAssetEditing();
                SliceSheets();
            }
            finally
            {
                AssetDatabase.StopAssetEditing();
                AssetDatabase.Refresh();
            }

            int made = BuildClips();
            int lib = WireLibrary();
            int skills = WireSkills();

            AssetDatabase.SaveAssets();
            Debug.Log($"[EffectImportBuilder] 클립 {made}개 · 라이브러리 {lib}칸 · 스킬 {skills}장 배선 완료.");
        }

        /// <summary>표가 쓰는 시트만, 쓰는 행만 잘라낸다.</summary>
        private static void SliceSheets()
        {
            // 같은 시트를 여러 행으로 쓰는 경우(Part 1/26)가 있어 행을 모아서 한 번에 자른다.
            var rowsBySheet = new Dictionary<string, SortedSet<int>>();
            foreach (ClipSpec s in Specs)
            {
                if (!rowsBySheet.TryGetValue(s.sheet, out SortedSet<int> rows))
                    rowsBySheet[s.sheet] = rows = new SortedSet<int>();
                rows.Add(s.row);
            }

            foreach (KeyValuePair<string, SortedSet<int>> kv in rowsBySheet)
                SliceSheet(SheetPath(kv.Key), kv.Value);
        }

        /// <summary>
        /// 시트 한 장을 격자로 자른다. 필요한 행만 잘라 서브에셋 수를 눌러 둔다.
        ///
        /// 유니티의 sprite rect는 <b>왼쪽 아래</b>가 원점인데 시트는 위에서부터 읽으므로
        /// 행 번호를 뒤집어 y를 잡는다. 이걸 빼먹으면 색이 통째로 어긋난다.
        /// </summary>
        private static void SliceSheet(string path, IEnumerable<int> rows)
        {
            var importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer == null)
            {
                Debug.LogError($"[EffectImportBuilder] 시트를 찾지 못했다 — {path}");
                return;
            }

            byte[] bytes = File.ReadAllBytes(path);
            var probe = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            if (!probe.LoadImage(bytes))
            {
                Object.DestroyImmediate(probe);
                Debug.LogError($"[EffectImportBuilder] PNG를 읽지 못했다 — {path}");
                return;
            }

            int w = probe.width;
            int h = probe.height;

            if (h != Cell * RowCount || w % Cell != 0)
            {
                Object.DestroyImmediate(probe);
                Debug.LogWarning($"[EffectImportBuilder] 64x64 격자가 아니다({w}x{h}) — 건너뛴다: {path}");
                return;
            }

            string baseName = Path.GetFileNameWithoutExtension(path);
            int cols = w / Cell;

            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Multiple;
            importer.spritePixelsPerUnit = PixelsPerUnit;
            importer.filterMode = FilterMode.Point;          // 픽셀 아트가 뭉개지지 않게
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.mipmapEnabled = false;
            importer.alphaIsTransparency = true;
            importer.wrapMode = TextureWrapMode.Clamp;

            var rects = new List<SpriteRect>();
            foreach (int row in rows)
            {
                int last = LastVisibleColumn(probe, row, cols);
                if (last < 0)
                {
                    Debug.LogWarning($"[EffectImportBuilder] {baseName} 행 {row}이 비어 있다 — 건너뛴다.");
                    continue;
                }

                // y 뒤집기. row 0이 시트의 맨 윗줄이다.
                float y = h - (row + 1) * Cell;

                for (int col = 0; col <= last; col++)
                {
                    rects.Add(new SpriteRect
                    {
                        name = $"{baseName}_r{row}_{col:00}",
                        spriteID = GUID.Generate(),
                        rect = new Rect(col * Cell, y, Cell, Cell),
                        alignment = SpriteAlignment.Center,
                        pivot = new Vector2(0.5f, 0.5f),
                    });
                }
            }

            Object.DestroyImmediate(probe);
            if (rects.Count == 0) return;

            var factories = new SpriteDataProviderFactories();
            factories.Init();
            ISpriteEditorDataProvider provider = factories.GetSpriteEditorDataProviderFromObject(importer);
            provider.InitSpriteEditorDataProvider();
            provider.SetSpriteRects(rects.ToArray());

            // 이름 ↔ fileId 표를 같이 갱신하지 않으면 클립이 물고 있던 참조가 끊긴다.
            var names = provider.GetDataProvider<ISpriteNameFileIdDataProvider>();
            if (names != null)
                names.SetNameFileIdPairs(rects.Select(r => new SpriteNameFileIdPair(r.name, r.spriteID)));

            provider.Apply();
            importer.SaveAndReimport();
        }

        /// <summary>
        /// 그림이 남아 있는 마지막 칸. 시트 끝의 빈 칸을 프레임에 넣으면
        /// 이펙트가 다 사라진 뒤에도 재생 시간이 흘러 다음 타격이 늦게 뜬다.
        /// </summary>
        private static int LastVisibleColumn(Texture2D tex, int row, int cols)
        {
            int y0 = tex.height - (row + 1) * Cell;
            Color32[] px = tex.GetPixels32(0);

            for (int col = cols - 1; col >= 0; col--)
            {
                bool any = false;

                for (int y = y0; y < y0 + Cell && !any; y++)
                {
                    int line = y * tex.width;
                    for (int x = col * Cell; x < (col + 1) * Cell; x++)
                    {
                        if (px[line + x].a > AlphaFloor) { any = true; break; }
                    }
                }

                if (any) return col;
            }

            return -1;
        }

        // ── 클립 ────────────────────────────

        /// <summary>표대로 <see cref="VfxClip"/>을 굽는다. 이미 있으면 값만 덮어쓴다.</summary>
        private static int BuildClips()
        {
            EnsureFolder(ClipFolder);

            int made = 0;
            foreach (ClipSpec s in Specs)
            {
                Sprite[] frames = LoadRow(SheetPath(s.sheet), s.row);
                if (frames.Length == 0)
                {
                    Debug.LogError($"[EffectImportBuilder] {s.clipName}: {s.sheet} 행 {s.row}에서 스프라이트를 못 찾았다.");
                    continue;
                }

                string path = $"{ClipFolder}/{s.clipName}.asset";
                var clip = AssetDatabase.LoadAssetAtPath<VfxClip>(path);
                bool isNew = clip == null;
                if (isNew) clip = ScriptableObject.CreateInstance<VfxClip>();

                clip.frames = frames;
                clip.fps = s.fps;
                clip.loops = 1;
                clip.fitRadius = true;
                clip.scale = 1f;
                clip.heightOffset = 0f;
                clip.sortingOffset = s.sortingOffset;
                clip.rotateToFacing = s.rotateToFacing;
                clip.flipToFacing = false;
                clip.tint = Color.white;          // 시트에 색이 들어 있다. 곱하면 탁해진다.
                clip.useSkillColor = s.useSkillColor;
                clip.fadeOut = s.fadeOut;

                if (isNew) AssetDatabase.CreateAsset(clip, path);
                else EditorUtility.SetDirty(clip);

                made++;
            }

            return made;
        }

        /// <summary>슬라이스된 서브에셋에서 한 행만 뽑아 열 순서로 정렬한다.</summary>
        private static Sprite[] LoadRow(string sheetPath, int row)
        {
            string prefix = $"_r{row}_";

            return AssetDatabase.LoadAllAssetsAtPath(sheetPath)
                .OfType<Sprite>()
                .Where(sp => sp.name.Contains(prefix))
                .OrderBy(sp => Index(sp.name))   // 문자열 정렬은 _10을 _2 앞에 놓는다
                .ToArray();
        }

        // ── 배선 ────────────────────────────

        private static int WireLibrary()
        {
            var lib = AssetDatabase.LoadAssetAtPath<VfxLibrary>(LibraryPath);
            if (lib == null)
            {
                Debug.LogError($"[EffectImportBuilder] VfxLibrary가 없다 — {LibraryPath}");
                return 0;
            }

            int n = 0;
            if (Assign(ref lib.impact, "VFX_Impact")) n++;
            if (Assign(ref lib.shock, "VFX_Shock")) n++;
            if (Assign(ref lib.cast, "VFX_Cast")) n++;
            if (Assign(ref lib.wall, "VFX_Wall")) n++;

            EditorUtility.SetDirty(lib);
            return n;
        }

        private static int WireSkills()
        {
            int n = 0;
            foreach ((string skill, string hit, string cast) in SkillWiring)
            {
                string path = $"{SkillFolder}/{skill}.asset";
                var data = AssetDatabase.LoadAssetAtPath<SkillData>(path);
                if (data == null)
                {
                    Debug.LogWarning($"[EffectImportBuilder] 스킬 에셋이 없다 — {path}");
                    continue;
                }

                VfxClip hitClip = data.vfx.hitClip;
                VfxClip castClip = data.vfx.castClip;
                Assign(ref hitClip, hit);
                Assign(ref castClip, cast);

                data.vfx.hitClip = hitClip;
                data.vfx.castClip = castClip;

                EditorUtility.SetDirty(data);
                n++;
            }

            return n;
        }

        private static bool Assign(ref VfxClip slot, string clipName)
        {
            var clip = AssetDatabase.LoadAssetAtPath<VfxClip>($"{ClipFolder}/{clipName}.asset");
            if (clip == null || !clip.IsValid)
            {
                Debug.LogWarning($"[EffectImportBuilder] 클립을 찾지 못했다 — {clipName}");
                return false;
            }

            slot = clip;
            return true;
        }

        private static string SheetPath(string sheet) => $"{EffectRoot}/{sheet}.png";

    }

    // ══ VfxClipBuilder ══════════════════════════════════════════════
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

    }
}

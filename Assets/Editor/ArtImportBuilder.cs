using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.U2D.Sprites;
using UnityEngine;

namespace Prototype.EditorTools
{
    /// <summary>
    /// 프로젝트 <b>바깥</b>(<c>캡디/arts</c>)에 있는 아트 팩을 Assets로 들여오고,
    /// 슬라이스 · 클립 · 컨트롤러 · 라이브러리까지 한 번에 굽는다.
    ///
    /// 손으로 하면 시트 슬라이스 격자와 피벗을 매번 다시 맞춰야 하고,
    /// 캐릭터 피벗이 발밑이 아니면 <see cref="BeltScrollView"/>의 오프셋과 어긋나
    /// 발이 바닥에 안 닿는다. 격자 · 피벗 · PPU를 코드가 들고 있으면 다시 돌리면 복구된다.
    ///
    /// 여러 번 돌려도 같은 결과가 나온다.
    /// </summary>
    public static class ArtImportBuilder
    {
        // ── 원본 (프로젝트 밖) ──────────────────────────
        private const string PackDir = "Pixel UI pack 3";
        private const string CharDir = "120x80_PNGSheets";

        /// <summary>차징 게이지. 8프레임, 0이 가득 참.</summary>
        private const string GaugeSheet = "05.png";
        /// <summary>고리 3종 × 5프레임 회전.</summary>
        private const string RingSheet = "03.png";

        // ── 사본 (프로젝트 안) ──────────────────────────
        private const string ArtRoot = "Assets/Art";
        private const string CharFolder = ArtRoot + "/Character/Ally";
        private const string UiFolder = ArtRoot + "/Ui";
        private const string VfxFolder = ArtRoot + "/Vfx";

        private const string ClipFolder = "Assets/Data/Vfx";
        private const string AnimFolder = "Assets/Data/Animation";
        private const string ResFolder = "Assets/Data/Resources";
        private const string AllyControllerPath = AnimFolder + "/AllyAnimator.controller";
        private static string AllyPrefabPath => PrefabLocator.AllyPath;

        /// <summary>클립이 물리는 자식. <see cref="AnimationBuilder"/>와 같은 경로여야 한다.</summary>
        private const string SpritePath = "View/Sprite";

        // ── 캐릭터 시트 규격 ────────────────────────────
        private const int CharW = 120;
        private const int CharH = 80;

        /// <summary>
        /// 시트 안에서 캐릭터 키가 약 38px이다. 28로 나누면 1.36 유닛 —
        /// 기존 플레이스홀더(1 유닛)보다 조금 크고, 방 크기에 묻히지 않는다.
        /// </summary>
        private const int CharPpu = 28;

        private const int RingCell = 48;
        private const int GaugeW = 48;
        private const int GaugeH = 32;

        /// <summary>Animator 상태 이름 ↔ 원본 시트. 상태 이름은 <see cref="EntityAnimator"/>가 정한다.</summary>
        internal struct ClipSpec
        {
            public string state;      // Animator 상태 이름
            public string sheet;      // 원본 파일명(확장자 없이)
            public float fps;
            public bool loop;
            public int from;          // 사용할 프레임 구간. to < 0이면 끝까지.
            public int to;
            public bool reverse;

            public ClipSpec(string state, string sheet, float fps, bool loop,
                            int from = 0, int to = -1, bool reverse = false)
            {
                this.state = state; this.sheet = sheet; this.fps = fps; this.loop = loop;
                this.from = from; this.to = to; this.reverse = reverse;
            }
        }

        private static readonly ClipSpec[] AllyClips =
        {
            new ClipSpec("Idle",         "_Idle",             8f,  true),
            new ClipSpec("Move",         "_Run",             14f,  true),
            new ClipSpec("Jump",         "_Jump",            10f,  false),
            new ClipSpec("Attack",       "_Attack",          14f,  false),
            new ClipSpec("AerialAttack", "_Attack2",         14f,  false),
            new ClipSpec("Hit",          "_Hit",             10f,  false),
            new ClipSpec("AerialHit",    "_Fall",             8f,  true),

            // 다운은 쓰러진 뒤 자세를 유지해야 한다. 사망 시트의 뒷부분만 쓴다.
            new ClipSpec("Down",         "_Death",            8f,  false, 6, 9),
            // 기상은 그 구간을 거꾸로 돌린 것이다. 시트를 따로 안 써도 읽힌다.
            new ClipSpec("Getup",        "_Death",           12f,  false, 6, 9, true),
            new ClipSpec("Dead",         "_Death",           10f,  false),

            // 스킬은 종류가 23개인데 시트는 한 벌이다. 콤보 공격으로 통일한다.
            new ClipSpec("Skill",        "_AttackCombo2hit", 14f,  false),
        };

        [MenuItem("Prototype/아트 - 전부 임포트 + 배선")]
        public static void BuildAll()
        {
            string src = SourceRoot();
            if (!Directory.Exists(src))
            {
                Debug.LogError($"[ArtImportBuilder] 원본 폴더를 못 찾았다: {src}");
                return;
            }

            try
            {
                AssetDatabase.StartAssetEditing();
                ImportTextures(src);
            }
            finally
            {
                AssetDatabase.StopAssetEditing();
                AssetDatabase.Refresh();
            }

            SliceAll();

            VfxClip cast = BuildRingClip("VFX_CastRing", 2, 20f, 2, -20);
            VfxClip impact = BuildRingClip("VFX_ImpactRing", 0, 24f, 1, 50);
            VfxClip gauge = BuildGaugeClip();

            BuildLibrary(cast, impact, gauge);

            AnimatorController controller = BuildAllyController();
            RigAlly(controller);
            TintPartyMembers();

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log("[ArtImportBuilder] 완료 — 캐릭터 시트 · 고리 이펙트 · 차징 게이지 배선됨.");
        }

        /// <summary>Assets/../.. = 리포 상위. 거기 arts가 있다.</summary>
        private static string SourceRoot()
            => Path.GetFullPath(Path.Combine(Application.dataPath, "../../arts"));

        // ── 1. 복사 ────────────────────────────────────

        private static void ImportTextures(string src)
        {
            EnsureFolder(CharFolder);
            EnsureFolder(UiFolder);
            EnsureFolder(VfxFolder);

            var sheets = new HashSet<string>();
            foreach (ClipSpec c in AllyClips) sheets.Add(c.sheet);

            foreach (string sheet in sheets)
                Copy(Path.Combine(src, CharDir, sheet + ".png"), $"{CharFolder}/{sheet}.png");

            Copy(Path.Combine(src, PackDir, GaugeSheet), $"{UiFolder}/ChargeGauge.png");
            Copy(Path.Combine(src, PackDir, RingSheet), $"{VfxFolder}/Rings.png");
        }

        private static void Copy(string from, string to)
        {
            if (!File.Exists(from))
            {
                Debug.LogWarning($"[ArtImportBuilder] 원본 없음: {from}");
                return;
            }

            File.Copy(from, to, overwrite: true);
        }

        // ── 2. 슬라이스 ────────────────────────────────

        private static void SliceAll()
        {
            var sheets = new HashSet<string>();
            foreach (ClipSpec c in AllyClips) sheets.Add(c.sheet);

            // 캐릭터는 발밑 피벗. BeltScrollView가 바닥 좌표에 그대로 놓으므로
            // 피벗이 가운데면 발이 땅에 박히거나 떠 보인다.
            foreach (string sheet in sheets)
                Slice($"{CharFolder}/{sheet}.png", CharW, CharH, CharPpu, new Vector2(0.5f, 0f));

            Slice($"{UiFolder}/ChargeGauge.png", GaugeW, GaugeH, GaugeW, new Vector2(0.5f, 0.5f));
            Slice($"{VfxFolder}/Rings.png", RingCell, RingCell, RingCell, new Vector2(0.5f, 0.5f));
        }

        private static void Slice(string assetPath, int cellW, int cellH, int ppu, Vector2 pivot)
        {
            var importer = AssetImporter.GetAtPath(assetPath) as TextureImporter;
            if (importer == null)
            {
                Debug.LogWarning($"[ArtImportBuilder] 임포터 없음: {assetPath}");
                return;
            }

            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Multiple;
            importer.filterMode = FilterMode.Point;
            importer.spritePixelsPerUnit = ppu;
            importer.mipmapEnabled = false;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.SaveAndReimport();

            var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(assetPath);
            if (tex == null) return;

            int cols = Mathf.Max(1, tex.width / cellW);
            int rows = Mathf.Max(1, tex.height / cellH);
            string baseName = Path.GetFileNameWithoutExtension(assetPath);

            var factories = new SpriteDataProviderFactories();
            factories.Init();

            ISpriteEditorDataProvider provider = factories.GetSpriteEditorDataProviderFromObject(importer);
            provider.InitSpriteEditorDataProvider();

            var rects = new List<SpriteRect>(cols * rows);
            for (int r = 0; r < rows; r++)
            {
                for (int c = 0; c < cols; c++)
                {
                    // 이미지의 위쪽 줄이 0번이 되게 뒤집는다. 사람이 보는 순서와 맞춘다.
                    int y = tex.height - (r + 1) * cellH;

                    rects.Add(new SpriteRect
                    {
                        name = $"{baseName}_{r * cols + c}",
                        spriteID = GUID.Generate(),
                        rect = new Rect(c * cellW, y, cellW, cellH),
                        alignment = SpriteAlignment.Custom,
                        pivot = pivot,
                    });
                }
            }

            provider.SetSpriteRects(rects.ToArray());
            provider.Apply();

            AssetDatabase.ImportAsset(assetPath, ImportAssetOptions.ForceUpdate);
        }

        /// <summary>슬라이스 결과를 번호 순으로 돌려준다.</summary>
        internal static Sprite[] LoadSprites(string assetPath)
        {
            var list = new List<Sprite>();
            foreach (Object o in AssetDatabase.LoadAllAssetsAtPath(assetPath))
                if (o is Sprite s) list.Add(s);

            list.Sort((a, b) => Index(a.name).CompareTo(Index(b.name)));
            return list.ToArray();
        }

        private static int Index(string name)
        {
            int at = name.LastIndexOf('_');
            return at >= 0 && int.TryParse(name.Substring(at + 1), out int n) ? n : 0;
        }

        // ── 3. 이펙트 클립 ──────────────────────────────

        /// <summary>고리 시트의 한 줄(5프레임 회전)을 VfxClip으로 굽는다.</summary>
        private static VfxClip BuildRingClip(string name, int row, float fps, int loops, int sortingOffset)
        {
            Sprite[] all = LoadSprites($"{VfxFolder}/Rings.png");
            if (all.Length < (row + 1) * 5)
            {
                Debug.LogWarning($"[ArtImportBuilder] Rings.png 슬라이스가 모자라다 ({all.Length}장).");
                return null;
            }

            var frames = new Sprite[5];
            System.Array.Copy(all, row * 5, frames, 0, 5);

            VfxClip clip = LoadOrCreate<VfxClip>($"{ClipFolder}/{name}.asset");
            clip.frames = frames;
            clip.fps = fps;
            clip.loops = loops;
            clip.fitRadius = true;
            clip.scale = 1f;
            clip.sortingOffset = sortingOffset;
            clip.rotateToFacing = false;
            clip.flipToFacing = false;
            clip.tint = Color.white;
            // 흑백 시트라 스킬 색이 그대로 얹힌다.
            clip.useSkillColor = true;
            clip.fadeOut = 0.35f;

            EditorUtility.SetDirty(clip);
            return clip;
        }

        private static VfxClip BuildGaugeClip()
        {
            Sprite[] frames = LoadSprites($"{UiFolder}/ChargeGauge.png");
            if (frames.Length == 0) return null;

            VfxClip clip = LoadOrCreate<VfxClip>($"{ClipFolder}/UI_ChargeGauge.asset");
            clip.frames = frames;
            // 게이지는 시간이 아니라 비율로 읽는다. fps는 쓰이지 않는다.
            clip.fps = 8f;
            clip.loops = 1;
            clip.fitRadius = false;
            clip.useSkillColor = false;
            clip.tint = Color.white;

            EditorUtility.SetDirty(clip);
            return clip;
        }

        private static void BuildLibrary(VfxClip cast, VfxClip impact, VfxClip gauge)
        {
            EnsureFolder(ResFolder);

            VfxLibrary lib = LoadOrCreate<VfxLibrary>($"{ResFolder}/{VfxLibrary.ResourcePath}.asset");
            if (cast != null) lib.cast = cast;
            if (impact != null) lib.impact = impact;
            if (gauge != null) lib.chargeGauge = gauge;

            EditorUtility.SetDirty(lib);
        }

        // ── 4. 동료 애니메이터 ──────────────────────────

        private static AnimatorController BuildAllyController()
        {
            EnsureFolder(AnimFolder);

            var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(AllyControllerPath);
            if (controller == null)
                controller = AnimatorController.CreateAnimatorControllerAtPath(AllyControllerPath);

            AnimatorStateMachine sm = controller.layers[0].stateMachine;

            // 여러 번 돌려도 같은 결과가 나오게 기존 상태를 비운다.
            foreach (ChildAnimatorState st in sm.states)
                sm.RemoveState(st.state);

            foreach (ClipSpec spec in AllyClips)
            {
                AnimationClip clip = BuildSpriteClip(spec);
                if (clip == null) continue;

                AnimatorState state = sm.AddState(spec.state);
                state.motion = clip;
                // 클립이 안 건드리는 값을 매 프레임 되돌리지 않는다(AnimationBuilder와 같은 이유).
                state.writeDefaultValues = false;
            }

            AnimatorState idle = null;
            foreach (ChildAnimatorState st in sm.states)
                if (st.state.name == "Idle") idle = st.state;
            if (idle != null) sm.defaultState = idle;

            EditorUtility.SetDirty(controller);
            return controller;
        }

        internal static AnimationClip BuildSpriteClip(ClipSpec spec)
        {
            Sprite[] all = LoadSprites($"{CharFolder}/{spec.sheet}.png");
            if (all.Length == 0)
            {
                Debug.LogWarning($"[ArtImportBuilder] {spec.sheet} 슬라이스 결과가 없다.");
                return null;
            }

            int to = spec.to < 0 ? all.Length - 1 : Mathf.Min(spec.to, all.Length - 1);
            int from = Mathf.Clamp(spec.from, 0, to);

            var frames = new List<Sprite>();
            for (int i = from; i <= to; i++) frames.Add(all[i]);
            if (spec.reverse) frames.Reverse();

            float fps = Mathf.Max(1f, spec.fps);
            var keys = new ObjectReferenceKeyframe[frames.Count + 1];
            for (int i = 0; i < frames.Count; i++)
                keys[i] = new ObjectReferenceKeyframe { time = i / fps, value = frames[i] };

            // 마지막 프레임도 제 몫의 시간을 갖게 한 칸 더 찍는다. 없으면 순식간에 지나간다.
            keys[frames.Count] = new ObjectReferenceKeyframe
            {
                time = frames.Count / fps,
                value = frames[frames.Count - 1],
            };

            var clip = new AnimationClip { frameRate = fps };

            var binding = EditorCurveBinding.PPtrCurve(SpritePath, typeof(SpriteRenderer), "m_Sprite");
            AnimationUtility.SetObjectReferenceCurve(clip, binding, keys);

            // 플레이스홀더 클립이 남긴 스케일 · 알파를 되돌린다.
            // 두 컨트롤러를 오가면 찌그러진 채로 굳는 것을 막는다.
            float len = frames.Count / fps;
            SetConstant(clip, typeof(Transform), "m_LocalScale.x", 1f, len);
            SetConstant(clip, typeof(Transform), "m_LocalScale.y", 1f, len);
            SetConstant(clip, typeof(SpriteRenderer), "m_Color.a", 1f, len);

            if (spec.loop)
            {
                clip.wrapMode = WrapMode.Loop;
                AnimationClipSettings settings = AnimationUtility.GetAnimationClipSettings(clip);
                settings.loopTime = true;
                AnimationUtility.SetAnimationClipSettings(clip, settings);
            }

            return SaveClip(clip, "Ally_" + spec.state);
        }

        private static void SetConstant(AnimationClip clip, System.Type type, string property, float value, float len)
        {
            var curve = new AnimationCurve();
            curve.AddKey(0f, value);
            curve.AddKey(Mathf.Max(0.01f, len), value);

            AnimationUtility.SetEditorCurve(clip, EditorCurveBinding.FloatCurve(SpritePath, type, property), curve);
        }

        internal static AnimationClip SaveClip(AnimationClip clip, string name)
        {
            string path = $"{AnimFolder}/{name}.anim";
            var existing = AssetDatabase.LoadAssetAtPath<AnimationClip>(path);

            if (existing == null)
            {
                clip.name = name;
                AssetDatabase.CreateAsset(clip, path);
                return clip;
            }

            // 에셋을 새로 만들면 컨트롤러 참조가 끊긴다. 내용만 덮어쓴다.
            EditorUtility.CopySerialized(clip, existing);
            existing.name = name;
            EditorUtility.SetDirty(existing);
            return existing;
        }

        // ── 5. 프리팹 배선 ──────────────────────────────

        /// <summary>동료 프리팹만 새 컨트롤러로 바꾼다. 플레이어 · 적은 플레이스홀더 그대로 둔다.</summary>
        private static void RigAlly(AnimatorController controller)
        {
            GameObject root = PrefabUtility.LoadPrefabContents(AllyPrefabPath);
            if (root == null)
            {
                Debug.LogWarning($"[ArtImportBuilder] 프리팹 없음: {AllyPrefabPath}");
                return;
            }

            try
            {
                var animator = root.GetComponent<Animator>();
                if (animator == null) animator = root.AddComponent<Animator>();

                animator.runtimeAnimatorController = controller;
                animator.applyRootMotion = false;
                // 컬링되면 화면 밖에서 상태가 안 돌아 판정과 어긋난다.
                animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;

                Transform sprite = root.transform.Find(SpritePath);
                var sr = sprite != null ? sprite.GetComponent<SpriteRenderer>() : null;

                if (sr != null)
                {
                    Sprite[] idle = LoadSprites($"{CharFolder}/_Idle.png");
                    if (idle.Length > 0) sr.sprite = idle[0];
                    sr.flipX = false;
                    sprite.localScale = Vector3.one;

                    // 플레이스홀더는 통짜 파랑이었다. 그대로 두면 시트 색이 전부 파랗게 덮인다.
                    sr.color = Color.white;
                }

                var view = root.GetComponent<BeltScrollView>();
                if (view != null)
                {
                    var so = new SerializedObject(view);
                    // 피벗이 발밑이라 더 올릴 필요가 없다.
                    so.FindProperty("spriteOffsetY").floatValue = 0f;
                    so.FindProperty("flipToFacing").boolValue = true;
                    so.FindProperty("facingRenderer").objectReferenceValue = sr;
                    so.ApplyModifiedPropertiesWithoutUndo();
                }

                PrefabUtility.SaveAsPrefabAsset(root, AllyPrefabPath);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        /// <summary>
        /// 동료 4명이 같은 시트를 쓰므로 그대로 두면 누가 탱커인지 구분이 안 된다.
        /// 흰색 쪽으로 크게 섞은 <b>옅은</b> 색만 얹는다 — 진하게 넣으면 도트가 다시 묻힌다.
        ///
        /// 예전 이름은 <c>TintSceneAllies</c>였다. 파티가 씬에서 프리팹 안으로 들어가면서
        /// 칠하는 대상이 씬 인스턴스에서 <see cref="PartyMemberData"/> 에셋으로 바뀌었다.
        /// </summary>
        private static void TintPartyMembers()
        {
            bool dirty = false;

            // 씬이 아니라 표(PartyMemberData)에 쓴다. 파티는 BattleInput 프리팹 안으로 들어갔고,
            // 씬 인스턴스에 칠하면 없앤 오버라이드가 씬마다 되살아난다.
            // 실제로 칠하는 것은 런타임의 PartyAssembler.ApplyTint 다.
            foreach (string guid in AssetDatabase.FindAssets("t:" + nameof(PartyMemberData)))
            {
                var member = AssetDatabase.LoadAssetAtPath<PartyMemberData>(
                    AssetDatabase.GUIDToAssetPath(guid));
                if (member == null) continue;

                member.spriteTint = RoleTint(member.role);
                EditorUtility.SetDirty(member);
                dirty = true;
            }

            if (!dirty)
            {
                Debug.LogWarning("[ArtImportBuilder] PartyMemberData 가 하나도 없다. " +
                                 "'Prototype ▸ 파티 - 1단계: 씬에서 표 추출'을 먼저 돌릴 것.");
                return;
            }

            AssetDatabase.SaveAssets();
        }

        private static Color RoleTint(Role role)
        {
            Color c;
            switch (role)
            {
                case Role.Tanker: c = new Color(0.35f, 0.55f, 1f); break;
                case Role.Warrior: c = new Color(1f, 0.45f, 0.35f); break;
                case Role.Archer: c = new Color(0.4f, 1f, 0.5f); break;
                case Role.Wizard: c = new Color(0.75f, 0.45f, 1f); break;
                default: return Color.white;
            }

            return Color.Lerp(c, Color.white, 0.6f);
        }

        // ── 공용 ───────────────────────────────────────

        private static T LoadOrCreate<T>(string path) where T : ScriptableObject
        {
            var asset = AssetDatabase.LoadAssetAtPath<T>(path);
            if (asset != null) return asset;

            EnsureFolder(Path.GetDirectoryName(path).Replace('\\', '/'));

            asset = ScriptableObject.CreateInstance<T>();
            AssetDatabase.CreateAsset(asset, path);
            return asset;
        }

        /// <summary>
        /// <c>AssetDatabase.CreateFolder</c>는 쓰지 않는다.
        /// 폴더가 디스크에 이미 있는데 DB에 아직 안 올라온 순간(임포트 중, 재실행 직후)에 부르면
        /// 거절하는 대신 <c>Art 1</c> · <c>Art 2</c> 처럼 이름을 비켜서 새로 만든다.
        /// 디렉터리를 직접 만들고 임포트만 시키면 그 사고가 안 난다.
        /// </summary>
        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;

            string abs = Path.GetFullPath(Path.Combine(Application.dataPath, "..", path));
            Directory.CreateDirectory(abs);

            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
        }
    }
}

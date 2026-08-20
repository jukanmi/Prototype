using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.U2D.Sprites;
using UnityEngine;

namespace Prototype.EditorTools
{
    /// <summary>
    /// 보스 시트를 슬라이스하고 <c>BossAnimator.controller</c>를 굽는다.
    ///
    /// 원본이 이미 <c>Assets/Art/Character/BossWarrior</c>에 들어와 있어
    /// <see cref="ArtImportBuilder"/>와 달리 프로젝트 밖에서 복사하는 단계가 없다.
    ///
    /// 여러 번 돌려도 같은 결과가 나온다.
    /// </summary>
    public static class BossArtImportBuilder
    {
        private const string SheetFolder = "Assets/Art/Character/BossWarrior";
        private const string AnimFolder = "Assets/Data/Animation";
        private const string ControllerPath = AnimFolder + "/BossAnimator.controller";

        /// <summary>클립이 물리는 자식. <see cref="ArtImportBuilder"/>와 같은 경로여야 한다.</summary>
        private const string SpritePath = "View/Sprite";

        // ── 시트 규격 ──────────────────────────────────
        // 전부 150x150 균일 격자다. 세로 150은 모든 시트가 공유한다.
        private const int Cell = 150;

        /// <summary>
        /// 캐릭터 발이 놓인 줄. 셀 아래에서 이만큼 위다.
        ///
        /// 시트 안 캐릭터는 셀 바닥에 붙어 있지 않다 — 발끝 아래로 55px의 여백이 있다.
        /// 피벗을 (0.5, 0)으로 두면 <see cref="BeltScrollView"/>가 그 여백까지 바닥으로 쳐서
        /// 보스가 55px만큼 공중에 뜬 채로 걸어 다닌다.
        /// </summary>
        private const int FootFromBottom = 55;

        /// <summary>
        /// 서 있는 키가 시트 안에서 41px이다. 18로 나누면 약 2.3유닛 —
        /// 동료(1.36유닛)의 1.7배라 한 화면에서 바로 보스로 읽힌다.
        /// </summary>
        private const int Ppu = 18;

        private static Vector2 Pivot => new Vector2(0.5f, (float)FootFromBottom / Cell);

        /// <summary>Animator 상태 이름 ↔ 원본 시트. 상태 이름은 <see cref="EntityAnimator"/>가 정한다.</summary>
        private struct ClipSpec
        {
            public string state;
            public string sheet;
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

        /// <summary>
        /// 상태머신이 직접 재생하는 클립. 평타(Attack)는 프리팹의 평타 길이(0.55초)에 맞춘 fps다 —
        /// <see cref="BossPrefabBuilder"/>가 그 값을 쥐고 있다.
        /// </summary>
        private static readonly ClipSpec[] BaseClips =
        {
            new ClipSpec("Idle",         "Idle",       8f,  true),
            new ClipSpec("Move",         "Run",       12f,  true),
            new ClipSpec("Jump",         "Jump",       8f,  false),
            new ClipSpec("AerialHit",    "Fall",       8f,  true),
            new ClipSpec("Hit",          "Take Hit",  12f,  false),
            new ClipSpec("Attack",       "Attack1",    7f,  false),
            new ClipSpec("AerialAttack", "Attack1",   12f,  false),

            // 다운은 쓰러진 뒤 자세를 유지해야 한다. 사망 시트의 뒷부분만 쓴다.
            new ClipSpec("Down",         "Death",      8f,  false, 4, 5),
            // 기상은 그 구간을 거꾸로 돌린 것이다. 시트를 따로 안 써도 읽힌다.
            new ClipSpec("Getup",        "Death",     10f,  false, 4, 5, true),
            new ClipSpec("Dead",         "Death",      8f,  false),
        };

        [MenuItem("Prototype/보스 - 아트 임포트 + 애니메이터")]
        public static void BuildAll()
        {
            if (!AssetDatabase.IsValidFolder(SheetFolder))
            {
                Debug.LogError($"[BossArtImportBuilder] 시트 폴더가 없다: {SheetFolder}");
                return;
            }

            EnsureFolder(AnimFolder);

            SliceAll();
            AnimatorController controller = BuildController();

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log($"[BossArtImportBuilder] 완료 — {ControllerPath}", controller);
        }

        // ── 1. 슬라이스 ────────────────────────────────

        private static void SliceAll()
        {
            foreach (string sheet in AllSheets())
                Slice($"{SheetFolder}/{sheet}.png");
        }

        /// <summary>기본 클립과 패턴 표에 나오는 시트 전부. 중복은 한 번만 자른다.</summary>
        private static IEnumerable<string> AllSheets()
        {
            var seen = new HashSet<string>();

            foreach (ClipSpec c in BaseClips)
                if (seen.Add(c.sheet)) yield return c.sheet;

            foreach (BossPatternTable.Entry e in BossPatternTable.All)
                if (seen.Add(e.sheet)) yield return e.sheet;
        }

        private static void Slice(string assetPath)
        {
            var importer = AssetImporter.GetAtPath(assetPath) as TextureImporter;
            if (importer == null)
            {
                Debug.LogWarning($"[BossArtImportBuilder] 임포터 없음: {assetPath}");
                return;
            }

            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Multiple;
            importer.filterMode = FilterMode.Point;
            importer.spritePixelsPerUnit = Ppu;
            importer.mipmapEnabled = false;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.SaveAndReimport();

            var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(assetPath);
            if (tex == null) return;

            int cols = Mathf.Max(1, tex.width / Cell);
            string baseName = Path.GetFileNameWithoutExtension(assetPath);

            var factories = new SpriteDataProviderFactories();
            factories.Init();

            ISpriteEditorDataProvider provider = factories.GetSpriteEditorDataProviderFromObject(importer);
            provider.InitSpriteEditorDataProvider();

            var rects = new List<SpriteRect>(cols);
            for (int c = 0; c < cols; c++)
            {
                rects.Add(new SpriteRect
                {
                    name = $"{baseName}_{c}",
                    spriteID = GUID.Generate(),
                    rect = new Rect(c * Cell, 0f, Cell, Cell),
                    alignment = SpriteAlignment.Custom,
                    pivot = Pivot,
                });
            }

            provider.SetSpriteRects(rects.ToArray());
            provider.Apply();

            AssetDatabase.ImportAsset(assetPath, ImportAssetOptions.ForceUpdate);
        }

        /// <summary>슬라이스 결과를 번호 순으로 돌려준다.</summary>
        private static Sprite[] LoadSprites(string sheet)
        {
            string assetPath = $"{SheetFolder}/{sheet}.png";

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

        // ── 2. 애니메이터 ──────────────────────────────

        private static AnimatorController BuildController()
        {
            var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath);
            if (controller == null)
                controller = AnimatorController.CreateAnimatorControllerAtPath(ControllerPath);

            AnimatorStateMachine sm = controller.layers[0].stateMachine;

            // 여러 번 돌려도 같은 결과가 나오게 기존 상태를 비운다.
            foreach (ChildAnimatorState st in sm.states)
                sm.RemoveState(st.state);

            foreach (ClipSpec spec in BaseClips)
                AddState(sm, spec);

            AddPatternStates(sm);

            AnimatorState idle = Find(sm, "Idle");
            if (idle != null) sm.defaultState = idle;

            EditorUtility.SetDirty(controller);
            return controller;
        }

        /// <summary>
        /// 패턴마다 상태가 둘이다(차징기는 셋).
        ///
        /// <b>예고</b>는 첫 프레임을 붙들고 루프한다 — 칼을 치켜든 자세로 멈춰 있는 것이
        /// "지금 뭔가 온다"는 신호가 된다. 예고 길이는 패턴마다 다른데 클립 하나로 다 덮으려면
        /// 정지 자세여야 한다.
        ///
        /// <b>발동</b>은 나머지 프레임을 발동 길이에 딱 맞춰 재생한다(<see cref="BossPatternTable.Entry.ActiveFps"/>).
        /// </summary>
        private static void AddPatternStates(AnimatorStateMachine sm)
        {
            foreach (BossPatternTable.Entry e in BossPatternTable.All)
            {
                AddState(sm, new ClipSpec(e.windupState, e.sheet, 8f, true, 0, 0));
                AddState(sm, new ClipSpec(e.state, e.sheet, e.ActiveFps, false, 1, -1));

                // 차징기는 상태가 하나 더 있다. 예고와 같은 정지 자세 루프다 —
                // 모으는 시간은 패턴마다 다르고 밀리기까지 하므로 클립 하나로 덮으려면
                // 길이를 타지 않아야 한다. 예고와 나눠 두는 이유는 연출을 따로 손보기 위해서다.
                if (!string.IsNullOrEmpty(e.chargeState))
                    AddState(sm, new ClipSpec(e.chargeState, e.sheet, 8f, true, 0, 0));
            }
        }

        private static void AddState(AnimatorStateMachine sm, ClipSpec spec)
        {
            AnimationClip clip = BuildSpriteClip(spec);
            if (clip == null) return;

            AnimatorState state = sm.AddState(spec.state);
            state.motion = clip;
            // 클립이 안 건드리는 값을 매 프레임 되돌리지 않는다(ArtImportBuilder와 같은 이유).
            state.writeDefaultValues = false;
        }

        private static AnimatorState Find(AnimatorStateMachine sm, string name)
        {
            foreach (ChildAnimatorState st in sm.states)
                if (st.state.name == name) return st.state;
            return null;
        }

        private static AnimationClip BuildSpriteClip(ClipSpec spec)
        {
            Sprite[] all = LoadSprites(spec.sheet);
            if (all.Length == 0)
            {
                Debug.LogWarning($"[BossArtImportBuilder] {spec.sheet} 슬라이스 결과가 없다.");
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

            return SaveClip(clip, "Boss_" + spec.state);
        }

        private static void SetConstant(AnimationClip clip, System.Type type, string property, float value, float len)
        {
            var curve = new AnimationCurve();
            curve.AddKey(0f, value);
            curve.AddKey(Mathf.Max(0.01f, len), value);

            AnimationUtility.SetEditorCurve(clip, EditorCurveBinding.FloatCurve(SpritePath, type, property), curve);
        }

        private static AnimationClip SaveClip(AnimationClip clip, string name)
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

        /// <summary>
        /// <c>AssetDatabase.CreateFolder</c>는 쓰지 않는다 — 디스크에 이미 있는데 DB에 안 올라온
        /// 순간에 부르면 <c>Animation 1</c>처럼 이름을 비켜서 새로 만든다(ArtImportBuilder 참조).
        /// </summary>
        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;

            string abs = Path.GetFullPath(Path.Combine(Application.dataPath, "..", path));
            Directory.CreateDirectory(abs);

            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
        }

        /// <summary>보스가 서 있는 키(유닛). 프리팹 빌더가 몸통·히트박스 크기를 여기 맞춘다.</summary>
        internal const float StandingHeight = 41f / Ppu;

        /// <summary>보스 스프라이트가 쓰는 PPU. 테스트가 피벗 계산을 검증할 때 읽는다.</summary>
        internal static int PixelsPerUnit => Ppu;

        /// <summary>발밑 피벗의 Y 비율. 0이 아니라는 것이 이 시트의 핵심이다.</summary>
        internal static float FootPivotY => (float)FootFromBottom / Cell;
    }
}

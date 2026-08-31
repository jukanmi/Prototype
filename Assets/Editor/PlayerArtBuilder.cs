using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.U2D.Sprites;
using UnityEngine;

namespace Prototype.EditorTools
{
    /// <summary>
    /// 플레이어 전용 아트(<c>Assets/Art/Character/player</c>)를 굽는다.
    ///
    /// <b>Ally 파이프라인(<see cref="ArtImportBuilder"/>)과 다른 이유</b>: 그쪽은 이미 슬라이스된
    /// 한 장짜리 시트(<c>_Attack.png</c> 등)를 읽는다. 플레이어 원본은 <b>낱장 PNG 연속</b>
    /// (<c>attack1_1.png</c>, <c>attack1_2.png</c>, …)이고, Unity가 알파 여백을 프레임마다
    /// 다르게 잘라내(Auto Tight Trim) 피벗을 그대로 쓰면 재생할 때 발이 위아래로 들썩인다.
    /// 여기서 <b>피벗을 원본 캔버스 기준으로 되돌리고 배율을 통일한 뒤</b> 굽는다.
    /// </summary>
    public static class PlayerArtBuilder
    {
        private const string SourceFolder = "Assets/Art/Character/player";
        private const string AnimFolder = "Assets/Data/Animation";
        private const string SpritePath = "View/Sprite";

        // ── 배율 ──────────────────────────────────────────
        //
        // 하나의 ppu를 캐릭터 전체에 강제했더니(800 → 1200 → 1350) idle · jump · attack이
        // 저마다 다른 크기로 나왔다. 원인은 소스 자체다 — attack · idle · jump · fall이
        // 서로 다른 익스포트 세션(줌 배율)에서 나온 낱장 팩이라, 같은 ppu를 넣어도
        // "캔버스에서 캐릭터가 차지하는 비율"이 폴더마다 다르다.
        //
        // 그래서 <b>폴더별로 ppu를 따로</b> 잡는다. 기준은 <c>hit.png</c> — 씬에서 크기가
        // 맞다고 확인된 유일한 값이다. 각 폴더의 최대(가장 안 웅크린) 프레임 높이를 hit과 같은
        // 화면 높이로 나오게 역산한다: ppu = 그 폴더의 기준 높이(px) / TargetWorldHeight.
        //
        // 죽음(Down · Getup · Dead)은 예외다 — 쓰러진 자세라 "키가 얼마나 크나"를 비교할
        // 기준 프레임이 없다. hit.png와 <b>같은 폴더</b>(death)에서 나온 낱장이라 같은
        // 세션으로 보고 hit의 ppu를 그대로 물려받는다.

        /// <summary>hit.png의 원본(트림 전) 세로 픽셀 — Sprite Editor에서 실측.</summary>
        private const float HitReferenceHeightPx = 1770f;

        /// <summary>hit.png에 넣었을 때 씬에서 맞다고 확인된 ppu.</summary>
        private const float HitReferencePpu = 1350f;

        /// <summary>위 둘로 정해지는 "정답" 화면 높이(유닛). 모든 폴더가 이 높이에 맞춰진다.</summary>
        private const float TargetWorldHeight = HitReferenceHeightPx / HitReferencePpu;

        // 아래 픽셀 값은 각 폴더에서 가장 안 웅크린(=키가 가장 크게 나오는) 프레임의
        // 원본 세로 픽셀이다 — Sprite Editor에서 실측. 프레임 번호는 주석에 적어 둔다.
        private const float Attack1ReferenceHeightPx = 2541f; // attack1_1 (내려베기 시작 자세)
        private const float Attack2ReferenceHeightPx = 2315f; // attack2_6
        private const float IdleReferenceHeightPx = 2899f;    // idle_1 · idle_7
        private const float WalkReferenceHeightPx = 2850f;    // walk_4
        private const float JumpReferenceHeightPx = 2224f;    // jump_3
        private const float FallReferenceHeightPx = 2577f;    // fall_1

        private const float Attack1Ppu = Attack1ReferenceHeightPx / TargetWorldHeight;
        private const float Attack2Ppu = Attack2ReferenceHeightPx / TargetWorldHeight;
        private const float IdlePpu = IdleReferenceHeightPx / TargetWorldHeight;
        private const float WalkPpu = WalkReferenceHeightPx / TargetWorldHeight;
        private const float JumpPpu = JumpReferenceHeightPx / TargetWorldHeight;
        private const float FallPpu = FallReferenceHeightPx / TargetWorldHeight;

        /// <summary>
        /// 1 · 2타는 player 자체 프레임으로, 3타(마무리)는 전용 스윙이 없어 1타 프레임을
        /// 재활용한다 — 콤보 중간에만 그림이 바뀌면 더 어색하므로 세 타를 한 번에 간다.
        /// </summary>
        [MenuItem("Prototype/평타 - Player 3연타 클립 굽기 (3타는 1타 재활용)")]
        public static void BuildAttackCombo()
        {
            string[] attack1 = FramePaths($"{SourceFolder}/attack", "attack1", 1, 4);
            string[] attack2 = FramePaths($"{SourceFolder}/attack", "attack2", 1, 6);

            // 원본 캔버스가 프레임마다 동일해야 발 위치를 역산할 수 있다.
            // 다르면 조용히 진행하지 않고 여기서 멈춘다 — 잘못된 피벗으로 굽는 것보다 낫다.
            if (!FixSpriteImport(attack1, Attack1Ppu, out string error1))
            {
                Debug.LogError($"[PlayerArtBuilder] attack1 임포트 보정 실패 — {error1}");
                return;
            }
            if (!FixSpriteImport(attack2, Attack2Ppu, out string error2))
            {
                Debug.LogError($"[PlayerArtBuilder] attack2 임포트 보정 실패 — {error2}");
                return;
            }

            AnimationClip c1 = BuildFrameClip(attack1, "Player_Attack", fps: 14f, loop: false);
            AnimationClip c2 = BuildFrameClip(attack2, "Player_Attack2", fps: 14f, loop: false);
            AnimationClip c3 = BuildFrameClip(attack1, "Player_Attack3", fps: 14f, loop: false);
            if (c1 == null || c2 == null || c3 == null) return;

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log($"[PlayerArtBuilder] Player_Attack({attack1.Length}장) · " +
                      $"Player_Attack2({attack2.Length}장) · Player_Attack3(attack1 재활용) 을 구웠다. " +
                      "Player.prefab 의 3단계 슬롯에 손으로 물릴 것 — 프리팹은 이 도구가 건드리지 않는다.", c1);
        }

        private const string ControllerPath = AnimFolder + "/PlayerAnimator.controller";
        private const string HeroDataPath = "Assets/Data/Resources/Party/Hero_Player.asset";

        /// <summary>
        /// 스킬은 <see cref="EntityAnimator.SwapSkillClip"/>이 <c>Skill_Placeholder</c>라는
        /// <b>이름으로</b> 오버라이드 자리를 찾는다 — 컨트롤러의 Skill 상태 모션이 이 클립 자체가
        /// 아니면 스킬 시전 때 클립 교체가 안 먹는다. player 전용 클립을 새로 굽지 않고
        /// 공용 플레이스홀더를 그대로 물린다. 23개 스킬은 이 파이프라인 범위 밖이다.
        /// </summary>
        private const string SkillPlaceholderPath = AnimFolder + "/Skill_Placeholder.anim";

        /// <summary>
        /// 대기 · 이동 · 점프 · 피격 · 다운/기상/사망을 굽고, Player 전용 컨트롤러를 만들어
        /// <c>Hero_Player.asset.animatorController</c>에 꽂는다.
        ///
        /// <b>Player.prefab 자체는 안 건드린다.</b> <see cref="PartyAssembler.ApplyLook"/>이
        /// Awake 전에 이 표의 <c>animatorController</c> 필드를 보고 갈아 끼우는 경로가 이미
        /// 있다 — Ally · EnemyData와 같은 "0 · null은 안 건드린다" 규약. 지금 그 필드가
        /// 비어 있어(<c>fileID: 0</c>) 공용 컨트롤러로 도는 중이었다.
        ///
        /// <b>Attack · AerialAttack · Skill은 새로 안 굽는다.</b> Attack은
        /// <see cref="AllyBasicAttack"/>의 콤보 단계가 런타임에 클립을 갈아 끼우므로 기본값만
        /// 있으면 되고(1타 클립), AerialAttack은 Ally 관례를 따라 2타 클립을 재활용한다.
        /// Skill은 위 참고.
        /// </summary>
        [MenuItem("Prototype/캐릭터 - Player 애니메이터 굽기 (대기·이동·점프·피격·다운)")]
        public static void BuildBaseStatesAndController()
        {
            string art = $"{SourceFolder}";

            // Down · Getup · Dead는 hit.png와 같은 death 폴더에서 나온 낱장이다 —
            // 쓰러진 자세라 "키" 비교 기준이 없으므로 hit의 ppu를 그대로 물려받는다.
            var groups = new (string name, string[] paths, float fps, bool loop, float ppu)[]
            {
                ("Player_Idle",  FramePaths($"{art}/idle walk", "idle", 1, 7), 8f, true, IdlePpu),
                ("Player_Move",  FramePaths($"{art}/idle walk", "walk", 1, 9), 14f, true, WalkPpu),
                ("Player_Jump",  FramePaths($"{art}/jump", "jump", 1, 3), 10f, false, JumpPpu),
                ("Player_Fall",  FramePaths($"{art}/fall", "fall", 1, 3), 8f, true, FallPpu),
                ("Player_Hit",   new[] { $"{art}/death/hit.png" }, 10f, false, HitReferencePpu),
                ("Player_Dead",  FramePaths($"{art}/death", "death", 1, 9), 10f, false, HitReferencePpu),
                ("Player_Down",  FramePaths($"{art}/death", "death", 6, 9), 8f, false, HitReferencePpu),
                ("Player_Getup", FramePaths($"{art}/death", "death", 6, 9).Reverse().ToArray(), 12f, false, HitReferencePpu),
            };

            var clips = new Dictionary<string, AnimationClip>();
            foreach (var g in groups)
            {
                if (!FixSpriteImport(g.paths, g.ppu, out string error))
                {
                    Debug.LogError($"[PlayerArtBuilder] {g.name} 임포트 보정 실패 — {error}");
                    return;
                }

                AnimationClip clip = BuildFrameClip(g.paths, g.name, g.fps, g.loop);
                if (clip == null) return;

                clips[g.name] = clip;
            }

            var attack1 = AssetDatabase.LoadAssetAtPath<AnimationClip>($"{AnimFolder}/Player_Attack.anim");
            var attack2 = AssetDatabase.LoadAssetAtPath<AnimationClip>($"{AnimFolder}/Player_Attack2.anim");
            var skillPlaceholder = AssetDatabase.LoadAssetAtPath<AnimationClip>(SkillPlaceholderPath);

            if (attack1 == null || attack2 == null)
            {
                Debug.LogError("[PlayerArtBuilder] Player_Attack · Player_Attack2 가 없다 — " +
                                "먼저 '평타 - Player 3연타 클립 굽기'를 돌릴 것.");
                return;
            }

            var stateMotions = new (string state, AnimationClip clip)[]
            {
                ("Idle", clips["Player_Idle"]),
                ("Move", clips["Player_Move"]),
                ("Jump", clips["Player_Jump"]),
                ("Attack", attack1),
                ("AerialAttack", attack2),
                ("Hit", clips["Player_Hit"]),
                ("AerialHit", clips["Player_Fall"]),
                ("Down", clips["Player_Down"]),
                ("Getup", clips["Player_Getup"]),
                ("Dead", clips["Player_Dead"]),
                ("Skill", skillPlaceholder),
            };

            AnimatorController controller = BuildController(stateMotions);
            WireHeroData(controller);
            SyncPrefabPreview(controller, clips["Player_Idle"]);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log("[PlayerArtBuilder] PlayerAnimator.controller 를 굽고 Hero_Player.asset 에 물렸다. " +
                      "Attack · AerialAttack · Skill 은 재사용(새로 안 구움).", controller);
        }

        /// <summary>
        /// Player.prefab 자체의 기본 Animator · 미리보기 스프라이트를 맞춘다.
        ///
        /// <b>런타임 동작에는 영향 없다</b> — <see cref="PartyAssembler.ApplyLook"/>이 Awake 전에
        /// <c>Hero_Player.asset.animatorController</c>로 항상 덮어쓰므로. 그래도 프리팹을 프로젝트
        /// 창에서 열거나 씬에 직접 끌어다 놓으면 여기 값이 보인다 — 옛 Ally 클립(ppu 28짜리
        /// _Idle_0)이 그대로 남아 있으면 확대되어 보인다. <see cref="ArtImportBuilder.RigAlly"/>가
        /// Ally.prefab에 하는 것과 같은 일이다.
        /// </summary>
        private static void SyncPrefabPreview(AnimatorController controller, AnimationClip idleClip)
        {
            const string path = "Assets/Prefabs/Player/Player.prefab";

            GameObject root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                var animator = root.GetComponent<Animator>();
                if (animator != null) animator.runtimeAnimatorController = controller;

                Transform sprite = root.transform.Find(SpritePath);
                var sr = sprite != null ? sprite.GetComponent<SpriteRenderer>() : null;
                if (sr != null)
                {
                    var binding = EditorCurveBinding.PPtrCurve(SpritePath, typeof(SpriteRenderer), "m_Sprite");
                    ObjectReferenceKeyframe[] keys = AnimationUtility.GetObjectReferenceCurve(idleClip, binding);
                    if (keys.Length > 0) sr.sprite = keys[0].value as Sprite;

                    sr.color = Color.white;
                    sprite.localScale = Vector3.one;
                }

                PrefabUtility.SaveAsPrefabAsset(root, path);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        private static AnimatorController BuildController((string state, AnimationClip clip)[] motions)
        {
            var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath);
            if (controller == null)
                controller = AnimatorController.CreateAnimatorControllerAtPath(ControllerPath);

            AnimatorStateMachine sm = controller.layers[0].stateMachine;

            // 여러 번 돌려도 같은 결과가 나오게 기존 상태를 비운다.
            foreach (ChildAnimatorState st in sm.states)
                sm.RemoveState(st.state);

            AnimatorState idle = null;
            foreach (var m in motions)
            {
                if (m.clip == null)
                {
                    Debug.LogWarning($"[PlayerArtBuilder] {m.state} 클립이 없다 — 건너뜀.");
                    continue;
                }

                AnimatorState state = sm.AddState(m.state);
                state.motion = m.clip;
                state.writeDefaultValues = false;

                if (m.state == "Idle") idle = state;
            }

            if (idle != null) sm.defaultState = idle;

            EditorUtility.SetDirty(controller);
            return controller;
        }

        /// <summary>Hero_Player.asset의 animatorController 필드에 물린다. private 필드라 SerializedObject로 간다.</summary>
        private static void WireHeroData(AnimatorController controller)
        {
            var data = AssetDatabase.LoadAssetAtPath<ScriptableObject>(HeroDataPath);
            if (data == null)
            {
                Debug.LogError($"[PlayerArtBuilder] {HeroDataPath} 를 못 찾았다.");
                return;
            }

            var so = new SerializedObject(data);
            so.FindProperty("animatorController").objectReferenceValue = controller;
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(data);
        }

        private static string[] FramePaths(string folder, string prefix, int from, int to)
        {
            var list = new List<string>();
            for (int i = from; i <= to; i++)
                list.Add($"{folder}/{prefix}_{i}.png");
            return list.ToArray();
        }

        /// <summary>
        /// 낱장 프레임의 피벗을 "원본 캔버스 바닥 · 가로 중앙"에 맞추고, 배율을
        /// <paramref name="pixelsPerUnit"/>로 맞춘다(폴더마다 다른 값 — 위 배율 표 참고).
        ///
        /// <b>피벗</b> — Unity가 알파 여백을 잘라낸 사각형(<c>rect</c>)은 프레임마다 크기 ·
        /// 위치가 다르다. 세로는 무기를 휘두르며 팔다리가 늘어나고 접혀서, 가로는 idle
        /// 숨쉬기처럼 가슴이 부풀며 실루엣 폭 자체가 늘었다 줄었다 해서 그렇다. 기본 피벗
        /// (잘린 사각형 자체의 중앙)을 그대로 쓰면 그때그때 다른 지점을 가리켜 재생 중
        /// 캐릭터가 상하로 들썩이거나(세로) 서 있는 채로 좌우로 미끄러진다(가로).
        ///
        /// 대신 <b>원본(트림 전) 캔버스의 바닥 · 가로 중앙</b>을 기준으로 삼는다 — 캔버스가
        /// 프레임마다 같은 크기로 나온 연속 익스포트라면 그 점은 항상 같은 자리(발이 딛는
        /// 자리 · 몸통 중심선)를 가리킨다. 그 점을 각 프레임의 잘린 사각형 기준 정규화
        /// 좌표로 바꿔 피벗에 넣으면(0~1 밖으로 나갈 수 있다 — 잘려나간 여백 쪽을 가리키므로
        /// 정상이다) 발이 항상 같은 화면 위치에 남는다.
        ///
        /// <b>배율</b> — 기본 임포트 값(100)을 그대로 두면 원본이 커서(2000px대) 캐릭터가
        /// Ally보다 몇 배 거대하게 나온다. 씬의 다른 몸과 눈대중이라도 맞도록 고정값을 강제한다.
        ///
        /// <b><c>maxTextureSize</c>도 같이 올린다.</b> 원본이 기본 클램프(2048)보다 커서
        /// (이 팩은 3568x2917) 임포트되면서 축소된다. <c>spritePixelsPerUnit</c>은 <b>원본
        /// 해상도</b> 기준으로 저장되는데 실제 화면에 나가는 건 <b>축소된</b> 텍스처라, 800을
        /// 넣어도 축소 비율(약 0.57)만큼 깎여 459 정도로 작아진다 — 원본을 다 담을 만큼
        /// <c>maxTextureSize</c>를 올려야 설정한 배율이 그대로 먹는다.
        ///
        /// <b>이 API를 쓰는 이유</b> — <c>TextureImporter.spritesheet</c>(<c>SpriteMetaData[]</c>)는
        /// 이 Unity 버전에서 조용히 무시된다. 대입하고 <c>SaveAndReimport</c>해도 메타 파일에
        /// 안 남는다 — 스프라이트 에디터가 실제로 쓰는 <see cref="ISpriteEditorDataProvider"/>로
        /// 가야 반영된다.
        /// </summary>
        private static bool FixSpriteImport(string[] paths, float pixelsPerUnit, out string error)
        {
            error = null;
            int? canvasW = null, canvasH = null;

            foreach (string path in paths)
            {
                var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
                if (tex == null) { error = $"{path} 를 못 읽었다"; return false; }

                if (canvasW == null) { canvasW = tex.width; canvasH = tex.height; }
                else if (tex.width != canvasW || tex.height != canvasH)
                {
                    error = $"{path} 캔버스가 {tex.width}x{tex.height} — 첫 프레임({canvasW}x{canvasH})과 다르다";
                    return false;
                }
            }

            var factory = new SpriteDataProviderFactories();
            factory.Init();

            foreach (string path in paths)
            {
                var importer = AssetImporter.GetAtPath(path) as TextureImporter;
                if (importer == null) { error = $"{path}: TextureImporter가 아니다"; return false; }

                // 원본을 그대로 담을 만큼 올린다. 8192가 Unity가 허용하는 상한이다.
                importer.GetSourceTextureWidthAndHeight(out int srcW, out int srcH);
                int need = Mathf.Max(srcW, srcH);
                int pot = 32;
                while (pot < need && pot < 8192) pot *= 2;
                importer.maxTextureSize = pot;

                ISpriteEditorDataProvider provider = factory.GetSpriteEditorDataProviderFromObject(importer);
                provider.InitSpriteEditorDataProvider();

                SpriteRect[] rects = provider.GetSpriteRects();
                if (rects.Length == 0)
                {
                    error = $"{path}: 스프라이트를 하나도 못 찾았다(완전히 투명한 프레임?)";
                    return false;
                }

                if (rects.Length > 1)
                {
                    // 알파 노이즈(먼지 픽셀 등)가 별도 섬으로 잡힌 경우다. 가장 큰 섬만 남긴다 —
                    // 원본 프레임을 손보기 전까지 쓸 수 있는 임시방편이다.
                    int biggest = 0;
                    for (int i = 1; i < rects.Length; i++)
                        if (rects[i].rect.width * rects[i].rect.height > rects[biggest].rect.width * rects[biggest].rect.height)
                            biggest = i;

                    Debug.LogWarning($"[PlayerArtBuilder] {path}: 알파 섬이 {rects.Length}개 잡혔다 — " +
                                      $"가장 큰 것만 쓴다. 원본 프레임을 확인해 볼 것.");
                    rects = new[] { rects[biggest] };
                }

                SpriteRect r = rects[0];

                // 캔버스 바닥(y=0)이 잘린 사각형 기준 어디인지. 사각형 바닥 아래로 rect.y만큼
                // 여백이 잘려나갔으므로, 정규화하면 음수가 나오는 게 정상이다 — 비율이라
                // 이 API가 원본 해상도 기준으로 돌려주는 rect를 써도 그대로 맞는다.
                float pivotY = -r.rect.y / r.rect.height;

                // 가로도 같은 문제다. idle 숨쉬기처럼 가슴이 부풀면 트림 폭이 프레임마다
                // 늘었다 줄었다 하는데, 기본 피벗(0.5 = "그 프레임 자체 폭의 중앙")은
                // 매번 다른 지점을 가리켜 서 있는 채로 좌우로 미끄러진다. 세로와 같은 원리로
                // 캔버스 가로 중앙(srcW/2)을 기준 삼는다 — 몸이 캔버스 한가운데 그려졌다고 본다.
                float pivotX = (srcW / 2f - r.rect.x) / r.rect.width;

                r.alignment = SpriteAlignment.Custom;
                r.pivot = new Vector2(pivotX, pivotY);
                rects[0] = r;

                provider.SetSpriteRects(rects);
                provider.Apply();

                importer.spritePixelsPerUnit = pixelsPerUnit;
                importer.SaveAndReimport();
            }

            return true;
        }

        /// <summary>
        /// 낱장 스프라이트 배열을 클립으로 굽는다. <see cref="ArtImportBuilder.BuildSpriteClip"/>과
        /// 같은 커브 기법이다 — 한 장짜리 시트 대신 개별 파일에서 읽는다는 점만 다르다.
        /// </summary>
        private static AnimationClip BuildFrameClip(string[] paths, string clipName, float fps, bool loop)
        {
            var frames = new Sprite[paths.Length];
            for (int i = 0; i < paths.Length; i++)
            {
                frames[i] = AssetDatabase.LoadAssetAtPath<Sprite>(paths[i]);
                if (frames[i] != null) continue;

                foreach (Object o in AssetDatabase.LoadAllAssetsAtPath(paths[i]))
                    if (o is Sprite s) { frames[i] = s; break; }
            }

            for (int i = 0; i < frames.Length; i++)
            {
                if (frames[i] != null) continue;
                Debug.LogError($"[PlayerArtBuilder] {paths[i]} 에서 스프라이트를 못 찾았다.");
                return null;
            }

            fps = Mathf.Max(1f, fps);
            var keys = new ObjectReferenceKeyframe[frames.Length + 1];
            for (int i = 0; i < frames.Length; i++)
                keys[i] = new ObjectReferenceKeyframe { time = i / fps, value = frames[i] };

            // 마지막 프레임도 제 몫의 시간을 갖게 한 칸 더 찍는다. 없으면 순식간에 지나간다.
            keys[frames.Length] = new ObjectReferenceKeyframe
            {
                time = frames.Length / fps,
                value = frames[frames.Length - 1],
            };

            var clip = new AnimationClip { frameRate = fps };

            var binding = EditorCurveBinding.PPtrCurve(SpritePath, typeof(SpriteRenderer), "m_Sprite");
            AnimationUtility.SetObjectReferenceCurve(clip, binding, keys);

            float len = frames.Length / fps;
            SetConstant(clip, typeof(Transform), "m_LocalScale.x", 1f, len);
            SetConstant(clip, typeof(Transform), "m_LocalScale.y", 1f, len);
            SetConstant(clip, typeof(SpriteRenderer), "m_Color.a", 1f, len);

            if (loop)
            {
                clip.wrapMode = WrapMode.Loop;
                AnimationClipSettings settings = AnimationUtility.GetAnimationClipSettings(clip);
                settings.loopTime = true;
                AnimationUtility.SetAnimationClipSettings(clip, settings);
            }

            return SaveClip(clip, clipName);
        }

        private static void SetConstant(AnimationClip clip, System.Type type, string property, float value, float len)
        {
            var curve = new AnimationCurve();
            curve.AddKey(0f, value);
            curve.AddKey(Mathf.Max(0.01f, len), value);

            AnimationUtility.SetEditorCurve(clip, EditorCurveBinding.FloatCurve(SpritePath, type, property), curve);
        }

        /// <summary>같은 이름으로 다시 구우면 GUID를 지킨다 — 프리팹의 클립 참조가 안 끊긴다.</summary>
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

            EditorUtility.CopySerialized(clip, existing);
            existing.name = name;
            EditorUtility.SetDirty(existing);
            return existing;
        }
    }
}

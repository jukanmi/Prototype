using System.Collections.Generic;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace Prototype.EditorTools
{
    /// <summary>
    /// 아트가 없는 동안 쓰는 플레이스홀더 애니메이션.
    /// SkillData의 castTime · hitInterval · recoveryTime을 그대로 읽어
    /// 선딜 → 각 타격 → 후딜이 <b>화면에서 구분돼 보이게</b> 한다.
    /// 히트박스가 켜지는 순간과 눈에 보이는 타이밍이 맞는지 검증하는 게 목적이다.
    ///
    /// 색은 건드리지 않는다 — 캐릭터마다 색이 정체성이라(플레이어 초록, 적 빨강)
    /// 스케일과 알파만 움직인다. BeltScrollView가 position과 rotation을 매 프레임
    /// 덮어쓰므로 그 둘도 쓰면 안 된다.
    /// </summary>
    public static class AnimationBuilder
    {
        private const string ClipFolder = "Assets/Data/Animation";
        private const string ControllerPath = ClipFolder + "/EntityAnimator.controller";

        /// <summary>
        /// 클립이 물리는 자식. SceneLayoutBuilder가 만든 깊이 배율 노드 아래에 있다.
        /// 배율은 View가, 스쿼시 · 스트레치는 Sprite가 나눠 갖는다 —
        /// 한 트랜스폼에 둘을 얹으면 Animator와 BeltScrollView가 매 프레임 서로 덮어쓴다.
        /// </summary>
        private const string SpritePath = "View/Sprite";

        [MenuItem("Prototype/애니메이션 - 플레이스홀더 굽기 + 배선")]
        public static void BuildAll()
        {
            EnsureFolder(ClipFolder);

            AnimatorController controller = BuildController();
            int skillClips = BuildSkillClips();
            RigEntities(controller);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            UnityEditor.SceneManagement.EditorSceneManager.MarkAllScenesDirty();
            UnityEditor.SceneManagement.EditorSceneManager.SaveOpenScenes();

            Debug.Log($"[AnimationBuilder] 컨트롤러 1 + 스킬 클립 {skillClips}장 → {ClipFolder}", controller);
        }

        // ── 기본 상태 클립 ────────────────────────────────

        /// <summary>Entity 상태 클래스 이름과 1:1로 맞춘다. EntityAnimator가 이름으로 찾는다.</summary>
        private static AnimatorController BuildController()
        {
            var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath);
            if (controller == null)
                controller = AnimatorController.CreateAnimatorControllerAtPath(ControllerPath);

            AnimatorStateMachine sm = controller.layers[0].stateMachine;

            // 여러 번 돌려도 같은 결과가 나오게 기존 상태를 비운다.
            foreach (ChildAnimatorState st in sm.states)
                sm.RemoveState(st.state);

            AddState(sm, "Idle", Bob(1.2f, 0.03f));
            AddState(sm, "Move", Bob(0.35f, 0.06f));
            AddState(sm, "Jump", Stretch(0.4f, 1.12f, 0.9f));
            AddState(sm, "Attack", Swing(0.45f, 0.12f, 0.24f));
            AddState(sm, "AerialAttack", Swing(0.4f, 0.08f, 0.2f));
            AddState(sm, "Hit", Shake(0.25f));
            AddState(sm, "AerialHit", Shake(0.35f));
            AddState(sm, "Down", Flat(0.6f, 1.25f, 0.55f));
            AddState(sm, "Getup", Stretch(0.4f, 0.7f, 1f));
            AddState(sm, "Dead", Flat(0.5f, 1.3f, 0.4f));

            // 스킬 자리. 런타임에 AnimatorOverrideController가 여기에 실제 클립을 꽂는다.
            // SaveClip은 기존 에셋이 있으면 그쪽을 돌려주므로 반환값을 반드시 받아야 한다.
            AnimationClip slot = SaveClip(Swing(0.6f, 0.15f, 0.35f), EntityAnimator.SkillSlotClip);
            AddState(sm, "Skill", slot);

            sm.defaultState = FindState(sm, "Idle");

            EditorUtility.SetDirty(controller);
            return controller;
        }

        private static void AddState(AnimatorStateMachine sm, string name, AnimationClip clip)
        {
            if (clip.name != EntityAnimator.SkillSlotClip)
                clip = SaveClip(clip, name);

            AnimatorState state = sm.AddState(name);
            state.motion = clip;

            // writeDefaults를 켜면 클립이 안 건드리는 값을 Animator가 매 프레임 기본값으로 되돌린다.
            // Dead 클립은 알파를 안 건드리는데 DeadState가 코드로 페이드아웃하므로,
            // 켜 두면 시체가 영영 안 사라진다. 모든 클립이 t=0에 필요한 값을 명시하므로 꺼도 안전하다.
            state.writeDefaultValues = false;
        }

        private static AnimatorState FindState(AnimatorStateMachine sm, string name)
        {
            foreach (ChildAnimatorState st in sm.states)
                if (st.state.name == name) return st.state;
            return null;
        }

        // ── 스킬 클립 ─────────────────────────────────────

        private static int BuildSkillClips()
        {
            string[] guids = AssetDatabase.FindAssets("t:SkillData");
            int made = 0;

            foreach (string guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                var data = AssetDatabase.LoadAssetAtPath<SkillData>(path);
                if (data == null) continue;

                AnimationClip clip = BuildSkillClip(data);
                clip = SaveClip(clip, "SK_" + System.IO.Path.GetFileNameWithoutExtension(path).Replace("SK_", ""));

                // 손으로 진짜 클립을 물려 뒀으면 덮어쓰지 않는다.
                if (data.animation == null || AssetDatabase.GetAssetPath(data.animation).StartsWith(ClipFolder))
                {
                    data.animation = clip;
                    EditorUtility.SetDirty(data);
                }

                made++;
            }

            return made;
        }

        /// <summary>
        /// 선딜에는 움츠리고, 각 타격 시점에 튀고, 후딜에 가라앉는다.
        /// 클립 길이 = SkillData.TotalDuration 이라 재생 속도를 따로 맞출 필요가 없다.
        /// </summary>
        private static AnimationClip BuildSkillClip(SkillData data)
        {
            float total = Mathf.Max(0.05f, data.TotalDuration);
            int hits = Mathf.Max(1, data.hitDataList != null ? data.hitDataList.Count : 1);

            var scale = new AnimationCurve();
            var alpha = new AnimationCurve();

            // 선딜 — 움츠리며 흐려진다.
            scale.AddKey(0f, 1f);
            alpha.AddKey(0f, 0.55f);

            float windupEnd = Mathf.Max(0f, data.castTime - 0.02f);
            if (windupEnd > 0f)
            {
                scale.AddKey(windupEnd, 0.82f);
                alpha.AddKey(windupEnd, 0.7f);
            }

            // 타격 — 매 히트마다 튄다.
            for (int i = 0; i < hits; i++)
            {
                float t = Mathf.Clamp(data.castTime + data.hitInterval * i, 0f, total);
                scale.AddKey(t, 1.35f);
                alpha.AddKey(t, 1f);

                float settle = Mathf.Min(t + data.hitInterval * 0.45f, total);
                if (settle > t) scale.AddKey(settle, 1.05f);
            }

            // 후딜 — 원래 크기로.
            scale.AddKey(total, 1f);
            alpha.AddKey(total, 1f);

            var clip = new AnimationClip { frameRate = 60f };
            SetScale(clip, scale);
            SetAlpha(clip, alpha);
            return clip;
        }

        // ── 곡선 헬퍼 ─────────────────────────────────────

        private static AnimationClip Bob(float period, float amount)
        {
            var s = new AnimationCurve();
            s.AddKey(0f, 1f);
            s.AddKey(period * 0.5f, 1f + amount);
            s.AddKey(period, 1f);

            var clip = new AnimationClip { frameRate = 60f };
            clip.wrapMode = WrapMode.Loop;
            SetScale(clip, s);

            var settings = AnimationUtility.GetAnimationClipSettings(clip);
            settings.loopTime = true;
            AnimationUtility.SetAnimationClipSettings(clip, settings);
            return clip;
        }

        private static AnimationClip Stretch(float len, float peak, float end)
        {
            var s = new AnimationCurve();
            s.AddKey(0f, 1f);
            s.AddKey(len * 0.3f, peak);
            s.AddKey(len, end);

            var clip = new AnimationClip { frameRate = 60f };
            SetScale(clip, s);
            return clip;
        }

        private static AnimationClip Swing(float len, float windup, float active)
        {
            var s = new AnimationCurve();
            s.AddKey(0f, 1f);
            s.AddKey(windup, 0.85f);
            s.AddKey(active, 1.3f);
            s.AddKey(len, 1f);

            var clip = new AnimationClip { frameRate = 60f };
            SetScale(clip, s);
            return clip;
        }

        private static AnimationClip Shake(float len)
        {
            var a = new AnimationCurve();
            a.AddKey(0f, 1f);
            a.AddKey(len * 0.15f, 0.35f);
            a.AddKey(len * 0.35f, 1f);
            a.AddKey(len * 0.55f, 0.45f);
            a.AddKey(len, 1f);

            var s = new AnimationCurve();
            s.AddKey(0f, 1.15f);
            s.AddKey(len, 1f);

            var clip = new AnimationClip { frameRate = 60f };
            SetScale(clip, s);
            SetAlpha(clip, a);
            return clip;
        }

        private static AnimationClip Flat(float len, float wide, float low)
        {
            var x = new AnimationCurve();
            x.AddKey(0f, 1f);
            x.AddKey(len * 0.3f, wide);

            var y = new AnimationCurve();
            y.AddKey(0f, 1f);
            y.AddKey(len * 0.3f, low);

            var clip = new AnimationClip { frameRate = 60f };
            AnimationUtility.SetEditorCurve(clip, Bind("m_LocalScale.x"), x);
            AnimationUtility.SetEditorCurve(clip, Bind("m_LocalScale.y"), y);
            return clip;
        }

        private static void SetScale(AnimationClip clip, AnimationCurve curve)
        {
            AnimationUtility.SetEditorCurve(clip, Bind("m_LocalScale.x"), curve);
            AnimationUtility.SetEditorCurve(clip, Bind("m_LocalScale.y"), curve);
        }

        private static void SetAlpha(AnimationClip clip, AnimationCurve curve)
        {
            var binding = EditorCurveBinding.FloatCurve(SpritePath, typeof(SpriteRenderer), "m_Color.a");
            AnimationUtility.SetEditorCurve(clip, binding, curve);
        }

        private static EditorCurveBinding Bind(string property)
            => EditorCurveBinding.FloatCurve(SpritePath, typeof(Transform), property);

        private static AnimationClip SaveClip(AnimationClip clip, string name)
        {
            string path = $"{ClipFolder}/{name}.anim";
            var existing = AssetDatabase.LoadAssetAtPath<AnimationClip>(path);

            if (existing == null)
            {
                clip.name = name;
                AssetDatabase.CreateAsset(clip, path);
                return clip;
            }

            // 에셋을 새로 만들면 참조가 끊긴다. 내용만 덮어쓴다.
            EditorUtility.CopySerialized(clip, existing);
            existing.name = name;
            EditorUtility.SetDirty(existing);
            return existing;
        }

        // ── 씬 배선 ───────────────────────────────────────

        private static void RigEntities(AnimatorController controller)
        {
            foreach (Entity e in Object.FindObjectsByType<Entity>(FindObjectsInactive.Include))
            {
                // 동료는 실제 스프라이트 시트를 쓴다(ArtImportBuilder). 플레이스홀더로 되돌리지 않는다.
                if (e is Ally) continue;

                var animator = e.GetComponent<Animator>();
                if (animator == null) animator = Undo.AddComponent<Animator>(e.gameObject);

                animator.runtimeAnimatorController = controller;
                animator.applyRootMotion = false;
                // 컬링되면 화면 밖에서 상태가 안 돌아 판정과 어긋난다.
                animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;

                var view = e.GetComponent<EntityAnimator>();
                if (view == null) view = Undo.AddComponent<EntityAnimator>(e.gameObject);

                var so = new SerializedObject(view);
                so.FindProperty("animator").objectReferenceValue = animator;
                so.ApplyModifiedProperties();

                EditorUtility.SetDirty(e.gameObject);
            }
        }

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;

            string[] parts = path.Split('/');
            string cur = parts[0];

            for (int i = 1; i < parts.Length; i++)
            {
                string next = $"{cur}/{parts[i]}";
                if (!AssetDatabase.IsValidFolder(next))
                    AssetDatabase.CreateFolder(cur, parts[i]);
                cur = next;
            }
        }
    }
}

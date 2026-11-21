using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using UnityEngine.Rendering;

namespace Prototype.EditorTools
{
    /// <summary>Reproducible cutout rig and authored motions for the supplied Nun parts.
    /// Gameplay owns movement and hit windows; these clips only move visual children.</summary>
    public static class NunAnimationBuilder
    {
        public const string PrefabPath = "Assets/Prefabs/Player/Allies/Ally_Tan.prefab";
        public const string Output = "Assets/Data/Animation/Nun";
        private const string Art = "Assets/Art/Character/Party/Nun";
        private const float Ppu = 1050f;
        private static readonly string[] JointNames =
        { "Hips", "Torso", "Head", "ArmL", "ElbowL", "WristL", "ArmR", "ElbowR", "WristR",
          "LegL", "KneeL", "LegR", "KneeR", "Skirt", "Braid", "HairFront", "HairBack" };

        private sealed class Pose
        {
            public readonly Dictionary<string, float> angles = JointNames.ToDictionary(n => n, n => 0f);
            public float x, y = 1.2f, turn;
            public void Set(string name, float value) => angles[name] = value;
        }

        [MenuItem("Prototype/Nun/Build Ally_Tan motions")]
        public static void Build()
        {
            Directory.CreateDirectory(Output);
            AssetDatabase.Refresh();
            foreach (string file in Directory.GetFiles(Art + "/images", "*.png"))
            {
                var importer = (TextureImporter)AssetImporter.GetAtPath(file.Replace('\\', '/'));
                importer.textureType = TextureImporterType.Sprite;
                importer.spriteImportMode = SpriteImportMode.Single;
                importer.spritePixelsPerUnit = Ppu;
                var textureSettings = new TextureImporterSettings();
                importer.ReadTextureSettings(textureSettings);
                textureSettings.spriteAlignment = (int)SpriteAlignment.Center;
                importer.SetTextureSettings(textureSettings);
                importer.textureShape = TextureImporterShape.Texture2D;
                importer.spritePivot = new Vector2(.5f, .5f);
                importer.mipmapEnabled = false;
                importer.alphaIsTransparency = true;
                importer.filterMode = FilterMode.Bilinear;
                importer.textureCompression = TextureImporterCompression.Uncompressed;
                importer.SaveAndReimport();
            }

            GameObject root = PrefabUtility.LoadPrefabContents(PrefabPath);
            try
            {
                Transform spriteRoot = root.transform.Find("View/Sprite");
                var proxy = spriteRoot.GetComponent<SpriteRenderer>();
                var material = AssetDatabase.LoadAssetAtPath<Material>(Output + "/NunSprite.mat");
                if (material == null)
                {
                    var shader = Shader.Find("Universal Render Pipeline/2D/Sprite-Unlit-Default");
                    if (shader == null) throw new Exception("URP 2D unlit sprite shader missing");
                    material = new Material(shader);
                    AssetDatabase.CreateAsset(material, Output + "/NunSprite.mat");
                }
                proxy.sharedMaterial = material;
                // The first integration kept a single static image. The rig now owns the artwork.
                foreach (var c in spriteRoot.GetComponents<MonoBehaviour>())
                    if (c != null && c.GetType().Name == "FixedCharacterSprite") UnityEngine.Object.DestroyImmediate(c);
                Transform oldRig = spriteRoot.Find("NunRig");
                if (oldRig != null) UnityEngine.Object.DestroyImmediate(oldRig.gameObject);
                var oldVisual = spriteRoot.GetComponent<LayeredCharacterVisual>();
                if (oldVisual != null) UnityEngine.Object.DestroyImmediate(oldVisual);
                proxy.color = Color.white;
                proxy.forceRenderingOff = false;
                spriteRoot.localPosition = Vector3.zero;
                spriteRoot.localRotation = Quaternion.identity;

                Transform rig = Node("NunRig", spriteRoot, Vector3.zero);
                var group = rig.gameObject.AddComponent<SortingGroup>();
                Transform motion = Node("Motion", rig, new Vector3(0f, 1.2f, 0f));
                var joints = new Dictionary<string, Transform>();
                var pivots = new Dictionary<string, Vector2>();
                Action<string, string, float, float> joint = (name, parent, x, y) =>
                {
                    var world = new Vector3(x / Ppu, (y - 105f) / Ppu, 0);
                    Transform p = parent == null ? motion : joints[parent];
                    Vector3 origin = parent == null ? new Vector3(0, 1.2f, 0) : new Vector3(pivots[parent].x / Ppu, (pivots[parent].y - 105f) / Ppu, 0);
                    joints[name] = Node(name, p, world - origin);
                    pivots[name] = new Vector2(x, y);
                };
                joint("Hips", null, -10, 1400);
                joint("Torso", "Hips", -10, 1640);
                joint("Head", "Torso", 0, 2050);
                joint("ArmL", "Torso", 145, 1915);
                joint("ElbowL", "ArmL", 202, 1560);
                joint("WristL", "ElbowL", 300, 1290);
                joint("ArmR", "Torso", -185, 1920);
                joint("ElbowR", "ArmR", -310, 1540);
                joint("WristR", "ElbowR", -360, 1295);
                joint("LegL", "Hips", 135, 1360);
                joint("KneeL", "LegL", 220, 790);
                joint("LegR", "Hips", -145, 1360);
                joint("KneeR", "LegR", -220, 810);
                joint("Skirt", "Hips", -20, 1400);
                joint("Braid", "Head", -60, 2040);
                joint("HairFront", "Head", 0, 2350);
                joint("HairBack", "Head", 0, 2350);

                string[] names = { "Hair_back", "UpperLeg_L", "LowerLeg_L", "UpperArm_L", "Hand_L_Open", "LowerArm_L", "Body", "UpperLeg_R", "LowerLeg_R", "Skirt", "Hair_Braid", "Head", "Face", "Hair_Front", "Hand_R", "UpperArm_R", "LowerArm_R" };
                string[] parents = { "HairBack", "LegL", "KneeL", "ArmL", "WristL", "ElbowL", "Torso", "LegR", "KneeR", "Skirt", "Braid", "Head", "Head", "HairFront", "WristR", "ArmR", "ElbowR" };
                string json = File.ReadAllText(Art + "/Nun.json");
                var parts = new List<SpriteRenderer>();
                Transform hand = null;
                for (int i = 0; i < names.Length; i++)
                {
                    string name = names[i];
                    Match match = Regex.Match(json, "\"" + name + "\"\\s*:\\s*\\{\\s*\"x\"\\s*:\\s*([-0-9.]+),\\s*\"y\"\\s*:\\s*([-0-9.]+)");
                    if (!match.Success) throw new Exception("Attachment missing: " + name);
                    Vector2 center = new Vector2(float.Parse(match.Groups[1].Value, CultureInfo.InvariantCulture), float.Parse(match.Groups[2].Value, CultureInfo.InvariantCulture));
                    Vector2 delta = (center - pivots[parents[i]]) / Ppu;
                    Transform part = Node(name, joints[parents[i]], new Vector3(delta.x, delta.y, 0));
                    var renderer = part.gameObject.AddComponent<SpriteRenderer>();
                    renderer.sprite = AssetDatabase.LoadAssetAtPath<Sprite>(Art + "/images/" + name + ".png");
                    if (renderer.sprite == null) throw new Exception("Sprite import failed: " + name);
                    renderer.sharedMaterial = proxy.sharedMaterial;
                    renderer.sortingOrder = i;
                    parts.Add(renderer);
                    if (name == "Hand_L_Open") hand = part;
                }

                var clips = new Dictionary<string, AnimationClip>();
                string[] states = { "Idle", "Move", "Attack", "Attack2", "Attack3", "AerialAttack", "Jump", "Fall", "Hit", "AerialHit", "Down", "Getup", "Dead", "Skill", "ChainPull", "Guard" };
                foreach (string state in states)
                    clips[state] = Bake(root.transform, motion, joints, hand, state);

                string controllerPath = Output + "/NunAnimator.controller";
                var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(controllerPath);
                if (controller == null) controller = AnimatorController.CreateAnimatorControllerAtPath(controllerPath);
                var machine = controller.layers[0].stateMachine;
                foreach (var state in machine.states) machine.RemoveState(state.state);
                foreach (string state in states.Where(s => s != "Attack2" && s != "Attack3" && s != "ChainPull" && s != "Guard"))
                {
                    var s = machine.AddState(state);
                    s.motion = clips[state];
                    s.writeDefaultValues = false;
                    if (state == "Idle") machine.defaultState = s;
                }
                root.GetComponent<Animator>().runtimeAnimatorController = controller;
                var animator = new SerializedObject(root.GetComponent<EntityAnimator>());
                var skills = animator.FindProperty("skillMotions");
                skills.arraySize = 2;
                string[] skillIds = { "162b0d25154687b634727a49a865b9f3", "f350a5b837d446148f3d2a368e433c2e" };
                for (int i = 0; i < 2; i++)
                {
                    skills.GetArrayElementAtIndex(i).FindPropertyRelative("skill").objectReferenceValue = AssetDatabase.LoadAssetAtPath<SkillData>(AssetDatabase.GUIDToAssetPath(skillIds[i]));
                    skills.GetArrayElementAtIndex(i).FindPropertyRelative("clip").objectReferenceValue = clips[i == 0 ? "ChainPull" : "Guard"];
                }
                animator.ApplyModifiedPropertiesWithoutUndo();
                var attacks = new SerializedObject(root.GetComponent<BasicAttackProfile>());
                var stages = attacks.FindProperty("stages");
                for (int i = 0; i < stages.arraySize; i++)
                    stages.GetArrayElementAtIndex(i).FindPropertyRelative("clip").objectReferenceValue = clips[i == 0 ? "Attack" : "Attack" + (i + 1)];
                attacks.ApplyModifiedPropertiesWithoutUndo();

                var visual = spriteRoot.gameObject.AddComponent<LayeredCharacterVisual>();
                var so = new SerializedObject(visual);
                so.FindProperty("source").objectReferenceValue = proxy;
                so.FindProperty("rig").objectReferenceValue = rig;
                so.FindProperty("sortingGroup").objectReferenceValue = group;
                var renderers = so.FindProperty("parts");
                renderers.arraySize = parts.Count;
                for (int i = 0; i < parts.Count; i++) renderers.GetArrayElementAtIndex(i).objectReferenceValue = parts[i];
                so.ApplyModifiedPropertiesWithoutUndo();
                visual.Sync();
                PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
                EditorUtility.SetDirty(controller);
                AssetDatabase.SaveAssets();
                Debug.Log("[NunAnimationBuilder] Built 17-part rig, 16 motions, three combo stages and two character-local skill motions.");
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }

        private static Transform Node(string name, Transform parent, Vector3 position)
        {
            var node = new GameObject(name).transform;
            node.SetParent(parent, false);
            node.localPosition = position;
            return node;
        }

        public static float Duration(string state)
        {
            switch (state)
            {
                case "Idle": return 2f;
                case "Move": return .8f;
                case "Attack": return .32f;
                case "Attack2": return .4f;
                case "Attack3": return .7f;
                case "Hit": return .3f;
                case "Down": return .4f;
                case "Getup": return .4f;
                case "Dead": return 1.5f;
                case "Guard": return .3f;
                case "ChainPull": return .5f;
                default: return .6f;
            }
        }

        private static AnimationClip Bake(Transform root, Transform motion, Dictionary<string, Transform> joints, Transform hand, string state)
        {
            string name = state == "Skill" ? EntityAnimator.SkillSlotClip : "Nun_" + state;
            string path = Output + "/" + name + ".anim";
            var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(path);
            if (clip == null) { clip = new AnimationClip(); AssetDatabase.CreateAsset(clip, path); }
            clip.ClearCurves();
            clip.name = name;
            clip.frameRate = 30;
            float length = Duration(state);
            int steps = Mathf.CeilToInt(length * 30);
            var poses = Enumerable.Range(0, steps + 1).Select(i => Sample(state, i * length / steps, length)).ToArray();
            foreach (string joint in JointNames)
                Curve(clip, AnimationUtility.CalculateTransformPath(joints[joint], root), typeof(Transform), "localEulerAnglesRaw.z", poses, length, p => p.angles[joint]);
            string motionPath = AnimationUtility.CalculateTransformPath(motion, root);
            Curve(clip, motionPath, typeof(Transform), "m_LocalPosition.x", poses, length, p => p.x);
            Curve(clip, motionPath, typeof(Transform), "m_LocalPosition.y", poses, length, p => p.y);
            Curve(clip, motionPath, typeof(Transform), "localEulerAnglesRaw.z", poses, length, p => p.turn);
            Sprite open = AssetDatabase.LoadAssetAtPath<Sprite>(Art + "/images/Hand_L_Open.png");
            Sprite fist = AssetDatabase.LoadAssetAtPath<Sprite>(Art + "/images/Hand_L_Fist.png");
            bool closed = state.StartsWith("Attack") || state == "AerialAttack" || state == "Guard";
            AnimationUtility.SetObjectReferenceCurve(clip, EditorCurveBinding.PPtrCurve(AnimationUtility.CalculateTransformPath(hand, root), typeof(SpriteRenderer), "m_Sprite"),
                new[] { new ObjectReferenceKeyframe {time = 0, value = closed ? fist : open}, new ObjectReferenceKeyframe {time = length, value = closed ? fist : open} });
            // The alternate hand is a different crop; compensate its center in every clip.
            Vector3 pos = hand.localPosition;
            if (closed) pos += new Vector3(-25.5f / Ppu, 38.5f / Ppu, 0);
            foreach (var axis in new[] { "x", "y" })
                clip.SetCurve(AnimationUtility.CalculateTransformPath(hand, root), typeof(Transform), "m_LocalPosition." + axis,
                    AnimationCurve.Constant(0, length, axis == "x" ? pos.x : pos.y));
            var settings = AnimationUtility.GetAnimationClipSettings(clip);
            settings.loopTime = state == "Idle" || state == "Move";
            settings.stopTime = length;
            AnimationUtility.SetAnimationClipSettings(clip, settings);
            EditorUtility.SetDirty(clip);
            return clip;
        }

        private static void Curve(AnimationClip clip, string path, Type type, string property, Pose[] poses, float length, Func<Pose, float> value)
        {
            var curve = new AnimationCurve(poses.Select((p, i) => new Keyframe(i * length / (poses.Length - 1), value(p))).ToArray());
            for (int i = 0; i < curve.length; i++)
            {
                AnimationUtility.SetKeyLeftTangentMode(curve, i, AnimationUtility.TangentMode.ClampedAuto);
                AnimationUtility.SetKeyRightTangentMode(curve, i, AnimationUtility.TangentMode.ClampedAuto);
            }
            clip.SetCurve(path, type, property, curve);
        }

        private static float Ease(float a, float b, float t) => Mathf.SmoothStep(a, b, Mathf.Clamp01(t));
        private static float Pulse(float t, float peak, float end) => t < peak ? Ease(0, 1, t / peak) : Ease(1, 0, (t - peak) / (end - peak));

        private static Pose Sample(string state, float t, float length)
        {
            var p = new Pose();
            float u = t / length, phase = u * Mathf.PI * 2;
            p.Set("ArmL", 8); p.Set("ElbowL", 10);
            p.Set("ArmR", -5); p.Set("ElbowR", -8);
            switch (state)
            {
                case "Idle":
                    p.y += .009f * Mathf.Sin(phase);
                    p.Set("Torso", .7f * Mathf.Sin(phase));
                    p.Set("Head", -.8f * Mathf.Sin(phase + .3f));
                    p.Set("Braid", 2f * Mathf.Sin(phase - .5f));
                    p.Set("HairFront", .8f * Mathf.Sin(phase - .4f));
                    p.Set("HairBack", 1.2f * Mathf.Sin(phase - .7f));
                    p.Set("Skirt", .6f * Mathf.Sin(phase));
                    break;
                case "Move":
                    float s = Mathf.Sin(phase);
                    p.y += .018f * (1 - Mathf.Cos(phase * 2));
                    p.Set("Torso", -3 + 1.2f * s);
                    p.Set("Head", 2 - s);
                    p.Set("LegL", 11 * s); p.Set("LegR", -11 * s);
                    p.Set("KneeL", -12 * Mathf.Max(0, -s)); p.Set("KneeR", -12 * Mathf.Max(0, s));
                    p.Set("ArmL", 8 - 12 * s); p.Set("ArmR", 5 + 12 * s);
                    p.Set("ElbowL", 16 + 5 * s); p.Set("ElbowR", 12 - 5 * s);
                    p.Set("Skirt", 2.4f * Mathf.Sin(phase - .3f));
                    p.Set("Braid", -4 + 5 * Mathf.Sin(phase - .6f));
                    p.Set("HairBack", 2 * Mathf.Sin(phase - .4f));
                    break;
                case "Attack": case "Attack2": case "Attack3": case "AerialAttack":
                    float hit = state == "Attack" ? .1f : state == "Attack2" ? .12f : state == "Attack3" ? .18f : .19f;
                    float attack = Pulse(t, hit, length);
                    float prep = Pulse(t, hit * .5f, hit);
                    bool left = state != "Attack2";
                    p.Set(left ? "ArmL" : "ArmR", 8 + 88 * attack - 16 * prep);
                    p.Set(left ? "ElbowL" : "ElbowR", 10 - 30 * attack + 40 * prep);
                    p.Set(left ? "ArmR" : "ArmL", 18 + 20 * attack);
                    p.Set(left ? "ElbowR" : "ElbowL", 25 + 28 * attack);
                    p.Set("Torso", -8 * attack + 5 * prep);
                    p.Set("Head", 4 * attack);
                    p.Set("Skirt", 4 * attack); p.Set("Braid", -9 * attack);
                    p.x = .06f * attack; p.y -= .025f * attack;
                    if (state == "Attack3") { p.Set("ArmL", 12 + 123 * attack - 30 * prep); p.Set("WristL", -25 * attack); p.Set("Torso", -12 * attack); }
                    if (state == "AerialAttack") { p.Set("LegL", 18 * attack); p.Set("KneeL", -30 * attack); }
                    break;
                case "Skill": case "ChainPull":
                    float reach = Pulse(t, length * .6f, length);
                    float pull = Pulse(t, length * .82f, length);
                    p.Set("ArmL", 8 + 72 * reach - 30 * pull);
                    p.Set("ElbowL", 10 - 15 * reach + 70 * pull);
                    p.Set("ArmR", 10 + 38 * reach); p.Set("ElbowR", 30 + 30 * pull);
                    p.Set("Torso", -7 * reach + 10 * pull);
                    p.Set("Head", 4 * reach - 4 * pull);
                    p.Set("Braid", -8 * reach + 8 * pull); p.Set("Skirt", 3 * pull);
                    p.x = .04f * reach - .04f * pull;
                    break;
                case "Guard":
                    float guard = Pulse(t, .1f, length);
                    p.Set("ArmL", 22 + 30 * guard); p.Set("ArmR", -5 - 28 * guard);
                    p.Set("ElbowL", 15 + 72 * guard); p.Set("ElbowR", -8 - 72 * guard);
                    p.Set("Head", 5 * guard); p.Set("Torso", 3 * guard);
                    p.y -= .045f * guard; p.Set("Skirt", -2 * guard);
                    break;
                case "Jump": case "Fall":
                    float jump = state == "Jump" ? Mathf.Sin(u * Mathf.PI) : .75f;
                    p.Set("ArmL", -15 * jump); p.Set("ArmR", 20 * jump);
                    p.Set("LegL", 18 * jump); p.Set("LegR", -12 * jump);
                    p.Set("KneeL", -28 * jump); p.Set("KneeR", -18 * jump);
                    p.Set("Skirt", -4 * jump); p.Set("Braid", -12 * jump);
                    p.Set("Torso", -4 * jump); p.Set("Head", 4 * jump);
                    break;
                case "Hit": case "AerialHit":
                    float recoil = Pulse(t, .06f, length);
                    p.Set("Torso", 13 * recoil); p.Set("Head", 9 * recoil);
                    p.Set("ArmL", -20 * recoil); p.Set("ArmR", -15 * recoil);
                    p.Set("Braid", 13 * recoil); p.Set("Skirt", -4 * recoil);
                    p.x = -.06f * recoil; p.y -= .045f * recoil;
                    if (state == "AerialHit") { p.turn = 12 * recoil; p.Set("KneeL", -32 * recoil); p.Set("LegR", -20 * recoil); }
                    break;
                case "Down": case "Dead": case "Getup":
                    float down = state == "Getup" ? Ease(1, 0, u) : Ease(0, 1, t / .32f);
                    p.turn = 78 * down; p.y = Mathf.Lerp(1.2f, .32f, down); p.x = -.1f * down;
                    p.Set("Torso", 7 * down); p.Set("Head", 9 * down);
                    p.Set("ArmL", -20 * down); p.Set("ArmR", -16 * down);
                    p.Set("ElbowL", 28 * down); p.Set("ElbowR", 22 * down); p.Set("KneeL", -18 * down);
                    p.Set("Skirt", -5 * down); p.Set("Braid", 20 * down);
                    // DeadState owns the fade and despawn; avoid two competing alpha writers.
                    break;
            }
            return p;
        }
    }
}

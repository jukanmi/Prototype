using System.Collections.Generic;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Prototype.Tests
{
    /// <summary>
    /// 깊이 배율을 어느 트랜스폼이 갖느냐가 이 기능의 전부다.
    /// Sprite의 localScale은 애니 클립(Idle 바운스 · Jump 스트레치) 소유라
    /// 거기 배율을 얹으면 매 프레임 서로 덮어써서 애니가 통째로 죽는다.
    /// </summary>
    public class BeltScrollViewDepthTests
    {
        private readonly List<GameObject> spawned = new List<GameObject>();

        [TearDown]
        public void TearDown()
        {
            for (int i = 0; i < spawned.Count; i++)
                if (spawned[i] != null) Object.DestroyImmediate(spawned[i]);
            spawned.Clear();

            // Sync가 static을 덮어쓴다. 다음 테스트로 새지 않게 되돌린다.
            BeltScroll.DepthToScreen = 0.5f;
            BeltScroll.DepthToScreenX = 0.45f;
            BeltScroll.DepthScalePerUnit = 0.06f;
        }

        /// <summary>root(Physics + BeltScrollView) → [View →] Sprite, root → Shadow.</summary>
        private BeltScrollView NewRig(Vector3 position, bool withDepthRoot)
        {
            var root = new GameObject("Rig");
            spawned.Add(root);

            // Rigidbody는 RequireComponent가 붙여준다.
            root.AddComponent<Physics>();
            root.transform.position = position;

            var shadow = new GameObject("Shadow").transform;
            shadow.SetParent(root.transform, false);

            Transform depthRoot = null;
            Transform spriteParent = root.transform;
            if (withDepthRoot)
            {
                depthRoot = new GameObject("View").transform;
                depthRoot.SetParent(root.transform, false);
                spriteParent = depthRoot;
            }

            var sprite = new GameObject("Sprite").transform;
            sprite.SetParent(spriteParent, false);
            sprite.gameObject.AddComponent<SpriteRenderer>();

            var view = root.AddComponent<BeltScrollView>();

            var so = new SerializedObject(view);
            so.FindProperty("sprite").objectReferenceValue = sprite;
            so.FindProperty("shadow").objectReferenceValue = shadow;
            so.FindProperty("depthRoot").objectReferenceValue = depthRoot;
            so.FindProperty("depthToScreen").floatValue = 0.9f;
            so.FindProperty("depthToScreenX").floatValue = 0.45f;
            so.FindProperty("depthScalePerUnit").floatValue = 0.06f;
            so.FindProperty("spriteOffsetY").floatValue = 0.5f;
            so.ApplyModifiedPropertiesWithoutUndo();

            return view;
        }

        [Test]
        public void WithDepthRoot_ScalesTheViewNode()
        {
            BeltScrollView view = NewRig(new Vector3(0f, 0f, 3f), withDepthRoot: true);
            Transform depthRoot = view.transform.Find("View");

            view.Sync();

            Assert.That(depthRoot.localScale.x, Is.EqualTo(0.82f).Within(0.001f));
            Assert.That(depthRoot.localScale.y, Is.EqualTo(0.82f).Within(0.001f));
        }

        /// <summary>이 테스트가 이 태스크의 핵심이다. 애니 충돌 회귀 방지.</summary>
        [Test]
        public void WithDepthRoot_LeavesSpriteLocalScaleAlone()
        {
            BeltScrollView view = NewRig(new Vector3(0f, 0f, 3f), withDepthRoot: true);
            Transform sprite = view.transform.Find("View/Sprite");
            sprite.localScale = new Vector3(1.12f, 0.9f, 1f);   // Animator가 쓴 값을 흉내낸다

            view.Sync();

            Assert.That(sprite.localScale.x, Is.EqualTo(1.12f).Within(0.0001f));
            Assert.That(sprite.localScale.y, Is.EqualTo(0.9f).Within(0.0001f));
        }

        /// <summary>
        /// 센터 피벗 오프셋도 같이 줄어야 발이 그림자 위에 붙는다.
        /// ToView(z=3).y = 3 × 0.9 = 2.7, 거기에 spriteOffsetY(0.5) × 0.82.
        /// </summary>
        [Test]
        public void WithDepthRoot_ScalesTheFootOffset()
        {
            BeltScrollView view = NewRig(new Vector3(0f, 0f, 3f), withDepthRoot: true);
            Transform depthRoot = view.transform.Find("View");

            view.Sync();

            Assert.That(depthRoot.position.y, Is.EqualTo(2.7f + 0.5f * 0.82f).Within(0.001f));
        }

        /// <summary>그림자는 바닥에 눕는 물체라 같은 깊이 배율을 받아야 한다.</summary>
        [Test]
        public void WithDepthRoot_ScalesTheShadow()
        {
            BeltScrollView view = NewRig(new Vector3(0f, 0f, 3f), withDepthRoot: true);
            Transform shadow = view.transform.Find("Shadow");

            view.Sync();

            // shadowBaseScale 기본값 (0.9, 0.35, 1). 높이 0이라 shrink는 1이다.
            Assert.That(shadow.localScale.x, Is.EqualTo(0.9f * 0.82f).Within(0.001f));
            Assert.That(shadow.localScale.y, Is.EqualTo(0.35f * 0.82f).Within(0.001f));
        }

        /// <summary>배선 안 된 프리팹(변종 적 등)이 깨지면 안 된다.</summary>
        [Test]
        public void WithoutDepthRoot_KeepsLegacyBehaviour()
        {
            BeltScrollView view = NewRig(new Vector3(0f, 0f, 3f), withDepthRoot: false);
            Transform sprite = view.transform.Find("Sprite");

            view.Sync();

            Assert.That(sprite.position.y, Is.EqualTo(2.7f + 0.5f).Within(0.001f), "배율이 끼면 안 된다");
            Assert.That(sprite.localScale, Is.EqualTo(Vector3.one));
        }
    }
}

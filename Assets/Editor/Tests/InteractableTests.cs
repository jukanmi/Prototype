using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

namespace Prototype.Tests
{
    /// <summary>
    /// 씬에 놓는 상호작용 지점(<see cref="Interactable"/>). 누구에게 말을 거는가 — 반경 안 제일 가까운 하나,
    /// 높이는 안 본다 — 와 눌렀을 때 인스펙터에 꽂은 결과가 불리는가.
    /// </summary>
    public class InteractableTests
    {
        private readonly List<GameObject> made = new List<GameObject>();

        [SetUp]
        public void SetUp() => ResetRegistry();

        [TearDown]
        public void TearDown()
        {
            ResetRegistry();
            foreach (GameObject go in made) Object.DestroyImmediate(go);
            made.Clear();
        }

        private static void ResetRegistry()
            => typeof(Interactable).GetMethod("ResetStatics", BindingFlags.Static | BindingFlags.NonPublic)
                .Invoke(null, null);

        /// <summary>에디트 모드는 OnEnable을 보장하지 않는다. 껐다 켜서 정확히 한 번 등록한다.</summary>
        private Interactable Make(Vector3 position, float radius)
        {
            var go = new GameObject("Interactable");
            go.transform.position = position;
            made.Add(go);

            Interactable spot = go.AddComponent<Interactable>();
            spot.Configure("상인", radius);
            Call(spot, "OnDisable");
            Call(spot, "OnEnable");
            return spot;
        }

        private static void Call(Interactable spot, string method)
            => typeof(Interactable).GetMethod(method, BindingFlags.Instance | BindingFlags.NonPublic)
                .Invoke(spot, null);

        [Test]
        public void Nearest_OutsideRadius_IsNull()
        {
            Make(Vector3.zero, 1.5f);

            Assert.IsNull(Interactable.Nearest(new Vector3(2f, 0f, 0f)));
        }

        [Test]
        public void Nearest_PicksClosestInRange()
        {
            Make(Vector3.zero, 3f);
            Interactable closer = Make(new Vector3(2f, 0f, 0f), 3f);

            Assert.AreSame(closer, Interactable.Nearest(new Vector3(1.5f, 0f, 0f)));
        }

        [Test]
        public void Nearest_IgnoresHeight()
        {
            Interactable spot = Make(Vector3.zero, 1.5f);

            Assert.AreSame(spot, Interactable.Nearest(new Vector3(0f, 5f, 0.5f)));
        }

        [Test]
        public void Interact_WithoutNpc_InvokesResultAtOnce()
        {
            Interactable spot = Make(Vector3.zero, 1.5f);
            int calls = 0;
            spot.OnInteract.AddListener(() => calls++);

            spot.Interact();

            Assert.AreEqual(1, calls);
        }

        [Test]
        public void Interact_NpcWithoutLines_InvokesResultAtOnce()
        {
            Interactable spot = Make(Vector3.zero, 1.5f);
            spot.gameObject.AddComponent<Npc>();   // 대사 0줄
            int calls = 0;
            spot.OnInteract.AddListener(() => calls++);

            spot.Interact();

            Assert.AreEqual(1, calls, "읽을 대사가 없으면 대화창 없이 바로 결과(상점 · 제단 등)로 가야 한다.");
            Assert.IsFalse(DialogueWindow.IsOpen);
        }

        [Test]
        public void Interact_RemembersWhoWasTalkedTo()
        {
            // 이벤트 대화는 "누가 불렀나"를 여기서 읽어 그 NPC 이름 · 그림으로 말한다.
            // UnityEvent는 부른 쪽을 안 넘기므로 이게 비면 이름 없는 대화창이 뜬다.
            Interactable first = Make(Vector3.zero, 1.5f);
            Interactable second = Make(new Vector3(5f, 0f, 0f), 1.5f);

            Interactable seen = null;
            second.OnInteract.AddListener(() => seen = Interactable.Current);

            first.Interact();
            second.Interact();

            Assert.AreSame(second, seen, "결과가 불리는 순간 Current는 부른 그 지점이어야 한다.");
            Assert.AreSame(second, Interactable.Current);
        }

        [Test]
        public void Disabled_IsNotFoundAndNotFocused()
        {
            // 열어 버린 상자 · 날아가는 두루마리는 꺼 둔다. 꺼진 지점이 계속 잡히면 빈 상자를 또 연다.
            Interactable spot = Make(Vector3.zero, 1.5f);
            Call(spot, "OnDisable");

            Assert.IsNull(Interactable.Nearest(Vector3.zero));
            Assert.IsFalse(spot.IsFocused);
        }
    }
}

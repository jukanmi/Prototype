using System.Collections.Generic;
using UnityEngine;

namespace Prototype
{
    /// <summary>
    /// 적 머리 위에 전투 상태를 글자로 띄운다.
    ///
    /// <see cref="EnemyStateTint"/>의 색과 짝을 이룬다 — 색만으로는 난전에서 안 읽히고,
    /// 색약자는 아예 구분하지 못한다.
    ///
    /// <see cref="ChargeGauge"/>와 같은 자리(<c>[BattleVfx]</c>)에 붙는다 — 씬 배선 0.
    /// </summary>
    public class EnemyStateLabel : MonoBehaviour
    {
        [Tooltip("머리 위로 띄우는 높이. 차징 게이지(1.6)보다 위여야 겹치지 않는다.")]
        [SerializeField] private float headOffset = 1.9f;

        [Tooltip("정렬 오프셋. 차징 게이지(300)보다 앞이다.")]
        [SerializeField] private int sortingOffset = 320;

        [Tooltip("월드 단위 글자 크기.")]
        [SerializeField] private float characterSize = 0.06f;

        [SerializeField] private int fontSize = 48;

        /// <summary>TextMesh와 그 MeshRenderer를 같이 들고 다닌다 — 매 프레임 GetComponent하지 않게.</summary>
        private struct Slot
        {
            public TextMesh text;
            public MeshRenderer renderer;
        }

        private readonly List<Slot> pool = new List<Slot>();

        /// <summary>
        /// 라벨이 놓일 자리.
        ///
        /// 논리 좌표가 아니라 <see cref="BeltScroll.ToView"/>를 거친 <b>그리는 위치</b>다.
        /// 루트 transform은 깊이가 화면 세로로 접히기 전 값(x, 0, z)을 들고 있어서,
        /// 그대로 쓰면 라벨이 발밑 훨씬 아래에 찍힌다.
        ///
        /// 머리 오프셋에는 깊이 배율을 먹인다 — 뒤에 선 적은 몸이 줄어 머리도 내려온다.
        /// 글자 크기는 안 건드린다. 어느 깊이에서나 같게 읽혀야 한다.
        /// </summary>
        public static Vector3 LabelPosition(Vector3 ground, float height, float headOffset)
            => BeltScroll.ToView(ground, height + headOffset * BeltScroll.ScaleAt(ground.z));

        // 캐릭터 위치는 LateUpdate에 확정된다(BeltScrollView). 그 뒤에 읽는다.
        // TimeControl을 보지 않으므로 불릿타임 중에도 보인다 — 조준하는 동안 상태가 보여야 한다.
        private void LateUpdate()
        {
            int used = Draw(BattleRegistry.Enemies);

            // 남는 슬롯은 끈다. 파괴하지 않는다 — 다음 타격에 다시 쓴다.
            for (int i = used; i < pool.Count; i++)
                pool[i].renderer.enabled = false;
        }

        private int Draw(IReadOnlyList<Entity> list)
        {
            if (list == null) return 0;

            int n = 0;
            for (int i = 0; i < list.Count; i++)
            {
                Entity e = list[i];
                if (e == null || e.Combat == null) continue;

                CombatState state = e.Combat.CombatState;

                if (CombatStateVisuals.ShouldShow(state))
                {
                    Draw(Take(n), e, CombatStateVisuals.Label(state), CombatStateVisuals.StateColor(state));
                    n++;
                    continue;
                }

                // 예고는 상태가 아니라 행동이라 CombatState에 없다. 맞기 전에 읽을 수 있는
                // 유일한 신호라서 몸 색(EnemyStateTint)과 같이 글자로도 띄운다.
                if (e.IsTelegraphing && !e.Combat.IsDead)
                {
                    Draw(Take(n), e, CombatStateVisuals.TelegraphLabel, Color.white);
                    n++;
                }
            }

            return n;
        }

        private void Draw(Slot slot, Entity e, string label, Color color)
        {
            Physics phys = e.Physics;
            Vector3 ground = phys.GroundPosition;

            slot.text.text = label;
            slot.text.color = color;

            slot.text.transform.position = LabelPosition(ground, phys.Height, headOffset);
            slot.text.transform.rotation = Quaternion.identity;   // 빌보드

            // 캐릭터와 같은 깊이 정렬 공식(BeltScrollView.ApplySorting) 위에서 오프셋만 준다.
            slot.renderer.sortingOrder = Mathf.RoundToInt(-ground.z * 100f) + sortingOffset;
            slot.renderer.enabled = true;
        }

        private Slot Take(int index)
        {
            while (pool.Count <= index)
            {
                var go = new GameObject("EnemyStateLabel");
                go.transform.SetParent(transform, false);

                var text = go.AddComponent<TextMesh>();
                text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
                text.fontSize = fontSize;
                text.fontStyle = FontStyle.Bold;
                text.characterSize = characterSize;
                text.anchor = TextAnchor.MiddleCenter;
                text.alignment = TextAlignment.Center;

                var renderer = go.GetComponent<MeshRenderer>();

                // TextMesh는 폰트를 꽂아도 렌더러 머티리얼을 스스로 맞추지 않는다.
                // 이걸 빼면 글자가 분홍 사각형으로 나온다.
                renderer.sharedMaterial = text.font.material;
                renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                renderer.receiveShadows = false;
                renderer.enabled = false;

                pool.Add(new Slot { text = text, renderer = renderer });
            }

            return pool[index];
        }
    }
}

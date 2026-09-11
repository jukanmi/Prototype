// 조우가 벌어지는 <b>자리</b> — 씬이 아는 값만 든다.
//
// 좌우 경계와 문 둘. 그게 전부다. 무엇이 나오는지(애셋)도, 언제 시작하는지(디렉터)도 모른다.
//
// <b>씬에 남는 이유</b>: 프리팹 에셋은 씬 오브젝트를 참조할 수 없다. 문은 씬 오브젝트고
// 경계는 방마다 다른 값이라, 이 둘만은 씬에 있어야 한다. 나머지는 전부 프리팹과 애셋으로 뺐다.
//
// 계획서: docs/Stage_Encounter_Unification_Plan.md (2항 · 3.6)

using UnityEngine;

namespace Prototype
{
    /// <summary>
    /// 조우 하나가 벌어지는 자리. 지금은 아레나만 쓰지만, 통합이 끝나면
    /// 웨이브 방도 "자리 하나짜리 스테이지"로 같은 것을 쓴다.
    ///
    /// <see cref="PartySpawnPoint"/> · <see cref="StageWaveBoard"/>와 같은 규약이다 —
    /// 방마다 다른 값이고, 방이 아는 값이다.
    /// </summary>
    public class EncounterSite : MonoBehaviour
    {
        [Header("경계")]
        [Tooltip("카메라가 잠기는 좌우 범위. 적이 벽에서 나오는 자리도 여기서 계산한다.")]
        [SerializeField] private float minX = -6f;

        [SerializeField] private float maxX = 6f;

        [Header("문")]
        [Tooltip("들어온 문. 조우가 시작되면 닫히고 <b>다시 열리지 않는다</b>.")]
        [SerializeField] private ArenaGate entryGate;

        [Tooltip("나가는 문. 조우를 깨면 열린다.")]
        [SerializeField] private ArenaGate exitGate;

        public float MinX => Mathf.Min(minX, maxX);

        public float MaxX => Mathf.Max(minX, maxX);

        /// <summary>카메라 락이 읽는 구간.</summary>
        public StageSection Section => StageSection.Of(SectionKind.Arena, MinX, MaxX);

        /// <summary>플레이어가 이 선을 넘으면 조우가 열린다.</summary>
        public float TriggerLine => MinX;

        /// <summary>
        /// 문 둘을 닫는다. <b>갇혔다는 걸 보여 준다</b> —
        /// 조용히 벽만 세우면 조작이 씹힌 것으로 읽힌다.
        /// </summary>
        public void CloseGates()
        {
            entryGate?.Close();
            exitGate?.Close();
        }

        /// <summary>
        /// 나가는 문만 연다. <b>입구는 열지 않는다</b> —
        /// 되돌아갈 수 있으면 자리를 나눈 의미가 없다.
        /// </summary>
        public void OpenExit() => exitGate?.Open();

        /// <summary>씬 빌더와 테스트가 한 번에 꽂는다.</summary>
        public void Configure(float sectionMinX, float sectionMaxX, ArenaGate entry, ArenaGate exit)
        {
            minX = sectionMinX;
            maxX = sectionMaxX;
            entryGate = entry;
            exitGate = exit;
        }

#if UNITY_EDITOR
        /// <summary>
        /// 경계를 눈으로 볼 수 있어야 한다. 숫자로만 두면 카메라가 왜 저기서 멈추는지
        /// 씬 뷰와 인스펙터를 오가며 찾게 된다.
        /// </summary>
        private void OnDrawGizmosSelected()
        {
            float z = ArenaSpawnPlanner.ArenaHalfZ;

            Gizmos.color = new Color(0.4f, 0.7f, 1f, 0.8f);
            Gizmos.DrawLine(new Vector3(MinX, 0f, -z), new Vector3(MinX, 0f, z));
            Gizmos.DrawLine(new Vector3(MaxX, 0f, -z), new Vector3(MaxX, 0f, z));

            Gizmos.color = new Color(1f, 0.8f, 0.3f, 0.9f);
            Gizmos.DrawLine(new Vector3(TriggerLine, 0f, -z), new Vector3(TriggerLine, 0f, z));
        }
#endif
    }
}

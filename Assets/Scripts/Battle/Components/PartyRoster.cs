using System.Collections.Generic;
using UnityEngine;

namespace Prototype
{
    /// <summary>
    /// 파티 명부와 전투 시작 배선. 주인공을 시작 자리에 세우고 조작 · 카메라 · 시전 감독을 잇는다.
    ///
    /// 조작은 늘 주인공이고, 동료는 <see cref="CastDirector"/>가 어시스트로만 불러낸다.
    /// <see cref="Roster"/>(주인공 + 동료)는 체력 HUD와 스테이지 패배 판정이 읽는다.
    /// </summary>
    [RequireComponent(typeof(AssistManager), typeof(CastDirector))]
    public class PartyRoster : MonoBehaviour
    {
        [SerializeField] private Player player;
        [SerializeField] private BulletTimeController bulletTime;
        [SerializeField] private CameraFollow cameraFollow;
        [SerializeField] private CameraAnchor cameraAnchor;
        [SerializeField] private PlayerPilot pilot;
        private readonly List<Entity> roster = new List<Entity>();
        private Vector3 seedGround;
        private Vector3 seedFacing = Vector3.right;
        private bool hasSeed;

        /// <summary>주인공(0번)과 동료들. <see cref="Start"/>에서 채워진다.</summary>
        public IReadOnlyList<Entity> Roster => roster;

        /// <summary>조작 중인 몸. 언제나 주인공이다.</summary>
        public Entity Current => player;

        /// <summary>살아 있는 조작 몸 수 — 주인공이 살아 있으면 1.</summary>
        public int AliveCount => player != null && !player.Combat.IsDead ? 1 : 0;

        /// <summary>만든 주인공을 꽂는다. <see cref="PartyAssembler"/>가 Awake에서 부른다.</summary>
        public void SetHero(Player hero) { if (hero != null) player = hero; }

        /// <summary>
        /// 시작 자리와 바라볼 방향. <see cref="PartyAssembler"/>가 넘기고,
        /// 실제 착지는 <see cref="Start"/>에서 한다 — 카메라 앵커만 먼저 옮겨 둔다.
        /// </summary>
        public void SeedSeat(Vector3 ground, Vector3 facing)
        {
            seedGround = ground;
            seedFacing = facing.sqrMagnitude > 0.0001f ? facing : Vector3.right;
            hasSeed = true;
            if (cameraAnchor == null) cameraAnchor = GetComponentInChildren<CameraAnchor>(true);
            cameraAnchor?.SnapTo(ground.x);
        }

        private void Awake()
        {
            if (player == null) player = FindAnyObjectByType<Player>();
            if (bulletTime == null) bulletTime = FindAnyObjectByType<BulletTimeController>();
            if (pilot == null) pilot = FindAnyObjectByType<PlayerPilot>();
            if (cameraFollow == null) cameraFollow = FindAnyObjectByType<CameraFollow>();
            if (cameraAnchor == null) cameraAnchor = GetComponentInChildren<CameraAnchor>(true);
        }

        private void Start()
        {
            if (player == null) return;
            roster.Add(player);
            roster.AddRange(player.Party);
            player.TimeDomain = TimeDomain.Battle;
            player.gameObject.SetActive(true);
            if (hasSeed) { player.Physics.Teleport(seedGround); player.Physics.Face(seedFacing); }
            pilot?.Take(player);
            if (cameraAnchor != null) cameraAnchor.SetFocus(player.transform);
            else cameraFollow?.SetTarget(player.transform);
            CastDirector director = GetComponent<CastDirector>();
            director.Configure(player, bulletTime);
            if (bulletTime != null) bulletTime.SetCastDirector(director);
        }
    }
}

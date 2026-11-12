using System.Collections.Generic;
using UnityEngine;

namespace Prototype
{
    /// <summary>Serialized scene adapter. The hero is permanently controlled; CastDirector owns assists.</summary>
    [RequireComponent(typeof(AssistManager), typeof(CastDirector))]
    public class TagSwapController : MonoBehaviour
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
        public IReadOnlyList<Entity> Roster => roster;
        public Entity Current => player;
        public int CurrentIndex => player != null ? 0 : -1;
        public int AliveCount => player != null && !player.Combat.IsDead ? 1 : 0;
        public Vector3 ControlledGround => player != null ? player.Physics.GroundPosition : seedGround;
        public float CooldownRemaining => 0f;
        public bool CanSwap => false;
        public bool SwapNext() => false;
        public bool EnsureActive(Entity body) => body != null && body == player;
        public void SetHero(Player hero) { if (hero != null) player = hero; }
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

using System.Collections.Generic;
using UnityEngine;

namespace Prototype
{
    /// <summary>Reuses the party bodies as a pool. The hero never leaves the field.</summary>
    public sealed class AssistManager : MonoBehaviour
    {
        [SerializeField] private float moveSpeed = 7f;
        [SerializeField] private float lingerSeconds = 0.15f;
        [SerializeField] private float formationSpacing = 1.15f;
        private readonly Dictionary<Ally, AssistControlModule> controls = new Dictionary<Ally, AssistControlModule>();
        private readonly List<AssistControlModule> formation = new List<AssistControlModule>();
        private readonly Queue<CastTicket> deferred = new Queue<CastTicket>();
        private bool recalling;
        public Player Hero { get; private set; }
        public float MoveSpeed => Mathf.Max(0.1f, moveSpeed);
        public float LingerSeconds => Mathf.Max(0f, lingerSeconds);
        public bool BulletMode { get; private set; }
        public bool FormationReady
        {
            get { foreach (var c in formation) if (c != null && !c.InFormation) return false; return true; }
        }
        public bool FormationGone
        {
            get { foreach (var c in formation) if (c != null && c.Phase != AssistPhase.Hidden) return false; return true; }
        }

        public void Initialize(Player hero)
        {
            if (Hero != null) Hero.Combat.OnDead -= HandleHeroDeath;
            Hero = hero;
            if (hero == null) return;
            Hero.Combat.OnDead += HandleHeroDeath;
            foreach (Ally ally in hero.Party)
            {
                if (ally == null) continue;
                Control[] oldControls = ally.GetComponents<Control>();
                foreach (Control old in oldControls) old.enabled = false;
                var control = ally.GetComponent<AssistControlModule>();
                if (control == null) control = ally.gameObject.AddComponent<AssistControlModule>();
                control.enabled = true;
                control.Configure(this);
                controls[ally] = control;
                control.CancelAll();
                IgnoreBodyContacts(ally);
                ally.gameObject.SetActive(false);
            }
        }

        private static void IgnoreBodyContacts(Ally ally)
        {
            // Ignore only body collisions. Attack triggers retain their existing layer rules.
            var body = ally.GetComponent<Collider>();
            if (body == null) return;
            foreach (Entity entity in FindObjectsByType<Entity>(FindObjectsInactive.Include))
            {
                if (entity == ally) continue;
                var other = entity.GetComponent<Collider>();
                if (other != null) UnityEngine.Physics.IgnoreCollision(body, other);
            }
        }

        public CastTicket Queue(CastRequest request, System.Action<CastTicket> completed = null)
        {
            if (recalling || !request.IsValid || Hero == null || Hero.Combat.IsDead || !isActiveAndEnabled) return null;
            if (request.BulletTime && !BulletMode) return null;
            if (!controls.TryGetValue(request.Caster, out AssistControlModule control)) return null;
            var ticket = new CastTicket(request, completed);
            if (!request.BulletTime && BulletMode) deferred.Enqueue(ticket);
            else
            {
                Activate(request.Caster, control);
                if (request.BulletTime) control.PrepareSkill(request.Skill, request.TargetEntity);
                control.Enqueue(ticket);
            }
            return ticket;
        }

        private void Activate(Ally ally, AssistControlModule control)
        {
            if (control.Phase != AssistPhase.Hidden) return;
            ally.gameObject.SetActive(true);
            IgnoreBodyContacts(ally);
            Vector3 facing = Hero.Physics.Facing;
            Vector3 destination = ClampPosition(Hero.Physics.GroundPosition - facing * 1.25f);
            Vector3 spawn = ClampPosition(destination - facing * 0.75f);
            control.Appear(spawn, destination, facing);
        }

        public void BeginSelection()
        {
            BulletMode = true;
            foreach (var control in controls.Values)
            {
                control.HoldSelection();
                control.DeferAutomatic(deferred);
            }
        }

        public void PrepareFormation(IEnumerable<CastRequest> requests)
        {
            formation.Clear();
            var participants = new List<Ally>();
            foreach (CastRequest request in requests)
                if (request.IsValid && controls.ContainsKey(request.Caster) && !participants.Contains(request.Caster))
                    participants.Add(request.Caster);
            for (int i = 0; i < participants.Count; i++)
            {
                Ally ally = participants[i];
                var control = controls[ally];
                Activate(ally, control);
                float offset = (i - (participants.Count - 1) * 0.5f) * formationSpacing;
                Vector3 point = Hero.Physics.GroundPosition - Hero.Physics.Facing * 1.4f + Vector3.forward * offset;
                control.HoldFormation(ClampPosition(point));
                foreach (CastRequest request in requests)
                {
                    if (request.Caster != ally) continue;
                    control.PrepareSkill(request.Skill, request.TargetEntity);
                    break;
                }
                formation.Add(control);
            }
            foreach (var control in controls.Values)
                if (!formation.Contains(control)) control.SelectionHeld = false;
        }

        public void ReleaseFormation()
        {
            foreach (var control in formation) if (control != null) control.ReleaseFormation();
        }

        public void EndSelection()
        {
            BulletMode = false;
            formation.Clear();
            foreach (var control in controls.Values) control.SelectionHeld = false;
            while (deferred.Count > 0)
            {
                CastTicket ticket = deferred.Dequeue();
                if (!ticket.Request.IsValid) { ticket.Complete(true); continue; }
                var control = controls[ticket.Request.Caster];
                Activate(ticket.Request.Caster, control);
                control.Enqueue(ticket);
            }
        }

        public Vector3 ExitPosition(Entity ally)
            => ClampPosition(ally.Physics.GroundPosition - Hero.Physics.Facing * 0.8f);

        public Vector3 ClampPosition(Vector3 point)
        {
            StageBounds bounds = StageBounds.Instance;
            float min = float.NegativeInfinity, max = float.PositiveInfinity;
            if (bounds != null && bounds.UnitMax > bounds.UnitMin)
            { min = bounds.UnitMin + 0.4f; max = bounds.UnitMax - 0.4f; }
            Camera camera = Camera.main;
            if (camera != null && camera.orthographic)
            {
                float half = camera.orthographicSize * camera.aspect;
                min = Mathf.Max(min, camera.transform.position.x - half + 0.6f);
                max = Mathf.Min(max, camera.transform.position.x + half - 0.6f);
            }
            point.x = min <= max ? Mathf.Clamp(point.x, min, max) : (min + max) * 0.5f;
            // Search back toward the hero if the proposed point lies past a platform edge.
            if (Hero != null && GroundRegistry.HasPlates)
            {
                Vector3 heroPoint = Hero.Physics.GroundPosition;
                point.y = heroPoint.y;
                for (int i = 0; i <= 20; i++)
                {
                    Vector3 candidate = Vector3.Lerp(point, heroPoint, i / 20f);
                    if (!GroundRegistry.TryHeightAt(candidate, -0.15f, out float y)) continue;
                    candidate.y = y;
                    return candidate;
                }
                return heroPoint;
            }
            return point;
        }

        public void Recycle(AssistControlModule control)
        {
            control.CancelAll();
            control.gameObject.SetActive(false);
        }

        public void RecallAll()
        {
            if (recalling) return;
            recalling = true;
            BulletMode = false;
            formation.Clear();
            // Snapshot pending tickets before invoking callbacks which may touch the deck.
            var cancelled = deferred.ToArray();
            deferred.Clear();
            foreach (var control in controls.Values) if (control != null) Recycle(control);
            foreach (var ticket in cancelled) ticket.Complete(true);
            recalling = false;
        }

        private void HandleHeroDeath() => GetComponent<CastDirector>()?.Abort();
        private void OnDisable() => RecallAll();
        private void OnDestroy() { if (Hero != null) Hero.Combat.OnDead -= HandleHeroDeath; }
    }
}

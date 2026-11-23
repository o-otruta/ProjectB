using UnityEngine;
using ProjectB.Combat;

namespace ProjectB.Abilities
{
    /// <summary>
    /// Holographic decoy spawned by <see cref="ShadowDecoyAbility"/>. Acts as an attack target for taunted
    /// enemies (absorbs contact damage), pulses taunt and detonates when its lifetime ends.
    /// </summary>
    public class ShadowDecoyEntity : MonoBehaviour, IDamageable
    {
        private const float HOVER_AMPLITUDE = 0.12f;
        private const float HOVER_SPEED = 4f;

        private ShadowDecoyAbility owner;
        private float remaining;
        private float nextTauntTime;
        private bool detonated = true;
        private Vector3 groundPosition;
        private Transform visual;

        public bool IsDead => detonated;
        public float Remaining => remaining;
        public bool IsActive => !detonated;
        public int AbsorbedHits { get; private set; }

        public void Activate(ShadowDecoyAbility owner, Vector3 position, float lifetime)
        {
            this.owner = owner;
            remaining = lifetime;
            nextTauntTime = 0f;
            detonated = false;
            AbsorbedHits = 0;
            groundPosition = position;
            transform.position = position;
            if (visual == null && transform.childCount > 0) visual = transform.GetChild(0);
            gameObject.SetActive(true);
        }

        public void TakeDamage(int amount)
        {
            if (!detonated && amount > 0) AbsorbedHits++;
        }

        private void Update()
        {
            Step(Time.deltaTime);
        }

        public void Step(float dt)
        {
            if (detonated || dt <= 0f) return;

            remaining -= dt;
            if (visual != null)
            {
                visual.localPosition = new Vector3(0f, Mathf.Sin(Time.time * HOVER_SPEED) * HOVER_AMPLITUDE, 0f);
                visual.Rotate(0f, 90f * dt, 0f, Space.Self);
            }

            if (Time.time >= nextTauntTime && owner != null)
            {
                nextTauntTime = Time.time + owner.TauntPulseInterval;
                owner.PulseTaunt(this);
            }

            if (remaining <= 0f)
            {
                Detonate();
            }
        }

        public void Detonate()
        {
            if (detonated) return;
            detonated = true;
            owner?.HandleDecoyDetonated(this, groundPosition);
            gameObject.SetActive(false);
        }

        public void ForceDeactivate()
        {
            detonated = true;
            gameObject.SetActive(false);
        }
    }
}

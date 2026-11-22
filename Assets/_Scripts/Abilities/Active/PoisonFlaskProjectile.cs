using System;
using UnityEngine;
using UnityEngine.Pool;

namespace ProjectB.Abilities
{
    public class PoisonFlaskProjectile : MonoBehaviour
    {
        private Vector3 startPos;
        private Vector3 endPos;
        private float flightDuration;
        private float elapsed;
        private bool isReturned;
        private Action<Vector3> onLanded;
        private IObjectPool<PoisonFlaskProjectile> pool;

        public void Initialize(
            Vector3 startPos,
            Vector3 targetPos,
            float flightDuration,
            Action<Vector3> onLanded,
            IObjectPool<PoisonFlaskProjectile> pool)
        {
            this.startPos = startPos;
            this.endPos = new Vector3(targetPos.x, 0.05f, targetPos.z);
            this.flightDuration = Mathf.Max(0.05f, flightDuration);
            this.elapsed = 0f;
            this.onLanded = onLanded;
            this.pool = pool;
            this.isReturned = false;

            transform.position = startPos;
        }

        private void Update()
        {
            if (isReturned || Time.deltaTime <= 0f) return;

            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / flightDuration);

            Vector3 pos = Vector3.Lerp(startPos, endPos, t);
            float arcHeight = 2.4f * 4f * t * (1f - t);
            pos.y += arcHeight;
            transform.position = pos;
            transform.Rotate(360f * Time.deltaTime, 180f * Time.deltaTime, 0f);

            if (t >= 1f)
            {
                Land();
            }
        }

        public void ForceLand()
        {
            if (isReturned) return;
            Land();
        }

        private void Land()
        {
            var cb = onLanded;
            onLanded = null;
            cb?.Invoke(endPos);
            ReturnToPool();
        }

        public void ReturnToPool()
        {
            if (isReturned) return;
            isReturned = true;

            if (pool != null)
            {
                pool.Release(this);
            }
            else
            {
                gameObject.SetActive(false);
            }
        }
    }
}

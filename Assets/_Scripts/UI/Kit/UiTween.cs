using UnityEngine;
using UnityEngine.UI;

namespace ProjectB.UI
{
    public enum UiEase
    {
        Linear = 0,
        OutCubic = 1,
        InCubic = 2,
        OutBack = 3,
        InOutSine = 4
    }

    /// <summary>
    /// Tiny Zero-GC tween helper for UI (Sprint 13.1): scale / fade / move / punch / shake / colour on unscaled time,
    /// so it keeps running while <c>timeScale = 0</c> (Paused / LevelUp / GameOver). Tweens live in a pre-allocated
    /// struct array inside a hidden runner; starting a tween on a target replaces the previous tween of the same kind.
    /// Outside Play Mode every tween snaps to its end value immediately.
    /// </summary>
    public static class UiTween
    {
        internal enum Kind : byte
        {
            Scale = 0,
            Fade = 1,
            Move = 2,
            Punch = 3,
            Shake = 4,
            Color = 5
        }

        internal struct Tween
        {
            public bool alive;
            public Kind kind;
            public Object target;
            public Transform transform;
            public RectTransform rect;
            public CanvasGroup group;
            public Graphic graphic;
            public Vector4 from;
            public Vector4 to;
            public float duration;
            public float delay;
            public float elapsed;
            public float strength;
            public UiEase ease;
            public bool disableOnComplete;
        }

        private static UiTweenRunner runner;

        public static int ActiveCount => runner != null ? runner.ActiveCount : 0;

        public static void Scale(Transform target, Vector3 from, Vector3 to, float duration, float delay = 0f, UiEase ease = UiEase.OutBack)
        {
            if (target == null) return;
            if (!TryGetRunner(out UiTweenRunner r) || duration <= 0f)
            {
                target.localScale = to;
                return;
            }
            target.localScale = from;
            r.Add(new Tween
            {
                kind = Kind.Scale,
                target = target,
                transform = target,
                from = from,
                to = to,
                duration = duration,
                delay = delay,
                ease = ease
            });
        }

        public static void Fade(CanvasGroup group, float from, float to, float duration, float delay = 0f, bool disableOnComplete = false)
        {
            if (group == null) return;
            if (!TryGetRunner(out UiTweenRunner r) || duration <= 0f)
            {
                group.alpha = to;
                if (disableOnComplete && to <= 0.001f) group.gameObject.SetActive(false);
                return;
            }
            group.alpha = from;
            r.Add(new Tween
            {
                kind = Kind.Fade,
                target = group,
                group = group,
                from = new Vector4(from, 0f, 0f, 0f),
                to = new Vector4(to, 0f, 0f, 0f),
                duration = duration,
                delay = delay,
                ease = UiEase.OutCubic,
                disableOnComplete = disableOnComplete
            });
        }

        public static void Move(RectTransform rect, Vector2 from, Vector2 to, float duration, float delay = 0f, UiEase ease = UiEase.OutCubic)
        {
            if (rect == null) return;
            if (!TryGetRunner(out UiTweenRunner r) || duration <= 0f)
            {
                rect.anchoredPosition = to;
                return;
            }
            rect.anchoredPosition = from;
            r.Add(new Tween
            {
                kind = Kind.Move,
                target = rect,
                rect = rect,
                from = from,
                to = to,
                duration = duration,
                delay = delay,
                ease = ease
            });
        }

        /// <summary>Scale punch around the current resting scale (e.g. 1 → 1+strength → 1).</summary>
        public static void Punch(Transform target, float strength = 0.12f, float duration = 0.22f)
        {
            if (target == null) return;
            if (!TryGetRunner(out UiTweenRunner r) || duration <= 0f) return;
            Vector3 rest = r.GetRestScale(target);
            r.Add(new Tween
            {
                kind = Kind.Punch,
                target = target,
                transform = target,
                from = rest,
                to = rest,
                strength = strength,
                duration = duration
            });
        }

        /// <summary>Horizontal shake (e.g. "not enough coins").</summary>
        public static void Shake(RectTransform rect, float amplitude = 14f, float duration = 0.3f)
        {
            if (rect == null) return;
            if (!TryGetRunner(out UiTweenRunner r) || duration <= 0f) return;
            Vector2 rest = r.GetRestPosition(rect);
            r.Add(new Tween
            {
                kind = Kind.Shake,
                target = rect,
                rect = rect,
                from = rest,
                to = rest,
                strength = amplitude,
                duration = duration
            });
        }

        public static void Color(Graphic graphic, Color from, Color to, float duration, float delay = 0f)
        {
            if (graphic == null) return;
            if (!TryGetRunner(out UiTweenRunner r) || duration <= 0f)
            {
                graphic.color = to;
                return;
            }
            graphic.color = from;
            r.Add(new Tween
            {
                kind = Kind.Color,
                target = graphic,
                graphic = graphic,
                from = from,
                to = to,
                duration = duration,
                delay = delay,
                ease = UiEase.OutCubic
            });
        }

        /// <summary>Stops every tween on <paramref name="target"/>, snapping it to its end / resting value.</summary>
        public static void Kill(Object target, bool complete = true)
        {
            if (runner != null && target != null) runner.Kill(target, complete);
        }

        public static bool IsTweening(Object target)
        {
            return runner != null && target != null && runner.IsTweening(target);
        }

        /// <summary>Snaps every running tween to its end value (screenshots, tests, scene unload).</summary>
        public static void CompleteAll()
        {
            if (runner != null) runner.CompleteAll();
        }

        public static float Evaluate(UiEase ease, float t)
        {
            t = Mathf.Clamp01(t);
            switch (ease)
            {
                case UiEase.OutCubic:
                {
                    float inv = 1f - t;
                    return 1f - inv * inv * inv;
                }
                case UiEase.InCubic:
                    return t * t * t;
                case UiEase.OutBack:
                {
                    const float c1 = 1.70158f;
                    const float c3 = c1 + 1f;
                    float u = t - 1f;
                    return 1f + c3 * u * u * u + c1 * u * u;
                }
                case UiEase.InOutSine:
                    return -(Mathf.Cos(Mathf.PI * t) - 1f) * 0.5f;
                default:
                    return t;
            }
        }

        private static bool TryGetRunner(out UiTweenRunner r)
        {
            r = null;
            if (!Application.isPlaying) return false;
            if (runner == null)
            {
                var go = new GameObject("[UiTweenRunner]");
                go.hideFlags = HideFlags.HideAndDontSave;
                Object.DontDestroyOnLoad(go);
                runner = go.AddComponent<UiTweenRunner>();
            }
            r = runner;
            return true;
        }

        [DefaultExecutionOrder(1000)]
        internal sealed class UiTweenRunner : MonoBehaviour
        {
            private Tween[] tweens = new Tween[64];
            private int count;

            public int ActiveCount => count;

            public void Add(Tween tween)
            {
                tween.alive = true;
                // Replace a running tween of the same kind on the same target (no duplicates, no growth).
                for (int i = 0; i < count; i++)
                {
                    if (tweens[i].target == tween.target && tweens[i].kind == tween.kind)
                    {
                        if (tween.kind == Kind.Punch || tween.kind == Kind.Shake)
                        {
                            tween.from = tweens[i].from;
                            tween.to = tweens[i].to;
                        }
                        tweens[i] = tween;
                        return;
                    }
                }

                if (count == tweens.Length)
                {
                    System.Array.Resize(ref tweens, tweens.Length * 2);
                }
                tweens[count++] = tween;
            }

            public Vector3 GetRestScale(Transform t)
            {
                for (int i = 0; i < count; i++)
                {
                    if (tweens[i].transform == t && tweens[i].kind == Kind.Punch) return tweens[i].from;
                    if (tweens[i].transform == t && tweens[i].kind == Kind.Scale) return tweens[i].to;
                }
                return t.localScale;
            }

            public Vector2 GetRestPosition(RectTransform rt)
            {
                for (int i = 0; i < count; i++)
                {
                    if (tweens[i].rect == rt && tweens[i].kind == Kind.Shake) return tweens[i].from;
                    if (tweens[i].rect == rt && tweens[i].kind == Kind.Move) return tweens[i].to;
                }
                return rt.anchoredPosition;
            }

            public bool IsTweening(Object target)
            {
                for (int i = 0; i < count; i++)
                {
                    if (tweens[i].target == target) return true;
                }
                return false;
            }

            public void Kill(Object target, bool complete)
            {
                for (int i = count - 1; i >= 0; i--)
                {
                    if (tweens[i].target != target) continue;
                    if (complete) Apply(ref tweens[i], 1f, true);
                    RemoveAt(i);
                }
            }

            public void CompleteAll()
            {
                for (int i = count - 1; i >= 0; i--)
                {
                    if (tweens[i].target != null)
                    {
                        Apply(ref tweens[i], 1f, true);
                        if (tweens[i].disableOnComplete && tweens[i].group != null) tweens[i].group.gameObject.SetActive(false);
                    }
                    RemoveAt(i);
                }
            }

            private void RemoveAt(int index)
            {
                count--;
                tweens[index] = tweens[count];
                tweens[count] = default;
            }

            private void Update()
            {
                float dt = Time.unscaledDeltaTime;
                for (int i = count - 1; i >= 0; i--)
                {
                    ref Tween tw = ref tweens[i];
                    if (tw.target == null)
                    {
                        RemoveAt(i);
                        continue;
                    }

                    if (tw.delay > 0f)
                    {
                        tw.delay -= dt;
                        if (tw.delay > 0f) continue;
                        dt = -tw.delay;
                        tw.delay = 0f;
                    }

                    tw.elapsed += dt;
                    float t = tw.duration > 0f ? Mathf.Clamp01(tw.elapsed / tw.duration) : 1f;
                    bool done = t >= 1f;
                    Apply(ref tw, t, done);
                    if (done)
                    {
                        bool disable = tw.disableOnComplete && tw.group != null;
                        GameObject toDisable = disable ? tw.group.gameObject : null;
                        RemoveAt(i);
                        if (toDisable != null) toDisable.SetActive(false);
                    }
                    dt = Time.unscaledDeltaTime;
                }
            }

            private static void Apply(ref Tween tw, float t, bool done)
            {
                switch (tw.kind)
                {
                    case Kind.Scale:
                        if (tw.transform != null)
                        {
                            float e = done ? 1f : Evaluate(tw.ease, t);
                            tw.transform.localScale = Vector3.LerpUnclamped(tw.from, tw.to, e);
                        }
                        break;
                    case Kind.Fade:
                        if (tw.group != null)
                        {
                            float e = done ? 1f : Evaluate(tw.ease, t);
                            tw.group.alpha = Mathf.LerpUnclamped(tw.from.x, tw.to.x, e);
                        }
                        break;
                    case Kind.Move:
                        if (tw.rect != null)
                        {
                            float e = done ? 1f : Evaluate(tw.ease, t);
                            tw.rect.anchoredPosition = Vector2.LerpUnclamped(tw.from, tw.to, e);
                        }
                        break;
                    case Kind.Punch:
                        if (tw.transform != null)
                        {
                            float k = done ? 0f : Mathf.Sin(t * Mathf.PI) * (1f - t * 0.35f);
                            tw.transform.localScale = (Vector3)tw.from * (1f + tw.strength * k);
                        }
                        break;
                    case Kind.Shake:
                        if (tw.rect != null)
                        {
                            float k = done ? 0f : Mathf.Sin(t * Mathf.PI * 7f) * (1f - t);
                            tw.rect.anchoredPosition = (Vector2)tw.from + new Vector2(tw.strength * k, 0f);
                        }
                        break;
                    case Kind.Color:
                        if (tw.graphic != null)
                        {
                            float e = done ? 1f : Evaluate(tw.ease, t);
                            tw.graphic.color = UnityEngine.Color.LerpUnclamped(tw.from, tw.to, e);
                        }
                        break;
                }
            }
        }
    }
}

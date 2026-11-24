using UnityEngine;
using ProjectB.Combat;
using ProjectB.Data;
using ProjectB.Meta;
using ProjectB.UI;
using ProjectB.Visuals;
using VContainer;

namespace ProjectB.Player
{
    public class HeroMovement : MonoBehaviour, ISlowable
    {
        [SerializeField] private HeroData heroData;
        private VirtualJoystick joystick;
        
        [Tooltip("Модель персонажа (должна быть дочерним объектом), чтобы вращать только визуал, не трогая физику")]
        [SerializeField] private Transform visualModel;

        [Tooltip("Покадровый аниматор воксельной модели героя")]
        [SerializeField] private VoxelMeshAnimator meshAnimator;
        
        private HeroHealth hp;
        private Rigidbody rb;
        private HeroRuntimeStats runtimeStats;
        private SelectedHeroService selectedHeroService;
        private HeroData activeHeroData;
        private float baseSpeed = 5f;
        private float moveSpeed;
        private float slowFactor = 1f;
        private float slowTimer = 0f;

        public float CurrentSlowFactor => slowTimer > 0f ? slowFactor : 1f;
        public bool IsSlowed => slowTimer > 0f;
        public float EffectiveMoveSpeed => moveSpeed * CurrentSlowFactor;

        [Inject]
        public void Construct(VirtualJoystick joystick, HeroRuntimeStats runtimeStats, SelectedHeroService selectedHeroService = null)
        {
            this.joystick = joystick;
            this.runtimeStats = runtimeStats;
            this.selectedHeroService = selectedHeroService;
        }

        void Start()
        {
            hp = GetComponent<HeroHealth>();
            rb = GetComponent<Rigidbody>();
            activeHeroData = selectedHeroService?.GetSelectedHero() ?? heroData;
            baseSpeed = activeHeroData != null ? activeHeroData.moveSpeed : 5f;

            if (runtimeStats != null && activeHeroData != null && activeHeroData.visualModelPrefab != null && visualModel != null)
            {
                for (int i = 0; i < visualModel.childCount; i++)
                {
                    Transform child = visualModel.GetChild(i);
                    if (child.name != "FirePoint")
                    {
                        child.gameObject.SetActive(false);
                    }
                }

                GameObject modelInstance = Instantiate(activeHeroData.visualModelPrefab, visualModel);
                modelInstance.transform.localPosition = Vector3.zero;
                modelInstance.transform.localRotation = Quaternion.identity;
                modelInstance.transform.localScale = Vector3.one;
                meshAnimator = modelInstance.GetComponentInChildren<VoxelMeshAnimator>();
                hp?.RefreshRenderers();
            }

            if (meshAnimator == null)
            {
                meshAnimator = GetComponentInChildren<VoxelMeshAnimator>();
            }

            if (runtimeStats != null)
            {
                runtimeStats.OnStatsChanged += RefreshMoveSpeed;
            }
            RefreshMoveSpeed();
        }

        private void OnDestroy()
        {
            if (runtimeStats != null)
            {
                runtimeStats.OnStatsChanged -= RefreshMoveSpeed;
            }
        }

        private void RefreshMoveSpeed()
        {
            moveSpeed = runtimeStats != null ? runtimeStats.GetMoveSpeed(baseSpeed) : baseSpeed;
        }

        void FixedUpdate()
        {
            if (joystick == null || rb == null) return;
            if (hp != null && hp.IsDead) 
            {
                rb.linearVelocity = Vector3.zero;
                return;
            }

            // Перемещение по XZ (джойстик X это мирской X, джойстик Y это мирской Z)
            Vector3 moveDir = new Vector3(joystick.Direction.x, 0f, joystick.Direction.y);
            
            // Устанавливаем скорость напрямую. 
            // Это жестко контролирует движение и сбрасывает любые силы (импульсы),
            // которые враги пытаются передать герою при столкновении.
            rb.linearVelocity = moveDir * EffectiveMoveSpeed;
        }

        void Update()
        {
            if (slowTimer > 0f && Time.deltaTime > 0f)
            {
                slowTimer -= Time.deltaTime;
                if (slowTimer <= 0f)
                {
                    slowTimer = 0f;
                    slowFactor = 1f;
                }
            }

            HeroData data = activeHeroData != null ? activeHeroData : heroData;
            if (data == null || joystick == null || Time.deltaTime <= 0f) return;
            if (hp != null && hp.IsDead)
            {
                ClearSlow();
                if (meshAnimator != null && meshAnimator.IsPlaying)
                {
                    meshAnimator.Stop();
                }
                return;
            }

            Vector3 moveDir = new Vector3(joystick.Direction.x, 0f, joystick.Direction.y);
            bool isMoving = moveDir.sqrMagnitude > 0.01f;
            
            // Плавный поворот визуальной модели в сторону движения (framerate-independent)
            if (isMoving && visualModel != null)
            {
                Quaternion targetRotation = Quaternion.LookRotation(moveDir);
                float t = 1f - Mathf.Exp(-data.rotationSpeed * Time.deltaTime);
                visualModel.rotation = Quaternion.Slerp(visualModel.rotation, targetRotation, t);
            }

            if (meshAnimator != null)
            {
                if (isMoving)
                {
                    meshAnimator.Play();
                }
                else if (meshAnimator.IsPlaying)
                {
                    meshAnimator.Stop();
                }
            }
        }

        public float MoveSpeed => moveSpeed;

        /// <summary>Visual model that turns toward the movement direction (hero facing for melee sweeps).</summary>
        public Transform VisualModel => visualModel;

        public void ApplySlow(float factor, float duration)
        {
            if (duration <= 0f) return;
            float clampedFactor = Mathf.Clamp(factor, 0.25f, 1f);
            if (slowTimer <= 0f || clampedFactor < slowFactor)
            {
                slowFactor = clampedFactor;
            }
            if (duration > slowTimer)
            {
                slowTimer = duration;
            }
        }

        public void ClearSlow()
        {
            slowFactor = 1f;
            slowTimer = 0f;
        }

        public void IncreaseMoveSpeed(float amount)
        {
            if (runtimeStats != null)
            {
                runtimeStats.AddMoveSpeed(amount);
            }
            else
            {
                moveSpeed += amount;
            }
        }
    }
}

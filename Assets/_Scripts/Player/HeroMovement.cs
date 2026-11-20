using UnityEngine;
using ProjectB.Data;
using ProjectB.Meta;
using ProjectB.UI;
using VContainer;

namespace ProjectB.Player
{
    public class HeroMovement : MonoBehaviour
    {
        [SerializeField] private HeroData heroData;
        private VirtualJoystick joystick;
        
        [Tooltip("Модель персонажа (должна быть дочерним объектом), чтобы вращать только визуал, не трогая физику")]
        [SerializeField] private Transform visualModel; 
        
        private HeroHealth hp;
        private Rigidbody rb;
        private HeroRuntimeStats runtimeStats;
        private SelectedHeroService selectedHeroService;
        private HeroData activeHeroData;
        private float baseSpeed = 5f;
        private float moveSpeed;

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
            rb.linearVelocity = moveDir * moveSpeed;
        }

        void Update()
        {
            HeroData data = activeHeroData != null ? activeHeroData : heroData;
            if (data == null || joystick == null || Time.deltaTime <= 0f) return;
            if (hp != null && hp.IsDead) return;

            Vector3 moveDir = new Vector3(joystick.Direction.x, 0f, joystick.Direction.y);
            
            // Плавный поворот визуальной модели в сторону движения (framerate-independent)
            if (moveDir.sqrMagnitude > 0.01f && visualModel != null)
            {
                Quaternion targetRotation = Quaternion.LookRotation(moveDir);
                float t = 1f - Mathf.Exp(-data.rotationSpeed * Time.deltaTime);
                visualModel.rotation = Quaternion.Slerp(visualModel.rotation, targetRotation, t);
            }
        }

        public float MoveSpeed => moveSpeed;

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

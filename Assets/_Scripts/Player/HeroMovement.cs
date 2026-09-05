using UnityEngine;
using ProjectB.Data;
using ProjectB.UI;
using VContainer;
using ProjectB.Meta;

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
        private MetaUpgradeManager metaUpgradeManager;
        private float moveSpeed;

        [Inject]
        public void Construct(VirtualJoystick joystick, MetaUpgradeManager metaManager)
        {
            this.joystick = joystick;
            this.metaUpgradeManager = metaManager;
        }

        void Start()
        {
            hp = GetComponent<HeroHealth>();
            rb = GetComponent<Rigidbody>();
            
            float speedBonus = metaUpgradeManager != null ? metaUpgradeManager.GetTotalBonus(MetaUpgradeEffectType.HeroSpeed) : 0f;
            float baseSpeed = heroData != null ? heroData.moveSpeed : 5f;
            moveSpeed = baseSpeed * (1f + speedBonus / 100f);
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
            if (heroData == null || joystick == null) return;
            if (hp != null && hp.IsDead) return;

            Vector3 moveDir = new Vector3(joystick.Direction.x, 0f, joystick.Direction.y);
            
            // Плавный поворот визуальной модели в сторону движения
            if (moveDir.sqrMagnitude > 0.01f && visualModel != null)
            {
                Quaternion targetRotation = Quaternion.LookRotation(moveDir);
                visualModel.rotation = Quaternion.Slerp(visualModel.rotation, targetRotation, heroData.rotationSpeed * Time.deltaTime);
            }
        }

        public float MoveSpeed => moveSpeed;

        public void IncreaseMoveSpeed(float amount)
        {
            moveSpeed += amount;
            Debug.Log($"[HeroMovement] MoveSpeed increased by {amount}. New MoveSpeed: {moveSpeed}");
        }
    }
}

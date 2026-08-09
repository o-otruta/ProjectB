using UnityEngine;
using ProjectB.Data;
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

        [Inject]
        public void Construct(VirtualJoystick joystick)
        {
            this.joystick = joystick;
        }

        void Start()
        {
            hp = GetComponent<HeroHealth>();
        }

        void Update()
        {
            if (heroData == null || joystick == null) return;
            if (hp != null && hp.IsDead) return;

            // Перемещение по XZ (джойстик X это мирской X, джойстик Y это мирской Z)
            Vector3 moveDir = new Vector3(joystick.Direction.x, 0f, joystick.Direction.y);
            
            transform.position += moveDir * (heroData.moveSpeed * Time.deltaTime);
            
            // Плавный поворот визуальной модели в сторону движения
            if (moveDir.sqrMagnitude > 0.01f && visualModel != null)
            {
                Quaternion targetRotation = Quaternion.LookRotation(moveDir);
                visualModel.rotation = Quaternion.Slerp(visualModel.rotation, targetRotation, heroData.rotationSpeed * Time.deltaTime);
            }
        }
    }
}

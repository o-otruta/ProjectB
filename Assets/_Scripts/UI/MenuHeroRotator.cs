using UnityEngine;

namespace ProjectB.UI
{
    public class MenuHeroRotator : MonoBehaviour
    {
        public float speed = 30f;

        private void Update()
        {
            transform.Rotate(0, speed * Time.deltaTime, 0);
        }
    }
}

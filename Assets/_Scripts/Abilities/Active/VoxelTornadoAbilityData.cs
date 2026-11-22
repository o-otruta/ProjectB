using UnityEngine;

namespace ProjectB.Abilities
{
    [CreateAssetMenu(fileName = "NewVoxelTornadoAbility", menuName = "ProjectB/Data/Abilities/Voxel Tornado Ability")]
    public class VoxelTornadoAbilityData : ActiveAbilityData
    {
        [Header("Voxel Tornado Specs")]
        public Material tornadoMaterial;
        public float vortexRadius = 2.2f;
        public float duration = 4f;
        public int tornadoCount = 1;
        public float moveSpeed = 4.2f;
        public float pullForce = 4.5f;
        public float tickInterval = 0.25f;
        public float waveAmplitude = 1.6f;
        public float waveFrequency = 2.5f;

        public override AbilityBase CreateAbility(Transform parent)
        {
            var go = new GameObject($"Ability_{id}");
            go.transform.SetParent(parent, false);
            return go.AddComponent<VoxelTornadoAbility>();
        }
    }
}

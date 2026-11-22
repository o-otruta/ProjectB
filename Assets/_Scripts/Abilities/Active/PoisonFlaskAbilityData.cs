using UnityEngine;

namespace ProjectB.Abilities
{
    [CreateAssetMenu(fileName = "NewPoisonFlaskAbility", menuName = "ProjectB/Data/Abilities/Poison Flask Ability")]
    public class PoisonFlaskAbilityData : ActiveAbilityData
    {
        [Header("Poison Flask Specs")]
        public GameObject flaskPrefab;
        public Material puddleMaterial;
        public float puddleRadius = 2.5f;
        public float puddleDuration = 4f;
        public float tickInterval = 0.4f;
        public int flaskCount = 1;
        public float throwRange = 10f;
        public float flightDuration = 0.45f;
        public float slowFactor = 0.8f;

        public override AbilityBase CreateAbility(Transform parent)
        {
            var go = new GameObject($"Ability_{id}");
            go.transform.SetParent(parent, false);
            return go.AddComponent<PoisonFlaskAbility>();
        }
    }
}

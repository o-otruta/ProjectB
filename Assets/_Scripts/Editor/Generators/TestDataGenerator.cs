using UnityEditor;
using UnityEngine;
using ProjectB.LevelUp;
using ProjectB.Abilities;
using System.Collections.Generic;

namespace ProjectB.EditorScripts
{
    public class TestDataGenerator
    {
        [MenuItem("Tools/Generate Test Upgrade Cards")]
        public static void GenerateCards()
        {
            // 1. Create +1 Sphere Modifier
            var countMod = ScriptableObject.CreateInstance<AbilityModifierCardData>();
            countMod.cardName = "Orbital Sword: +1 Sphere";
            countMod.description = "Adds one extra orbiting sphere.";
            countMod.targetAbilityId = "orbital_sword";
            countMod.modifierType = ModifierType.Count;
            countMod.value = 1;
            AssetDatabase.CreateAsset(countMod, "Assets/_Scripts/Abilities/Data/OrbitalSword_Plus1Sphere.asset");

            // 2. Create +10 Damage Modifier
            var dmgMod = ScriptableObject.CreateInstance<AbilityModifierCardData>();
            dmgMod.cardName = "Orbital Sword: +10 Damage";
            dmgMod.description = "Increases damage by 10.";
            dmgMod.targetAbilityId = "orbital_sword";
            dmgMod.modifierType = ModifierType.Damage;
            dmgMod.value = 10;
            AssetDatabase.CreateAsset(dmgMod, "Assets/_Scripts/Abilities/Data/OrbitalSword_Plus10Damage.asset");

            // 3. Create Stat Upgrade Card (+20 Max HP)
            var hpMod = ScriptableObject.CreateInstance<StatUpgradeCardData>();
            hpMod.cardName = "+20 Max HP";
            hpMod.description = "Increases maximum health by 20.";
            hpMod.statType = StatType.MaxHP;
            hpMod.value = 20;
            AssetDatabase.CreateAsset(hpMod, "Assets/_Scripts/LevelUp/Cards/Stat_MaxHP.asset");

            AssetDatabase.SaveAssets();
            Debug.Log("Test cards generated! Please manually assign the modifiers to 'unlockedModifiers' in your OrbitalSwordCard asset.");
        }
    }
}

using UnityEngine;
using UnityEditor;
using ProjectB.Abilities;
using ProjectB.LevelUp;

public class AbilitiesGenerator
{
    [MenuItem("Tools/Generate Abilities Data")]
    public static void GenerateAll()
    {
        CreateAbilityData<ShooterAbilityData>("Shooter", "Shooter", "Periodically fires homing projectiles at nearby enemies.", AbilityType.Active, CardRarity.Common, CardRarity.Common);
        CreateAbilityData<FireballAbilityData>("Fireball", "Fireball", "Launches explosive fireballs that deal area damage.", AbilityType.Active, CardRarity.Rare, CardRarity.Rare);
        CreateAbilityData<IceAuraAbilityData>("IceAura", "Ice Aura", "Slows and damages nearby enemies around you.", AbilityType.Active, CardRarity.Rare, CardRarity.Rare);
        CreateAbilityData<BoomerangAbilityData>("Boomerang", "Boomerang", "Throws piercing boomerangs that return to you.", AbilityType.Active, CardRarity.Rare, CardRarity.Rare);
        CreateAbilityData<LaserTurretAbilityData>("LaserTurret", "Laser Turret", "Deploys a stationary turret that fires a continuous laser beam.", AbilityType.Active, CardRarity.Epic, CardRarity.Epic);
        CreateAbilityData<BlackHoleAbilityData>("BlackHole", "Black Hole", "Pulls enemies inward and deals periodic damage.", AbilityType.Active, CardRarity.Legendary, CardRarity.Epic);

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("Ability assets generated!");
    }

    private static void CreateAbilityData<T>(
        string id,
        string abilityName,
        string desc,
        AbilityType type,
        CardRarity cardRarity,
        CardRarity modRarity) where T : AbilityData
    {
        string dataPath = $"Assets/_ScriptableObjects/Abilities/{id}Data.asset";
        T data = AssetDatabase.LoadAssetAtPath<T>(dataPath);
        if (data == null)
        {
            data = ScriptableObject.CreateInstance<T>();
            AssetDatabase.CreateAsset(data, dataPath);
        }
        
        data.id = id;
        data.abilityName = abilityName;
        data.description = desc;
        data.type = type;

        EditorUtility.SetDirty(data);

        // Card
        string cardPath = $"Assets/_ScriptableObjects/Cards/{id}Card.asset";
        AbilityCardData card = AssetDatabase.LoadAssetAtPath<AbilityCardData>(cardPath);
        if (card == null)
        {
            card = ScriptableObject.CreateInstance<AbilityCardData>();
            AssetDatabase.CreateAsset(card, cardPath);
        }

        card.cardName = abilityName;
        card.description = desc;
        card.abilityData = data;
        card.rarity = cardRarity;
        card.isConsumable = true;
        
        EditorUtility.SetDirty(card);

        // Modifiers (1 for now)
        string modPath = $"Assets/_ScriptableObjects/Cards/{id}Mod_Damage.asset";
        AbilityModifierCardData mod = AssetDatabase.LoadAssetAtPath<AbilityModifierCardData>(modPath);
        if (mod == null)
        {
            mod = ScriptableObject.CreateInstance<AbilityModifierCardData>();
            AssetDatabase.CreateAsset(mod, modPath);
        }

        mod.cardName = $"{abilityName}: +5 Damage";
        mod.description = $"Increases {abilityName} damage by 5.";
        mod.rarity = modRarity;
        mod.targetAbilityId = id;
        mod.modifierType = ModifierType.Damage;
        mod.value = 5f;
        
        EditorUtility.SetDirty(mod);

        // Connect mod to card unlocks
        if (card.unlockedModifiers == null) card.unlockedModifiers = new System.Collections.Generic.List<CardData>();
        if (!card.unlockedModifiers.Contains(mod))
        {
            card.unlockedModifiers.Add(mod);
            EditorUtility.SetDirty(card);
        }
    }
}

using UnityEngine;
using UnityEditor;
using ProjectB.Abilities;
using ProjectB.LevelUp;

public class AbilitiesGenerator
{
    [MenuItem("Tools/Generate Abilities Data")]
    public static void GenerateAll()
    {
        CreateAbilityData<ShooterAbilityData>("Shooter", "Стрелок", "Периодически выстреливает снаряды", AbilityType.Active);
        CreateAbilityData<FireballAbilityData>("Fireball", "Огонек", "Периодически выпускает взрывающийся фаербол", AbilityType.Active);
        CreateAbilityData<IceAuraAbilityData>("IceAura", "Ледяная Аура", "Замедляет врагов вокруг и наносит урон", AbilityType.Active);
        CreateAbilityData<BoomerangAbilityData>("Boomerang", "Бумеранг", "Бросает бумеранг, который пронзает врагов", AbilityType.Active);
        CreateAbilityData<LaserTurretAbilityData>("LaserTurret", "Лазерная Турель", "Устанавливает турель с лазером", AbilityType.Active);
        CreateAbilityData<BlackHoleAbilityData>("BlackHole", "Черная Дыра", "Затягивает и уничтожает врагов", AbilityType.Active);

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("Ability assets generated!");
    }

    private static void CreateAbilityData<T>(string id, string abilityName, string desc, AbilityType type) where T : AbilityData
    {
        string dataPath = $"Assets/_Scripts/Abilities/Data/{id}Data.asset";
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
        string cardPath = $"Assets/_Scripts/LevelUp/Cards/{id}Card.asset";
        AbilityCardData card = AssetDatabase.LoadAssetAtPath<AbilityCardData>(cardPath);
        if (card == null)
        {
            card = ScriptableObject.CreateInstance<AbilityCardData>();
            AssetDatabase.CreateAsset(card, cardPath);
        }

        card.cardName = "Получить: " + abilityName;
        card.description = desc;
        card.abilityData = data;
        card.isConsumable = true;
        
        EditorUtility.SetDirty(card);

        // Modifiers (1 for now)
        string modPath = $"Assets/_Scripts/LevelUp/Cards/{id}Mod_Damage.asset";
        AbilityModifierCardData mod = AssetDatabase.LoadAssetAtPath<AbilityModifierCardData>(modPath);
        if (mod == null)
        {
            mod = ScriptableObject.CreateInstance<AbilityModifierCardData>();
            AssetDatabase.CreateAsset(mod, modPath);
        }

        mod.cardName = abilityName + ": +Урон";
        mod.description = "Увеличивает урон";
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

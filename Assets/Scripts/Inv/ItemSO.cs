// ItemSO.cs
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "InventoryItem", menuName = "ScriptableObjects/InventoryItem", order = 1)]
public class ItemSO : ScriptableObject
{
    [SerializeField] public string itemName;
    [SerializeField] public string itemDescription;
    [SerializeField] public Sprite icon;
    [SerializeField] public GameObject worldItem;
    [SerializeField] public GameObject ItemEffect;
    [Tooltip("Base spawn chance for this item. Final spawn chance is baseChance * biomeMultiplier.")]
    [SerializeField] public float spawnChance;
    [Tooltip("Which biomes this item is allowed to spawn in.")]
    [SerializeField] public List<BiomeSO> allowedBiomes;
    [Tooltip("How many of this item can stack in inventory.")]
    [SerializeField] public int maxStackSize = 1;
    [Tooltip("Select the category/type of this item.")]
    public List<ItemType> itemTypes = new List<ItemType>();
    [System.Serializable]
    public struct BiomeSpawnMultiplier
    {
        public BiomeSO biome;
        [Tooltip("Multiplier applied to base spawnChance when in this biome.")]
        public float multiplier;
    }

    [Tooltip("Overrides for biome-specific spawn rate. If a biome is not listed here, multiplier = 1.")]
    [SerializeField] public List<BiomeSpawnMultiplier> biomeMultipliers;

    /// <summary>
    /// Returns the spawn‐rate multiplier for the given biome. If not overridden, returns 1.
    /// </summary>
    public float GetSpawnMultiplier(BiomeSO biome)
    {
        if (biomeMultipliers != null)
        {
            for (int i = 0; i < biomeMultipliers.Count; i++)
            {
                if (biomeMultipliers[i].biome == biome)
                    return biomeMultipliers[i].multiplier;
            }
        }
        return 1f;
    }
}

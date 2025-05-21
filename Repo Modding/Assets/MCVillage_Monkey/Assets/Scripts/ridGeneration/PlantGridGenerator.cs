// PlantGridGenerator.cs - Modified to support multiple plant types
using UnityEngine;
using System.Collections.Generic;
using System.Linq;

namespace GridGeneration
{
    /// <summary>
    /// Plant-specific grid generator with support for multiple plant types
    /// </summary>
    public class PlantGridGenerator : BaseGridGenerator
    {
        [Header("Plant Types")]
        [SerializeField] private List<PlantTypeConfig> plantTypeConfigs = new List<PlantTypeConfig>();
        [SerializeField] private SpriteSheetManager.PlantPrefabType defaultPlantType = SpriteSheetManager.PlantPrefabType.Crop;
        [SerializeField][Range(0f, 1f)] private float plantTypeProbability = 0.5f; // Probability to use non-default plant type

        [System.Serializable]
        public class PlantTypeConfig
        {
            public string name;
            public GameObject prefab;
            public SpriteSheetManager.PlantPrefabType plantType;
            [Range(0f, 1f)] public float spawnWeight = 1.0f; // Higher weight = more likely to spawn
            public bool enabled = true;
        }

        [Header("Plant Transform Randomization")]
        public TransformRandomizer transformRandomizer = new TransformRandomizer();

        [Header("Plant Shader Settings")]
        public ShaderParameterManager shaderManager = new ShaderParameterManager();

        [Header("Plant Sprite Sheets")]
        public SpriteSheetManager spriteSheetManager = new SpriteSheetManager();

        protected override void Awake()
        {
            // Validate plant type configs on startup
            ValidatePlantConfigs();
            base.Awake();
        }

        private void ValidatePlantConfigs()
        {
            // Make sure we have at least one enabled plant type
            if (plantTypeConfigs.Count == 0 || !plantTypeConfigs.Any(p => p.enabled && p.prefab != null))
            {
                Debug.LogWarning("No valid plant prefabs configured. Using default prefab.");
            }
        }

        // Get a random plant prefab based on configured weights
        private PlantTypeConfig GetRandomPlantConfig()
        {
            // Get all enabled plant configs with valid prefabs
            var validConfigs = plantTypeConfigs.Where(c => c.enabled && c.prefab != null).ToList();

            if (validConfigs.Count == 0)
            {
                Debug.LogWarning("No valid plant configs found, using default prefab");
                return new PlantTypeConfig
                {
                    name = "Default",
                    prefab = prefab,
                    plantType = defaultPlantType
                };
            }

            // Single config case - just return it
            if (validConfigs.Count == 1)
            {
                return validConfigs[0];
            }

            // *** FIXED WEIGHTED SELECTION ALGORITHM ***

            // 1. Calculate total weight of all valid configs
            float totalWeight = validConfigs.Sum(c => c.spawnWeight);

            // 2. Generate a random value between 0 and totalWeight
            float randomPoint = Random.Range(0f, totalWeight);

            // 3. Iterate through configs, accumulating weights until we pass randomPoint
            float currentWeight = 0f;
            foreach (var config in validConfigs)
            {
                currentWeight += config.spawnWeight;
                if (randomPoint <= currentWeight)
                {
                    return config;
                }
            }

            // This should never happen if weights are positive, but just in case
            return validConfigs[0];
        }

        // Override from base class to add plant-specific behavior
        protected override GameObject InstantiateGridObject(Vector3 position, int x, int y, int z)
        {
            // Decide which plant type to use
            PlantTypeConfig selectedConfig = GetRandomPlantConfig();
            Debug.Log($"Selected plant: {selectedConfig.name}, Type: {selectedConfig.plantType}");

            // Use the selected prefab
            GameObject actualPrefab = selectedConfig.prefab != null ? selectedConfig.prefab : prefab;

            // Create the instance using the actual prefab
            GameObject instance = Instantiate(actualPrefab, instanceContainer);
            instance.transform.localPosition = position;

            // Apply transform randomization if enabled
            transformRandomizer.ApplyRandomization(instance.transform);

            // Choose appropriate sprite sheet based on plant type
            if (spriteSheetManager.useSpriteSheets && spriteSheetManager.spriteSheets.Count > 0)
            {
                spriteSheetManager.ApplySpriteSheet(instance, shaderManager, selectedConfig.plantType);
            }
            else
            {
                // Just apply shader parameters without changing the sprite
                shaderManager.RandomizeShaderParameters(instance);
            }

            instance.name = $"{selectedConfig.name}_Plant_{x}_{y}_{z}";
            return instance;
        }

        // Method to add a new plant type configuration
        public void AddPlantType(string name, GameObject prefab, SpriteSheetManager.PlantPrefabType type)
        {
            PlantTypeConfig newConfig = new PlantTypeConfig
            {
                name = name,
                prefab = prefab,
                plantType = type,
                enabled = true,
                spawnWeight = 1.0f
            };

            plantTypeConfigs.Add(newConfig);
        }
    }
}
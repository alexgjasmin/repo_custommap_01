// CustomGridGenerator.cs - Flexible grid generator that works with multiple object types
using UnityEngine;
using System.Collections.Generic;

namespace GridGeneration
{

    /// <summary>
    /// Flexible grid generator that can work with multiple object types
    /// </summary>
    public class CustomGridGenerator : BaseGridGenerator
    {
        [System.Serializable]
        public class GridObjectTypeEntry
        {
            public string typeName;
            public GameObject prefab;
            public bool enabled = true;
            public float spawnProbability = 1.0f; // Between 0 and 1
            public TransformRandomizer transformRandomizer = new TransformRandomizer();
            public ShaderParameterManager shaderManager = new ShaderParameterManager();
            public SpriteSheetManager spriteSheetManager = new SpriteSheetManager();

            public bool UseSpawnProbability => spawnProbability < 1.0f;

            public bool ShouldSpawn()
            {
                if (!enabled) return false;
                if (!UseSpawnProbability) return true;
                return Random.value <= spawnProbability;
            }
        }

        [Header("Object Types")]
        public List<GridObjectTypeEntry> objectTypes = new List<GridObjectTypeEntry>();

        public override void GenerateGrid()
        {
            if (objectTypes.Count == 0)
            {
                Debug.LogError("No object types defined for generation!");
                return;
            }

            // Filter enabled object types
            List<GridObjectTypeEntry> enabledTypes = objectTypes.FindAll(t => t.enabled && t.prefab != null);

            if (enabledTypes.Count == 0)
            {
                Debug.LogError("No enabled object types with valid prefabs!");
                return;
            }

            // Set random seed for deterministic generation if needed
            if (!useRandomSeed)
            {
                Random.InitState(seed);
            }

            // Clear existing instances if needed
            if (clearOnGenerate)
            {
                ClearGrid();
            }

            // Create container if it doesn't exist
            if (instanceContainer == null)
            {
                GameObject container = new GameObject("GridContainer");
                container.transform.parent = this.transform;
                container.transform.localPosition = Vector3.zero;
                instanceContainer = container.transform;
            }

            // Calculate offsets for centering
            Vector3 offset = CalculateGridOffset();

            // Generate instances
            for (int x = 0; x < gridSizeX; x++)
            {
                for (int y = 0; y < gridSizeY; y++)
                {
                    for (int z = 0; z < gridSizeZ; z++)
                    {
                        Vector3 position = new Vector3(
                            x * spacing + offset.x,
                            y * spacing + offset.y,
                            z * spacing + offset.z
                        );

                        // Choose a random enabled object type that passes the spawn probability check
                        List<GridObjectTypeEntry> validTypes = enabledTypes.FindAll(t => t.ShouldSpawn());

                        if (validTypes.Count > 0)
                        {
                            GridObjectTypeEntry objectType = validTypes[Random.Range(0, validTypes.Count)];
                            InstantiateCustomGridObject(position, x, y, z, objectType);
                        }
                    }
                }
            }

            // Reset the seed after generation if we're using a custom seed
            if (!useRandomSeed)
            {
                Random.InitState((int)System.DateTime.Now.Ticks);
            }
        }

        protected GameObject InstantiateCustomGridObject(Vector3 position, int x, int y, int z, GridObjectTypeEntry objectType)
        {
            GameObject instance = Instantiate(objectType.prefab, instanceContainer);
            instance.name = $"{objectType.typeName}_{x}_{y}_{z}";
            instance.transform.localPosition = position;

            // Apply transform randomization if enabled
            objectType.transformRandomizer.ApplyRandomization(instance.transform);

            // Apply sprite sheet if enabled
            if (objectType.spriteSheetManager.useSpriteSheets &&
                objectType.spriteSheetManager.spriteSheets.Count > 0)
            {
                objectType.spriteSheetManager.ApplySpriteSheet(instance, objectType.shaderManager);
            }
            else
            {
                // Just apply shader parameters without changing the sprite
                objectType.shaderManager.RandomizeShaderParameters(instance);
            }

            return instance;
        }

        /// <summary>
        /// Add a new object type to the generator
        /// </summary>
        public void AddObjectType(string name, GameObject prefab)
        {
            GridObjectTypeEntry newType = new GridObjectTypeEntry
            {
                typeName = name,
                prefab = prefab,
                enabled = true
            };

            objectTypes.Add(newType);
        }
    }
}
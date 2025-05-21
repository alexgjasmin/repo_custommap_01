// BaseGridGenerator.cs - Base abstract class for all grid generators
using UnityEngine;
using System.Collections.Generic;

namespace GridGeneration
{

    /// <summary>
    /// Base abstract class for all grid generators
    /// </summary>
    public abstract class BaseGridGenerator : MonoBehaviour
    {
        [Header("Grid Settings")]
        public GameObject prefab;
        public int gridSizeX = 5;
        public int gridSizeY = 1;
        public int gridSizeZ = 5;
        public float spacing = 1.0f;

        [Header("Auto Generation")]
        public bool generateOnWake = true;
        public bool generateInEditor = false;

        [Header("Generation")]
        public bool alignToCenter = true;
        public bool clearOnGenerate = true;
        public bool useRandomSeed = true;
        public int seed = 12345;

        [Header("Visualization")]
        public bool showGridBounds = true;
        public Color gridBoundsColor = new Color(0.2f, 0.8f, 0.2f, 1f);
        public float gridLineThickness = 2f;

        // Parent object to hold all instances
        protected Transform instanceContainer;
        protected bool hasGenerated = false;

        protected virtual void Awake()
        {
            if (generateOnWake)
            {
                GenerateGrid();
            }
        }

        protected virtual void OnEnable()
        {
#if UNITY_EDITOR
            // Subscribe to enter play mode event
            if (generateOnWake && generateInEditor && !Application.isPlaying)
            {
                UnityEditor.EditorApplication.update += OnEditorUpdate;
            }
#endif
        }

        protected virtual void OnDisable()
        {
#if UNITY_EDITOR
            // Unsubscribe from enter play mode event
            if (!Application.isPlaying)
            {
                UnityEditor.EditorApplication.update -= OnEditorUpdate;
            }
#endif
        }

#if UNITY_EDITOR
        protected virtual void OnEditorUpdate()
        {
            // Generate only once in editor mode
            if (!hasGenerated && generateInEditor && !Application.isPlaying)
            {
                GenerateGrid();
                hasGenerated = true;

                // Unsubscribe after generation
                UnityEditor.EditorApplication.update -= OnEditorUpdate;
            }
        }

        public void ResetGenerationFlag()
        {
            hasGenerated = false;
        }
#endif

        /// <summary>
        /// Generate the grid of instances
        /// </summary>
        public virtual void GenerateGrid()
        {
            if (prefab == null)
            {
                Debug.LogError("No prefab assigned to generate!");
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

                        InstantiateGridObject(position, x, y, z);
                    }
                }
            }

            // Reset the seed after generation if we're using a custom seed
            if (!useRandomSeed)
            {
                Random.InitState((int)System.DateTime.Now.Ticks);
            }
        }

        /// <summary>
        /// Instantiate a single grid object at the specified position
        /// </summary>
        protected virtual GameObject InstantiateGridObject(Vector3 position, int x, int y, int z)
        {
            GameObject instance = Instantiate(prefab, instanceContainer);
            instance.name = $"GridObject_{x}_{y}_{z}";
            instance.transform.localPosition = position;
            return instance;
        }

        /// <summary>
        /// Calculate the offset for grid alignment
        /// </summary>
        protected Vector3 CalculateGridOffset()
        {
            Vector3 offset = Vector3.zero;
            if (alignToCenter)
            {
                offset.x = -((gridSizeX - 1) * spacing) / 2;
                offset.y = -((gridSizeY - 1) * spacing) / 2;
                offset.z = -((gridSizeZ - 1) * spacing) / 2;
            }
            return offset;
        }

        /// <summary>
        /// Clear all instances from the grid
        /// </summary>
        public virtual void ClearGrid()
        {
            if (instanceContainer != null)
            {
                // In editor mode, use DestroyImmediate
#if UNITY_EDITOR
                if (!Application.isPlaying)
                {
                    while (instanceContainer.childCount > 0)
                    {
                        DestroyImmediate(instanceContainer.GetChild(0).gameObject);
                    }
                    return;
                }
#endif

                // In play mode, use Destroy
                while (instanceContainer.childCount > 0)
                {
                    Destroy(instanceContainer.GetChild(0).gameObject);
                }
            }
        }

        /// <summary>
        /// Calculate grid bounds for visualization
        /// </summary>
        public Bounds CalculateGridBounds()
        {
            Vector3 size = new Vector3(
                (gridSizeX - 1) * spacing,
                (gridSizeY - 1) * spacing,
                (gridSizeZ - 1) * spacing
            );

            Vector3 center = Vector3.zero;
            if (alignToCenter)
            {
                // Center is already at 0,0,0
            }
            else
            {
                center = size / 2;
            }

            return new Bounds(transform.TransformPoint(center), size);
        }
    }
}
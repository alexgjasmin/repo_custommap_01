using UnityEngine;
using UnityEditor;
using System.Collections.Generic;

public class CubeGridGenerator : MonoBehaviour
{
    [Header("Grid Settings")]
    public GameObject cubePrefab;
    public int gridSizeX = 5;
    public int gridSizeY = 1;
    public int gridSizeZ = 5;
    public float spacing = 1.0f;

    [Header("Transform Randomization")]
    public bool randomizeScale = false;
    public Vector3 minScale = new Vector3(0.8f, 0.8f, 0.8f);
    public Vector3 maxScale = new Vector3(1.2f, 1.2f, 1.2f);

    public bool randomizeRotation = false;
    public Vector3 minRotation = Vector3.zero;
    public Vector3 maxRotation = new Vector3(0, 360, 0);

    [System.Serializable]
    public class ShaderFloatParameter
    {
        public string name;
        public bool randomize = true;
        public float minValue = 0f;
        public float maxValue = 1f;

        public ShaderFloatParameter(string name, float min, float max)
        {
            this.name = name;
            this.minValue = min;
            this.maxValue = max;
        }
    }

    [Header("Shader Randomization")]
    public List<ShaderFloatParameter> shaderParameters = new List<ShaderFloatParameter>
    {
        new ShaderFloatParameter("_Stage", 0f, 1f),
        new ShaderFloatParameter("_WaveSpeed", 0.5f, 2f)
    };

    [Header("Generation")]
    public bool alignToCenter = true;
    public bool clearOnGenerate = true;

    [Header("Visualization")]
    public bool showGridBounds = true;
    public Color gridBoundsColor = new Color(0.2f, 0.8f, 0.2f, 1f);
    public float gridLineThickness = 2f;

    // Parent object to hold all the cubes
    private Transform cubeContainer;

    // Method to generate the grid
    public void GenerateGrid()
    {
        if (cubePrefab == null)
        {
            Debug.LogError("No prefab assigned to generate!");
            return;
        }

        // Clear existing cubes if needed
        if (clearOnGenerate)
        {
            ClearGrid();
        }

        // Create container if it doesn't exist
        if (cubeContainer == null)
        {
            GameObject container = new GameObject("CubeGrid");
            container.transform.parent = this.transform;
            container.transform.localPosition = Vector3.zero;
            cubeContainer = container.transform;
        }

        // Calculate offsets for centering
        Vector3 offset = Vector3.zero;
        if (alignToCenter)
        {
            offset.x = -((gridSizeX - 1) * spacing) / 2;
            offset.y = -((gridSizeY - 1) * spacing) / 2;
            offset.z = -((gridSizeZ - 1) * spacing) / 2;
        }

        // Generate cubes
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

                    GameObject cube = Instantiate(cubePrefab, cubeContainer);
                    cube.name = $"Cube_{x}_{y}_{z}";
                    cube.transform.localPosition = position;

                    // Apply transform randomization if enabled
                    if (randomizeScale)
                    {
                        cube.transform.localScale = new Vector3(
                            Random.Range(minScale.x, maxScale.x),
                            Random.Range(minScale.y, maxScale.y),
                            Random.Range(minScale.z, maxScale.z)
                        );
                    }

                    if (randomizeRotation)
                    {
                        cube.transform.localRotation = Quaternion.Euler(
                            Random.Range(minRotation.x, maxRotation.x),
                            Random.Range(minRotation.y, maxRotation.y),
                            Random.Range(minRotation.z, maxRotation.z)
                        );
                    }

                    // Randomize shader parameters
                    RandomizeShaderParameters(cube);
                }
            }
        }
    }

    // Method to randomize shader parameters
    private void RandomizeShaderParameters(GameObject obj)
    {
        // Get all active shader parameters that should be randomized
        List<ShaderFloatParameter> activeParams = shaderParameters.FindAll(p => p.randomize);

        if (activeParams.Count == 0)
            return;

        // Try to get renderers (including child renderers)
        Renderer[] renderers = obj.GetComponentsInChildren<Renderer>();

        if (renderers.Length == 0)
        {
            Debug.LogWarning($"No renderer found on {obj.name} to modify shader properties.");
            return;
        }

        Dictionary<Material, Material> materialInstances = new Dictionary<Material, Material>();

        foreach (Renderer renderer in renderers)
        {
            Material[] originalMaterials = renderer.sharedMaterials;
            Material[] newMaterials = new Material[originalMaterials.Length];
            bool materialChanged = false;

            // Process each material on the renderer
            for (int i = 0; i < originalMaterials.Length; i++)
            {
                Material originalMaterial = originalMaterials[i];

                if (originalMaterial == null)
                {
                    newMaterials[i] = null;
                    continue;
                }

                // Create or reuse material instance
                Material instanceMaterial;
                if (materialInstances.TryGetValue(originalMaterial, out Material existingInstance))
                {
                    instanceMaterial = existingInstance;
                }
                else
                {
                    instanceMaterial = new Material(originalMaterial);
                    materialInstances[originalMaterial] = instanceMaterial;
                }

                newMaterials[i] = instanceMaterial;

                // Apply randomization to all active shader parameters
                foreach (ShaderFloatParameter param in activeParams)
                {
                    if (instanceMaterial.HasProperty(param.name))
                    {
                        float randomValue = Random.Range(param.minValue, param.maxValue);
                        instanceMaterial.SetFloat(param.name, randomValue);
                        materialChanged = true;

#if UNITY_EDITOR
                        if (Debug.isDebugBuild)
                        {
                            Debug.Log($"Set {param.name} to {randomValue} on {obj.name}");
                        }
#endif
                    }
#if UNITY_EDITOR
                    else
                    {
                        Debug.LogWarning($"Material {originalMaterial.name} doesn't have property '{param.name}'");
                    }
#endif
                }
            }

            // Only update the materials if we actually made changes
            if (materialChanged)
            {
                renderer.materials = newMaterials;
            }
        }
    }

    // Method to clear the grid
    public void ClearGrid()
    {
        if (cubeContainer != null)
        {
            while (cubeContainer.childCount > 0)
            {
                DestroyImmediate(cubeContainer.GetChild(0).gameObject);
            }
        }
    }

    // Calculate grid bounds for visualization
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

#if UNITY_EDITOR
[CustomEditor(typeof(CubeGridGenerator))]
public class CubeGridGeneratorEditor : Editor
{
    // Add a custom property to test shader materials
    private bool showShaderTestSection = false;
    private GameObject testObject;
    private Vector2 shaderTestScroll;

    public override void OnInspectorGUI()
    {
        CubeGridGenerator generator = (CubeGridGenerator)target;

        // Draw the default inspector
        DrawDefaultInspector();

        EditorGUILayout.Space(10);

        // Add generation buttons
        if (GUILayout.Button("Generate Grid", GUILayout.Height(30)))
        {
            generator.GenerateGrid();
        }

        if (GUILayout.Button("Clear Grid", GUILayout.Height(30)))
        {
            generator.ClearGrid();
        }

        // Add a shader property tester section
        EditorGUILayout.Space(10);
        showShaderTestSection = EditorGUILayout.Foldout(showShaderTestSection, "Shader Property Tester", true);

        if (showShaderTestSection && generator.cubePrefab != null)
        {
            EditorGUILayout.HelpBox("Test if shader properties exist on your prefab materials.", MessageType.Info);

            // Button to test all properties
            if (GUILayout.Button("Test All Shader Properties"))
            {
                TestAllShaderProperties(generator);
            }

            // Display test results in a scrollable area
            if (testObject != null)
            {
                EditorGUILayout.Space(5);
                EditorGUILayout.LabelField("Test Results:", EditorStyles.boldLabel);

                shaderTestScroll = EditorGUILayout.BeginScrollView(shaderTestScroll, GUILayout.Height(150));

                Renderer[] renderers = testObject.GetComponentsInChildren<Renderer>();
                bool foundAnyRenderer = renderers.Length > 0;

                if (foundAnyRenderer)
                {
                    foreach (Renderer renderer in renderers)
                    {
                        EditorGUILayout.LabelField($"Renderer: {renderer.name}", EditorStyles.boldLabel);

                        foreach (Material material in renderer.sharedMaterials)
                        {
                            if (material == null) continue;

                            EditorGUILayout.LabelField($"  Material: {material.name}", EditorStyles.miniBoldLabel);

                            bool foundAnyProperty = false;
                            foreach (var param in generator.shaderParameters)
                            {
                                if (material.HasProperty(param.name))
                                {
                                    float value = material.GetFloat(param.name);
                                    EditorGUILayout.LabelField($"    {param.name} = {value}", EditorStyles.miniLabel);
                                    foundAnyProperty = true;
                                }
                                else
                                {
                                    EditorGUILayout.LabelField($"    {param.name} = <not found>", EditorStyles.miniLabel);
                                }
                            }

                            if (!foundAnyProperty)
                            {
                                EditorGUILayout.LabelField("    No shader properties found", EditorStyles.miniLabel);
                            }
                        }
                    }
                }
                else
                {
                    EditorGUILayout.LabelField("No renderers found on prefab!", EditorStyles.miniLabel);
                }

                EditorGUILayout.EndScrollView();
            }
        }

        // Add helpful instructions
        EditorGUILayout.Space(5);
        EditorGUILayout.HelpBox("1. Assign a cube prefab\n2. Adjust grid parameters\n3. Set shader parameters to randomize\n4. Click 'Generate Grid'\n5. Adjust parameters and regenerate as needed", MessageType.Info);
    }

    private void TestAllShaderProperties(CubeGridGenerator generator)
    {
        // Create a temporary instance to test
        if (testObject != null)
        {
            DestroyImmediate(testObject);
        }

        testObject = Instantiate(generator.cubePrefab);
        testObject.name = "ShaderTest_" + generator.cubePrefab.name;
        testObject.hideFlags = HideFlags.HideAndDontSave;
    }

    private void OnDisable()
    {
        // Clean up test object when the inspector is no longer visible
        if (testObject != null)
        {
            DestroyImmediate(testObject);
            testObject = null;
        }
    }

    private void OnSceneGUI()
    {
        CubeGridGenerator generator = (CubeGridGenerator)target;

        if (!generator.showGridBounds)
            return;

        // Calculate grid dimensions
        float width = (generator.gridSizeX - 1) * generator.spacing;
        float height = (generator.gridSizeY - 1) * generator.spacing;
        float depth = (generator.gridSizeZ - 1) * generator.spacing;

        // Calculate offset for centered grid
        Vector3 offset = Vector3.zero;
        if (generator.alignToCenter)
        {
            offset.x = -width / 2;
            offset.y = -height / 2;
            offset.z = -depth / 2;
        }

        // Draw the grid wireframe
        Handles.matrix = generator.transform.localToWorldMatrix;
        Handles.color = generator.gridBoundsColor;

        Vector3 p000 = new Vector3(offset.x, offset.y, offset.z);
        Vector3 p100 = new Vector3(offset.x + width, offset.y, offset.z);
        Vector3 p010 = new Vector3(offset.x, offset.y + height, offset.z);
        Vector3 p110 = new Vector3(offset.x + width, offset.y + height, offset.z);
        Vector3 p001 = new Vector3(offset.x, offset.y, offset.z + depth);
        Vector3 p101 = new Vector3(offset.x + width, offset.y, offset.z + depth);
        Vector3 p011 = new Vector3(offset.x, offset.y + height, offset.z + depth);
        Vector3 p111 = new Vector3(offset.x + width, offset.y + height, offset.z + depth);

        // Bottom face
        Handles.DrawLine(p000, p100, generator.gridLineThickness);
        Handles.DrawLine(p100, p101, generator.gridLineThickness);
        Handles.DrawLine(p101, p001, generator.gridLineThickness);
        Handles.DrawLine(p001, p000, generator.gridLineThickness);

        // Top face
        Handles.DrawLine(p010, p110, generator.gridLineThickness);
        Handles.DrawLine(p110, p111, generator.gridLineThickness);
        Handles.DrawLine(p111, p011, generator.gridLineThickness);
        Handles.DrawLine(p011, p010, generator.gridLineThickness);

        // Connecting edges
        Handles.DrawLine(p000, p010, generator.gridLineThickness);
        Handles.DrawLine(p100, p110, generator.gridLineThickness);
        Handles.DrawLine(p101, p111, generator.gridLineThickness);
        Handles.DrawLine(p001, p011, generator.gridLineThickness);

        // Draw dimension labels if the grid is large enough
        float minSize = 3f;  // Minimum size to show labels
        if (width > minSize || height > minSize || depth > minSize)
        {
            GUIStyle style = new GUIStyle(EditorStyles.label);
            style.normal.textColor = generator.gridBoundsColor;
            style.fontStyle = FontStyle.Bold;

            // Draw dimension labels at the midpoints of edges
            Vector3 widthMidpoint = (p000 + p100) / 2 + new Vector3(0, -0.2f, 0);
            Handles.Label(widthMidpoint, $"X: {generator.gridSizeX} ({width:F1}m)", style);

            Vector3 heightMidpoint = (p000 + p010) / 2 + new Vector3(-0.2f, 0, 0);
            Handles.Label(heightMidpoint, $"Y: {generator.gridSizeY} ({height:F1}m)", style);

            Vector3 depthMidpoint = (p000 + p001) / 2 + new Vector3(0, -0.2f, 0);
            Handles.Label(depthMidpoint, $"Z: {generator.gridSizeZ} ({depth:F1}m)", style);
        }
    }
}
#endif
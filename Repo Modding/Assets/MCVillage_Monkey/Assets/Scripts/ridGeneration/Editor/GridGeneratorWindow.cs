#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using System.Linq;

namespace GridGeneration.Editor
{

    /// <summary>
    /// Editor window for managing grid generators
    /// </summary>
    public class GridGeneratorWindow : EditorWindow
    {
        private Vector2 scrollPosition;
        private List<BaseGridGenerator> gridGenerators = new List<BaseGridGenerator>();
        private bool showInactiveGenerators = true;
        private bool showSceneGenerators = true;
        private bool showPrefabGenerators = false;
        private GUIStyle headerStyle;
        private GUIStyle subHeaderStyle;
        private Texture2D headerBackground;
        private Color headerColor = new Color(0.3f, 0.5f, 0.85f);
        private float buttonHeight = 30f;

        // Template prefabs
        private GameObject plantTemplatePrefab;
        private GameObject customTemplatePrefab;

        [MenuItem("Tools/Grid Generator")]
        public static void ShowWindow()
        {
            GridGeneratorWindow window = GetWindow<GridGeneratorWindow>("Grid Generator");
            window.minSize = new Vector2(350, 300);
            window.Show();
        }

        private void OnEnable()
        {
            // Create styles when the window is opened
            CreateStyles();
            // Find all grid generators in the scene
            FindGenerators();
            // Try to find template prefabs
            FindTemplatePrefabs();
            // Subscribe to hierarchy changes
            EditorApplication.hierarchyChanged += OnHierarchyChanged;
        }

        private void OnDisable()
        {
            // Unsubscribe from hierarchy changes
            EditorApplication.hierarchyChanged -= OnHierarchyChanged;
        }

        private void OnHierarchyChanged()
        {
            // Find generators again when hierarchy changes
            FindGenerators();
            Repaint();
        }

        private void FindTemplatePrefabs()
        {
            // Try to find the plantTemplate prefab in the project
            if (plantTemplatePrefab == null)
            {
                string[] plantTemplateGuids = AssetDatabase.FindAssets("plantTemplate t:prefab");
                if (plantTemplateGuids.Length > 0)
                {
                    string plantTemplatePath = AssetDatabase.GUIDToAssetPath(plantTemplateGuids[0]);
                    plantTemplatePrefab = AssetDatabase.LoadAssetAtPath<GameObject>(plantTemplatePath);
                }
            }

            // Try to find the customTemplate prefab in the project
            if (customTemplatePrefab == null)
            {
                string[] customTemplateGuids = AssetDatabase.FindAssets("customTemplate t:prefab");
                if (customTemplateGuids.Length > 0)
                {
                    string customTemplatePath = AssetDatabase.GUIDToAssetPath(customTemplateGuids[0]);
                    customTemplatePrefab = AssetDatabase.LoadAssetAtPath<GameObject>(customTemplatePath);
                }
            }
        }

        private void FindGenerators()
        {
            gridGenerators.Clear();

            // Find all grid generators in the scene
            if (showSceneGenerators)
            {
                BaseGridGenerator[] sceneGenerators = FindObjectsOfType<BaseGridGenerator>(showInactiveGenerators);
                gridGenerators.AddRange(sceneGenerators);
            }

            // Find grid generators in prefabs
            if (showPrefabGenerators)
            {
                // This would search through project prefabs
                // Left as a potential extension point
            }
        }

        private void CreateStyles()
        {
            // Create a background texture for headers
            headerBackground = new Texture2D(1, 1);
            headerBackground.SetPixel(0, 0, headerColor);
            headerBackground.Apply();

            // Create header style
            headerStyle = new GUIStyle(EditorStyles.boldLabel)
            {
                fontSize = 14,
                alignment = TextAnchor.MiddleLeft,
                padding = new RectOffset(10, 10, 8, 8)
            };
            headerStyle.normal.background = headerBackground;
            headerStyle.normal.textColor = Color.white;

            // Create subheader style
            subHeaderStyle = new GUIStyle(EditorStyles.boldLabel)
            {
                fontSize = 12,
                alignment = TextAnchor.MiddleLeft,
                padding = new RectOffset(5, 5, 5, 5)
            };
        }

        private void OnGUI()
        {
            DrawToolbar();
            DrawTemplatePrefabFields();
            DrawGeneratorList();
            DrawBottomButtons();
        }

        private void DrawToolbar()
        {
            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);

            if (GUILayout.Button("Refresh", EditorStyles.toolbarButton, GUILayout.Width(60)))
            {
                FindGenerators();
            }

            showInactiveGenerators = EditorGUILayout.ToggleLeft("Show Inactive", showInactiveGenerators, GUILayout.Width(100));
            showSceneGenerators = EditorGUILayout.ToggleLeft("Scene Objects", showSceneGenerators, GUILayout.Width(100));
            showPrefabGenerators = EditorGUILayout.ToggleLeft("Prefabs", showPrefabGenerators, GUILayout.Width(80));

            GUILayout.FlexibleSpace();

            if (GUILayout.Button("Generate All", EditorStyles.toolbarButton, GUILayout.Width(80)))
            {
                GenerateAllGrids();
            }

            if (GUILayout.Button("Clear All", EditorStyles.toolbarButton, GUILayout.Width(80)))
            {
                ClearAllGrids();
            }

            EditorGUILayout.EndHorizontal();
        }

        private void DrawTemplatePrefabFields()
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.LabelField("Template Prefabs", EditorStyles.boldLabel);

            EditorGUI.BeginChangeCheck();

            plantTemplatePrefab = (GameObject)EditorGUILayout.ObjectField(
                new GUIContent("Plant Template", "Prefab to instantiate when creating a new Plant Grid Generator"),
                plantTemplatePrefab,
                typeof(GameObject),
                false
            );

            customTemplatePrefab = (GameObject)EditorGUILayout.ObjectField(
                new GUIContent("Custom Template", "Prefab to instantiate when creating a new Custom Grid Generator"),
                customTemplatePrefab,
                typeof(GameObject),
                false
            );

            if (EditorGUI.EndChangeCheck())
            {
                // Save the prefab references in EditorPrefs for persistence between sessions
                if (plantTemplatePrefab != null)
                {
                    EditorPrefs.SetString("GridGeneratorWindow_PlantTemplatePath", AssetDatabase.GetAssetPath(plantTemplatePrefab));
                }

                if (customTemplatePrefab != null)
                {
                    EditorPrefs.SetString("GridGeneratorWindow_CustomTemplatePath", AssetDatabase.GetAssetPath(customTemplatePrefab));
                }
            }

            EditorGUILayout.EndVertical();
        }

        private void DrawGeneratorList()
        {
            scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition);

            if (gridGenerators.Count == 0)
            {
                EditorGUILayout.HelpBox("No grid generators found in the scene.\n\nAdd a Grid Generator component to a GameObject to get started.", MessageType.Info);
            }
            else
            {
                // Group generators by type
                var generatorsByType = gridGenerators
                    .GroupBy(g => g.GetType().Name)
                    .OrderBy(g => g.Key);

                foreach (var group in generatorsByType)
                {
                    DrawGeneratorGroup(group.Key, group.ToList());
                }
            }

            EditorGUILayout.EndScrollView();
        }

        private void DrawGeneratorGroup(string typeName, List<BaseGridGenerator> generators)
        {
            GUILayout.Space(10);

            // Draw header
            EditorGUILayout.BeginHorizontal(headerStyle);
            EditorGUILayout.LabelField($"{typeName} ({generators.Count})", GUILayout.ExpandWidth(true));
            EditorGUILayout.EndHorizontal();

            // Draw group buttons
            EditorGUILayout.BeginHorizontal();

            if (GUILayout.Button($"Generate All {typeName}", GUILayout.Height(buttonHeight)))
            {
                foreach (var generator in generators)
                {
                    generator.GenerateGrid();
                }
            }

            if (GUILayout.Button($"Clear All {typeName}", GUILayout.Height(buttonHeight)))
            {
                foreach (var generator in generators)
                {
                    generator.ClearGrid();
                }
            }

            EditorGUILayout.EndHorizontal();

            // Draw individual generators
            foreach (var generator in generators)
            {
                DrawGeneratorItem(generator);
            }
        }

        private void DrawGeneratorItem(BaseGridGenerator generator)
        {
            bool isActive = generator.gameObject.activeInHierarchy;

            EditorGUILayout.BeginVertical(EditorStyles.helpBox);

            // Generator header
            EditorGUILayout.BeginHorizontal();

            // Show object icon
            GUIContent iconContent = EditorGUIUtility.ObjectContent(null, generator.GetType());
            GUILayout.Label(iconContent.image, GUILayout.Width(20), GUILayout.Height(20));

            // Object name with dim color if inactive
            GUIStyle nameStyle = new GUIStyle(EditorStyles.boldLabel);
            if (!isActive)
            {
                nameStyle.normal.textColor = new Color(0.5f, 0.5f, 0.5f);
            }

            if (GUILayout.Button(generator.gameObject.name, nameStyle, GUILayout.ExpandWidth(true)))
            {
                Selection.activeGameObject = generator.gameObject;
                EditorGUIUtility.PingObject(generator.gameObject);
            }

            EditorGUILayout.EndHorizontal();

            // Grid size info
            EditorGUILayout.BeginHorizontal();
            GUILayout.Space(25);
            EditorGUILayout.LabelField($"Grid: {generator.gridSizeX} × {generator.gridSizeY} × {generator.gridSizeZ}", EditorStyles.miniLabel);

            // Show prefab preview if possible
            if (generator.prefab != null)
            {
                GUIStyle previewStyle = new GUIStyle(EditorStyles.miniLabel);
                previewStyle.normal.textColor = new Color(0.4f, 0.8f, 0.4f);
                EditorGUILayout.LabelField($"Prefab: {generator.prefab.name}", previewStyle);
            }

            EditorGUILayout.EndHorizontal();

            // Generate and clear buttons
            EditorGUILayout.BeginHorizontal();

            if (GUILayout.Button("Generate", GUILayout.Height(buttonHeight)))
            {
                generator.GenerateGrid();
            }

            if (GUILayout.Button("Clear", GUILayout.Height(buttonHeight)))
            {
                generator.ClearGrid();
            }

            if (GUILayout.Button("Select", GUILayout.Width(60), GUILayout.Height(buttonHeight)))
            {
                Selection.activeGameObject = generator.gameObject;
                EditorGUIUtility.PingObject(generator.gameObject);
            }

            EditorGUILayout.EndHorizontal();

            EditorGUILayout.EndVertical();

            GUILayout.Space(5);
        }

        private void DrawBottomButtons()
        {
            GUILayout.FlexibleSpace();

            EditorGUILayout.BeginHorizontal();

            if (GUILayout.Button("Create New Generator", GUILayout.Height(buttonHeight)))
            {
                ShowAddMenu();
            }

            EditorGUILayout.EndHorizontal();
        }

        private void ShowAddMenu()
        {
            // Debug log to verify this method is being called
            Debug.Log("ShowAddMenu called");

            GenericMenu menu = new GenericMenu();

            menu.AddItem(new GUIContent("Plant Grid Generator"), false, () => {
                Debug.Log("Plant Grid Generator menu item clicked");
                CreateNewGenerator<PlantGridGenerator>("Plant Grid", plantTemplatePrefab);
            });

            menu.AddItem(new GUIContent("Custom Grid Generator"), false, () => {
                Debug.Log("Custom Grid Generator menu item clicked");
                CreateNewGenerator<CustomGridGenerator>("Custom Grid", customTemplatePrefab);
            });

            // You can add more generator types here

            menu.ShowAsContext();
        }

        private void CreateNewGenerator<T>(string name, GameObject templatePrefab) where T : BaseGridGenerator
        {
            Debug.Log($"CreateNewGenerator called: {typeof(T).Name}, templatePrefab: {(templatePrefab != null ? templatePrefab.name : "null")}");

            GameObject newObject;

            // If we have a template prefab, instantiate it
            if (templatePrefab != null)
            {
                Debug.Log($"Using template prefab: {templatePrefab.name}");
                newObject = PrefabUtility.InstantiatePrefab(templatePrefab) as GameObject;

                if (newObject == null)
                {
                    Debug.LogError("Failed to instantiate template prefab!");
                    return;
                }

                Undo.RegisterCreatedObjectUndo(newObject, "Create Grid Generator from Template");

                // Make sure it has the right component
                if (!newObject.GetComponent<T>())
                {
                    Debug.Log($"Template doesn't have {typeof(T).Name}, adding it");
                    T generator = Undo.AddComponent<T>(newObject);
                }
            }
            else
            {
                Debug.Log($"No template prefab, creating new GameObject");
                // Create a new GameObject with the component
                newObject = new GameObject(name);
                Undo.RegisterCreatedObjectUndo(newObject, "Create Grid Generator");
                T generator = Undo.AddComponent<T>(newObject);
            }

            Debug.Log($"Created new generator: {newObject.name}");
            Selection.activeGameObject = newObject;

            // Make sure the list updates
            EditorApplication.delayCall += () => {
                FindGenerators();
                Repaint();
            };
        }

        private void GenerateAllGrids()
        {
            foreach (var generator in gridGenerators)
            {
                generator.GenerateGrid();
            }
        }

        private void ClearAllGrids()
        {
            foreach (var generator in gridGenerators)
            {
                generator.ClearGrid();
            }
        }
    }
}
#endif
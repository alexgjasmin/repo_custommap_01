// SpriteSheetManager.cs - Manages sprite sheets for grid objects
using UnityEngine;
using System.Collections.Generic;
using System.Linq;

namespace GridGeneration
{

    /// <summary>
    /// Manages sprite sheets for grid objects
    /// </summary>
    [System.Serializable]
    public class SpriteSheetManager
    {
        // Sprite sheet property names
        public string mainTextureProperty = "_MainTex";
        public string stageProperty = "_Stage";
        public string columnsProperty = "_Columns";
        public string rowsProperty = "_Rows";
        public string stageCountProperty = "_StageCount";
        public string colorProperty = "_Color"; // For tinting grayscale sprites

        // Enum for different plant prefab types
        public enum PlantPrefabType
        {
            Any,
            Single,   // 1 block plants like short flowers, short grass
            Double,   // 2 block tall plants like tall flowers, tall grass
            Crop      // Farmland crops
        }

        [System.Serializable]
        public class SpriteSheet
        {
            public string name;
            public Texture2D spriteSheet;
            public Vector2Int gridSize = new Vector2Int(1, 1); // x = columns, y = rows
            public int frameCount = 1; // Total number of frames/stages to use
            public bool expanded = true; // For editor foldout

            // Plant type compatibility
            public PlantPrefabType prefabType = PlantPrefabType.Any;

            // Color tinting (for grayscale sprites)
            public bool useColorTint = false;
            public Color colorTint = Color.white;

            public int GetTotalFrames()
            {
                return Mathf.Min(frameCount, gridSize.x * gridSize.y);
            }
        }

        public List<SpriteSheet> spriteSheets = new List<SpriteSheet>();
        public bool useSpriteSheets = true;

        /// <summary>
        /// Try to detect grid size from a texture
        /// </summary>
        public Vector2Int DetectGridSize(Texture2D texture)
        {
            // This is a simple heuristic - you might need to adjust this
            // based on your specific sprite sheets

            if (texture == null)
                return new Vector2Int(1, 1);

#if UNITY_EDITOR
            // Try to get sprite data from the texture asset
            string assetPath = UnityEditor.AssetDatabase.GetAssetPath(texture);
            UnityEditor.TextureImporter importer = UnityEditor.AssetImporter.GetAtPath(assetPath) as UnityEditor.TextureImporter;

            if (importer != null && importer.spriteImportMode == UnityEditor.SpriteImportMode.Multiple)
            {
                // Get the sprites from the texture
                Object[] sprites = UnityEditor.AssetDatabase.LoadAllAssetsAtPath(assetPath);
                int spriteCount = sprites.Count(obj => obj is Sprite);

                // Try to determine a reasonable grid layout
                if (spriteCount > 0)
                {
                    // For simplicity, we'll assume a horizontal layout by default
                    return new Vector2Int(spriteCount, 1);
                }
            }
#endif

            // Default fallback - guess based on common sprite sheet layouts
            if (texture.width > texture.height * 2)
            {
                // Likely a horizontal strip
                int estimatedFrames = Mathf.RoundToInt(texture.width / (texture.height * 1.0f));
                return new Vector2Int(estimatedFrames, 1);
            }
            else if (texture.height > texture.width * 2)
            {
                // Likely a vertical strip
                int estimatedFrames = Mathf.RoundToInt(texture.height / (texture.width * 1.0f));
                return new Vector2Int(1, estimatedFrames);
            }
            else
            {
                // Might be a grid - try to estimate
                int estimatedSide = Mathf.RoundToInt(Mathf.Sqrt(texture.width * texture.height / (texture.width * 0.25f)));
                return new Vector2Int(estimatedSide, estimatedSide);
            }
        }
    }
}
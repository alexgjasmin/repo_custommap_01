// ShaderParameterManager.cs - Manages shader parameters

using System.Collections.Generic;
using UnityEngine;

namespace GridGeneration
{

    /// <summary>
    /// Manages shader parameters for grid objects
    /// </summary>
    [System.Serializable]
    public class ShaderParameterManager
    {
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

        public List<ShaderFloatParameter> shaderParameters = new List<ShaderFloatParameter> {
            new ShaderFloatParameter("_Stage", 0f, 1f),
            new ShaderFloatParameter("_WaveSpeed", 0.5f, 2f)
        };

        /// <summary>
        /// Apply randomized shader parameters to all materials on an object
        /// </summary>
        public void RandomizeShaderParameters(GameObject obj)
        {
            Renderer[] renderers = obj.GetComponentsInChildren<Renderer>();

            if (renderers.Length == 0)
            {
                Debug.LogWarning($"No renderer found on {obj.name} to modify shader properties.");
                return;
            }

            foreach (Renderer renderer in renderers)
            {
                Material[] materials = renderer.materials;  // Creates a copy we can modify
                bool materialChanged = false;

                foreach (Material material in materials)
                {
                    if (material != null)
                    {
                        RandomizeShaderParametersForMaterial(material);
                        materialChanged = true;
                    }
                }

                // Only reassign materials if needed
                if (materialChanged)
                {
                    renderer.materials = materials;
                }
            }
        }

        /// <summary>
        /// Randomize shader parameters on a specific material
        /// </summary>
        public void RandomizeShaderParametersForMaterial(Material material, string stageProperty = "_Stage")
        {
            if (material == null)
                return;

            // Get all active shader parameters that should be randomized
            List<ShaderFloatParameter> activeParams = shaderParameters.FindAll(p =>
                p.randomize &&
                p.name != stageProperty  // We handle stage separately for plants
            );

            foreach (ShaderFloatParameter param in activeParams)
            {
                if (material.HasProperty(param.name))
                {
                    float randomValue = Random.Range(param.minValue, param.maxValue);
                    material.SetFloat(param.name, randomValue);

#if UNITY_EDITOR
                    if (Debug.isDebugBuild)
                    {
                        Debug.Log($"Set {param.name} to {randomValue}");
                    }
#endif
                }
            }
        }

        /// <summary>
        /// Check if a material has the specified shader property
        /// </summary>
        public bool HasShaderProperty(Material material, string propertyName)
        {
            return material != null && material.HasProperty(propertyName);
        }
    }
}
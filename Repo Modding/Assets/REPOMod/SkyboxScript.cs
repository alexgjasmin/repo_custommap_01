using BepInEx;
using BepInEx.Logging;
using HarmonyLib;
using UnityEngine;
using System.Collections.Generic;
using System.Reflection;
using System;

namespace REPOSkyboxMod
{
    [BepInPlugin("com.yourusername.reposkyboxmod", "REPO Skybox Mod", "1.0.0")]
    [BepInDependency("Zehs-REPOLib-1.0.0", BepInDependency.DependencyFlags.HardDependency)]
    public class REPOSkyboxMod : BaseUnityPlugin
    {
        private static ManualLogSource logger;
        private Harmony harmony;
        private Material skyboxMaterial;

        private void Awake()
        {
            // Set up the logger for debugging
            logger = Logger;
            logger.LogInfo("REPO Skybox Mod is starting...");

            // Initialize Harmony for patching
            harmony = new Harmony("com.yourusername.reposkyboxmod");
            harmony.PatchAll(Assembly.GetExecutingAssembly());

            // Create a skybox material
            CreateSkyboxMaterial();

            // Run our camera modification code when the game is fully loaded
            StartCoroutine(ApplySkyboxWhenReady());
        }

        private System.Collections.IEnumerator ApplySkyboxWhenReady()
        {
            // Wait a few frames to ensure game is loaded
            yield return new WaitForSeconds(2f);

            // Find all cameras in the scene and apply skybox settings
            ModifyCameraSettings();

            logger.LogInfo("Skybox applied to cameras");
        }

        private void CreateSkyboxMaterial()
        {
            try
            {
                // Create a new skybox material with the default skybox shader
                skyboxMaterial = new Material(Shader.Find("Skybox/Procedural"));

                if (skyboxMaterial != null)
                {
                    // Configure the procedural skybox parameters
                    // Adjust these values to get your desired sky appearance
                    skyboxMaterial.SetFloat("_SunSize", 0.04f);
                    skyboxMaterial.SetFloat("_AtmosphereThickness", 1.0f);
                    skyboxMaterial.SetFloat("_Exposure", 1.3f);
                    skyboxMaterial.SetColor("_SkyTint", new Color(0.5f, 0.5f, 0.5f));
                    skyboxMaterial.SetColor("_GroundColor", new Color(0.369f, 0.349f, 0.341f));

                    // You can also use a 6-sided skybox if you have the textures
                    // skyboxMaterial = new Material(Shader.Find("Skybox/6 Sided"));
                    // skyboxMaterial.SetTexture("_FrontTex", frontTexture);
                    // ... set other textures for right, left, back, up, down

                    logger.LogInfo("Skybox material created successfully");
                }
                else
                {
                    logger.LogError("Failed to create skybox material: Shader not found");
                }
            }
            catch (Exception ex)
            {
                logger.LogError($"Error creating skybox material: {ex.Message}");
            }
        }

        private void ModifyCameraSettings()
        {
            try
            {
                // Apply the skybox to the global render settings
                RenderSettings.skybox = skyboxMaterial;

                // Enable fog for better atmosphere (optional)
                RenderSettings.fog = true;
                RenderSettings.fogColor = new Color(0.5f, 0.5f, 0.5f, 1.0f);
                RenderSettings.fogMode = FogMode.Linear;
                RenderSettings.fogStartDistance = 10f;
                RenderSettings.fogEndDistance = 300f;

                // Find all cameras in the scene
                Camera[] allCameras = GameObject.FindObjectsOfType<Camera>();

                foreach (Camera camera in allCameras)
                {
                    // Make sure the camera uses the skybox for clearing
                    camera.clearFlags = CameraClearFlags.Skybox;

                    // Add a Skybox component if it doesn't exist
                    if (camera.GetComponent<Skybox>() == null)
                    {
                        Skybox skybox = camera.gameObject.AddComponent<Skybox>();
                        skybox.material = skyboxMaterial;
                    }
                    else
                    {
                        camera.GetComponent<Skybox>().material = skyboxMaterial;
                    }

                    logger.LogInfo($"Applied skybox to camera: {camera.name}");
                }
            }
            catch (Exception ex)
            {
                logger.LogError($"Error modifying camera settings: {ex.Message}");
            }
        }

        private void OnDestroy()
        {
            // Clean up when the plugin is unloaded
            if (harmony != null)
                harmony.UnpatchSelf();

            logger.LogInfo("REPO Skybox Mod unloaded");
        }
    }
}
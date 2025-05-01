using UnityEngine;
using UnityEditor;

#if UNITY_EDITOR
[ExecuteInEditMode]
public class ChainGenerator : MonoBehaviour
{
    [Header("References")]
    public GameObject lanternModel;        // The lantern object
    public GameObject lanternMountModel;   // The mount where the lantern attaches
    public GameObject chainLinkPrefab;     // The chain link prefab to instantiate

    [Header("Chain Settings")]
    [Range(0.01f, 1.0f)]
    public float chainLinkSpacing = 0.1f;  // Space between chain links
    public float chainThickness = 1.0f;    // Scale multiplier for chain thickness
    public bool autoUpdate = false;        // Auto update when parameters change

    [Header("Last Link Settings")]
    public bool customizeLastLink = true;  // Enable custom rotation for last link
    public Vector3 lastLinkRotation = new Vector3(0, 0, 0);  // Custom Euler angles for last link

    [Header("Generated Chain")]
    public Transform chainParent;          // Parent object for the generated chain

    private int previousLinkCount = 0;

    // This function will create the chain between lantern and mount
    public void GenerateChain()
    {
        // Check if all required references are assigned
        if (lanternModel == null || lanternMountModel == null || chainLinkPrefab == null)
        {
            Debug.LogError("Please assign all required models in the inspector!");
            return;
        }

        // Debug positions to help identify issues
        Debug.Log("Lantern position: " + lanternModel.transform.position);
        Debug.Log("Mount position: " + lanternMountModel.transform.position);

        // Create or find a parent for the chain
        if (chainParent == null)
        {
            GameObject newParent = new GameObject("Chain_Parent");
            newParent.transform.SetParent(transform);
            chainParent = newParent.transform;
        }

        // Clear existing chain
        ClearChain();

        // Calculate start and end positions
        Vector3 startPos = lanternMountModel.transform.position;
        Vector3 endPos = lanternModel.transform.position;

        // Check for invalid positions (NaN)
        if (float.IsNaN(startPos.x) || float.IsNaN(startPos.y) || float.IsNaN(startPos.z) ||
            float.IsNaN(endPos.x) || float.IsNaN(endPos.y) || float.IsNaN(endPos.z))
        {
            Debug.LogError("Invalid position detected (NaN). Check your lantern and mount transforms!");
            return;
        }

        // Calculate direction and distance
        Vector3 direction = endPos - startPos;
        float distance = direction.magnitude;
        Vector3 normalizedDirection = direction.normalized;

        // Calculate how many chain links we need
        int linkCount = Mathf.Max(2, Mathf.FloorToInt(distance / chainLinkSpacing));
        previousLinkCount = linkCount;

        // Create the chain links
        for (int i = 0; i < linkCount; i++)
        {
            // Calculate position along the path - avoid division by zero
            float t = (linkCount > 1) ? (float)i / (linkCount - 1) : 0;
            Vector3 position = Vector3.Lerp(startPos, endPos, t);

            // Check for valid position before instantiating
            if (float.IsNaN(position.x) || float.IsNaN(position.y) || float.IsNaN(position.z))
            {
                Debug.LogError("Invalid position calculated for chain link " + i + ": " + position);
                continue;
            }

            // Instantiate chain link
            GameObject chainLink = Instantiate(chainLinkPrefab, position, Quaternion.identity);
            chainLink.name = "ChainLink_" + i;

            // Look at the next link position (or the end point for the last link)
            if (i < linkCount - 1)
            {
                // Avoid division by zero when calculating next position
                float nextT = (linkCount > 1) ? (float)(i + 1) / (linkCount - 1) : 1;
                Vector3 nextPos = Vector3.Lerp(startPos, endPos, nextT);

                // Check if the position is valid (not NaN)
                if (!float.IsNaN(nextPos.x) && !float.IsNaN(nextPos.y) && !float.IsNaN(nextPos.z))
                {
                    chainLink.transform.LookAt(nextPos);
                }
            }
            else
            {
                // Special handling for the last link
                if (customizeLastLink)
                {
                    // Instead of trying to automatically fix it, we'll apply exact rotation values
                    // First, apply the basic look rotation to align with the chain
                    chainLink.transform.LookAt(endPos);

                    // Then apply the custom rotation specified in the inspector
                    chainLink.transform.rotation = Quaternion.Euler(
                        chainLink.transform.rotation.eulerAngles.x + lastLinkRotation.x,
                        chainLink.transform.rotation.eulerAngles.y + lastLinkRotation.y,
                        chainLink.transform.rotation.eulerAngles.z + lastLinkRotation.z
                    );
                }
                else if (!float.IsNaN(endPos.x) && !float.IsNaN(endPos.y) && !float.IsNaN(endPos.z))
                {
                    // If customization is disabled, just look at end position
                    chainLink.transform.LookAt(endPos);
                }
            }

            // Apply rotation offset if needed (adjust this based on your model's forward direction)
            chainLink.transform.Rotate(90, 0, 0, Space.Self); // Base rotation for all chain links

            // Apply alternating Y rotation (0° for odd indices, 90° for even indices)
            if (i % 2 == 0) // Even numbered links (including 0)
            {
                chainLink.transform.Rotate(0, 90, 0, Space.Self);
            }
            // Odd numbered links keep the default rotation

            // Apply thickness
            chainLink.transform.localScale = new Vector3(
                chainLink.transform.localScale.x * chainThickness,
                chainLink.transform.localScale.y * chainThickness,
                chainLink.transform.localScale.z * chainThickness
            );

            // Parent to chain parent
            chainLink.transform.SetParent(chainParent);
        }

        Debug.Log($"Generated chain with {linkCount} links between lantern and mount.");
    }

    // Clear all chain links
    public void ClearChain()
    {
        if (chainParent != null)
        {
            // Remove all children
            while (chainParent.childCount > 0)
            {
                DestroyImmediate(chainParent.GetChild(0).gameObject);
            }
        }
    }

    // Update is called when values change in editor
    void Update()
    {
        if (!Application.isPlaying && autoUpdate)
        {
            // Only regenerate if the objects have moved
            if (lanternModel != null && lanternMountModel != null)
            {
                Vector3 startPos = lanternMountModel.transform.position;
                Vector3 endPos = lanternModel.transform.position;
                float distance = Vector3.Distance(startPos, endPos);
                int linkCount = Mathf.Max(2, Mathf.FloorToInt(distance / chainLinkSpacing));

                // Regenerate if distance or link count changed
                if (linkCount != previousLinkCount)
                {
                    GenerateChain();
                }
            }
        }
    }
}

// Custom editor to add buttons
[CustomEditor(typeof(ChainGenerator))]
public class ChainGeneratorEditor : Editor
{
    public override void OnInspectorGUI()
    {
        // Draw default inspector
        DrawDefaultInspector();

        // Get reference to the script
        ChainGenerator chainGenerator = (ChainGenerator)target;

        // Add space
        EditorGUILayout.Space();

        // Add Generate button
        if (GUILayout.Button("Generate Chain"))
        {
            chainGenerator.GenerateChain();
        }

        // Add Clear button
        if (GUILayout.Button("Clear Chain"))
        {
            chainGenerator.ClearChain();
        }
    }
}
#endif
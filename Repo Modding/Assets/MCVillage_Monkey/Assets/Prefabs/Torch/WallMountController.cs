using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
#endif

public class WallMountController : MonoBehaviour
{
    [SerializeField] private Transform meshTransform;
    [SerializeField] private bool isWallMounted = false;

    public enum WallDirection { North, East, South, West }
    [SerializeField] private WallDirection wallDirection = WallDirection.North;

    // Constants for positioning and rotation
    private const float POSITION_BOUND = 0.5f;
    private const float X_ROTATION_ANGLE = 25f;

    // Store original local position and rotation for resetting
    private Vector3 originalLocalPosition;
    private Quaternion originalLocalRotation;

    private void Awake()
    {
        // If meshTransform is not assigned, try to find it 
        // (assuming it's the first child named "torchMesh")
        if (meshTransform == null)
        {
            meshTransform = transform.Find("torchMesh");
            if (meshTransform == null)
            {
                Debug.LogError("No mesh transform assigned and couldn't find a child named 'torchMesh'.");
                return;
            }
        }

        // Store the original local position and rotation
        originalLocalPosition = meshTransform.localPosition;
        originalLocalRotation = meshTransform.localRotation;
    }

    private void Start()
    {
        ApplyWallMountSettings();
    }

    // This can be called whenever settings change
    public void ApplyWallMountSettings()
    {
        if (meshTransform == null)
            return;

        if (isWallMounted)
        {
            ApplyWallMount();
        }
        else
        {
            ResetPosition();
        }
    }

    private void ApplyWallMount()
    {
        // Set relative position based on wall direction
        // This keeps the object within -0.5 to 0.5 range relative to parent
        Vector3 localOffset = Vector3.zero;

        switch (wallDirection)
        {
            case WallDirection.North:
                localOffset = new Vector3(0, 0, POSITION_BOUND);
                break;
            case WallDirection.East:
                localOffset = new Vector3(POSITION_BOUND, 0, 0);
                break;
            case WallDirection.South:
                localOffset = new Vector3(0, 0, -POSITION_BOUND);
                break;
            case WallDirection.West:
                localOffset = new Vector3(-POSITION_BOUND, 0, 0);
                break;
        }

        // Apply the local offset to the mesh transform
        meshTransform.localPosition = localOffset;

        // First, apply the local X rotation of 25 degrees to make it look like it's leaning from a wall
        Quaternion localXRotation = Quaternion.Euler(X_ROTATION_ANGLE, 0, 0);

        // Get the global Y rotation value based on the wall direction
        float globalYRotation = 0f;
        switch (wallDirection)
        {
            case WallDirection.North:
                globalYRotation = 0f;  // Facing north (Z+)
                break;
            case WallDirection.East:
                globalYRotation = 90f; // Facing east (X+)
                break;
            case WallDirection.South:
                globalYRotation = 180f; // Facing south (Z-)
                break;
            case WallDirection.West:
                globalYRotation = 270f; // Facing west (X-)
                break;
        }

        // Create a rotation that is relative to the world
        Quaternion worldYRotation = Quaternion.Euler(0, globalYRotation, 0);

        // To apply the world Y rotation to the mesh:
        // First convert the mesh to world space with the X rotation
        meshTransform.localRotation = localXRotation;
        // Then override its world rotation's Y component
        Vector3 currentEuler = meshTransform.rotation.eulerAngles;
        meshTransform.rotation = Quaternion.Euler(currentEuler.x, globalYRotation, currentEuler.z);
    }

    private void ResetPosition()
    {
        // Reset to the original local position and rotation
        meshTransform.localPosition = originalLocalPosition;
        meshTransform.localRotation = originalLocalRotation;
    }

    // Public methods to control settings through code if needed
    public void SetWallMounted(bool mounted)
    {
        isWallMounted = mounted;
        ApplyWallMountSettings();
    }

    public void SetWallDirection(WallDirection direction)
    {
        wallDirection = direction;
        if (isWallMounted)
        {
            ApplyWallMountSettings();
        }
    }
}

#if UNITY_EDITOR
[CustomEditor(typeof(WallMountController))]
public class WallMountControllerEditor : Editor
{
    public override void OnInspectorGUI()
    {
        // Draw the default inspector
        DrawDefaultInspector();

        // Get a reference to the script
        WallMountController controller = (WallMountController)target;

        // Add a button to apply changes immediately
        if (GUILayout.Button("Apply Settings"))
        {
            controller.ApplyWallMountSettings();
            // Mark the scene as dirty so Unity knows it needs to be saved
            EditorUtility.SetDirty(controller.gameObject);
            if (!Application.isPlaying)
            {
                UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(controller.gameObject.scene);
            }
        }
    }
}
#endif
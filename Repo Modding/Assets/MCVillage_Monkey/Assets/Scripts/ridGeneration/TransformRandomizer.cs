// TransformRandomizer.cs - Component for randomizing transform properties

using UnityEngine;

namespace GridGeneration
{

    /// <summary>
    /// Component for randomizing transform properties
    /// </summary>
    [System.Serializable]
    public class TransformRandomizer
    {
        [Header("Scale Randomization")]
        public bool randomizeScale = false;
        public Vector3 minScale = new Vector3(0.8f, 0.8f, 0.8f);
        public Vector3 maxScale = new Vector3(1.2f, 1.2f, 1.2f);

        [Header("Rotation Randomization")]
        public bool randomizeRotation = false;
        public Vector3 minRotation = Vector3.zero;
        public Vector3 maxRotation = new Vector3(0, 360, 0);

        /// <summary>
        /// Apply randomization to a transform
        /// </summary>
        public void ApplyRandomization(Transform transform)
        {
            if (randomizeScale)
            {
                transform.localScale = new Vector3(
                    Random.Range(minScale.x, maxScale.x),
                    Random.Range(minScale.y, maxScale.y),
                    Random.Range(minScale.z, maxScale.z)
                );
            }

            if (randomizeRotation)
            {
                transform.localRotation = Quaternion.Euler(
                    Random.Range(minRotation.x, maxRotation.x),
                    Random.Range(minRotation.y, maxRotation.y),
                    Random.Range(minRotation.z, maxRotation.z)
                );
            }
        }
    }
}


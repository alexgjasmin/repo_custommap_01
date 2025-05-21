// CustomGridObjectType.cs - Interface for custom grid object types
using UnityEngine;

namespace GridGeneration
{

    /// <summary>
    /// Interface for custom grid object types
    /// </summary>
    public interface IGridObjectType
    {
        string Name { get; }
        void SetupInstance(GameObject instance, Vector3Int gridPosition);
    }

    /// <summary>
    /// Base class for custom grid object types
    /// </summary>
    [System.Serializable]
    public abstract class BaseGridObjectType : IGridObjectType
    {
        [SerializeField] protected string typeName;
        [SerializeField] protected GameObject prefab;
        [SerializeField] protected bool enabled = true;

        public string Name => typeName;
        public GameObject Prefab => prefab;
        public bool Enabled => enabled;

        public abstract void SetupInstance(GameObject instance, Vector3Int gridPosition);
    }
}
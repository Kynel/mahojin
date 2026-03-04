using UnityEngine;

namespace DuckovProto.AI
{
    public sealed class ArenaWallBootstrapper : MonoBehaviour
    {
        [SerializeField] private bool createOnStart = true;
        [SerializeField] private float halfSizeX = 14f;
        [SerializeField] private float halfSizeZ = 14f;
        [SerializeField] private float wallHeight = 2f;
        [SerializeField] private float wallThickness = 1f;
        [SerializeField] private float wallY = 1f;
        [SerializeField] private string wallLayerName = "Wall";

        private void Start()
        {
            if (!createOnStart)
            {
                return;
            }

            if (GameObject.Find("MapRoot") != null)
            {
                return;
            }

            if (GameObject.Find("Walls_Runtime") != null)
            {
                return;
            }

            int wallLayer = LayerMask.NameToLayer(wallLayerName);
            GameObject root = new GameObject("Walls_Runtime");
            root.transform.position = Vector3.zero;

            CreateWall(root.transform, "Wall_North", new Vector3(0f, wallY, halfSizeZ), new Vector3(halfSizeX * 2f, wallHeight, wallThickness), wallLayer);
            CreateWall(root.transform, "Wall_South", new Vector3(0f, wallY, -halfSizeZ), new Vector3(halfSizeX * 2f, wallHeight, wallThickness), wallLayer);
            CreateWall(root.transform, "Wall_East", new Vector3(halfSizeX, wallY, 0f), new Vector3(wallThickness, wallHeight, halfSizeZ * 2f), wallLayer);
            CreateWall(root.transform, "Wall_West", new Vector3(-halfSizeX, wallY, 0f), new Vector3(wallThickness, wallHeight, halfSizeZ * 2f), wallLayer);
        }

        private static void CreateWall(Transform parent, string name, Vector3 position, Vector3 scale, int wallLayer)
        {
            GameObject wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
            wall.name = name;
            wall.transform.SetParent(parent, false);
            wall.transform.position = position;
            wall.transform.localScale = scale;
            if (wallLayer >= 0)
            {
                wall.layer = wallLayer;
            }
        }
    }
}

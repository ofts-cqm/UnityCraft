using UnityEngine;

namespace render
{

    [RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
    public class TargetOutlineRender : MonoBehaviour
    {
        [Min(0.001f)] public float size = 1f;

        private Mesh _mesh;

        private void Awake()
        {
            CreateWireCube();
        }

        private void OnValidate()
        {
            if (Application.isPlaying)
                CreateWireCube();
        }

        private void CreateWireCube()
        {
            Vector3[] vertices =
            {
                new(0,    0,    0   ), // 0
                new(size, 0,    0   ), // 1
                new(size, 0,    size), // 2
                new(0,    0,    size), // 3

                new(0,    size, 0   ), // 4
                new(size, size, 0   ), // 5
                new(size, size, size), // 6
                new(0,    size, size), // 7
            };

            // Every pair forms one edge.
            int[] edges =
            {
                // Bottom
                0, 1,
                1, 2,
                2, 3,
                3, 0,

                // Top
                4, 5,
                5, 6,
                6, 7,
                7, 4,

                // Vertical
                0, 4,
                1, 5,
                2, 6,
                3, 7
            };

            if (_mesh == null)
            {
                _mesh = new Mesh
                {
                    name = "Wire Cube"
                };

                GetComponent<MeshFilter>().sharedMesh = _mesh;
            }
            else
            {
                _mesh.Clear();
            }

            _mesh.vertices = vertices;
            _mesh.SetIndices(edges, MeshTopology.Lines, 0);
            _mesh.RecalculateBounds();
        }
    }
}
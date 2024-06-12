using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class HalfEdge : MonoBehaviour
{
    MeshFilter meshFilter;
    Mesh mesh;

    struct Edge
    {
        public int v0;
        public int v1;
    }

    struct HalfEdgeStructure
    {
        public int next;
        public int prev;
        public int face;
        public int edge;

    }

    // Start is called before the first frame update
    void Start()
    {
        mesh = meshFilter.sharedMesh;
        int[] triangleIndices = mesh.GetTriangles(0);

        List<Edge> edges = new List<Edge>();
        for (int i = 0; i < mesh.triangles.Length; i++)
        {
            Edge edge = new Edge();
            edge.v0 = triangleIndices[i];
            edge.v1 = triangleIndices[i + 1];
            edges.Add(edge);
        }
    }

    // Update is called once per frame
    void Update()
    {
        
    }
}

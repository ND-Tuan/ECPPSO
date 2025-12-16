using UnityEngine;
using System.Collections.Generic;

[RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
public class PolygonMesh : MonoBehaviour
{
    [SerializeField] private MeshCollider meshCollider;

    public Vector2[] points;

    // private void Start()
    // {
    //     CreatePolygon(points);
    // }

    public void CreatePolygon(Vector2[] points)
    {
        this.points = points;

        Mesh mesh = new Mesh();

        // Chuyển Vector2 → Vector3
        Vector3[] vertices = new Vector3[points.Length];
        for (int i = 0; i < points.Length; i++)
        {
            vertices[i] = new Vector3(points[i].x, points[i].y, 0);
        }

        // Triangulate
        int[] triangles = Triangulate(points);

        mesh.vertices = vertices;
        mesh.triangles = triangles;
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();

        GetComponent<MeshFilter>().mesh = mesh;
        meshCollider.sharedMesh = mesh;
    }

    // Triangulation đơn giản (fan method – dùng cho đa giác lồi)
    private int[] Triangulate(Vector2[] pts)
    {
        List<int> tris = new List<int>();

        for (int i = 1; i < pts.Length - 1; i++)
        {
            tris.Add(0);
            tris.Add(i);
            tris.Add(i + 1);
        }

        return tris.ToArray();
    }

    public bool Contains(Vector2 worldPoint)
    {
        if (meshCollider == null) meshCollider = GetComponent<MeshCollider>();

        // Chuyển worldPoint sang local space
        Vector3 localPoint = transform.InverseTransformPoint(worldPoint);

        return meshCollider != null && meshCollider.bounds.Contains(localPoint);
    }

    // Find closest point on polygon edge and return outward normal
    public Vector2 GetClosestEdgeNormal(Vector2 worldPoint, out float distance)
    {
        distance = float.MaxValue;

        if (points == null || points.Length < 2)
            return Vector2.zero;

        // Tính tâm đa giác
        Vector2 center = Vector2.zero;
        for (int i = 0; i < points.Length; i++)
            center += points[i];
        center /= points.Length;

        float minAbsDist = float.MaxValue;
        Vector2 bestNormal = Vector2.zero;

        for (int i = 0; i < points.Length; i++)
        {
            Vector2 a = points[i];
            Vector2 b = points[(i + 1) % points.Length];
            Vector2 edge = b - a;

            float edgeLen = edge.magnitude;
            if (edgeLen < 1e-6f)
                continue;

            // normal vuông góc với cạnh
            Vector2 edgeDir = edge / edgeLen;
            Vector2 normal = new Vector2(-edgeDir.y, edgeDir.x);

            // khoảng cách vuông góc (signed)
            float signedDist = Vector2.Dot(worldPoint - a, normal);

            float absDist = Mathf.Abs(signedDist);
            if (absDist < minAbsDist)
            {
                minAbsDist = absDist;

                // normal hướng từ cạnh → point
                normal = signedDist >= 0 ? normal : -normal;

                // đảm bảo đẩy ra ngoài polygon
                Vector2 toCenter = center - a;
                if (Vector2.Dot(normal, toCenter) > 0)
                    normal = -normal;

                bestNormal = normal;
                distance = absDist;
            }
        }

        return bestNormal;
    }

}

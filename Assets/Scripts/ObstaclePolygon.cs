using UnityEngine;
using System.Collections.Generic;

[RequireComponent(typeof(PolygonCollider2D), typeof(SpriteRenderer))]
public class ObstaclePolygon : MonoBehaviour
{
    public PolygonCollider2D poly;
    public SpriteRenderer sr;
    public LineRenderer lineRenderer;
    [Range(3, 10)] public int vertexCount = 5; // số đỉnh
    public float minRadius = 2f;
    public float maxRadius = 6f;

    public void Initialize(Vector2 center, int vertexCount, float minRadius, float maxRadius)
    {
        transform.position = center;
        this.vertexCount = vertexCount;
        this.minRadius = minRadius;
        this.maxRadius = maxRadius;

        poly = GetComponent<PolygonCollider2D>();
        sr = GetComponent<SpriteRenderer>();

        // Sinh ngẫu nhiên polygon
        Vector2[] verts = new Vector2[vertexCount];
        float angleStep = 360f / vertexCount;
        float randomOffset = Random.Range(0f, 360f);

        for (int i = 0; i < vertexCount; i++)
        {
            float angle = (angleStep * i + randomOffset) * Mathf.Deg2Rad;
            float r = Random.Range(minRadius, maxRadius);
            verts[i] = new Vector2(Mathf.Cos(angle) * r, Mathf.Sin(angle) * r);
        }

        poly.points = verts;

        // random xoay
        transform.rotation = Quaternion.Euler(0, 0, Random.Range(0f, 360f));

        // random màu
        if (sr)
        {
            
            sr.sortingOrder = 1;
        }

        
        lineRenderer.positionCount = poly.points.Length + 1;
        lineRenderer.loop = true;
        lineRenderer.widthMultiplier = 0.5f;
        lineRenderer.useWorldSpace = false;
        lineRenderer.startColor = Color.red;
        lineRenderer.endColor = Color.red;

        for (int i = 0; i < poly.points.Length; i++)
            lineRenderer.SetPosition(i, poly.points[i]);
            lineRenderer.SetPosition(poly.points.Length, poly.points[0]);
        }

    // Kiểm tra 1 điểm có nằm trong vật cản không
    public bool Contains(Vector2 worldPoint)
    {
        Vector2 localPoint = transform.InverseTransformPoint(worldPoint);
        return poly.OverlapPoint(localPoint);
    }

    public float GetMaxRadius()
    {
        float max = 0f;
        foreach (var v in poly.points)
        {
            float dist = v.magnitude;
            if (dist > max) max = dist;
        }
        return max * transform.localScale.x;
    }
}

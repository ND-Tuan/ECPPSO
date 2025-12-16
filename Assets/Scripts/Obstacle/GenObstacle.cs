using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public static class GenObstacle 
{
    public static Vector2[] GenConvexPolygon(int vertexCount, float maxSize)
    {
        // 🔹 Center phân bố đều toàn vùng
    Vector2 center = new Vector2(
        Random.Range(-Controller.Instance.areaL / 2f, Controller.Instance.areaL / 2f),
        Random.Range(-Controller.Instance.areaW / 2f, Controller.Instance.areaW / 2f)
    );

    float baseRadius = maxSize * Random.Range(0.7f, 0.9f);
    float angleStep = Mathf.PI * 2f / vertexCount;

    List<Vector2> points = new List<Vector2>();

    for (int i = 0; i < vertexCount; i++)
    {
        // 🔹 Góc gần đều → không self-intersect
        float angle = i * angleStep
                    + Random.Range(-angleStep * 0.2f, angleStep * 0.2f);

        // 🔹 Bán kính dao động nhẹ → không méo
        float r = baseRadius
                + Random.Range(-maxSize * 0.15f, maxSize * 0.15f);

        Vector2 p = center + new Vector2(
            Mathf.Cos(angle) * r,
            Mathf.Sin(angle) * r
        );

        points.Add(p);
    }

    return points.ToArray();
    }
}

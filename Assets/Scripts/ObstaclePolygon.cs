using UnityEngine;
using System.Collections.Generic;

[RequireComponent(typeof(CircleCollider2D), typeof(SpriteRenderer))]
public class ObstaclePolygon : MonoBehaviour
{
    public CircleCollider2D circleCollider;
    public SpriteRenderer sr;
    
    public float radius = 5f;
    public Vector2 pos;

    public void Initialize(float radius, Vector2 position)
    {
        this.radius = radius;
        this.pos = position;


        if (circleCollider == null) circleCollider = GetComponent<CircleCollider2D>();
        if (sr == null) sr = GetComponent<SpriteRenderer>();

        //circleCollider.radius = radius;
        transform.position = position;
        transform.localScale = new Vector2(radius * 2, radius * 2);
    }

     public bool Contains(Vector2 worldPoint)
    {
        if (circleCollider == null) circleCollider = GetComponent<CircleCollider2D>();
        // OverlapPoint expects world-space coordinates for Collider2D
        return circleCollider != null && circleCollider.OverlapPoint(worldPoint);
    }
}

using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Station : MonoBehaviour
{
    public Vector2 position;
    public float radius;
    public Transform rangeCircle;

    public bool isMoving = false;

    public void Initialize(Vector2 pos, float rad)
    {
        position = pos;
        radius = rad;
        transform.position = new Vector3(pos.x, pos.y, 0);
        rangeCircle.localScale = new Vector3(rad * 2, rad * 2, 1); // Diameter
    }

    public void SetPosition(Vector2 pos)
    {
        position = pos;
        transform.position = new Vector3(pos.x, pos.y, 0);
    }

   
}

using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public class DataStruc
{
    public List<Vector2Group> positionGroups;
    public int areaL = 50;
    public int areaW = 50;
    public int station_num = 50;
    public float stationRadius = 5f;

    public int populationSize = 50;
    public int maxIterations = 200;
    public float w = 0.4f;
    public float c1 = 2.0f;
    public float c2 = 2.0f;

    public List<ObstacleData> obstacles;

    [Serializable]
    public class ObstacleData
    {
        public Vector2 pos;
        public float radius;
    }

    [Serializable]
    public class Vector2Group
    {
        public List<Vector2> points;
    }
}

using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public interface IOptimizer
{
    void Initialize(int populationSize, int stationNum, List<Vector2> initialPositions, int areaL, int areaW, float radius, float w, float c1, float c2, int maxIterations);
    void RunIteration();
    List<Vector2> GetBestSolution();
    float GetBestCoverage();
}


using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public interface IOptimizer
{
    void Initialize();
    void RunIteration();
    List<Vector2> GetBestSolution();
    float GetBestCoverage();
}


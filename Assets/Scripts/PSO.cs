using System.Collections.Generic;
using UnityEngine;
using System.Linq;
using System.Collections;

public class PSO : IOptimizer
{

    private int areaL, areaW;
    private float radius, r2;
    private int stationNum;
    private int populationSize;
    private float w = 0.4f, c1 = 2f, c2 = 2f;

    private List<Particle> population;
    private Particle gBest;
    private float globalBestFitness = -1f;
    private int iteration = 1;
    private int maxIterations = 200;

    private float vmax; // giới hạn vận tốc
    private List<List<Vector2>> initialPositions = new List<List<Vector2>>();

    // =====================================================
    // Init
    // =====================================================
    public void Initialize()
    {
        //Load giá trị từ Controller
        populationSize  = Controller.Instance.populationSize;
        stationNum      = Controller.Instance.station_num;
        areaL           = Controller.Instance.areaL;
        areaW           = Controller.Instance.areaW;
        radius          = Controller.Instance.stationRadius;
        w               = Controller.Instance.w;
        c1              = Controller.Instance.c1;
        c2              = Controller.Instance.c2;
        maxIterations   = Controller.Instance.maxIterations;

        r2 = radius * radius;
        vmax = radius;

        //  Load/Gen vị trí ban đầu
        if (Controller.Instance.testType == Controller.TestType.LoadInit)
            initialPositions = Controller.Instance.LoadInitial();
        else
            initialPositions.Clear();

        // Tạo quần thể
        population = new List<Particle>();
        for (int i = 0; i < populationSize; i++)
        {
            // Tạo cá thể mới
            var p = new Particle(stationNum);

            // Gán vị trí ban đầu
            if (Controller.Instance.testType == Controller.TestType.LoadInit)
                p.pos = initialPositions[i];
            else
            {
                for (int j = 0; j < stationNum; j++)
                    p.pos.Add(new Vector2(Random.Range(-areaL / 2f, areaL / 2f), Random.Range(-areaW / 2f, areaW / 2f)));

                initialPositions.Add(p.pos);
            }

            // Đánh giá fitness ban đầu
            Controller.Instance.Evaluate(p);
            p.pBest = new List<Vector2>(p.pos);
            p.pBestFitness = p.fitness;
            population.Add(p);
        }

        Controller.Instance.SetStationPositions(population[0].pos);

        // Xếp hạng quần thể và chọn gBest
        gBest = population.OrderByDescending(p => p.fitness).First();
        
        // Lưu vị trí ban đầu nếu là RandomInit
        if (Controller.Instance.testType == Controller.TestType.RandomInit && Controller.Instance.AutoSaveInitial)
            Controller.Instance.SaveInitial(initialPositions);
    }


    // =====================================================
    // Run iteration
    // =====================================================
    public void RunIteration()
    {
        foreach (var p in population)
        {
            for (int i = 0; i < stationNum; i++)
            {
                float r1 = Random.value;
                float r2r = Random.value;

                Vector2 inertia = w * p.vel[i];
                Vector2 cognitive = c1 * r1 * (p.pBest[i] - p.pos[i]);
                Vector2 social = c2 * r2r * (gBest.pos[i] - p.pos[i]);

                Vector2 v = inertia + cognitive + social;

                // giới hạn vận tốc
                v = Vector2.ClampMagnitude(v, vmax);

                Vector2 newPos = p.pos[i] + v;

                // bounce biên
                if (newPos.x > areaL / 2f || newPos.x < -areaL / 2f)
                {
                    v.x *= -0.5f;
                    newPos.x = Mathf.Clamp(newPos.x, -areaL / 2f, areaL / 2f);
                }
                if (newPos.y > areaW / 2f || newPos.y < -areaW / 2f)
                {
                    v.y *= -0.5f;
                    newPos.y = Mathf.Clamp(newPos.y, -areaW / 2f, areaW / 2f);
                }

                p.vel[i] = v;
                p.pos[i] = newPos;
            }

            Controller.Instance.Evaluate(p);
        }

        if (Controller.Instance.useGA) GA.ApplyGeneticEvolution(population);

        // ✅ update gBest toàn cục
        var candidate = population.OrderByDescending(p => p.fitness).First();
        if (candidate.fitness > gBest.fitness)
        {
            gBest = candidate;
        }

        iteration++;
    }

    public List<Vector2> GetBestSolution() => new List<Vector2>(gBest.pos);
    public float GetBestCoverage() => gBest.fitness * 100f;
}

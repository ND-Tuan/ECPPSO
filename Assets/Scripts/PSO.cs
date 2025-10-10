using System.Collections.Generic;
using UnityEngine;
using System.Linq;
using System.Collections;

public class PSO : IOptimizer
{
    private class Particle
    {
        public List<Vector2> pos;
        public List<Vector2> vel;
        public List<Vector2> pBest;
        public float fitness;
        public float pBestFitness;

        public Particle(int stationNum, int areaL, int areaW)
        {
            pos = new List<Vector2>();
            vel = new List<Vector2>();

            for (int i = 0; i < stationNum; i++)
            {
                pos.Add(new Vector2(Random.Range(-areaL / 2f, areaL / 2f),
                                    Random.Range(-areaW / 2f, areaW / 2f)));
                vel.Add(Vector2.zero);
            }

            pBest = new List<Vector2>(pos);
            pBestFitness = -1f;
        }
    }

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

    // =====================================================
    // Init
    // =====================================================
    public void Initialize(int populationSize, int stationNum, int areaL, int areaW, float radius, float w, float c1, float c2, int maxIterations)
    {
        this.populationSize = populationSize;
        this.stationNum = stationNum;
        this.areaL = areaL;
        this.areaW = areaW;
        this.radius = radius;
        r2 = radius * radius;
        vmax = radius; // tốc độ tối đa = bán kính phủ

        this.w = w;
        this.c1 = c1;
        this.c2 = c2;
        this.maxIterations = maxIterations;

        population = new List<Particle>();
        for (int i = 0; i < populationSize; i++)
        {
            var p = new Particle(stationNum, areaL, areaW);
            Evaluate(p);
            population.Add(p);
        }

        gBest = population.OrderByDescending(p => p.fitness).First();
        globalBestFitness = gBest.fitness;
    }

    // =====================================================
    // Evaluate coverage fitness
    // =====================================================
    private void Evaluate(Particle p)
    {
        int total = areaL * areaW;
        BitArray coveredBits = new BitArray(total); // mỗi bit = 1 sample
        int coveredCount = 0;

        float offsetX = -areaL / 2f;
        float offsetY = -areaW / 2f;
        float r2 = radius * radius;

        // Duyệt từng trạm
        foreach (var s in p.pos)
        {
            // Giới hạn vùng ảnh hưởng trạm
            int xMin = Mathf.Max(0, Mathf.FloorToInt(s.x - radius - offsetX));
            int xMax = Mathf.Min(areaL - 1, Mathf.CeilToInt(s.x + radius - offsetX));
            int yMin = Mathf.Max(0, Mathf.FloorToInt(s.y - radius - offsetY));
            int yMax = Mathf.Min(areaW - 1, Mathf.CeilToInt(s.y + radius - offsetY));

            for (int y = yMin; y <= yMax; y++)
            {
                for (int x = xMin; x <= xMax; x++)
                {
                    int index = y * areaL + x; // vị trí bit tương ứng với sample (x,y)
                    if (coveredBits[index]) continue; // ✅ skip nếu đã phủ

                    Vector2 sample = new Vector2(offsetX + x + 0.5f, offsetY + y + 0.5f);

                    if ((s - sample).sqrMagnitude <= r2)
                    {
                        coveredBits[index] = true;
                        coveredCount++;
                    }
                }
            }
        }

        // coverage %
        p.fitness = (float)coveredCount / total;

        // cập nhật pBest nếu tốt hơn
        if (p.fitness > p.pBestFitness)
        {
            p.pBestFitness = p.fitness;
            p.pBest = new List<Vector2>(p.pos);
        }
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

            Evaluate(p);
        }

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

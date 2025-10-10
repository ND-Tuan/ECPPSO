using System.Collections.Generic;
using UnityEngine;
using System.Linq;
using System.Collections;

/// <summary>
/// ECPPSO + Genetic Evolution (crossover + mutation)
/// - Dựa trên class ECPPSO bạn cung cấp; thêm GA step vào cuối mỗi iteration.
/// - GA parameters: eliteFrac, mutationRate, mutationStep.
/// - Children replace bottom G% (same G_percent used for SE group).
/// </summary>
public class ECPPSO : IOptimizer
{
    private class Particle
    {
        public List<Vector2> pos;
        public List<Vector2> vel;
        public List<Vector2> pBest;
        public float fitness;
        public float pBestFitness;
        public List<Vector2> u; // vector dự đoán (Eq.6)

        public Particle(int stationNum, int areaL, int areaW)
        {
            pos = new List<Vector2>();
            vel = new List<Vector2>();
            u = new List<Vector2>();

            for (int i = 0; i < stationNum; i++)
            {
                pos.Add(new Vector2(Random.Range(-areaL / 2f, areaL / 2f),
                                    Random.Range(-areaW / 2f, areaW / 2f)));
                vel.Add(Vector2.zero);
                u.Add(Vector2.zero);
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

    private int neighborCount = 3;
    private int G_percent = 15;

    private List<Particle> population;
    private Particle gBest;
    private int iteration = 1;
    private int maxIterations;

    // GA params (tunable)
    private float eliteFrac = 0.3f;      // top fraction used as parents
    private float mutationRate = 0.05f;  // per-gene mutation probability
    private float mutationStep = 1.0f;   // mutation displacement magnitude

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

        this.w = w;
        this.c1 = c1;
        this.c2 = c2;

        this.maxIterations = maxIterations;

        population = new List<Particle>();
        for (int i = 0; i < populationSize; i++)
        {
            var p = new Particle(stationNum, areaL, areaW);
            Evaluate(p);
            p.pBest = new List<Vector2>(p.pos);
            p.pBestFitness = p.fitness;
            population.Add(p);
        }
        gBest = population.OrderByDescending(p => p.fitness).First();
    }

    public void SetParams(int G_percent, int neighborCount)
    {
        this.G_percent = G_percent;
        this.neighborCount = neighborCount;
    }

    /// <summary>
    /// Optional: expose GA tuning
    /// </summary>
    public void SetGAParams(float eliteFraction = 0.3f, float mutationRate = 0.05f, float mutationStep = 1.0f)
    {
        this.eliteFrac = Mathf.Clamp01(eliteFraction);
        this.mutationRate = Mathf.Clamp01(mutationRate);
        this.mutationStep = Mathf.Max(0f, mutationStep);
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
    // NEP (Neighbor-based Evolution Prediction)
    // =====================================================
    private void ApplyNEP(Particle p)
    {
        // uri,t = tổng dự đoán từ neighbors
        var neighbors = population.OrderBy(q => Mathf.Abs(q.fitness - p.fitness))
                                  .Take(neighborCount);
        List<Vector2> uri = new List<Vector2>();
        for (int i = 0; i < stationNum; i++)
        {
            Vector2 sum = p.u[i];
            foreach (var nb in neighbors)
                sum += nb.u[i];
            uri.Add(sum);
        }

        // V update (Eq.8)
        for (int i = 0; i < stationNum; i++)
        {
            float r1 = Random.value, r2r = Random.value;

            Vector2 inertia = w * p.vel[i];
            Vector2 gBestTerm = c1 * r1 * (gBest.pos[i] - p.pos[i]);
            Vector2 nepTerm = c2 * r2r * (uri[i] / Mathf.Max(1, iteration));

            p.vel[i] = inertia + gBestTerm + nepTerm;
        }
    }

    // =====================================================
    // SE (Strengthening Evolution)
    // =====================================================
    private void ApplySE(Particle p)
    {
        int d = 2; // 2D
        float Fd = 1f + d / 3f;

        for (int i = 0; i < stationNum; i++)
        {
            float r1 = Random.value, r2r = Random.value;

            Vector2 inertia = w * p.vel[i];
            Vector2 gBestTerm = c1 * r1 * (gBest.pos[i] - p.pos[i]);
            Vector2 seTerm = c2 * r2r * p.u[i] * ((float)iteration / maxIterations) * Fd;

            p.vel[i] = inertia + gBestTerm + seTerm;
        }
    }

    // =====================================================
    // GA helpers: crossover + mutation
    // =====================================================
    private List<Vector2> CrossoverUniform(List<Vector2> A, List<Vector2> B)
    {
        List<Vector2> child = new List<Vector2>(A.Count);
        for (int i = 0; i < A.Count; i++)
        {
            if (Random.value < 0.5f)
                child.Add(A[i]);
            else
                child.Add(B[i]);
        }
        return child;
    }

    private void MutatePositions(List<Vector2> pos)
    {
        for (int i = 0; i < pos.Count; i++)
        {
            if (Random.value < mutationRate)
            {
                Vector2 delta = new Vector2(Random.Range(-mutationStep, mutationStep),
                                            Random.Range(-mutationStep, mutationStep));
                pos[i] += delta;
                pos[i] = new Vector2(Mathf.Clamp(pos[i].x, -areaL / 2f, areaL / 2f),
                                     Mathf.Clamp(pos[i].y, -areaW / 2f, areaW / 2f));
            }
        }
    }

    /// <summary>
    /// Apply GA: create children from elites and replace worst ones.
    /// Assumes population is sorted descending by fitness (best first).
    /// </summary>
    private void ApplyGeneticEvolution()
    {
        if (population == null || population.Count == 0) return;

        int Gcount = Mathf.Max(1, populationSize * G_percent / 100);
        int eliteCount = Mathf.Max(2, Mathf.CeilToInt(populationSize * eliteFrac));
        eliteCount = Mathf.Min(eliteCount, population.Count);

        // parents from top elites (population should be sorted descending before calling)
        var elites = population.Take(eliteCount).ToList();

        // create replaceCount children and replace bottom Gcount particles
        for (int i = 0; i < Gcount; i++)
        {
            // select two random parents from elites
            var parentA = elites[Random.Range(0, eliteCount)];
            var parentB = elites[Random.Range(0, eliteCount)];

            // crossover using parents' pBest (use pBest to bias towards good positions)
            List<Vector2> childPos = CrossoverUniform(parentA.pBest, parentB.pBest);

            // mutation
            MutatePositions(childPos);

            // build child particle (reuse Particle constructor then overwrite pos/vel/u/pBest)
            var child = new Particle(stationNum, areaL, areaW);

            // replace child's pos with generated childPos
            child.pos = new List<Vector2>(childPos);

            // reset vel and u to zero (start fresh)
            for (int j = 0; j < stationNum; j++)
            {
                child.vel[j] = Vector2.zero;
                child.u[j] = Vector2.zero;
            }

            // set pBest initial to child's pos (so child can evaluate from this state)
            child.pBest = new List<Vector2>(child.pos);
            child.pBestFitness = -1f;

            // evaluate child
            Evaluate(child);

            // put child into population replacing the worst (bottom) ones
            int replaceIndex = population.Count - 1 - i; // bottom-most, i=0 worst
            if (replaceIndex >= 0 && replaceIndex < population.Count)
                population[replaceIndex] = child;
        }

        // After replacement, re-sort population (best first)
        population = population.OrderByDescending(p => p.fitness).ToList();
    }

    // =====================================================
    // Run iteration
    // =====================================================
    public void RunIteration()
    {
        int Gcount = Mathf.Max(1, populationSize * G_percent / 100);

        
        foreach (var p in population)
        {
            for (int i = 0; i < stationNum; i++)
            {
                Vector2 delta = p.pBest[i] - p.pos[i];
                p.u[i] = 0.5f*p.u[i] + delta;
            }
        }

        // sort by fitness (cao -> thấp)
        population = population.OrderByDescending(p => p.fitness).ToList();

        // chạy qua từng cá thể
        for (int idx = 0; idx < population.Count; idx++)
        {
            var p = population[idx];

            if (idx >= population.Count - Gcount)
            {
                // nhóm yếu
                ApplySE(p);
            }
            else
            {
                // nhóm khá
                ApplyNEP(p);
            }

            // update position với bounce biên + vmax (giữ như trước)
            float vmax = radius; // giới hạn vận tốc tối đa
            for (int i = 0; i < stationNum; i++)
            {
                // giới hạn vận tốc
                p.vel[i] = Vector2.ClampMagnitude(p.vel[i], vmax);

                Vector2 newPos = p.pos[i] + p.vel[i];

               
                if (newPos.x > areaL / 2f || newPos.x < -areaL / 2f)
                {
                    Vector2 vel = p.vel[i];
                    vel.x *= -0.5f;
                    p.vel[i] = vel;
                    newPos.x = Mathf.Clamp(newPos.x, -areaL / 2f, areaL / 2f);
                    
                }
                if (newPos.y > areaW / 2f || newPos.y < -areaW / 2f)
                {
                    Vector2 vel = p.vel[i];
                    vel.y *= -0.5f;
                    p.vel[i] = vel;
                    newPos.y = Mathf.Clamp(newPos.y, -areaW / 2f, areaW / 2f);
                }

                p.pos[i] = newPos;
            }

            Evaluate(p);
        }

        // --- GA evolution step: create children from elites and replace bottom G% ---
        // population is currently evaluated; GA will replace worst particles.
        // Note: after ApplyGeneticEvolution we will re-sort and then update gBest below.
        ApplyGeneticEvolution();

        // giữ gBest toàn cục (best-so-far)
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

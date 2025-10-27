using System.Collections.Generic;
using UnityEngine;
using System.Linq;
using System.Collections;

public class ECPPSO : IOptimizer
{

    [Header("Core Settings")]
    private int areaL, areaW;
    private float radius, r2;
    private int stationNum;
    private int populationSize;
    private float w, c1, c2;
    private int maxIterations;

    [Header("ECPPSO Settings")]
    private int neighborCount = 3;
    private int G_percent = 15;

    [Header("Obstacle Settings")]
    private float gamma = 0.1f;         // Eq.6 hệ số né vật cản
    private float delta = 0.05f;        // Eq.13 hệ số phạt SE
    private float losFactor = 0.7f;     // giảm F_SE khi bị che khuất
    private float vmax; // velocity limit

    private List<Particle> population;
    private Particle gBest;
    private int iteration = 1;
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
        G_percent       = Controller.Instance.G_percent;
        neighborCount   = Controller.Instance.neighborCount;


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
                {
                    // random vị trí trong vùng
                    Vector2 pos = new Vector2(Random.Range(-areaL / 2f, areaL / 2f), Random.Range(-areaW / 2f, areaW / 2f));

                    if( Controller.Instance.Obstacles.Count == 0)
                    {
                        p.pos.Add(pos);
                        continue;
                    }

                    bool insideObstacle;
                    do
                    {
                        insideObstacle = false;
                        pos = new Vector2(Random.Range(-areaL / 2f, areaL / 2f),
                                        Random.Range(-areaW / 2f, areaW / 2f));

                        foreach (var ob in Controller.Instance.Obstacles)
                        {
                            if (ob.Contains(pos))
                            {
                                insideObstacle = true;
                                break;
                            }
                        }
                    } while (insideObstacle);

                    p.pos.Add(pos);
                }
                    

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

    public void SetParams(int G_percent, int neighborCount)
    {
        this.G_percent = G_percent;
        this.neighborCount = neighborCount;
    }

    // =====================================================
    // NEP (Neighbor Evolution Prediction)
    // =====================================================
    private void ApplyNEP(Particle p)
    {
        var neighbors = population.Where(q => q != p).OrderBy(q => Mathf.Abs(q.fitness - p.fitness)).Take(neighborCount);
        List<Vector2> uri = new List<Vector2>();

        for (int i = 0; i < stationNum; i++)
        {
            Vector2 sum = p.u[i];
            foreach (var nb in neighbors)
                sum += nb.u[i];
            uri.Add(sum);
        }

        

        for (int i = 0; i < stationNum; i++)
        {
            Vector2 avoidance = Vector2.zero;
            if (Controller.Instance.useObstacles)
            {
                // hướng tránh vật cản gần nhất
                var nearest = Controller.Instance.Obstacles.OrderBy(o => (p.pos[i] - o.pos).sqrMagnitude).FirstOrDefault();
                if (nearest != null)
                {
                    float dist = Vector2.Distance(p.pos[i], nearest.pos);
                    if (dist < nearest.radius * 1.5f)
                    {
                        Vector2 dir = (p.pos[i] - nearest.pos).normalized;
                        avoidance = 0.1f * dir; // γ = 0.1
                    }
                }
            }

            uri[i] /= (neighbors.Count() + 1f);

            float r1 = Random.value, r2r = Random.value;
            Vector2 inertia = w * p.vel[i];
            Vector2 gBestTerm = c1 * r1 * (gBest.pos[i] - p.pos[i]);
            Vector2 nepTerm = c2 * r2r * (uri[i] / Mathf.Max(1, iteration));

            p.vel[i] = inertia + gBestTerm + nepTerm + avoidance;
        }
    }

    // =====================================================
    // SE (Strengthening Evolution)
    // =====================================================
    private void ApplySE(Particle p)
    {
        int d = 2;
        float Fd = 1f + d / 3f;

        if (Controller.Instance.useObstacles)
        {
            int nearObs = p.pos.Count(pos => Controller.Instance.Obstacles.Any(o => (pos - o.pos).magnitude < o.radius * 2f));
            Fd -= 0.05f * nearObs; // δ = 0.05
        }

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
    // Main iteration
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

        population = population.OrderByDescending(p => p.fitness).ToList();

        for (int idx = 0; idx < population.Count; idx++)
        {
            var p = population[idx];
            if (idx >= population.Count - Gcount)
                ApplySE(p); // nhóm yếu
            else
                ApplyNEP(p); // nhóm khá

            for (int i = 0; i < stationNum; i++)
            {

                // limit velocity to avoid explosion and unstable moves
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

                // nếu có vật cản, bật ra ngoài (sử dụng ObstaclePolygon.Contains)
                if (Controller.Instance.useObstacles && Controller.Instance != null)
                {
                    foreach (var obs in Controller.Instance.Obstacles)
                    {
                        if (obs == null) continue;
                        if (obs.Contains(newPos))
                        {
                            Vector2 dir = (newPos - obs.pos);
                            if (dir.sqrMagnitude < 1e-6f)
                                dir = Random.insideUnitCircle.normalized;
                            else
                                dir = dir.normalized;

                            float margin = 0.1f;
                            newPos = obs.pos + dir * (obs.radius + radius + margin);
                        }
                    }
                }

                p.pos[i] = newPos;
            }

            Controller.Instance.Evaluate(p);
        }
        
        if (Controller.Instance.useGA) GA.ApplyGeneticEvolution(population);

        var candidate = population.OrderByDescending(p => p.fitness).First();
        if (candidate.fitness > gBest.fitness)
            gBest = candidate;

        iteration++;
    }

    public List<Vector2> GetBestSolution() => new List<Vector2>(gBest.pos);
    public float GetBestCoverage() => gBest.fitness * 100f;
}

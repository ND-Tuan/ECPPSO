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
        {
            // Chuyển đổi từ positionGroups sang List<List<Vector2>>
            initialPositions = new List<List<Vector2>>();
            if (Controller.Instance.dataStruc.positionGroups != null)
            {
                foreach (var group in Controller.Instance.dataStruc.positionGroups)
                {
                    initialPositions.Add(group.points);
                }
            }
        }
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

            Controller.Instance.SetStationPositions(p.pBest);
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
        var neighbors = population.OrderBy(q => Mathf.Abs(q.fitness - p.fitness)).Take(neighborCount);
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
                // Tính lực đẩy từ tất cả các vật cản
                foreach (var obs in Controller.Instance.Obstacles)
                {
                    if (obs == null) continue;
                    
                    float dist = Vector2.Distance(p.pos[i], obs.pos);
                    float effectiveRange = obs.radius * 2f;
                    
                    if (dist < effectiveRange && dist > 0.01f)
                    {
                        Vector2 dir = (p.pos[i] - obs.pos).normalized;
                        // Lực đẩy tỷ lệ nghịch với khoảng cách (càng gần càng mạnh)
                        float strength = Controller.Instance.delta * (1f - dist / effectiveRange);
                        avoidance += dir * strength;
                    }
                }
            }

            uri[i] /= (neighbors.Count() + 1f);

            uri[i] = uri[i] + avoidance;

            float r1 = Random.value, r2r = Random.value;
            Vector2 inertia = w * p.vel[i];
            Vector2 gBestTerm = c1 * r1 * (gBest.pos[i] - p.pos[i]);
            Vector2 nepTerm = c2 * r2r * (uri[i] / Mathf.Max(1, iteration));

            

            p.vel[i] = inertia + gBestTerm + nepTerm + Controller.Instance.CalculateAvoidanceForce(p.pos[i]);
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
            Fd -= Controller.Instance.epsilon * nearObs; // δ = 0.05
        }

        for (int i = 0; i < stationNum; i++)
        {
            float r1 = Random.value, r2r = Random.value;

            Vector2 inertia = w * p.vel[i];
            Vector2 gBestTerm = c1 * r1 * (gBest.pos[i] - p.pos[i]);
            Vector2 seTerm = c2 * r2r * p.u[i] * ((float)iteration / maxIterations) * Fd;

            Vector2 avoidance = Controller.Instance.CalculateAvoidanceForce(p.pos[i]);

            p.vel[i] = inertia + gBestTerm + seTerm + avoidance;
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
                p.u[i] =0.5f * p.u[i] + delta;
            }
        }

        population = population.OrderByDescending(p => p.fitness).ToList();

        for (int idx = 0; idx < population.Count; idx++)
        {
            var p = population[idx];
            if (idx >= population.Count - Gcount){
                if(Controller.Instance.useSE) ApplySE(p); // nhóm yếu
            }
            else
            {
                if(Controller.Instance.useNEP) ApplyNEP(p); // nhóm khá
            }
                

            for (int i = 0; i < stationNum; i++)
            {

                // limit velocity to avoid explosion and unstable moves
                Vector2 v = Vector2.ClampMagnitude(p.vel[i], vmax);

                Vector2 newPos = p.pos[i] + v;

                if (newPos.x > areaL / 2f || newPos.x < -areaL / 2f)
                {
                    Vector2 vel = v;
                    vel.x *= -0.5f;
                    v = vel;
                    newPos.x = Mathf.Clamp(newPos.x, -areaL / 2f, areaL / 2f);

                }
                if (newPos.y > areaW / 2f || newPos.y < -areaW / 2f)
                {
                    Vector2 vel = v;
                    vel.y *= -0.5f;
                    v = vel;
                    newPos.y = Mathf.Clamp(newPos.y, -areaW / 2f, areaW / 2f);
                }

                 // Bật ra ngoài nếu vào vật cản
                if (Controller.Instance.useObstacles)
                {
                    foreach (var obs in Controller.Instance.Obstacles)
                    {
                        if (obs.Contains(newPos))
                        {
                            Vector2 dir = (newPos - obs.pos).normalized;
                            newPos = obs.pos + dir * (obs.radius +0.5f);
                            v *= 0.5f; // giảm tốc độ sau va chạm
                        }
                    }
                }

                p.vel[i] = v;
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
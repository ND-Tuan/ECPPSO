using System.Collections.Generic;
using UnityEngine;
using System.Linq;

public class GA_alone : IOptimizer
{
    private int areaL, areaW;
    private float radius, r2;
    private int stationNum;
    private int populationSize;
    private int maxIterations;

    private List<Particle> population;
    private Particle gBest;
    private int iteration = 1;

    private List<List<Vector2>> initialPositions = new List<List<Vector2>>();

    public void Initialize()
    {
        populationSize = Controller.Instance.populationSize;
        stationNum = Controller.Instance.station_num;
        areaL = Controller.Instance.areaL;
        areaW = Controller.Instance.areaW;
        radius = Controller.Instance.stationRadius;
        maxIterations = Controller.Instance.maxIterations;

        r2 = radius * radius;

        // Load or clear initial positions
        if (Controller.Instance.testType == Controller.TestType.LoadInit)
            initialPositions = Controller.Instance.LoadInitial();
        else
            initialPositions.Clear();

        population = new List<Particle>();
        for (int i = 0; i < populationSize; i++)
        {
            var p = new Particle(stationNum);

            if (Controller.Instance.testType == Controller.TestType.LoadInit)
            {
                p.pos = initialPositions[i];
            }
            else
            {
                for (int j = 0; j < stationNum; j++)
                {
                    Vector2 pos = new Vector2(Random.Range(-areaL / 2f, areaL / 2f), Random.Range(-areaW / 2f, areaW / 2f));

                    if (Controller.Instance.Obstacles.Count == 0)
                    {
                        p.pos.Add(pos);
                        continue;
                    }

                    bool insideObstacle;
                    do
                    {
                        insideObstacle = false;
                        pos = new Vector2(Random.Range(-areaL / 2f, areaL / 2f), Random.Range(-areaW / 2f, areaW / 2f));

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

            Controller.Instance.Evaluate(p);
            // For GA standalone, treat current position as pBest reference
            p.pBest = new List<Vector2>(p.pos);
            p.pBestFitness = p.fitness;
            population.Add(p);
        }

        Controller.Instance.SetStationPositions(population[0].pos);

        gBest = population.OrderByDescending(p => p.fitness).First();

        if (Controller.Instance.testType == Controller.TestType.RandomInit && Controller.Instance.AutoSaveInitial)
            Controller.Instance.SaveInitial(initialPositions);
    }

    public void RunIteration()
    {
        if (population == null || population.Count == 0) return;

        // --- GA operators: elitism, selection, crossover, mutation ---
        int popSize = population.Count;
        float eliteFrac = Controller.Instance.E_percent / 100f;
        int eliteCount = Mathf.Max(1, Mathf.CeilToInt(popSize * eliteFrac));
        eliteCount = Mathf.Min(eliteCount, popSize - 1);

        // Sort population by fitness (descending)
        population = population.OrderByDescending(p => p.fitness).ToList();

        // Keep elites
        List<Particle> newPop = new List<Particle>();
        for (int e = 0; e < eliteCount; e++)
        {
            // make a copy of elite individual
            var elite = population[e];
            var copy = new Particle(stationNum);
            copy.pos = new List<Vector2>(elite.pos);
            copy.pBest = new List<Vector2>(copy.pos);
            copy.pBestFitness = elite.pBestFitness;
            // reset dynamics
            for (int j = 0; j < stationNum; j++) { copy.vel[j] = Vector2.zero; copy.u[j] = Vector2.zero; }
            Controller.Instance.Evaluate(copy);
            newPop.Add(copy);
        }

        // Fill rest of new population by creating children
        while (newPop.Count < popSize)
        {
            // selection (tournament)
            var parentA = TournamentSelect(population, 3);
            var parentB = TournamentSelect(population, 3);

            // crossover (uniform)
            List<Vector2> childPos = CrossoverUniform(parentA.pos, parentB.pos);

            // mutation
            MutatePositions(childPos);

            var child = new Particle(stationNum);
            child.pos = new List<Vector2>(childPos);
            for (int j = 0; j < stationNum; j++) { child.vel[j] = Vector2.zero; child.u[j] = Vector2.zero; }
            child.pBest = new List<Vector2>(child.pos);
            child.pBestFitness = -1f;

            Controller.Instance.Evaluate(child);

            newPop.Add(child);
        }

        population = newPop.OrderByDescending(p => p.fitness).ToList();

        // Update global best
        var candidate = population.First();
        if (gBest == null || candidate.fitness > gBest.fitness)
            gBest = candidate;

        iteration++;
    }

    public List<Vector2> GetBestSolution() => new List<Vector2>(gBest.pos);
    public float GetBestCoverage() => gBest.fitness * 100f;

    // --- Helper GA methods ---
    private Particle TournamentSelect(List<Particle> pool, int k)
    {
        Particle best = null;
        for (int i = 0; i < k; i++)
        {
            var cand = pool[Random.Range(0, pool.Count)];
            if (best == null || cand.fitness > best.fitness) best = cand;
        }
        return best;
    }

    private List<Vector2> CrossoverUniform(List<Vector2> A, List<Vector2> B)
    {
        int n = Mathf.Min(A.Count, B.Count);
        List<Vector2> child = new List<Vector2>(n);
        for (int i = 0; i < n; i++)
        {
            child.Add(Random.value < 0.5f ? A[i] : B[i]);
        }
        return child;
    }

    private void MutatePositions(List<Vector2> pos)
    {
        int areaL = Controller.Instance.areaL;
        int areaW = Controller.Instance.areaW;
        float mutationRate = Controller.Instance.mutationRate;
        float mutationStep = Controller.Instance.mutationStep;

        for (int i = 0; i < pos.Count; i++)
        {
            if (Random.value < mutationRate)
            {
                Vector2 delta = new Vector2(Random.Range(-mutationStep, mutationStep), Random.Range(-mutationStep, mutationStep));
                pos[i] += delta;
                pos[i] = new Vector2(Mathf.Clamp(pos[i].x, -areaL / 2f, areaL / 2f), Mathf.Clamp(pos[i].y, -areaW / 2f, areaW / 2f));

                // if inside obstacle, push it outside
                if (Controller.Instance.useObstacles && Controller.Instance.Obstacles != null && Controller.Instance.Obstacles.Count > 0)
                {
                    foreach (var obs in Controller.Instance.Obstacles)
                    {
                        if (obs == null) continue;
                        if (obs.Contains(pos[i]))
                        {
                            Vector2 dir = pos[i] - obs.pos;
                            if (dir.sqrMagnitude < 1e-6f) dir = Random.insideUnitCircle.normalized;
                            else dir = dir.normalized;
                            float margin = 0.1f;
                            pos[i] = obs.pos + dir * (obs.radius + Controller.Instance.stationRadius + margin);
                        }
                    }
                }
            }
        }
    }
}

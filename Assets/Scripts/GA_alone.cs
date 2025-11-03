using System.Collections.Generic;
using UnityEngine;
using System.Linq;

public class GA_alone : IOptimizer
{
    private int areaL, areaW;
    private int stationNum;
    private int populationSize;

    private List<Particle> population;
    private Particle gBest;

    private List<List<Vector2>> initialPositions = new List<List<Vector2>>();


    public void Initialize()
    {
        populationSize = Controller.Instance.populationSize;
        stationNum = Controller.Instance.station_num;
        areaL = Controller.Instance.areaL;
        areaW = Controller.Instance.areaW;

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
        population = population.OrderByDescending(p => p.fitness).ToList();

       

        for (int idx = 0; idx < population.Count; idx++)
        {

            var p = population[idx];
            Controller.Instance.Evaluate(p);
        }

        
        GA.ApplyGeneticEvolution(population);

        var candidate = population.OrderByDescending(p => p.fitness).First();
        if (candidate.fitness > gBest.fitness)
            gBest = candidate;
    }


    public List<Vector2> GetBestSolution() => new List<Vector2>(gBest.pos);
    public float GetBestCoverage() => gBest.fitness * 100f;

   
}

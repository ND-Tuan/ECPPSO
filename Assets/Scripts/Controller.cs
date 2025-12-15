using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using Unity.VisualScripting;
using UnityEngine;


public class Controller : MonoBehaviour
{
    public enum OptimizerType { PSO, ECPPSO, GA}
    public enum TestType { RandomInit, LoadInit }

    [Header("General Settings")]
    public bool useObstacles = false;

    public OptimizerType optimizerType;
    public TestType testType;
    public bool useGA = true;
    public bool useSE = true;
    public bool useNEP = true;

    public bool AutoSaveInitial = true;
    public string filePath;
    public bool SaveResults = true;
    public string fitnessFilePath;
    public bool SaveRunTimes = true;
    public string RunTimeFilePath;

    [SerializeField] private GameObject stationPrefab;
    private List<Station> Stations_List = new List<Station>();
    private List<float> FitnessValuesList = new List<float>();
    private List<Vector2> LineGraphPoints = new List<Vector2>();
    private List<List<Vector2>> SolutionsList = new List<List<Vector2>>();
    private IOptimizer optimizer;

    public float bounceOffset = 1.0f;
    private bool isOptimizing = false;
    private float bestCoverage = 0f;
    private int bestIter = 0;

    [Header("Area & Grid")]
    public int areaL = 50;
    public int areaW = 50;

    [Header("Base station & sampling")]
    public int station_num = 50; // number of stations to deploy (per solution)
    public float stationRadius = 5f;

    [Header("PSO params")]
    public int populationSize = 50;
    public int maxIterations = 200;
    public float w = 0.4f;
    public float c1 = 2.0f;
    public float c2 = 2.0f;

    [Header("ECPPSO params")]
    [Range(0, 100)] public int G_percent = 15; // bottom G% to apply SE
    public int neighborCount = 3;

    [Header("GA params")]
    [Range(0, 100)] public int E_percent = 15;      //% cá thể ưu tú làm cha mẹ
    [Range(0, 1)] public float mutationRate = 0.05f;  // xác suất đột biến
    [Range(0, 10)] public float mutationStep = 1.0f;   // độ dịch chuyển đột biến

    [Header("Obstacle")]
    public GameObject obstaclePolygonPrefab;
    public List<ObstaclePolygon> Obstacles = new List<ObstaclePolygon>();

    public int obstacleCount = 8;   // số vật cản
    public Vector2 radiusRange = new Vector2(2f, 6f);  // phạm vi bán kính vật cản
   
    [Header("Avoidance Settings")]
    public float avoidanceStrength = 0.4f;     // γ: cường độ lực né
    public float avoidanceRange = 2.0f;        // vùng ảnh hưởng: 2 × radius
    public float delta = 0.1f;         // 𝛿 hệ số đẩy ngược 
    public float epsilon = 0.05f;        //  𝜀 Hệ số giảm SE



    //List
    private List<float> GA_FitnessValues = new List<float>();
    private List<float> PSO_FitnessValues = new List<float>();
    private List<float> ECPPSO_FitnessValues = new List<float>();
    private List<float> PSO_GA_FitnessValues = new List<float>();
    private List<float> ECPPSO_GA_FitnessValues = new List<float>();
    private List<float> ECPPSO_SE_GA_FitnessValues = new List<float>();
    private List<float> ECPPSO_NEP_GA_FitnessValues = new List<float>();

    // Timing
    private List<float> GA_RunTimes = new List<float>();
    private List<float> PSO_RunTimes = new List<float>();
    private List<float> ECPPSO_RunTimes = new List<float>();
    private List<float> PSO_GA_RunTimes = new List<float>();
    private List<float> ECPPSO_GA_RunTimes = new List<float>();
    private List<float> ECPPSO_SE_GA_RunTimes = new List<float>();
    private List<float> ECPPSO_NEP_GA_RunTimes = new List<float>();

    private float timeOptimization = 0f;
    // runtime tracking
    private List<float> runTimes = new List<float>();

    // Singleton instance
    public static Controller Instance;

    private void Awake()
    {
        if (Instance == null)
            Instance = this;
        else
            Destroy(gameObject);
    }

    // Start is called before the first frame update
    void Start()
    {
        UI.Instance.GenMap(areaL, areaW);
        UI.Instance.GenGraph(maxIterations);

        if (useObstacles)  GenObstacles();

        GenStations();
        

        // switch (optimizerType)
        // {
        //     case OptimizerType.PSO:
        //         optimizer = new PSO();
        //         break;
        //     case OptimizerType.ECPPSO:
        //         optimizer = new ECPPSO();
        //         break;
        //     case OptimizerType.GA:
        //         optimizer = new GA_alone();
        //         break;
        // }

        // optimizer.Initialize();

        // StartCoroutine(RunOptimization()); 

        StartCoroutine(SetUp());

    }


    private IEnumerator RunOptimization()
    {
        isOptimizing = true;
        var best = optimizer.GetBestSolution();

        // start timer for this optimization run
        float runStart = Time.realtimeSinceStartup;

        for (int i = 0; i < maxIterations; i++)
        {
            float iterStart = Time.realtimeSinceStartup;

            optimizer.RunIteration();
            best = optimizer.GetBestSolution();

            SolutionsList.Add(best);

            yield return new WaitUntil(() => Stations_List[station_num - 1].isMoving == false);

            float coverage = optimizer.GetBestCoverage();

            if (coverage > bestCoverage)
            {
                bestCoverage = coverage;
                bestIter = i + 1;
            }

            float x = (i + 1) / (float)maxIterations;

            FitnessValuesList.Add(coverage);
            LineGraphPoints.Add(new Vector2(70 * x, 50 * (coverage / 100f)));

            UI.Instance.DrawPath(i, LineGraphPoints[i]);
            UI.Instance.UpdateInfoLine(i + 1, FitnessValuesList[i], LineGraphPoints[i]);

            float iterElapsed = (Time.realtimeSinceStartup - iterStart)*1000;

            runTimes.Add(iterElapsed);

            Debug.Log($"Iteration {i + 1}: Fitness = {coverage}%, IterTime = {iterElapsed:F2}ms");

            yield return null;
        }

        isOptimizing = false;

        if (SaveResults) SaveFitnessValues(FitnessValuesList);

        // record total runtime for this optimizer run
        timeOptimization = Time.realtimeSinceStartup - runStart;

        Debug.Log($"Best Coverage = {bestCoverage:F4}% in iteration {bestIter}. Total runtime = {timeOptimization:F4}s");

    }

    private void GenStations()
    {
        // Clear existing stations
        foreach (var station in Stations_List)
        {
            Destroy(station.gameObject);
        }
        Stations_List.Clear();


        for (int i = 0; i < station_num; i++)
        {
            Vector2 pos = Vector2.zero;
            GameObject stationObj = Instantiate(stationPrefab);
            Station station = stationObj.GetComponent<Station>();
            station.Initialize(pos, stationRadius);
            Stations_List.Add(station);
        }
    }

    public void GenObstacles()
    {
        foreach (var o in Obstacles)
        {
            if (o != null) Destroy(o.gameObject);
        }
        Obstacles.Clear();

        for (int i = 0; i < obstacleCount; i++)
        {
            Vector2 center = new Vector2(
                UnityEngine.Random.Range(-areaL / 2f, areaL / 2f),
                UnityEngine.Random.Range(-areaW / 2f, areaW / 2f)
            );

            float minR = radiusRange.x;
            float maxR = radiusRange.y;

            var obj = Instantiate(obstaclePolygonPrefab, transform);
            var ob = obj.GetComponent<ObstaclePolygon>();
            ob.Initialize( UnityEngine.Random.Range(minR, maxR), center);
            Obstacles.Add(ob);
        }
    }

    public void DisplayInfoGraph(float iter)
    {
        int i = (int)iter - 1;

        SetStationPositions(SolutionsList[i]);

        UI.Instance.UpdateInfoLine(i + 1, FitnessValuesList[i], LineGraphPoints[i]);
    }

    

    public void Evaluate(Particle p)
    {
        int total = areaL * areaW;
        BitArray coveredBits = new BitArray(total);
        int coveredCount = 0;

        float offsetX = -areaL / 2f;
        float offsetY = -areaW / 2f;

        foreach (var s in p.pos)
        {
            int xMin = Mathf.Max(0, Mathf.FloorToInt(s.x - stationRadius - offsetX));
            int xMax = Mathf.Min(areaL - 1, Mathf.CeilToInt(s.x + stationRadius - offsetX));
            int yMin = Mathf.Max(0, Mathf.FloorToInt(s.y - stationRadius - offsetY));
            int yMax = Mathf.Min(areaW - 1, Mathf.CeilToInt(s.y + stationRadius - offsetY));

            for (int y = yMin; y <= yMax; y++)
            {
                for (int x = xMin; x <= xMax; x++)
                {
                    int idx = y * areaL + x;
                    if (coveredBits[idx]) continue;

                    Vector2 sample = new Vector2(offsetX + x + 0.5f, offsetY + y + 0.5f);
                    if ((s - sample).sqrMagnitude <= stationRadius * stationRadius)
                    {
                        coveredBits[idx] = true;
                        coveredCount++;
                    }
                }
            }
        }

        float fitness = (float)coveredCount / total;

       
        p.fitness = Mathf.Clamp01(fitness);
        if (p.fitness > p.pBestFitness)
        {
            p.pBestFitness = p.fitness;
            p.pBest = new List<Vector2>(p.pos);
        }
    }

    // =====================================================
    // Lực né vật cản
    // =====================================================
    public Vector2 CalculateAvoidanceForce(Vector2 pos)
    {
        Vector2 force = Vector2.zero;

        if (!Controller.Instance.useObstacles) return force;

        foreach (var obs in Obstacles)
        {
            float dist = Vector2.Distance(pos, obs.pos);
            if (dist < avoidanceRange && dist > 0.01f)
            {
                Vector2 dir = (pos - obs.pos).normalized;
                float strength = avoidanceStrength * (1f - dist / avoidanceRange);
                force += dir * strength;
            }
        }

        return force;
    }


    // Đặt vị trí ban đầu
    public void SetStationPositions(List<Vector2> list)
    {
        if (list.Count == 0) return;

        int count = Math.Min(station_num, list.Count);
        float halfL = areaL / 2f;
        float halfW = areaW / 2f;

        for (int i = 0; i < count; i++)
        {
            Vector2 target = list[i];

            // If obstacles are enabled, push targets out of obstacles instead of placing inside
            if (useObstacles && Obstacles != null)
            {
                foreach (var obs in Obstacles)
                {
                    if (obs == null) continue;
                    if (obs.Contains(target))
                    {
                        // direction from obstacle center to target
                        Vector2 dir = target - obs.pos;
                        if (dir.sqrMagnitude < 1e-6f)
                        {
                            dir = UnityEngine.Random.insideUnitCircle.normalized;
                        }
                        else
                        {
                            dir = dir.normalized;
                        }

                        float margin = 0.1f;
                        target = obs.pos + dir * (obs.radius + stationRadius + margin);
                    }
                }

                // ensure within area bounds
                target.x = Mathf.Clamp(target.x, -halfL + 0.01f, halfL - 0.01f);
                target.y = Mathf.Clamp(target.y, -halfW + 0.01f, halfW - 0.01f);
            }

            Stations_List[i].SetPosition(target);
        }
    }

    //save and load positions

    public void SaveInitial(List<List<Vector2>> list)
    {
        // Xóa file cũ nếu có
        if (File.Exists(filePath))
            File.Delete(filePath);

        // Bọc dữ liệu
        List<Vector2Group> groupList = new List<Vector2Group>();
        foreach (var subList in list)
        {
            groupList.Add(new Vector2Group { points = subList });
        }

        Vector2GroupListWrapper wrapper = new Vector2GroupListWrapper { groups = groupList };

        // Chuyển sang JSON
        string json = JsonUtility.ToJson(wrapper, true);

        // Ghi ra file
        File.WriteAllText(filePath, json);
        Debug.Log("✅ Đã lưu vào: " + filePath);
    }

    public List<List<Vector2>> LoadInitial()
    {
        if (!File.Exists(filePath))
        {
            Debug.LogWarning("⚠️ File chưa tồn tại!");
            return new List<List<Vector2>>();
        }

        string json = File.ReadAllText(filePath);
        Vector2GroupListWrapper wrapper = JsonUtility.FromJson<Vector2GroupListWrapper>(json);

        // Nếu đọc lỗi hoặc trống
        if (wrapper == null || wrapper.groups == null)
        {
            Debug.LogWarning("⚠️ File rỗng hoặc sai cấu trúc!");
            return new List<List<Vector2>>();
        }

        // Chuyển ngược lại dạng List<List<Vector2>>
        List<List<Vector2>> result = new List<List<Vector2>>();
        foreach (var g in wrapper.groups)
        {
            result.Add(g.points);
        }

        Debug.Log("✅ Đã load " + result.Count + " nhóm Vector2");
        return result;
    }

    void SaveFitnessValues(List<float> list)
    {
        // Xóa file cũ nếu tồn tại
        if (File.Exists(fitnessFilePath))
        {
            File.Delete(fitnessFilePath);
        }

        string csvContent = "Iteration,Fitness\n";
        for (int i = 0; i < list.Count; i++)
        {
            csvContent += $"{i + 1},{list[i]:F4}\n";
        }
        File.WriteAllText(fitnessFilePath, csvContent);
    }




    private IEnumerator SetUp()
    {
        // // GA alone
        // c1 = 0.7f;
        // c2 = 0.7f;
        // useGA = false;
        // useSE = false;
        // useNEP = false;
        // optimizer = new GA_alone();
        // optimizer.Initialize();

        // isOptimizing = true;

        // UI.Instance.GenNewLine("GA");

        // StartCoroutine(RunOptimization());

        // yield return new WaitUntil(() => isOptimizing == false);

        // // record fitness values
        // GA_FitnessValues = new List<float>(FitnessValuesList);
        // // record runtime
        // GA_RunTimes = new List<float>(runTimes);
       
        // FitnessValuesList.Clear();
        // LineGraphPoints.Clear();
        // SolutionsList.Clear();
        // runTimes.Clear();

        //PSO
        c1 = 0.7f;
        c2 = 0.7f;
        useGA = false;
        optimizer = new PSO();
        optimizer.Initialize();

        isOptimizing = true;

        UI.Instance.GenNewLine("PSO");

        StartCoroutine(RunOptimization());

        yield return new WaitUntil(() => isOptimizing == false);
        
        // record fitness values
        PSO_FitnessValues = new List<float>(FitnessValuesList);

        // record runtime
        PSO_RunTimes = new List<float>(runTimes);

        FitnessValuesList.Clear();
        LineGraphPoints.Clear();
        SolutionsList.Clear();
        runTimes.Clear();



        //ECPPSO
        c1 = 2.0f;
        c2 = 2.0f;
        useGA = false;
        useSE = true;
        useNEP = true;
        optimizer = new ECPPSO();
        optimizer.Initialize();

        isOptimizing = true;

        UI.Instance.GenNewLine("ECPPSO");

        StartCoroutine(RunOptimization());

        yield return new WaitUntil(() => isOptimizing == false);

        // record fitness values
        ECPPSO_FitnessValues = new List<float>(FitnessValuesList);

        // record runtime
        ECPPSO_RunTimes = new List<float>(runTimes);

        FitnessValuesList.Clear();
        LineGraphPoints.Clear();
        SolutionsList.Clear();
        runTimes.Clear();


        //PSO + GA
        // c1 = 0.7f;
        // c2 = 0.7f;
        // useGA = true;
        // optimizer = new PSO();
        // optimizer.Initialize();

        // isOptimizing = true;

        // UI.Instance.GenNewLine("PSO + GA");

        // StartCoroutine(RunOptimization());

        // yield return new WaitUntil(() => isOptimizing == false);

        // // record fitness values
        // PSO_GA_FitnessValues = new List<float>(FitnessValuesList);

        // // record runtime
        // PSO_GA_RunTimes = new List<float>(runTimes);

        // FitnessValuesList.Clear();
        // LineGraphPoints.Clear();
        // SolutionsList.Clear();
        // runTimes.Clear();


        //ECPPSO + GA
        c1 = 2.0f;
        c2 = 2.0f;
        useGA = true;
        optimizer = new ECPPSO();
        optimizer.Initialize();

        isOptimizing = true;

        UI.Instance.GenNewLine("ECPPSO + GA");

        StartCoroutine(RunOptimization());

        yield return new WaitUntil(() => isOptimizing == false);

        // record fitness values
        ECPPSO_GA_FitnessValues = new List<float>(FitnessValuesList);

        // record runtime
        ECPPSO_GA_RunTimes = new List<float>(runTimes);
        
        FitnessValuesList.Clear();
        LineGraphPoints.Clear();
        SolutionsList.Clear();
        runTimes.Clear();

        // //ECPPSO + SE + GA
        // useGA = true;
        // useSE = true;
        // useNEP = false;
        // optimizer = new ECPPSO();
        // optimizer.Initialize();

        // isOptimizing = true;

        // UI.Instance.GenNewLine("ECPPSO no NEP + GA");

        // StartCoroutine(RunOptimization());

        // yield return new WaitUntil(() => isOptimizing == false);

        // // record fitness values
        // ECPPSO_SE_GA_FitnessValues = new List<float>(FitnessValuesList);

        // // record runtime
        // ECPPSO_SE_GA_RunTimes = new List<float>(runTimes);

        // FitnessValuesList.Clear();
        // LineGraphPoints.Clear();
        // SolutionsList.Clear();
        // runTimes.Clear();


        // //ECPPSO + NEP + GA
        // useGA = true;
        // useSE = false;
        // useNEP = true;
        // optimizer = new ECPPSO();
        // optimizer.Initialize();

        // isOptimizing = true;

        // UI.Instance.GenNewLine("ECPPSO no SE + GA");

        // StartCoroutine(RunOptimization());

        // yield return new WaitUntil(() => isOptimizing == false);

        // // record fitness values
        // ECPPSO_NEP_GA_FitnessValues = new List<float>(FitnessValuesList);

        // // record runtime
        // ECPPSO_NEP_GA_RunTimes = new List<float>(runTimes);
        
        // isOptimizing = false;

        // Save all results
        //Save(fitnessFilePath,GA_FitnessValues,PSO_FitnessValues, ECPPSO_FitnessValues, PSO_GA_FitnessValues, ECPPSO_GA_FitnessValues, ECPPSO_SE_GA_FitnessValues, ECPPSO_NEP_GA_FitnessValues);
        Save(RunTimeFilePath,GA_RunTimes,PSO_RunTimes, ECPPSO_RunTimes, PSO_GA_RunTimes, ECPPSO_GA_RunTimes, ECPPSO_SE_GA_RunTimes, ECPPSO_NEP_GA_RunTimes);

        SaveWithObstacles(PSO_FitnessValues, ECPPSO_FitnessValues, ECPPSO_GA_FitnessValues);
    }


    void Save(string filePath,List<float> GA, List<float> PSO, List<float> ECPPSO, List<float> PSO_GA, List<float> ECPPSO_GA, List<float> ECPPSO_SE_GA, List<float> ECPPSO_NEP_GA)
    {
        // Xóa file cũ nếu tồn tại
        if (File.Exists(filePath))
        {
            File.Delete(filePath);
        }

        string csvContent = "Iteration,GA,PSO,ECPPSO,PSO_GA,ECPPSO_GA,ECPPSO_SE_GA,ECPPSO_NEP_GA\n";
        int maxCount = Math.Max(GA.Count, Math.Max(PSO.Count, Math.Max(ECPPSO.Count, Math.Max(PSO_GA.Count, Math.Max(ECPPSO_GA.Count, Math.Max(ECPPSO_SE_GA.Count, ECPPSO_NEP_GA.Count))))));
        for (int i = 0; i < maxCount; i++)  
        {
            string gaValue = i < GA.Count ? GA[i].ToString("F4") : "";
            string psoValue = i < PSO.Count ? PSO[i].ToString("F4") : "";
            string ecppsoValue = i < ECPPSO.Count ? ECPPSO[i].ToString("F4") : "";
            string psoGaValue = i < PSO_GA.Count ? PSO_GA[i].ToString("F4") : "";
            string ecppsoGaValue = i < ECPPSO_GA.Count ? ECPPSO_GA[i].ToString("F4") : "";
            string ecppsoSeGaValue = i < ECPPSO_SE_GA.Count ? ECPPSO_SE_GA[i].ToString("F4") : "";
            string ecppsoNepGaValue = i < ECPPSO_NEP_GA.Count ? ECPPSO_NEP_GA[i].ToString("F4") : "";

            csvContent += $"{i + 1},{gaValue},{psoValue},{ecppsoValue},{psoGaValue},{ecppsoGaValue},{ecppsoSeGaValue},{ecppsoNepGaValue}\n";
        }
        File.WriteAllText(filePath, csvContent);
    }

    void SaveWithObstacles(List<float> PSO, List<float> ECPPSO, List<float> ECPPSO_GA)
    {
        // Xóa file cũ nếu tồn tại
        if (File.Exists(fitnessFilePath))
        {
            File.Delete(fitnessFilePath);
        }

        string csvContent = "Iteration, PSO,ECPPSO,ECPPSO_GA\n";
        int maxCount = Math.Max(PSO.Count, Math.Max(ECPPSO.Count, ECPPSO_GA.Count));
        for (int i = 0; i < maxCount; i++)
        {
            string psoValue = i < PSO.Count ? PSO[i].ToString("F4") : "";
            string ecppsoValue = i < ECPPSO.Count ? ECPPSO[i].ToString("F4") : "";
            string ecppsoGaValue = i < ECPPSO_GA.Count ? ECPPSO_GA[i].ToString("F4") : "";
           
            csvContent += $"{i + 1},{psoValue},{ecppsoValue},{ecppsoGaValue}\n";
        }
        File.WriteAllText(fitnessFilePath, csvContent);
    }

}

[Serializable]
public class Vector2Group
{
    public List<Vector2> points;
}

[Serializable]
public class Vector2GroupListWrapper
{
    public List<Vector2Group> groups;
}

public class RecordData
{
    public List<float> FitnessValues;
}





using System.Collections;
using System.Collections.Generic;
using System.IO;
using Unity.VisualScripting;
using UnityEngine;

public class Controller : MonoBehaviour
{
    public enum OptimizerType { PSO, ECPPSO }
    public enum TestType { RandomInit, LoadInit }
    public OptimizerType optimizerType;
    public TestType testType;

    public string filePath;

    [SerializeField] private GameObject stationPrefab;
    private List<Vector2> InitialPositions = new List<Vector2>();
    private List<Station> Stations_List = new List<Station>();
    private List<float> FitnessValuesList = new List<float>();
    private List<Vector2> LineGraphPoints = new List<Vector2>();
    private List<List<Vector2>> SolutionsList = new List<List<Vector2>>();
    private IOptimizer optimizer;

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

    // Start is called before the first frame update
    void Start()
    {
        UI.Instance.GenMap(areaL, areaW);
        UI.Instance.GenGraph(maxIterations);

        GenStations();


        switch (optimizerType)
        {
            case OptimizerType.PSO:
                optimizer = new PSO();
                break;
            case OptimizerType.ECPPSO:
                optimizer = new ECPPSO();
                break;
        }

        optimizer.Initialize(populationSize, station_num, areaL, areaW, stationRadius, w, c1, c2, maxIterations);

        if (optimizerType == OptimizerType.ECPPSO)
        {
            ECPPSO ecpso = (ECPPSO)optimizer;
            ecpso.SetParams(G_percent, neighborCount);
        }

        StartCoroutine(RunOptimization());

    }

    private void If(bool v)
    {
        throw new System.NotImplementedException();
    }

    private IEnumerator RunOptimization()
    {
        isOptimizing = true;
        var best = optimizer.GetBestSolution();

        for (int i = 0; i < maxIterations; i++)
        {
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

            Debug.Log($"{i + 1}: Fitness = {coverage}%");

            yield return null;
        }
        isOptimizing = false;

        SaveFitnessValues(FitnessValuesList);

        Debug.Log($"Best Coverage = {bestCoverage:F4}% in iteration {bestIter}");

    }

    private void GenStations()
    {
        // Clear existing stations
        foreach (var station in Stations_List)
        {
            Destroy(station.gameObject);
        }
        Stations_List.Clear();

        if(testType == TestType.LoadInit)
        {
            InitialPositions = LoadList();
            station_num = InitialPositions.Count;
        }

        for (int i = 0; i < station_num; i++)
        {
            Vector2 pos = testType == TestType.LoadInit ? InitialPositions[i] : new Vector2(Random.Range(-areaL / 2f, areaL / 2f), Random.Range(-areaW / 2f, areaW / 2f));
            GameObject stationObj = Instantiate(stationPrefab);
            Station station = stationObj.GetComponent<Station>();
            station.Initialize(pos, stationRadius);
            Stations_List.Add(station);

            InitialPositions.Add(pos);

        }
        
        

        float coverage = ComputeCoveragePercent();
        Debug.Log($"Coverage: {coverage}%");



        if(testType == TestType.LoadInit) return;
        SaveList(InitialPositions);
    }

    public void DisplayInfoGraph(float iter)
    {
        int i = (int)iter - 1;

        for (int j = 0; j < station_num; j++)
        {
            Stations_List[j].SetPosition(SolutionsList[i][j]);
        }

        UI.Instance.UpdateInfoLine(i + 1, FitnessValuesList[i], LineGraphPoints[i]);
    }

    private float ComputeCoveragePercent()
    {
        int covered = 0;
        int total = areaL * areaW;
        float r2 = stationRadius * stationRadius;

        float offsetX = -areaL / 2f;
        float offsetY = -areaW / 2f;

        for (int y = 0; y < areaW; y++)
        {
            for (int x = 0; x < areaL; x++)
            {
                // tọa độ trung tâm ô vuông (0.5f dịch giữa cell)
                Vector2 sample = new Vector2(offsetX + x + 0.5f, offsetY + y + 0.5f);

                bool isCovered = false;
                foreach (var station in Stations_List)
                {
                    if ((station.position - sample).sqrMagnitude <= r2)
                    {
                        isCovered = true;
                        break;
                    }
                }

                if (isCovered) covered++;
            }
        }

        return (float)covered / total * 100f;
    }


    //save and load positions

    void SaveList(List<Vector2> list)
    {
        if (File.Exists(filePath))
        {
            File.Delete(filePath);
        }

        Vector2ListWrapper wrapper = new Vector2ListWrapper { points = list };
        string json = JsonUtility.ToJson(wrapper, true);
        File.WriteAllText(filePath, json);
        Debug.Log("Đã lưu vào: " + filePath);
    }

    List<Vector2> LoadList()
    {
        if (!File.Exists(filePath))
            return new List<Vector2>();

        string json = File.ReadAllText(filePath);
        Vector2ListWrapper wrapper = JsonUtility.FromJson<Vector2ListWrapper>(json);
        return wrapper.points;
    }

    void SaveFitnessValues(List<float> list)
    {
        string filePath = Application.dataPath + "/Data/FitnessData.csv";
        
        // Xóa file cũ nếu tồn tại
        if (File.Exists(filePath))
        {
            File.Delete(filePath);
        }
        
        string csvContent = "Iteration,Fitness\n";
        for (int i = 0; i < list.Count; i++)
        {
            csvContent += $"{i + 1},{list[i]:F4}\n";
        }
        File.WriteAllText(filePath, csvContent);
    }

}

public class Vector2ListWrapper
{
    public List<Vector2> points;
}

public class RecordData{
    public List<float> FitnessValues;
}




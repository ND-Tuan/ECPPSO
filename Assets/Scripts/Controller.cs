using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using Unity.VisualScripting;
using UnityEngine;


public class Controller : MonoBehaviour
{
    public enum OptimizerType { PSO, ECPPSO }
    public enum TestType { RandomInit, LoadInit }

    [Header("General Settings")]
    public OptimizerType optimizerType;
    public TestType testType;
    public bool useGA = true;

    public bool AutoSaveInitial = true;
    public string filePath;
    public bool SaveResults = true;
    public string fitnessFilePath;

    [SerializeField] private GameObject stationPrefab;
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

    [Header("GA params")]
    [Range(0, 100)] public int eliteFrac = 30;      //% cá thể ưu tú làm cha mẹ
    [Range(0, 1)] public float mutationRate = 0.05f;  // xác suất đột biến
    [Range(0, 10)] public float mutationStep = 1.0f;   // độ dịch chuyển đột biến

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

        optimizer.Initialize();

        StartCoroutine(RunOptimization()); 

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

        if (SaveResults) SaveFitnessValues(FitnessValuesList);

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


        for (int i = 0; i < station_num; i++)
        {
            Vector2 pos = Vector2.zero;
            GameObject stationObj = Instantiate(stationPrefab);
            Station station = stationObj.GetComponent<Station>();
            station.Initialize(pos, stationRadius);
            Stations_List.Add(station);
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
        BitArray coveredBits = new BitArray(total); // mỗi bit = 1 sample
        int coveredCount = 0;

        float offsetX = -areaL / 2f;
        float offsetY = -areaW / 2f;
        float r2 = stationRadius * stationRadius;

        // Duyệt từng trạm
        foreach (var s in p.pos)
        {
            // Giới hạn vùng ảnh hưởng trạm
            int xMin = Mathf.Max(0, Mathf.FloorToInt(s.x - stationRadius - offsetX));
            int xMax = Mathf.Min(areaL - 1, Mathf.CeilToInt(s.x + stationRadius - offsetX));
            int yMin = Mathf.Max(0, Mathf.FloorToInt(s.y - stationRadius - offsetY));
            int yMax = Mathf.Min(areaW - 1, Mathf.CeilToInt(s.y + stationRadius - offsetY));

            for (int y = yMin; y <= yMax; y++)
            {
                for (int x = xMin; x <= xMax; x++)
                {
                    int index = y * areaL + x; // vị trí bit tương ứng với sample (x,y)
                    if (coveredBits[index]) continue; // skip nếu đã phủ

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


    // Đặt vị trí ban đầu
    public void SetStationPositions(List<Vector2> list)
    {
        if (list.Count == 0) return;

        for (int i = 0; i < station_num; i++)
        {
            Stations_List[i].SetPosition(list[i]);
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





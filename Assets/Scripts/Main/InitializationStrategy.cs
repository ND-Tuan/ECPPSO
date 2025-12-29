using System.Collections.Generic;
using UnityEngine;

public static class InitializationStrategy
{
    /// <summary>
    /// Khởi tạo đều trên toàn vùng (phương pháp cũ)
    /// </summary>
    public static void InitializeUniform(Particle p, int stationNum, int areaL, int areaW)
    {
        for (int j = 0; j < stationNum; j++)
        {
            Vector2 pos = GenerateValidPosition(areaL, areaW);
            p.pos.Add(pos);
        }
    }

    /// <summary>
    /// Khởi tạo theo cụm - tạo các tâm cụm và phân bố stations xung quanh
    /// </summary>
    public static void InitializeClustered(Particle p, int stationNum, int areaL, int areaW, float radius)
    {
        int clusterCount = Controller.Instance.clusterCount;
        List<Vector2> clusterCenters = new List<Vector2>();
        
        // Tạo các tâm cụm
        for (int c = 0; c < clusterCount; c++)
        {
            Vector2 center = GenerateValidPosition(areaL, areaW);
            clusterCenters.Add(center);
        }
        
        // Phân bố stations vào các cụm
        int stationsPerCluster = stationNum / clusterCount;
        int remainder = stationNum % clusterCount;
        
        for (int c = 0; c < clusterCount; c++)
        {
            int stationsInThisCluster = stationsPerCluster + (c < remainder ? 1 : 0);
            Vector2 clusterCenter = clusterCenters[c];
            
            for (int j = 0; j < stationsInThisCluster; j++)
            {
                // Tạo vị trí xung quanh tâm cụm với bán kính ngẫu nhiên
                float angle = Random.Range(0f, 360f) * Mathf.Deg2Rad;
                float distance = Random.Range(0f, radius * 2f); // phân bố trong vùng 2× radius
                
                Vector2 offset = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * distance;
                Vector2 pos = clusterCenter + offset;
                
                // Đảm bảo trong vùng
                pos.x = Mathf.Clamp(pos.x, -areaL / 2f, areaL / 2f);
                pos.y = Mathf.Clamp(pos.y, -areaW / 2f, areaW / 2f);
                
                // Kiểm tra vật cản
                if (Controller.Instance.Obstacles.Count > 0)
                {
                    bool valid = false;
                    int attempts = 0;
                    while (!valid && attempts < 50)
                    {
                        valid = true;
                        foreach (var ob in Controller.Instance.Obstacles)
                        {
                            if (ob.Contains(pos))
                            {
                                valid = false;
                                angle = Random.Range(0f, 360f) * Mathf.Deg2Rad;
                                distance = Random.Range(0f, radius * 2f);
                                offset = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * distance;
                                pos = clusterCenter + offset;
                                pos.x = Mathf.Clamp(pos.x, -areaL / 2f, areaL / 2f);
                                pos.y = Mathf.Clamp(pos.y, -areaW / 2f, areaW / 2f);
                                break;
                            }
                        }
                        attempts++;
                    }
                }
                
                p.pos.Add(pos);
            }
        }
    }

    /// <summary>
    /// Khởi tạo với khoảng cách tối thiểu giữa các stations
    /// </summary>
    public static void InitializeMinDistance(Particle p, int stationNum, int areaL, int areaW)
    {
        float minDist = Controller.Instance.minStationDistance;
        int maxAttempts = 100;
        
        for (int j = 0; j < stationNum; j++)
        {
            Vector2 pos = Vector2.zero;
            bool validPosition = false;
            int attempts = 0;
            
            while (!validPosition && attempts < maxAttempts)
            {
                pos = GenerateValidPosition(areaL, areaW);
                validPosition = true;
                
                // Kiểm tra khoảng cách với các stations đã có
                foreach (var existingPos in p.pos)
                {
                    if (Vector2.Distance(pos, existingPos) < minDist)
                    {
                        validPosition = false;
                        break;
                    }
                }
                
                attempts++;
            }
            
            // Nếu không tìm được vị trí thỏa mãn sau maxAttempts, chấp nhận vị trí hiện tại
            p.pos.Add(pos);
        }
    }

    /// <summary>
    /// Tạo một vị trí hợp lệ (không nằm trong vật cản)
    /// </summary>
    private static Vector2 GenerateValidPosition(int areaL, int areaW)
    {
        Vector2 pos = new Vector2(Random.Range(-areaL / 2f, areaL / 2f), Random.Range(-areaW / 2f, areaW / 2f));
        
        if (Controller.Instance.Obstacles.Count == 0)
            return pos;
        
        bool insideObstacle;
        int attempts = 0;
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
            attempts++;
        } while (insideObstacle && attempts < 100);
        
        return pos;
    }
}

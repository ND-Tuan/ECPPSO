using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public static class GA 
{
    // Lai ghép đồng nhất giữa hai cá thể A và B
    private static List<Vector2> CrossoverUniform(List<Vector2> A, List<Vector2> B)
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

    // Đột biến vị trí với xác suất mutationRate
    private static void MutatePositions(List<Vector2> pos)
    {
        int areaL = Controller.Instance.areaL;
        int areaW = Controller.Instance.areaW;
        float mutationRate = Controller.Instance.mutationRate;
        float mutationStep = Controller.Instance.mutationStep;

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

    // Thực hiện tiến hóa di truyền
    public static void ApplyGeneticEvolution(List<Particle> population)
    {
        int populationSize = population.Count;
        int stationNum = Controller.Instance.station_num;
        int G_percent = Controller.Instance.G_percent;
        float eliteFrac = Controller.Instance.E_percent / 100f;

        if (population == null || population.Count == 0) return;

        // Tính số lượng cá thể yếu cần thay thế (G% bottom)
        int Gcount = Mathf.Max(1, populationSize * G_percent / 100);
        
        // Tính số lượng cá thể ưu tú để làm cha mẹ
        int eliteCount = Mathf.Max(2, Mathf.CeilToInt(populationSize * eliteFrac));
        eliteCount = Mathf.Min(eliteCount, population.Count);

        // Lấy các cá thể ưu tú từ đầu quần thể
        var elites = population.Take(eliteCount).ToList();

        // Tạo con cái và thay thế G% cá thể yếu nhất
        for (int i = 0; i < Gcount; i++)
        {
            // Chọn ngẫu nhiên hai cha mẹ từ nhóm ưu tú
            var parentA = elites[Random.Range(0, eliteCount)];
            var parentB = elites[Random.Range(0, eliteCount)];

            // Lai ghép đồng nhất sử dụng pBest của cha mẹ (thiên về vị trí tốt)
            List<Vector2> childPos = CrossoverUniform(parentA.pBest, parentB.pBest);

            // Đột biến vị trí con cái
            MutatePositions(childPos);

            var child = new Particle(stationNum);

            // Thay thế vị trí con với vị trí được sinh ra
            child.pos = new List<Vector2>(childPos);

            // Reset vận tốc và vector dự đoán về 0 (bắt đầu mới)
            for (int j = 0; j < stationNum; j++)
            {
                child.vel[j] = Vector2.zero;
                child.u[j] = Vector2.zero;
            }

            // Đặt pBest ban đầu bằng vị trí hiện tại của con
            child.pBest = new List<Vector2>(child.pos);
            child.pBestFitness = -1f;

            // Đánh giá fitness của con cái
            Controller.Instance.Evaluate(child);

            // Đặt con vào quần thể, thay thế những cá thể yếu nhất (cuối bảng)
            int replaceIndex = population.Count - 1 - i;
            if (replaceIndex >= 0 && replaceIndex < population.Count)
                population[replaceIndex] = child;
        }

        // Sau khi thay thế, sắp xếp lại quần thể (tốt nhất trước)
        population = population.OrderByDescending(p => p.fitness).ToList();
    }
}

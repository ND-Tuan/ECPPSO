using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Particle
    {
        public List<Vector2> pos;
        public List<Vector2> vel;
        public List<Vector2> pBest;
        public float fitness;
        public float pBestFitness;
        public List<Vector2> u; // vector dự đoán

        public Particle(int stationNum)
        {
            pos = new List<Vector2>();
            vel = new List<Vector2>();
            u = new List<Vector2>();

            for (int i = 0; i < stationNum; i++)
            {
                
                vel.Add(Vector2.zero);
                u.Add(Vector2.zero);
            }

            pBest = new List<Vector2>(pos);
            pBestFitness = -1f;
        }
    }
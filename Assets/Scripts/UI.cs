using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class UI : MonoBehaviour
{
    [Header("UI Elements")]
    [SerializeField] private Transform Map;
    [SerializeField] private LineRenderer Line;
    [SerializeField] private Slider XSlider;

    [SerializeField] private LineRenderer InfoLine;
    [SerializeField] private TextMeshProUGUI XText;
    [SerializeField] private RectTransform YTextObj;
    [SerializeField] private TextMeshProUGUI YText;

    private float MaxX;

    public static UI Instance { get; private set; }

    void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
        }
        else
        {
            Destroy(gameObject);
        }
    }

    public void GenMap(int length, int width)
    {
        Map.localScale = new Vector3(length, width, 1);
    }

    public void GenGraph(int pointCount)
    {
        XSlider.maxValue = pointCount;
    }

    public void DrawPath(int i, Vector2 path)
    {
        Line.positionCount = i + 1;

        Line.SetPosition(i, new Vector3(path.x, path.y, 0));

        InfoLine.SetPosition(0, new Vector3(path.x, 0, 0));
        InfoLine.SetPosition(1, new Vector3(path.x, path.y, 0));
    }

    public void UpdateInfoLine(int X, float Y, Vector2 point)
    {
        XSlider.value = X;
       
        InfoLine.SetPosition(0, new Vector3(point.x, 0, 0));
        InfoLine.SetPosition(1, new Vector3(point.x, point.y, 0));

        YTextObj.anchoredPosition = new Vector2(point.x, point.y);

        XText.text = X.ToString();
        YText.text = Y.ToString("F2") + "%";
    }
    

}

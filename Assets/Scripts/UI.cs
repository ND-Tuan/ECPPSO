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

    [SerializeField] private GameObject Graph;
    [SerializeField] private GameObject LinePrefab;

    [SerializeField] private GameObject InfoPanelPrefab;
    [SerializeField] private GameObject InfoPanelParent;

    public static UI Instance { get; private set; }

    // Keep track of used hues to avoid duplicate colors
    private List<float> usedHues = new List<float>();

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

    public void GenNewLine(string lineName)
    {
        var lineObj = Instantiate(LinePrefab, Graph.transform);
        var InfoLineObj = Instantiate(InfoPanelPrefab, InfoPanelParent.transform);

        lineObj.transform.localPosition = Vector3.zero;

        Color colorA = PickDistinctBrightColor();

        Gradient gradient = new Gradient();
        gradient.SetKeys(
            new GradientColorKey[] {
                new GradientColorKey(colorA, 0f),
                new GradientColorKey(colorA, 1f)
            },
            new GradientAlphaKey[] {
                new GradientAlphaKey(colorA.a, 0f),
                new GradientAlphaKey(colorA.a, 1f)
            }
        );

        Line = lineObj.GetComponent<LineRenderer>();
        Line.colorGradient = gradient;

        InfoPanel infoPanel = InfoLineObj.GetComponent<InfoPanel>();
        infoPanel.SetTextAndColor(lineName, colorA);
    }

    

    // Pick a bright, saturated color whose hue is distinct from previously used hues.
    // Attempts multiple times and falls back to a random bright color.
    private Color PickDistinctBrightColor()
    {
        const int attempts = 12;
        const float minHueDistance = 0.12f; // about ~43 degrees

        for (int a = 0; a < attempts; a++)
        {
            float hue = Random.Range(0f, 1f);
            float sat = Random.Range(0.7f, 1f);
            float val = Random.Range(0.8f, 1f);

            bool ok = true;
            foreach (var used in usedHues)
            {
                float d = Mathf.Abs(Mathf.DeltaAngle(used * 360f, hue * 360f)) / 360f;
                if (d < minHueDistance)
                {
                    ok = false; break;
                }
            }

            if (ok)
            {
                usedHues.Add(hue);
                return Color.HSVToRGB(hue, sat, val);
            }
        }

        // fallback: pick any bright color and record its hue
        float fh = Random.Range(0f, 1f);
        usedHues.Add(fh);
        return Color.HSVToRGB(fh, Random.Range(0.7f, 1f), Random.Range(0.8f, 1f));
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

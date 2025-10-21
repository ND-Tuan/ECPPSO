using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class InfoPanel : MonoBehaviour
{
    public TextMeshProUGUI text;
    public Image image;


    public void SetTextAndColor(string content, Color color)
    {
        text.text = content;
        image.color = color;
    }
}

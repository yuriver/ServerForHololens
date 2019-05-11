using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class DetectionBox
{
    public int id;
    public float score;
    public Vector2 min;
    public Vector2 max;
    public Color color;
    public float intensity;

    public DetectionBox()
    {
        id = 0;
        score = 0f;
        min = max = Vector2.zero;
        color = Color.black;
        intensity = 0f;
    }

    public override string ToString()
    {
        string text = string.Empty;

        text += string.Format("{0}|", id);
        text += string.Format("{0}|", score);
        text += string.Format("{0}|{1}|", min.x, min.y);
        text += string.Format("{0}|{1}|", max.x, max.y);
        text += string.Format("{0}|{1}|{2}|", color.r, color.g, color.b);
        text += string.Format("{0}", intensity);

        return text;
    }
}

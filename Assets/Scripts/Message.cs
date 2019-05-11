using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Message
{
    public Vector3 hmdPosition;
    public Vector3 hmdRotation;
    public DetectionBox[] boxes;

    public Message()
    {
        hmdPosition = Vector3.zero;
        hmdRotation = Vector3.zero;
        boxes = null;
    }

    public override string ToString()
    {
        string result = string.Empty;

        string position = Vector3ToString(hmdPosition);
        string rotation = Vector3ToString(hmdRotation);
        string mergedBoxes = BoxesToString(boxes);
        
        result = string.Format("{0}:{1}:{2}", position, rotation, mergedBoxes);
        return result;
    }

    private string Vector3ToString(Vector3 value)
    {
        string result = string.Empty;
        result = string.Format("{0}|{1}|{2}", value.x, value.y, value.z);

        return result;
    }

    private string BoxesToString(DetectionBox[] boxes)
    {
        string result = string.Empty;

        string[] results = new string[boxes.Length];
        for (int i = 0; i < boxes.Length; i++)
        {
            results[i] = boxes[i].ToString();
        }

        result = string.Join("\\", results);
        return result;
    }
}

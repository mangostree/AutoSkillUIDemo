using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class AttriLinesList : MonoBehaviour
{
    public Dictionary<string, AttriLine> AttriLines;
    public Transform AttriLinesGo = null;

    void OnEnable()
    {
        PrepareAttriLines();
        PlayerAttributes.Instance.AttriLinesUI = this;
    }

    void OnDestroy()
    {
        PlayerAttributes.Instance.AttriLinesUI = null;
    }

    void PrepareAttriLines()
    {
        if (!AttriLinesGo)
        {
            return;
        }

        // has run
        if (AttriLines != null)
        {
            return;
        }

        AttriLines = new Dictionary<string, AttriLine>();

        AttriLine[] attriLinesArray = AttriLinesGo.GetComponentsInChildren<AttriLine>();
        for (UInt32 i = 0; i < attriLinesArray.Length; i++)
        {
            AttriLine attriLine = attriLinesArray[i];
            string attriName = attriLine.AttriName;

            AttriLines.Add(attriName, attriLine);
            Debug.Log($"AttriLinesList: found {attriName}");
        }
    }

    public void SetAttriValue(string attriName, float value)
    {
        if (AttriLines.TryGetValue(attriName, out AttriLine attriLine))
        {
            attriLine.AttriValue = value;
        }
        else
        {
            Debug.LogError($"non exist {attriName} when FetchAttriValue.");
        }
    }
}

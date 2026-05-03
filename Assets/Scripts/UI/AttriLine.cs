using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class AttriLine : MonoBehaviour
{
    public string AttriName;
    public TextMeshProUGUI AttriNameText;

    float attriValue = 0;
    public TextMeshProUGUI AttriValueText;

    public float AttriValue
    {
        get { return attriValue; }
        set { 
            attriValue = value;
            AttriValueText.text = attriValue.ToString();
        }
    }

    void Awake()
    {
        AttriNameText.text = AttriName;
        AttriValueText.text = Convert.ToInt32(attriValue).ToString();
    }
}

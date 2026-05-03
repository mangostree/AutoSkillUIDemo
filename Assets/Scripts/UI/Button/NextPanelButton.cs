using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class NextPanelButton : MonoBehaviour
{
    public string nextPanelPath = string.Empty;

    Button button;

    // Start is called before the first frame update
    void Start()
    {
        button = GetComponent<Button>();
        if (button)
        {
            button.onClick.AddListener(OnClick);
        }
        else
        { 
            Debug.LogError($"{gameObject.name} has no Button when NextPanelButton Start()");
        }
    }

    // Update is called once per frame
    void OnClick()
    {
        if (nextPanelPath.Length > 0)
        {
            UIController.Instance.CreatePanel(nextPanelPath);
        }
    }
}

using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class BackButton : MonoBehaviour
{
	[SerializeField] Button button;
	//public GameObject prefab;
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
            Debug.LogError($"{gameObject.name} has no Button when BackButton Start()");
        }
    }
	public void OnClick()
	{
		UIController.Instance.PopPanel();
	}
}

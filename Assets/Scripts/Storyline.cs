using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Storyline : SingletonBehaviour<Storyline>
{
	public string startUIPanelPath = "UI/MenuPanel";

	// Start is called before the first frame update
	void Start()
    {
		UIController.Instance.CreatePanel(startUIPanelPath);
	}

    // Update is called once per frame
    void Update()
    {
        
    }
}

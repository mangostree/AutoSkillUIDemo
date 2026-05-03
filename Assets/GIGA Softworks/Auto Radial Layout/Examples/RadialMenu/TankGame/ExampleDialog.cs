using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace GIGA.AutoRadialLayout.Examples
{

	public class ExampleDialog : MonoBehaviour
	{
		public Text title, body;
		public Button button;
		public VerticalLayoutGroup layoutRoot;

		public void Set(string title, string body, string button,Action buttonAction)
		{
			this.title.text = title;
			this.body.text = body;
			this.button.gameObject.SetActive(!string.IsNullOrEmpty(button));
			this.button.GetComponentInChildren<Text>().text = button;
			this.button.interactable = buttonAction != null;
			if(buttonAction != null)
				this.button.onClick.AddListener(()=> { buttonAction(); });

			// Resizing
			LayoutRebuilder.ForceRebuildLayoutImmediate(this.GetComponent<RectTransform>());
			float minHeight = this.title.GetComponent<RectTransform>().sizeDelta.y + this.body.GetComponent<RectTransform>().sizeDelta.y + this.button.GetComponent<RectTransform>().sizeDelta.y + this.layoutRoot.spacing * 3 + 50;
			this.GetComponent<LayoutElement>().minHeight = minHeight;
		}
	}
}

using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace GIGA.AutoRadialLayout.Examples
{
	public class EnemyLabel : MonoBehaviour
	{
		public TankGameEnemy enemy;
		private RectTransform canvasRT;
		// Update is called once per frame
		void Update()
		{
			if (enemy != null)
			{
				if (this.canvasRT == null)
					this.canvasRT = GameObject.FindObjectOfType<Canvas>().GetComponent<RectTransform>();
				// Placing on enemy
				Vector2 ViewportPosition = Camera.main.WorldToViewportPoint(enemy.transform.position + Vector3.up * 3);
				Vector2 WorldObject_ScreenPosition = new Vector2(
				((ViewportPosition.x * canvasRT.sizeDelta.x) - (canvasRT.sizeDelta.x * 0.5f)),
				((ViewportPosition.y * canvasRT.sizeDelta.y) - (canvasRT.sizeDelta.y * 0.5f)));

				//now you can set the position of the ui element
				this.GetComponent<RectTransform>().anchoredPosition = WorldObject_ScreenPosition;
			}
			else
				GameObject.Destroy(this.gameObject);
		}
	}
}

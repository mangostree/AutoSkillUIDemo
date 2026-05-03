using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;

namespace GIGA.AutoRadialLayout.Examples
{
	public class CameraController : MonoBehaviour
	{
		public TankGameManager gameManager;
		private Vector2 rotation;
		private Vector3 delta;
		private Camera _camera;

		public float speed = 3;
		private float startTime;
		private bool startedDragOnUI;

		private IEnumerator Start()
		{
			while (startTime < 1)
			{
				startTime += Time.deltaTime;
				yield return null;
			}
		}

		private void Awake()
		{
			rotation = transform.eulerAngles;
			_camera = this.GetComponent<Camera>();
		}

		void Update()
		{
			if (startTime < 1) // To avoid camera mess at startup
				return;

			delta = Vector3.zero;

			if (!IsOnUI() && !startedDragOnUI)
			{
				delta.z = Input.GetAxis("Mouse ScrollWheel") * speed * Time.deltaTime * 1000;

				if(Input.GetKey(KeyCode.W))
					delta.y = speed * Time.deltaTime * 1 * this._camera.orthographicSize;
				else if (Input.GetKey(KeyCode.S))
					delta.y = speed * Time.deltaTime * -1 * this._camera.orthographicSize;
				if (Input.GetKey(KeyCode.A))
					delta.x = speed * Time.deltaTime * -1 * this._camera.orthographicSize;
				else if (Input.GetKey(KeyCode.D))
					delta.x = speed * Time.deltaTime * 1 * this._camera.orthographicSize;

				if (Input.GetMouseButton(1))
				{
					rotation.y += Input.GetAxis("Mouse X") * speed * Time.deltaTime * 100;
					rotation.x += -Input.GetAxis("Mouse Y") * speed * Time.deltaTime * 100;
				}

				// Closing menus on camera move
				if (delta.x != 0 || delta.y != 0 || (Input.GetAxis("Mouse X") != 0 || Input.GetAxis("Mouse Y") != 0)  && Input.GetMouseButton(1) || delta.z != 0)
				{
					this.gameManager.enemyMenu.HideAndClose();
					this.gameManager.playerMenu.HideAndClose();
				}
			}
			else if (Input.GetMouseButton(0) || Input.GetMouseButton(0))
				startedDragOnUI = true;

			if (startedDragOnUI && !IsOnUI() && !Input.GetMouseButton(0) && !Input.GetMouseButton(1))
				startedDragOnUI = false;

			transform.eulerAngles = (Vector2)rotation;
			transform.position += this.transform.right * delta.x + this.transform.up * delta.y;
			this._camera.orthographicSize = Mathf.Clamp(this._camera.orthographicSize- delta.z,10,100);
		}

		public bool IsOnUI()
		{
			return EventSystem.current.IsPointerOverGameObject();
		}
	}
}

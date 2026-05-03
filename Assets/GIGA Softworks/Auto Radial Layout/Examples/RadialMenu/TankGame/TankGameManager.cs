using GIGA.AutoRadialLayout.RadialMenu;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace GIGA.AutoRadialLayout.Examples
{
	public class TankGameManager : MonoBehaviour
	{
		public static Bounds GameBounds = new Bounds(new Vector3(50, 0, 50), new Vector3(100, 20, 100));
		[HideInInspector]
		public bool gameOver;
		public RectTransform labelsRoot;
		public EnemyLabel enemyLabelPrefab;
		public GameObject markerPrefab;
		public GameObject Marker { get; private set; }

		public ExampleDialog dialog_welcome, dialog_info;
		public RadialLayoutMenu playerMenu, enemyMenu;

		private void Start()
		{
			// Adding labels to enemies
			foreach (var enemy in FindObjectsOfType<TankGameEnemy>())
			{
				EnemyLabel label = GameObject.Instantiate(this.enemyLabelPrefab, this.labelsRoot);
				label.GetComponent<Text>().text = enemy.Health.ToString();
				label.enemy = enemy;
				enemy.label = label.GetComponent<Text>();
			}
		}

		private void Update()
		{
			// Getting click on tanks and opening their radial menu
			if (Input.GetMouseButtonDown(0))
			{
				Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
				RaycastHit hit;
				if (Physics.Raycast(ray, out hit, 1000.0f))
				{
					if (hit.transform.GetComponent<TankGameEnemy>() != null)
					{
						this.playerMenu.HideAndClose();
						this.enemyMenu.ShowAndOpen(hit.transform.gameObject,true);
						this.enemyMenu.GetComponent<EnemyRadialMenuListener>().linkedEnemy = hit.transform.GetComponent<TankGameEnemy>();
					}
					else if (hit.transform.GetComponent<TankGamePlayer>() != null)
					{
						this.enemyMenu.HideAndClose();
						this.playerMenu.ShowAndOpen(hit.transform.gameObject);
					}
					else if (!EventSystem.current.IsPointerOverGameObject() && hit.transform.GetComponent<Terrain>() != null)
					{
						this.enemyMenu.HideAndClose();
						this.playerMenu.HideAndClose();
					}
				}
				else if(!EventSystem.current.IsPointerOverGameObject())
				{
					this.enemyMenu.HideAndClose();
					this.playerMenu.HideAndClose();
				}
			}

			if (Input.GetKeyDown(KeyCode.Escape))
				Application.Quit();
		}

		public void GameOver()
		{
			StartCoroutine(this.PlayerDeath());
			gameOver = true;
		}

		private IEnumerator PlayerDeath()
		{
			GameObject playerObj = GameObject.FindGameObjectWithTag("Player");
			if (playerObj != null)
			{
				float elapsed = 0;
				while (elapsed < 1)
				{
					playerObj.transform.localScale = Vector3.Lerp(playerObj.transform.localScale, Vector3.zero, elapsed);
					elapsed += Time.deltaTime;
					yield return null;
				}
			}
		}

		public void SetMarker(Vector3 position)
		{
			if (this.Marker == null)
				this.Marker = GameObject.Instantiate(this.markerPrefab);

			if (this.Marker != null)
			{
				this.Marker.transform.position = FindGroundPosition(position);
			}
		}

		public void HideMarker()
		{
			if (this.Marker != null)
				GameObject.Destroy(this.Marker);
			this.Marker = null;
		}

		public static Vector3 FindGroundPosition(Vector3 gamePos)
		{
			Vector3 position = new Vector3(Mathf.Clamp(gamePos.x,0,100),gamePos.y,Mathf.Clamp(gamePos.z,0,100));

			Ray ray = new Ray(position + Vector3.up * 100, Vector3.down);

			LayerMask terrainMask = LayerMask.NameToLayer("Default");
			var hits = Physics.RaycastAll(ray);
			foreach (var hit in hits)
				if (hit.collider.name == "Terrain")
					position.y = hit.point.y;

			return position;
		}

		#region Radial menu listeners

		// Top left menu

		public void ShowWelcomeDialog()
		{
			this.dialog_info.gameObject.SetActive(false);
			this.dialog_welcome.gameObject.SetActive(true);
		}

		public void ShowInfoDialog()
		{
			this.dialog_welcome.gameObject.SetActive(false);
			this.dialog_info.gameObject.SetActive(true);
		}

		public void RestartGame()
		{
			SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
		}


		#endregion
	}
}

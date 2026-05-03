using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace GIGA.AutoRadialLayout.Examples
{
	public class TankGameEnemy : MonoBehaviour
	{
		public const float ENEMY_SPEED = 1.5f;

		public int id;
		public float Health { get; set; }
		public Text label;
		TankGameManager gameManager;
		public GameObject playerObj;
		public float speed = ENEMY_SPEED;

		public bool followPlayer = false;

		private void Awake()
		{
			this.Health = 100;
		}

		private void Start()
		{
			this.followPlayer = false;
			gameManager = GameObject.FindObjectOfType<TankGameManager>();
		}

		private void Update()
		{
			if (this.followPlayer)
			{
				// Following player
				if (this.playerObj == null)
					this.playerObj = GameObject.FindGameObjectWithTag("Player");

				Vector3 targetPos = playerObj.transform.position;
				bool awareOfPlayer = false;


				if(this.playerObj != null && !gameManager.gameOver && (awareOfPlayer || (this.transform.position - targetPos).magnitude > 0.1f))
				{
					awareOfPlayer = (playerObj.transform.position - this.transform.position).magnitude < 10;
					if (awareOfPlayer)
						targetPos = playerObj.transform.position;

					Vector3 targetVec = (targetPos - this.transform.position);
					targetVec.y = 0;

					// Rotating toward target
					float angle = Vector3.Angle(this.transform.forward, targetVec);
					
					if (angle > 15)
					{
						float dir = Mathf.Sign(Vector3.Dot(this.transform.right, targetVec));
						this.transform.eulerAngles = new Vector3(this.transform.eulerAngles.x, this.transform.eulerAngles.y + Time.deltaTime * 40 * dir, this.transform.eulerAngles.z);
						angle = Vector3.Angle(this.transform.forward, targetVec);
					}
					else if (angle > 6)
					{
						float dir = Mathf.Sign(Vector3.Dot(this.transform.right, targetVec));
						this.transform.eulerAngles = new Vector3(this.transform.eulerAngles.x, this.transform.eulerAngles.y + Time.deltaTime * 40 * dir, this.transform.eulerAngles.z);
					}


					this.transform.position += targetVec.normalized * Time.deltaTime * this.speed;
					this.transform.position = TankGameManager.FindGroundPosition(this.transform.position);
				}
			}
		}

		private void OnTriggerEnter(Collider other)
		{

			if (other.gameObject.transform.CompareTag("Player"))
			{
				// Gameover
				GameObject.FindObjectOfType<TankGameManager>().GameOver();
			}
		}
	}
}

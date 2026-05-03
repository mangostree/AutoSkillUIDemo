using GIGA.AutoRadialLayout.RadialMenu;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace GIGA.AutoRadialLayout.Examples
{
	public class TankGamePlayer : MonoBehaviour
	{

		public const float PLAYER_SPEED = 2;

		private Coroutine moveCor = null;

		// References
		public Projectile projectilePrefab;
		public RadialLayoutMenuNode radialMenuCannonNode;

		// Flags & Timers
		private bool stop;
		private float cannonReloadTime;

		private void Update()
		{
			if (this.cannonReloadTime > 0)
				this.cannonReloadTime -= Time.deltaTime;

			this.radialMenuCannonNode.GetComponent<Button>().interactable = this.cannonReloadTime <= 0;
			this.radialMenuCannonNode.label.text = this.cannonReloadTime <= 0 ? "Cannon" : "Reloading...";
		}

		public void MoveForward()
		{
			this.Move("forward");
		}
		
		public void MoveLeft()
		{
			this.Move("left");
		}
		
		public void MoveRight()
		{
			this.Move("right");
		}

		public void Move(string direction)
		{
			if (this.moveCor != null)
				this.StopCoroutine(this.moveCor);
			this.stop = false;
			this.moveCor = this.StartCoroutine(this.MoveCoroutine(direction));
		}

		public void Stop()
		{
			this.stop = true;
		}

		private IEnumerator MoveCoroutine(string direction)
		{
			float amount = 8;
			float speed = PLAYER_SPEED;

			Vector3 movementDelta = Vector3.zero;
			switch (direction)
			{
				case "forward":
					movementDelta = this.transform.forward;
					break;
				case "back":
					movementDelta = -this.transform.forward;
					break;
				case "left":
					movementDelta = Quaternion.Euler(0, -45, 0) * this.transform.forward;
					break;
				case "right":
					movementDelta = Quaternion.Euler(0, 45, 0) * this.transform.forward;
					break;
			}

			// Moving until amount reached
			Vector3 target = this.transform.position + movementDelta * amount;


			TankGameManager gameManager = GameObject.FindObjectOfType<TankGameManager>();
			gameManager.SetMarker(target);

			Vector3 targetVec = (gameManager.Marker.transform.position - this.transform.position);
			targetVec.y = 0;


			while (targetVec.magnitude > 0.1f && !stop)
			{
				// Rotating toward target
				float angle = Vector3.Angle(this.transform.forward, targetVec);
				float initialDir = Mathf.Sign(Vector3.Dot(this.transform.right, targetVec));

				while (angle > 6 && !stop)
				{
					float dir = Mathf.Sign(Vector3.Dot(this.transform.right, targetVec));
					if (dir * initialDir < 0) // If rotating in the opposite direction
						break;
					this.transform.eulerAngles = new Vector3(this.transform.eulerAngles.x, this.transform.eulerAngles.y + Time.deltaTime * 40 * dir, this.transform.eulerAngles.z);
					angle = Vector3.Angle(this.transform.forward, targetVec);
					yield return null;
				}

				targetVec = (gameManager.Marker.transform.position - this.transform.position);
				targetVec.y = 0;
				this.transform.position += targetVec.normalized * Time.deltaTime * speed;
				this.transform.position = TankGameManager.FindGroundPosition(this.transform.position);
				yield return null;
			}

			gameManager.HideMarker();


		}

		public void FireCannon()
		{
			if (this.cannonReloadTime <= 0)
			{
				var proj = GameObject.Instantiate(this.projectilePrefab, this.transform.parent);
				proj.transform.position = this.transform.position + this.transform.forward * 1f + Vector3.up * 0.5f;
				proj.GetComponent<Projectile>().Fire(Projectile.ProjectileType.Cannon, this.transform.forward);
				this.cannonReloadTime = 4;
			}
		}

		public void FireMachineGun()
		{
			this.StartCoroutine(MachineGunBurst());
		}

		private IEnumerator MachineGunBurst()
		{
			for (int k = 0; k < 4; k++)
			{
				var proj = GameObject.Instantiate(this.projectilePrefab, this.transform.parent);
				proj.transform.position = this.transform.position + this.transform.forward * 1f + Vector3.up * 0.5f;
				proj.GetComponent<Projectile>().Fire(Projectile.ProjectileType.MachineGun, this.transform.forward);
				yield return new WaitForSeconds(0.25f);
			}
		}

	}
}

using GIGA.AutoRadialLayout.Examples;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace GIGA.AutoRadialLayout.Examples
{

    public class EnemyRadialMenuListener : MonoBehaviour
    {
        public TankGameEnemy linkedEnemy;

        public void FollowPlayer()
        {
            if (this.linkedEnemy != null)
                this.linkedEnemy.followPlayer = true;
        }

		public void Stop()
		{
			if (this.linkedEnemy != null)
				this.linkedEnemy.followPlayer = false;
		}

        public void IncreaseSpeed(float amount)
        {
            if (this.linkedEnemy != null)
            {
                this.linkedEnemy.speed += amount;
                this.linkedEnemy.speed = Mathf.Clamp(this.linkedEnemy.speed, 0f, TankGameEnemy.ENEMY_SPEED * 1.5f);
            }
		}
	}
}

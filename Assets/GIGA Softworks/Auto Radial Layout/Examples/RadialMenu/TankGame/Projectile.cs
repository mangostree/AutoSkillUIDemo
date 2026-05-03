using GIGA.AutoRadialLayout.Examples;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace GIGA.AutoRadialLayout.Examples
{
    public class Projectile : MonoBehaviour
    {
        public enum ProjectileType { Cannon, MachineGun }

        private Vector3 direction;
        private ProjectileType projectileType;
        private float timeLived;

        public void Fire(ProjectileType type, Vector3 direction)
        {
            this.direction = direction;
            this.projectileType = type;
        }

        void Update()
        {
            float speed = this.projectileType == ProjectileType.Cannon ? 30 : 45f;

            this.transform.position = this.transform.position + direction.normalized * Time.deltaTime * speed;
            timeLived += Time.deltaTime;

            if (this.projectileType == ProjectileType.Cannon && timeLived >= 3 || this.projectileType == ProjectileType.MachineGun && timeLived >= 2.25f)
                Destroy(this.gameObject);
        }

        private void OnTriggerEnter(Collider other)
        {
            var enemy = other.gameObject.transform.GetComponent<TankGameEnemy>();
            if (enemy != null)
            {
                enemy.Health -= this.projectileType == ProjectileType.Cannon ? 100 : 20;
                enemy.label.text = enemy.Health.ToString();
                if (enemy.Health <= 0)
                    Destroy(enemy.gameObject);
                Destroy(this.gameObject);

            }
        }
    }
}

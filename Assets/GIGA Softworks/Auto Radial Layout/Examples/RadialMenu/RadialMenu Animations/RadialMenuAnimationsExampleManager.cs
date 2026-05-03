using GIGA.AutoRadialLayout.RadialMenu;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace GIGA.AutoRadialLayout.Examples
{
    public class RadialMenuAnimationsExampleManager : MonoBehaviour
    {
        public GridLayoutGroup grid;

        void Start()
        {
            // Setting up
            foreach (var menu in this.grid.GetComponentsInChildren<RadialLayoutMenu>())
            {
                menu.showAnimationSpeed = 1.25f;
                menu.openingAnimationSpeed = 1;
			}

            this.StartCoroutine(AnimationLoop());
		}

        private IEnumerator AnimationLoop()
        {
            yield return new WaitForSeconds(1);

            while (true)
            {
                foreach (var menu in this.grid.GetComponentsInChildren<RadialLayoutMenu>())
                {
                    menu.ShowAndOpen();
                }

                yield return new WaitForSeconds(0.55f);

				int nodeIndex = 0;

				while (nodeIndex < 5)
				{
					foreach (var menu in this.grid.GetComponentsInChildren<RadialLayoutMenu>())
					{
						for (int k = 0; k < 5; k++)
						{
							if (k == nodeIndex)
							{
								menu.layout.Nodes[k].GetComponent<RadialLayoutMenuNode>().label.gameObject.SetActive(true);
								menu.layout.Nodes[k].GetComponent<RadialLayoutMenuNode>().label.transform.SetParent(menu.labelsRoot.transform);
							}
						}
					}

					yield return new WaitForSeconds(0.1f);
					nodeIndex++;
				}

				yield return new WaitForSeconds(0.55f);

				foreach (var menu in this.grid.GetComponentsInChildren<RadialLayoutMenu>())
				{
					for (int k = 0; k < 5; k++)
					{
						menu.layout.Nodes[k].GetComponent<RadialLayoutMenuNode>().label.gameObject.SetActive(false);
					}
				}

				foreach (var menu in this.grid.GetComponentsInChildren<RadialLayoutMenu>())
				{
					menu.HideAndClose();
				}

				yield return new WaitForSeconds(1.5f);

				

			}
		}

        
    }
}

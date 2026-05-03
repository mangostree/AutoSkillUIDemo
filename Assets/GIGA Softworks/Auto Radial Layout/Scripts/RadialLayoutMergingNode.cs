using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace GIGA.AutoRadialLayout
{
    public class RadialLayoutMergingNode : MonoBehaviour
    {
        public List<RadialLayoutNode> convergingNodes;

		/// <summary>
		/// Destroys all converging links to this node.
		/// </summary>
		/// <param name="clearConvergingNodesList">If True also clears the list of converging nodes</param>
		public void DestroyAllMergedLinks(bool clearConvergingNodesList)
		{
			// Destroying all converging links
			RadialLayoutNode node = this.GetComponent<RadialLayoutNode>();
			if (node != null && this.convergingNodes != null)
			{
				foreach (var convergingNode in this.convergingNodes)
				{
					// Checking if already existing
					for (int k = node.Layout.linksRoot.transform.childCount - 1; k >= 0; k--)
					{
						RadialLayoutLink link = node.Layout.linksRoot.transform.GetChild(k).GetComponent<RadialLayoutLink>();
						if (link != null && link.from == convergingNode && link.to == node)
						{
							RadialLayout.DestroyGameObject(link.gameObject);
							if (link.from.DepartingLinks != null)
								link.from.DepartingLinks.Remove(link);
						}
					}

				}
			}

			if (clearConvergingNodesList)
				this.convergingNodes = new List<RadialLayoutNode>();
		}

		private void OnDestroy()
		{
			this.DestroyAllMergedLinks(true);
        }
	}
}

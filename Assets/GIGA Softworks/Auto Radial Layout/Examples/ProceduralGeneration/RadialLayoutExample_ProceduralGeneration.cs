using GIGA.AutoRadialLayout.QuerySystem;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace GIGA.AutoRadialLayout.Examples
{
    public class RadialLayoutExample_ProceduralGeneration : MonoBehaviour
    {
        public Text introLabel;
        public RadialLayoutNode dynamicNodePrefab;

		public void Start()
		{
            this.introLabel.text = "This layout was generated procedurally by the RadialLayoutFactory component.\nClick yellow nodes to add children dynamically.";
		}


		/// <summary>
		/// This Example function will be invoked by the RadialLayoutFactory component and will build a new layout from scratch.
		/// </summary>
		/// <param name="factory">The calling RadialLayoutFactory component.</param>
		public void BuildLayout(RadialLayoutFactory factory)
        {
            // This is an example of a procedural build of an entire layout

            // Using a dictionary to keep track of key nodes that will be reused during the build process.
            Dictionary<string,RadialLayoutNode> nodesRef = new Dictionary<string,RadialLayoutNode>();

            // Adding the first 4 "root" nodes
            // The first parameter of the AddNode function is the ID in the prefabs pool set in the factory inspector
            nodesRef.Add("diamondsRoot",factory.AddNode("diamonds","diamondsRoot",new string[] {"diamonds","red","root"}));
			nodesRef.Add("spadesRoot", factory.AddNode("spades", "spadesRoot", new string[] { "spades","black", "root" }));
			nodesRef.Add("heartsRoot", factory.AddNode("hearts", "heartsRoot", new string[] { "hearts","red", "root" }));
			nodesRef.Add("clubsRoot", factory.AddNode("clubs", "clubsRoot", new string[] { "clubs","black", "root" }));

            // Adding 7 nodes to each root
            foreach (var root in nodesRef) // nodesRef contains only the roots at the moment
            {
                // The prefab pool id that we used (diamonds, spades...) is the key of the dictionary without the "Root" suffix (eg. diamondsRoot -> diamonds
                string prefabPoolId = root.Key.Replace("Root", "");

                // Disabling the node label
				root.Value.transform.Find("Number").GetComponent<Text>().enabled = false;

				// Getting the same tags of the root, but removing "root"
				List<string> tags = new List<string>(root.Value.GetComponent<RadialLayoutQueryTarget>().tags);
                tags.Remove("root");

                for (int k = 0; k < 7; k++)
                {
                    string nodeName = string.Join("_", tags) + "_" + k; // using tags to generate node name
                    var n = factory.AddNode(root.Value, prefabPoolId, nodeName, tags.ToArray());
                    // Setting the label
					n.transform.Find("Number").GetComponent<Text>().text = k.ToString();
				}

                // Nodes are added in a circular manner, so the numbering in the label will be circular
                // We can invert specific nodes order to distribute labels in a more ordered way (blacks always top to bottom, reds always left to right)
                if(tags.Contains("diamonds") || tags.Contains("spades"))
                    root.Value.InvertChildNodes();
			}

            // Changing the radius of circle of depth 2 (enabling multiple depth radius)
            // The 2nd level is the first element in the radii array, since element 0 is the base radius
            factory.MainLayout.enableSeparateExternalRadii = true;
            factory.MainLayout.externalCircleRadiusMultiplier = new float[1];
            factory.MainLayout.externalCircleRadiusMultiplier[0] = 1.45f; // Making the 2nd level 45% bigger than the base radius

            // Moving black roots a little further &  expanding the fan span
            nodesRef["spadesRoot"].distanceOffset = 180;
			nodesRef["clubsRoot"].distanceOffset = 180;
            foreach (var root in nodesRef)
            {
                root.Value.overrideFanSpan = true;
                // Different fan span based on color tag
                root.Value.fanSpanOverride = root.Value.GetComponent<RadialLayoutQueryTarget>().ContainsTag("black",false) ? 80 : 150;
            }

            // Alternating background colors of the nodes
            foreach (var root in nodesRef.ToList())
            {
                var childNodes = root.Value.GetChildNodes();
                for (int k = 0; k < childNodes.Length; k++)
                {
                    if (k % 2 == 0)
                        childNodes[k].transform.Find("Background").GetComponent<Image>().color = new Color(0.8f, 0.8f, 1f, 1);

                    // saving "external" nodes, we'll use them later
                    if ((root.Key.Contains("spades") || root.Key.Contains("clubs")) && (k == 0 || k == childNodes.Length - 1))
                        nodesRef.Add(root.Key.Replace("Root","External") + "_" + k.ToString(), childNodes[k]);
                }
            }

			// Adding a child node to every central node in the span, with 3 child nodes as well, and convert them to sublayouyts
			foreach (var root in nodesRef)
			{
                // we added other nodes to the reference dictionary, skipping them...
                if (!root.Key.EndsWith("Root"))
                    continue;

				var childNodes = root.Value.GetChildNodes();
                // we already know that the span is composed of 7 (odd) nodes...
                var middleNode = childNodes[childNodes.Length / 2];
                // using a different prefab for this node("flat")
                var addedNode = factory.AddNode(middleNode, "flat");
				//adding 3 nodes to this new node, using the same technique of before to determine the pool id
				string prefabPoolId = root.Key.Replace("Root", "");
				// Getting the same tags of the root, but removing "root" and adding "tip"
				List<string> tags = new List<string>(root.Value.GetComponent<RadialLayoutQueryTarget>().tags);
				tags.Remove("root");
                tags.Add("tip");

                // adding nodes & changing colors
				var n = factory.AddNode(addedNode, prefabPoolId, null,tags.ToArray());
                n.transform.Find("Background").GetComponent<Image>().color = Color.red;
                n.transform.Find("Number").GetComponent<Text>().enabled = false;
				n = factory.AddNode(addedNode, prefabPoolId, null, tags.ToArray());
				n.transform.Find("Background").GetComponent<Image>().color = Color.green;
				n.transform.Find("Number").GetComponent<Text>().enabled = false;
				n = factory.AddNode(addedNode, prefabPoolId, null, tags.ToArray());
				n.transform.Find("Background").GetComponent<Image>().color = Color.blue;
				n.transform.Find("Number").GetComponent<Text>().enabled = false;

				// converting the added note to a sublayout, child nodes will rearrange automatically
				// around it
				var subLayout = factory.ConvertToSublayout(addedNode);

                // now we can rotate the sublayout so it points outward, calculating the angle from the center and applying it to the
                // rotation offset
                float angle = Vector3.Angle(Vector3.up, subLayout.transform.position - factory.MainLayout.transform.position);
                if (subLayout.transform.position.x > factory.MainLayout.transform.position.x)
                    angle *= -1;
				subLayout.rotationOffset = angle;

                // enabling inner links for the sublayouts
                subLayout.showInnerLinks = true;

			}

            // Adding final "external" nodes 
            foreach (var root in nodesRef)
            {
                if (root.Key.Contains("External"))
                {
                    var n = factory.AddNode(root.Value, "clickable");
					n.transform.Find("Background").GetComponent<Image>().color = Color.yellow;

                    // Adding click listener to instantiate new nodes on click
                    n.GetComponent<Button>().onClick.AddListener(delegate { OnNodeClick(n); });
				}
			}
		}

        private void OnNodeClick(RadialLayoutNode node)
        {
            // Add new child node to calling node
            node.Layout.AddNodeFromPrefab(this.dynamicNodePrefab,"dynamicNode",node);   
        }
	}
}

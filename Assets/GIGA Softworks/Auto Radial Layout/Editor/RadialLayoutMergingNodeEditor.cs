#if UNITY_EDITOR
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;

namespace GIGA.AutoRadialLayout.Editor
{
	[CustomEditor(typeof(RadialLayoutMergingNode))]
	public class RadialLayoutMergingNodeEditor : UnityEditor.Editor
	{
		private int lastConvNodesCount = 0;

		public override void OnInspectorGUI()
		{
			serializedObject.Update();

			var t = target as RadialLayoutMergingNode;

			// Pre-checks
			// Checking if applied to a Node
			if (t.GetComponent<RadialLayoutNode>() == null)
				EditorGUILayout.HelpBox("MergingNode script must be added to a RadialLayoutNode gameobject", MessageType.Error);

			DrawDefaultInspector();

			// If changed the count of the converging nodes list, rebuild the layout
			if (t.convergingNodes != null)
			{
				if (t.convergingNodes.Count != this.lastConvNodesCount)
				{
					RadialLayoutNode node = t.GetComponent<RadialLayoutNode>();
					if (node != null && node.Layout != null)
					{
						if(node.Layout.autoRebuildMode == RadialLayout.AutoRebuildMode.Always || !Application.isPlaying && node.Layout.autoRebuildMode == RadialLayout.AutoRebuildMode.EditorOnly)
							node.Layout.Rebuild();
					}
				}

				this.lastConvNodesCount = t.convergingNodes.Count;
			}

			if (GUILayout.Button("Clear all converging nodes"))
				t.DestroyAllMergedLinks(true);

			serializedObject.ApplyModifiedProperties();

			// Version label
			RadialLayoutEditor.DrawVersionLabel();

		}
	}
}

#endif

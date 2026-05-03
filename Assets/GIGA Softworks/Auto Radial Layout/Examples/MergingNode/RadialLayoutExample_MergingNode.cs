using GIGA.AutoRadialLayout.QuerySystem;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace GIGA.AutoRadialLayout.Examples
{
    public class RadialLayoutExample_MergingNode : MonoBehaviour
    {
		public RadialLayout layout;

		// Storing progress of intermediate nodes
		public float[] diamondProgress = new float[3];
		public float[] clubsProgress = new float[2];

		// References to nodes in the skill tree
		public RadialLayoutNode rootNode_Right, rootNode_Left_0,rootNode_Left_1;

		private void OnGUI()
		{
			GUIStyle areaStyle = new GUIStyle("box");
			areaStyle.normal.background = RadialLayoutExamplesHelper.MakeTex(2, 2, new Color32(0x00, 0x4f, 0xe1, 255));
			GUIStyle boldLabel = new GUIStyle("label");
			boldLabel.fontStyle = FontStyle.Bold;


			GUILayout.BeginArea(new Rect(0, 0, Screen.width / 4f + 50, Screen.height), areaStyle);

			GUILayout.BeginVertical(GUILayout.Width(Screen.width / 4f));

			GUILayout.Label("Merging Node example", boldLabel);

			GUILayout.Label("To create a merging node, simply add the RadialLayoutMergingNode component to a node and assign the converging nodes in the inspector. Links will be generated automatically.\n\nUse the sliders below to advance skills progress and unlock the locked nodes.");

			bool valueChange = false;

			GUILayout.Space(20);

			// Diamonds progress
			GUILayout.BeginHorizontal();
			GUILayout.Label("Diamond Progress 0: ");
			float diamond_progress_0 = GUILayout.HorizontalSlider(this.diamondProgress[0], 0, 2);
			valueChange |= diamond_progress_0 != this.diamondProgress[0]; 
			GUILayout.EndHorizontal();

			GUILayout.BeginHorizontal();
			GUILayout.Label("Diamond Progress 1: ");
			float diamond_progress_1 = GUILayout.HorizontalSlider(this.diamondProgress[1], 0, 2);
			valueChange |= diamond_progress_1 != this.diamondProgress[1];
			GUILayout.EndHorizontal();

			GUILayout.BeginHorizontal();
			GUILayout.Label("Diamond Progress 2: ");
			float diamond_progress_2 = GUILayout.HorizontalSlider(this.diamondProgress[2], 0, 2);
			valueChange |= diamond_progress_2 != this.diamondProgress[2];
			GUILayout.EndHorizontal();

			GUILayout.Space(20);

			// Clubs progress
			GUILayout.BeginHorizontal();
			GUILayout.Label("Clubs Progress 0: ");
			float clubs_progress_0 = GUILayout.HorizontalSlider(this.clubsProgress[0], 0, 1);
			valueChange |= clubs_progress_0 != this.clubsProgress[0];
			GUILayout.EndHorizontal();

			GUILayout.BeginHorizontal();
			GUILayout.Label("Clubs Progress 1: ");
			float clubs_progress_1 = GUILayout.HorizontalSlider(this.clubsProgress[1], 0, 1);
			valueChange |= clubs_progress_1 != this.clubsProgress[1];
			GUILayout.EndHorizontal();

			this.diamondProgress[0] = diamond_progress_0;
			this.diamondProgress[1] = diamond_progress_1;
			this.diamondProgress[2] = diamond_progress_2;
			this.clubsProgress[0] = clubs_progress_0;
			this.clubsProgress[1] = clubs_progress_1;

			GUILayout.EndVertical();

			GUILayout.EndArea();

			if (valueChange)
				this.RefreshLinksProgress();
		}

		private void RefreshLinksProgress()
		{
			// Updating link progress and locked / unlocked status.
			// In production environments or for big trees it is recommended to store a reference to your nodes instead of using the query system every time

			// First diamond skill chain
			RadialLayoutNode diamondNode = RadialLayoutQueryManager.Begin(this.layout).GetNodesWithTags("diamond_1").Nodes[0];
			RadialLayoutNode hearthNode = RadialLayoutQueryManager.Begin(this.layout).GetNodesWithTags("hearth_right").Nodes[0];
			RadialLayoutLink resultLink = RadialLayoutQueryManager.Begin(this.layout).GetLinkConnecting(this.rootNode_Right, diamondNode);
			resultLink.ProgressValue = this.diamondProgress[0];
			resultLink = RadialLayoutQueryManager.Begin(this.layout).GetLinkConnecting(diamondNode,hearthNode);
			resultLink.ProgressValue = this.diamondProgress[0]-1;
			diamondNode.transform.Find("Background").GetComponent<Image>().color = this.diamondProgress[0] < 1 ? Color.white : Color.cyan;

			// Second diamond skill chain
			diamondNode = RadialLayoutQueryManager.Begin(this.layout).GetNodesWithTags("diamond_2").Nodes[0];
			resultLink = RadialLayoutQueryManager.Begin(this.layout).GetLinkConnecting(this.rootNode_Right, diamondNode);
			resultLink.ProgressValue = this.diamondProgress[1];
			resultLink = RadialLayoutQueryManager.Begin(this.layout).GetLinkConnecting(diamondNode, hearthNode);
			resultLink.ProgressValue = this.diamondProgress[1] - 1;
			diamondNode.transform.Find("Background").GetComponent<Image>().color = this.diamondProgress[1] < 1 ? Color.white : Color.cyan;

			// Third diamond skill chain
			diamondNode = RadialLayoutQueryManager.Begin(this.layout).GetNodesWithTags("diamond_3").Nodes[0];
			resultLink = RadialLayoutQueryManager.Begin(this.layout).GetLinkConnecting(this.rootNode_Right, diamondNode);
			resultLink.ProgressValue = this.diamondProgress[2];
			resultLink = RadialLayoutQueryManager.Begin(this.layout).GetLinkConnecting(diamondNode, hearthNode);
			resultLink.ProgressValue = this.diamondProgress[2] - 1;
			diamondNode.transform.Find("Background").GetComponent<Image>().color = this.diamondProgress[2] < 1 ? Color.white : Color.cyan;

			// Hearth right Lock/Unlock
			hearthNode.transform.Find("Locked").gameObject.SetActive(this.diamondProgress[0] < 2 || this.diamondProgress[1] < 2 || this.diamondProgress[2] < 2 );

			// Left clubs progress 1
			hearthNode = RadialLayoutQueryManager.Begin(this.layout).GetNodesWithTags("hearth_left").Nodes[0];
			resultLink = RadialLayoutQueryManager.Begin(this.layout).GetLinkConnecting(this.rootNode_Left_0, hearthNode);
			resultLink.ProgressValue = this.clubsProgress[0];

			// Left clubs progress 2
			resultLink = RadialLayoutQueryManager.Begin(this.layout).GetLinkConnecting(this.rootNode_Left_1, hearthNode);
			resultLink.ProgressValue = this.clubsProgress[1];

			// Hearth right Lock/Unlock
			hearthNode.transform.Find("Locked").gameObject.SetActive(this.clubsProgress[0] < 1 || this.clubsProgress[1] < 1);


		}

	}
}

using GIGA.AutoRadialLayout.QuerySystem;
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

namespace GIGA.AutoRadialLayout
{
    /// <summary>
    /// Factory class for procedural generation of a radial layout
    /// </summary>
    [RequireComponent(typeof(RadialLayout))]
    public class RadialLayoutFactory : MonoBehaviour
    {
        public RadialLayout MainLayout { get; private set; }
		/// <summary>
		/// The current layout on which next operations will be executed (default is the main layout)
		/// </summary>
		public RadialLayout CurrentLayout { get; private set; }

		[Serializable]
		public class NodePoolElement
		{
			public string id;
			public RadialLayoutNode node;
		}
		[Tooltip("Place your node prefabs in here and assign them an unique ID so they can be recalled during build process.")]
		public NodePoolElement[] nodePool;  // Pool of node prefabs that can be instantiated in this layout

		// Flags
		[Tooltip("If TRUE, building delegate will be called when game starts and the layout will be built automatically.\nTurn this off if the layout has already been built in the editor and doesn't need to be rebuilt.")]
		public bool runOnStart = true;
		[Tooltip("If TRUE, layout will be cleared before starting the building process.")]
		public bool clearLayoutBeforeBuild = true;
		[Tooltip("If TRUE, this component will be destroyed after the layout has been built. (Runtime only)")]
		public bool destroyAfterBuild = true;
		[Tooltip("Set to true if you want to enable the query system on this layout.")]
		public bool useQuerySystem = false;
		[Tooltip("If TRUE, nodes and links will keep prefab references when instantiated. (Editor only)")]
		public bool prefabBasedInstancing = false;
		private Dictionary<string, RadialLayoutNode> nodePoolDict;

		//This is the delegate function that will actually build the layout based on user logic.
		public UnityEvent<RadialLayoutFactory> buildDelegate = null;

		#region Execution

		private void Start()
		{
			if (this.runOnStart)
				this.Build();
		}

		/// <summary>
		/// Executes the build delegate method and builds the layout
		/// </summary>
		public void Build()
		{
			this.MainLayout = this.GetComponent<RadialLayout>();

			if (this.MainLayout == null)
			{
				Debug.LogError("RadialLayoutFactory: cannot find RadialLayout component. Make sure this script is added to a gameobject that contains a RadialLayout component.");
				return;
			}
			this.SetCurrentLayout(this.MainLayout);

			// Creating dictionary from pool array for quick reference
			this.nodePoolDict = new Dictionary<string, RadialLayoutNode>();
			foreach (var node in this.nodePool)
			{
				if (!string.IsNullOrEmpty(node.id) && node.node != null)
				{
					if (!this.nodePoolDict.ContainsKey(node.id))
						this.nodePoolDict.Add(node.id, node.node);
					else
						Debug.LogWarning($"RadialLayoutFactory: node id {node.id} is duplicated, ignoring this node from the node pool.");
				}
				else
					Debug.LogWarning("RadialLayoutFactory: found a node with empty id or empty node reference. Please check the provided node pool list.");
			}

			if (this.clearLayoutBeforeBuild)
				this.MainLayout.Clear();

			// Initializing layout
			this.MainLayout.Initialize();

			// Applying initial settings to the layout
			this.MainLayout.UseQuerySystem = this.useQuerySystem;
#if UNITY_EDITOR
			this.MainLayout.keepPrefabLinking = this.prefabBasedInstancing;
#endif

			// Calling the delegate building function
			if (this.buildDelegate.GetPersistentEventCount() == 0)
				Debug.LogWarning("RadialLayoutFactory has no registered delegate methods, building will not proceed.");
			this.buildDelegate?.Invoke(this);

			// Calling a final rebuild
			MainLayout.Rebuild();

			if (this.destroyAfterBuild && Application.isPlaying)
				GameObject.Destroy(this);
		}

		/// <summary>
		/// Clears the layout and deletes all nodes.
		/// </summary>
		public void ClearLayout()
		{
			this.MainLayout = this.GetComponent<RadialLayout>();

			if (this.MainLayout == null)
			{
				Debug.LogError("RadialLayoutFactory: cannot find RadialLayout component. Make sure this script is added to a gameobject that contains a RadialLayout component.");
				return;
			}

			this.MainLayout.Clear();
		}

#endregion

		#region Node Building

		/// <summary>
		/// Adds a node to the CurrentLayout (default is the main layout)
		/// <param name="nodePoolId">Id of the node prefab to instantiate, picked from the provided pool. If NULL, the default node will be instantiated (the one specified in the RadialLayout inspector)</param>
		/// <param name="nodeName">Name of the node GameObject, leave null to use the default prefab name.</param>
		/// <param name="tags">List of tags to add to the node, used only if the query system is enabled.</param>
		/// </summary>
		public RadialLayoutNode AddNode(string nodePoolId = null,string nodeName = null,string[] tags = null)
		{
			return this.AddNode(this.CurrentLayout,nodePoolId,nodeName,tags);
		}

		/// <summary>
		/// Adds a node to the specified layout
		/// </summary>
		/// <param name="layout">The target layout that will receive the node.</param>
		/// <param name="nodePoolId">Id of the node prefab to instantiate, picked from the provided pool. If NULL, the default node will be instantiated (the one specified in the RadialLayout inspector)</param>
		/// <param name="nodeName">Name of the node GameObject, leave null to use the default prefab name. </param>
		/// <param name="tags">List of tags to add to the node, used only if the query system is enabled.</param>
		public RadialLayoutNode AddNode(RadialLayout layout, string nodePoolId = null, string nodeName = null, string[] tags = null)
		{
			return AddNode(layout, null, nodePoolId, nodeName, tags);
		}

		/// <summary>
		/// Adds a child node to the specified parent node
		/// </summary>
		/// <param name="nodePoolId">Id of the node prefab to instantiate, picked from the provided pool. If NULL, the default node will be instantiated (the one specified in the RadialLayout inspector)</param>
		/// <param name="nodeName">Name of the node GameObject, leave null to use the default prefab name. </param>
		/// <param name="tags">List of tags to add to the node, used only if the query system is enabled.</param>
		/// <returns></returns>
		public RadialLayoutNode AddNode(RadialLayoutNode parentNode, string nodePoolId = null, string nodeName = null, string[] tags = null)
		{
			return this.AddNode(parentNode.Layout,parentNode,nodePoolId,nodeName,tags);	
		}

		private RadialLayoutNode AddNode(RadialLayout layout, RadialLayoutNode parentNode, string nodePoolId = null, string nodeName = null, string[] tags = null)
		{
			RadialLayoutNode addedNode = null;
			if (!string.IsNullOrEmpty(nodePoolId))
			{
				// Looking for the specified node prefab
				if (this.nodePoolDict.ContainsKey(nodePoolId))
				{
					if (this.nodePoolDict[nodePoolId] != null)
					{
						RadialLayoutNode prevNodePrefab = layout.prefab_node;
						layout.prefab_node = this.nodePoolDict[nodePoolId];
						addedNode = layout.AddNode(nodeName, parentNode);
						// Restoring prev prefab
						layout.prefab_node = prevNodePrefab;
					}
					else
						Debug.LogWarning($"RadialLayoutFactory: failed to instantiate node with id {nodePoolId}, referenced prefab is null.");
				}
				else
					Debug.LogWarning($"RadialLayoutFactory: failed to instantiate node with id {nodePoolId}, the id doesn't exist.");
			}
			else
			{
				// Adding the default node
				addedNode = layout.AddNode(nodeName, parentNode);
			}

			// Adding tags if needed
			if (this.useQuerySystem && tags != null && tags.Length > 0)
				this.AddTagsToNode(addedNode, tags);

			return addedNode;
		}

		public RadialLayout ConvertToSublayout(RadialLayoutNode node)
		{
			if(!node.IsSubLayout)
				node.ConvertToSubLayout();
			return node.GetSubLayout();
		}

		#endregion

		#region Query System

		/// <summary>
		/// Adds a list of tags to the specified node, works only if query system is enabled.
		/// </summary>
		/// <param name="node">The target node.</param>
		/// <param name="tags">The list of tags to add to this node.</param>
		public void AddTagsToNode(RadialLayoutNode node,string[] tags)
		{
			RadialLayoutQueryTarget queryTarget = node.GetComponent<RadialLayoutQueryTarget>();
			if (queryTarget != null && tags != null && tag.Length > 0)
			{
				if (queryTarget.tags == null)
					queryTarget.tags = new List<string>();

				foreach (var tag in tags)
				{
					if(!queryTarget.tags.Contains(tag))
						queryTarget.tags.Add(tag);
				}
			}
		}

		#endregion

		#region Navigation

		public void SetCurrentLayout(RadialLayout layout)
		{
			if (layout != null)
				this.CurrentLayout = layout;
		}

		#endregion

	}
}

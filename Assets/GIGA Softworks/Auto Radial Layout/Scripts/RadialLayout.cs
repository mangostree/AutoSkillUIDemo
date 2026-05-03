using GIGA.AutoRadialLayout.QuerySystem;
using System;
using System.Collections;
using System.Collections.Generic;
#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.Experimental.GraphView;

#endif
using UnityEngine;
using UnityEngine.UI;

namespace GIGA.AutoRadialLayout
{
    [ExecuteInEditMode]
    public class RadialLayout : MonoBehaviour
    {
#if UNITY_EDITOR
        public static GameObject forceSelectionSkippedObject = null; // used if Forced selection is active, to allow object selection if clicked a second time
        public static GameObject forceSelectionTargetedObject = null; // used if Forced selection is active, to allow object selection if clicked a second time
#endif

        // Constants
        public const string VERSION = "1.2.1";

        // Enums
        public enum AutoRebuildMode { Never, EditorOnly, Always };
        public enum NodesDistribution { Concentric, Branches };
        public enum RelativeRotation { RelativeToParent, KeepOwn }
        public enum LinksRebuildMode {Regenerate,KeepExisting };

        // Auto-rebuild
        public AutoRebuildMode autoRebuildMode = AutoRebuildMode.EditorOnly;

        // Layout settings
        public float circleRadius = 100;
        public float circleSlice = 360;
        public float rotationOffset = 0;
        public RelativeRotation relativeRotationMode = RelativeRotation.RelativeToParent;
        public float[] externalCircleRadiusMultiplier = new float[] { 1.0f };
        public bool enableSeparateExternalRadii;                                // If true, different multipliers can be applied to each depth
        public bool scaleNodeWithRadius = false;
        public float nodeScaleFactor = 100;
        public float nodeScale_min = 0.5f, nodeScale_max = 2f;
        public NodesDistribution nodesDistribution;
        public float fanDistributionCommonSpan = 60;                            // Shared span when using "branches" distribution
        public float branchesCommonLength = 1;                                  // Shared distance when using "branches" distribution (multiplier of the main circle radius)
        public bool showInnerNode = false;
        public float innerNodeScale = 1.0f;
        public bool showInnerLinks = false;
        public LinksRebuildMode linksRebuildMode = LinksRebuildMode.Regenerate;
        public RadialLayoutLink.ProgressMode linksProgressMode;
        public uint linksProgressSpeed = 40;
#if UNITY_EDITOR
        public bool forceNodesSelection = false;                                // Forces selection of node objects in editor
#endif
        // Getters
        public int MaxDepth { get; private set; }
        public bool IsSubLayout { get { return this.GetComponentInParent<RadialLayoutNode>() != null; } }
        public RadialLayout ParentLayout { get; private set; }
        public int NodeCount { get {
                int count = this.Nodes.Count;
                foreach (var subLayout in this.subLayouts)
                    count += subLayout.NodeCount;
                return count;
            } }
        public int SubLayoutsCount { get { return this.GetSubLayouts(true).Length; } }

        // References
        public GameObject innerNode;
        private Canvas _canvas;
        public Canvas Canvas{get{
                if (_canvas == null)
                {
                    _canvas = GetComponentInParent<Canvas>();
                    if (_canvas != null && _canvas.transform.parent != null)
                    {
                        Canvas parentCanvas = _canvas.transform.parent.GetComponentInParent<Canvas>();
                        this.IsNestedCanvas = parentCanvas != null && parentCanvas != _canvas;
                    }
                    else
                        this.IsNestedCanvas = false;
                }
                return _canvas;
            } }

        // Prefabs
        public RadialLayoutNode prefab_node;
        public RadialLayoutLink prefab_link;

        // Query System
        [SerializeField]
        private bool _useQuerySystem;
        public bool UseQuerySystem {
            get
            {
                if (!this.IsSubLayout)
                    return this._useQuerySystem;
                else
                {
                    var masterLayout = this.GetMasterLayout();
                    if (masterLayout != this)
                        return masterLayout.UseQuerySystem;
                    else
                    {
                        Debug.LogWarning($"Found a sub-layout that is master of itself. ({this.name})");
                        return this._useQuerySystem;
                    }
                }
            }
            set {
                this.SetupQuerySystem(value);
            } }

        // Editor Flags
#if UNITY_EDITOR
        public bool drawEditorGizmos = true;
        public bool drawEditorGizmos_circleRadius = true;
        public bool drawEditorGizmos_nodes = true;
        public bool drawEditorGizmos_handles = true;
        public bool handle_lockRadius = false;
        public bool handle_lockRotation = false;
        public bool handle_snap = false;
        public bool handle_snap_intValues = false;
        public float handle_snap_resolution = 10;
        public bool keepPrefabLinking = false;
#endif

        public List<RadialLayoutNode> Nodes { get; private set; }
        private List<RadialLayout> subLayouts;
        private int lastChildCount = 0;

        // Cached values for auto-rebuild
        private float lastRotationOffset;
        private float lastCircleRadius;
        private float lastCircleSlice;
        private float lastNodeScaleFactor;
        private float lastNodeScale_min, lastNodeScale_max;
        private bool lastScaleNodeWithRadius;
        private bool lastEnableSeparateExternalRadii;
        private float[] lastExternalCircleRadiusMultiplier;
        private NodesDistribution lastNodesDistribution;
        private float lastFanDistributionCommonSpan;
        private float lastsBranchesCommonLenght;
        private bool lastShowInnerNode;
        private float lastInnerNodeScale;
        private bool lastShowInnerLinks;
        private RelativeRotation lastRelativeRotation;

        public GameObject linksRoot;

        // Internal data
        private List<RadialLayoutNode> linksRebuildList;
        private byte linksRebuildFrameCounter;
        private Transform lastParent;


        // Events
        public Action<RadialLayout> onRebuild;

        // Flags
        private bool initialized;
        public bool IsNestedCanvas { get; private set; }
        public bool IsWorldSpace => this.Canvas != null && this._canvas.renderMode == RenderMode.WorldSpace;

		private void OnEnable()
		{
            this.ResetUpdateParameters();
            this.lastParent = this.transform.parent;
#if UNITY_EDITOR
            Undo.undoRedoPerformed += OnUndoPerformed;
#endif
        }

		private void OnDisable()
		{
#if UNITY_EDITOR
            Undo.undoRedoPerformed -= OnUndoPerformed;
#endif
        }


		void Update()
        {
            // Detecting parent change and refreshing canvas reference
            if (this.transform.parent != this.lastParent)
            {
                this.RefreshCanvasReference();
                if(this.subLayouts != null)
                    foreach (var sublayout in this.subLayouts)
                        sublayout.RefreshCanvasReference();
                this.lastParent = this.transform.parent;
            }

            if (!initialized)
            {
                this.Initialize();
            }
            else if (this.autoRebuildMode == AutoRebuildMode.Always || !Application.isPlaying && this.autoRebuildMode == AutoRebuildMode.EditorOnly || this.linksRebuildList != null)
            {
                bool rebuild = false;
                if (this.transform.childCount != this.lastChildCount ||
                    this.circleRadius != this.lastCircleRadius ||
					this.circleSlice != this.lastCircleSlice ||
					this.rotationOffset != this.lastRotationOffset ||
                    this.nodeScaleFactor != this.lastNodeScaleFactor ||
                    this.scaleNodeWithRadius != this.lastScaleNodeWithRadius ||
                    this.lastNodeScale_min != this.nodeScale_min ||
                    this.lastNodeScale_max != this.nodeScale_max ||
                    this.enableSeparateExternalRadii != this.lastEnableSeparateExternalRadii ||
                    this.nodesDistribution != this.lastNodesDistribution ||
                    this.fanDistributionCommonSpan != this.lastFanDistributionCommonSpan ||
                    this.branchesCommonLength != this.lastsBranchesCommonLenght ||
                    this.showInnerNode != this.lastShowInnerNode ||
                    this.innerNodeScale != this.lastInnerNodeScale ||
                    this.showInnerLinks != this.lastShowInnerLinks ||
                    this.relativeRotationMode != this.lastRelativeRotation)
                {
                    rebuild = true;
                    this.ResetUpdateParameters();
                }

                if (this.lastExternalCircleRadiusMultiplier == null || this.lastExternalCircleRadiusMultiplier.Length != this.externalCircleRadiusMultiplier.Length)
                {
                    this.lastExternalCircleRadiusMultiplier = new float[this.externalCircleRadiusMultiplier.Length];
                }
                for (int k = 0; k < this.externalCircleRadiusMultiplier.Length; k++)
                    if (this.lastExternalCircleRadiusMultiplier[k] != this.externalCircleRadiusMultiplier[k])
                    {
                        this.lastExternalCircleRadiusMultiplier[k] = this.externalCircleRadiusMultiplier[k];
                        rebuild = true;
                    }

                if(rebuild)
                    this.Rebuild();

                // Links rebuild
                this.RebuildLinks(initialized);

                initialized = true;

            }
            else if (Application.isPlaying && this.autoRebuildMode != AutoRebuildMode.Always)
                this.enabled = false;
        }



		#region Layout

		public void Initialize()
		{
            this.Rebuild();
            this.RebuildLinks(false);
            this.SetupQuerySystem(this._useQuerySystem);
            initialized = true;
        }

        private void RefreshCanvasReference()
        {
            this._canvas = null;
            this._canvas = this.Canvas;
        }

        /// <summary>
        /// Rebuilds the entire Node and Link structure
        /// </summary>
        public void Rebuild()
        {
            // Destroying links
            for (int k = this.linksRoot.transform.childCount - 1; k >= 0; k--)
            {
                if (this.GetMasterLayout().linksRebuildMode == LinksRebuildMode.Regenerate)
                {
                    // Destroying all links
                    DestroyGameObject(this.linksRoot.transform.GetChild(k).gameObject);
                }
                else
                {
                    // Destroying orphans
                    RadialLayoutLink link = this.linksRoot.transform.GetChild(k).GetComponent<RadialLayoutLink>();
                    if(link != null && link.to == null || link.from == null && link.fromLayout == null)
						DestroyGameObject(this.linksRoot.transform.GetChild(k).gameObject);

                    // Destroying inner links if option is turned off
                    if(link.from == null && link.fromLayout == this && !this.showInnerLinks)
						DestroyGameObject(this.linksRoot.transform.GetChild(k).gameObject);
				}
			}

			//Deleting orphans links root清理无主 linksRoot
			foreach (var linksRoot in this.transform.GetComponentsInChildren<RadialLayoutLinksRoot>())
			{
				if (linksRoot != this.linksRoot && linksRoot.parentLayout == null)
					DestroyGameObject(linksRoot.gameObject);
			}

			// Resetting data
			this.Nodes = new List<RadialLayoutNode>();
            this.subLayouts = new List<RadialLayout>();
            this.MaxDepth = 0;

            Dictionary<Transform, int> sortingDictionary = new Dictionary<Transform, int>();
            int subLayoutsCount = 0;

            foreach (Transform t in this.transform)//遍历当前布局的直接子物体
			{
                if (!t.gameObject.activeSelf)
                    continue;

                // Skipping inner node
                if (t.gameObject == this.innerNode)//中心节点
                {
                    sortingDictionary.Add(t, -1);
                    continue;
                }

                if (t.gameObject == this.linksRoot)//连接根节点
                    sortingDictionary.Add(t, 0);

                if (t.GetComponent<LayoutElement>() == null || !t.GetComponent<LayoutElement>().ignoreLayout)
                {
					//确保有LayoutNode组件，没有就补上
					if (t.GetComponent<RadialLayoutNode>() == null)
                        t.gameObject.AddComponent<RadialLayoutNode>();
                    RadialLayoutNode node = t.gameObject.GetComponent<RadialLayoutNode>();
                    this.Nodes.Add(node);
                    node.SetParent(this);
                    node.depth = 0;
                    int maxDepth = node.ScanForSubNodes();//找到当前分支扫描到的最大深度

					// Creating inner links
					if (this.showInnerLinks && node.depth == 0)
						//把一个节点加入“待重建连线”队列，并确保后续重建流程会被触发
						this.QueueNodeForLinkRebuild(node);

                    // Searching for sub-layouts
                    if (node.depth == 0)//从第一层节点开始扫描子布局
					{
                        var foundSubLayouts = node.ScanForSubLayouts(null);//让 node 去扫描并找出它下面的所有子布局
						if (foundSubLayouts.Length > 0)
                        {
                            bool isSubLayout = this.IsSubLayout;

                            foreach (var subLayout in foundSubLayouts)
                            {
                                if (!this.subLayouts.Contains(subLayout))
                                {
                                    subLayoutsCount++;
                                    this.subLayouts.Add(subLayout);
                                }
                                // Master layout is responsible for rebuilding and sorting links roots
                                if (!isSubLayout)
                                {
                                    if (!sortingDictionary.ContainsKey(subLayout.linksRoot.transform))
                                        sortingDictionary.Add(subLayout.linksRoot.transform, subLayoutsCount);
                                    else
                                    {
                                        // Found shared link root between 2 sub-layouts, creating a new links root
                                        Debug.LogWarning("Found shared links root between sub-layouts, fixing it...");
                                        GameObject linksRoot = new GameObject("LinksRoot(Sub-Layout)", typeof(LayoutElement), typeof(RadialLayoutLinksRoot));
                                        linksRoot.GetComponent<LayoutElement>().ignoreLayout = true;
                                        linksRoot.GetComponent<RadialLayoutLinksRoot>().parentLayout = subLayout;
                                        linksRoot.transform.SetParent(this.transform);
                                        subLayout.linksRoot = linksRoot;
                                        sortingDictionary.Add(subLayout.linksRoot.transform, subLayoutsCount);
                                    }
                                }
                                if (!subLayout.initialized)
                                    subLayout.Initialize();
                                else
                                    subLayout.Rebuild();
                            }
                        }
                    }

                    if (maxDepth > this.MaxDepth)
                        this.MaxDepth = maxDepth;
                }
                else if (t.GetComponent<RadialLayoutNode>() != null)
                {
                    if(!Application.isPlaying)
                        GameObject.DestroyImmediate(t.GetComponent<RadialLayoutNode>());
                    else
                        GameObject.Destroy(t.GetComponent<RadialLayoutNode>());
                }
            }
			//主布局下把所有子布局的 linksRoot 都纳入排序字典
			if (!this.IsSubLayout)
            {
                var allSubLayouts = this.GetSubLayouts(true);
                foreach (var sublayout in allSubLayouts)
                    if (!sortingDictionary.ContainsKey(sublayout.linksRoot.transform))
                        sortingDictionary.Add(sublayout.linksRoot.transform, subLayoutsCount++);
            }

            // Resorting links roots and inner nodes
            foreach (var obj in sortingDictionary)
            {
                if (obj.Value != -1)
                {
                    if (!this.IsSubLayout && obj.Key.gameObject.GetComponent<RadialLayoutLinksRoot>() != null)
                    {
                        obj.Key.SetParent(this.transform);
                        obj.Key.transform.position = obj.Key.gameObject.GetComponent<RadialLayoutLinksRoot>().parentLayout.transform.position;
                        obj.Key.transform.localScale = Vector3.one;
                    }
                    obj.Key.SetSiblingIndex(obj.Value);
                }
            }
            foreach (var obj in sortingDictionary)
            {
                if (obj.Value == -1)
                {
                    if (obj.Key.gameObject == this.innerNode.gameObject)
                    {
                        if(!this.IsSubLayout)
                            obj.Key.SetSiblingIndex(subLayoutsCount + 1);
                        else
							obj.Key.SetSiblingIndex(0);
					}
				}
            }

            // Placing on circle
            foreach (var node in this.Nodes)
            {
                //调用node里的方法
                node.PlaceOnCircle();

                // Scaling
                if (!this.IsSubLayout)
                {
                    if (this.scaleNodeWithRadius && this.circleRadius >= 0)
                    {
                        if (node.depth == 0)
                        {
                            float scaleFactor = this.circleRadius / this.nodeScaleFactor;
                            node.transform.localScale = Vector3.one * Mathf.Clamp(scaleFactor, this.nodeScale_min, this.nodeScale_max);
                        }
                        else
                        {
                            // Subnodes will have the scale of the parent node
                            node.transform.localScale = Vector3.one;
                        }
                    }
                    else
                        node.transform.localScale = Vector3.one;
                }
                else
                {
                    if (node.depth == 0)
                    {
                        float scaleFactor = 1;
                        if (this.scaleNodeWithRadius)
                        {
                            // Overriding master scale
                            scaleFactor = this.circleRadius / this.nodeScaleFactor;
                        }
                        else
                        {
                            // Using master scale
                            node.transform.localScale = Vector3.one;
                        }

                        node.transform.localScale = Vector3.one * Mathf.Clamp(scaleFactor, this.nodeScale_min, this.nodeScale_max);
                    }
                    else
                    {
                        // Subnodes will have the scale of the parent node
                        node.transform.localScale = Vector3.one;
                    }
                }

            }

            // Applying customization
            this.ShowInnerNode(this.showInnerNode);

#if UNITY_EDITOR
            // Links rebuild (called here to mitigate editor frame throttling effect
            this.RebuildLinks(initialized);
#endif

            this.onRebuild?.Invoke(this);

        }
		//采用延迟方法，把节点放进 linksRebuildList，并把 linksRebuildFrameCounter=1
		//若计数到位就对队列节点逐个 node.CreateLink()
		//否则计数递减
		private void RebuildLinks(bool deferred)
        {
#if UNITY_EDITOR
#if UNITY_2021_3_OR_NEWER
            // Links are not created in prefab staging scene
            if (UnityEditor.SceneManagement.PrefabStageUtility.GetCurrentPrefabStage() != null)
                return;
#else
            // Links are not created in prefab staging scene
            if (UnityEditor.Experimental.SceneManagement.PrefabStageUtility.GetCurrentPrefabStage() != null)
                return;
#endif
#endif

            if (this.linksRebuildList != null)
            {
                if (this.linksRebuildFrameCounter == 0 || !deferred)
                {
                    foreach (var node in this.linksRebuildList)
                        node.CreateLink();

                    this.linksRebuildList = null;
                    this.linksRebuildFrameCounter = 0;
                }
                else
                {
                    this.linksRebuildFrameCounter--;
                }
            }
        }

        /// <summary>
        /// Gets the circle radius multiplier (multiplier of base Circle Radius) for the specified depth.
        /// </summary>
        /// <param name="depth">Desired depth.</param>
        /// <returns></returns>
        public float GetNodesRadiusMultiplier(int depth)
        {
            if (depth == 0 || !this.enableSeparateExternalRadii)
                return 1;
            else
            {
                if (depth - 1 < this.externalCircleRadiusMultiplier.Length)
                    return this.externalCircleRadiusMultiplier[depth - 1];
                else
                {
                    // Auto adjusting array size
                    if (depth - 1 == this.externalCircleRadiusMultiplier.Length)
                    {
                        List<float> temp = new List<float>(this.externalCircleRadiusMultiplier);
                        if(temp.Count >= 1 && temp[temp.Count-1] != 1)
							temp.Add(temp[temp.Count - 1]);
						else
                            temp.Add(1);
                        this.externalCircleRadiusMultiplier = temp.ToArray();
                    }

                    return 1;
                }
            }
        }

        /// <summary>
        /// Sets the parent layout for sub-layouts.
        /// </summary>
        public void SetParentLayout(RadialLayout layout)
        {
            this.ParentLayout = layout;
        }

        /// <summary>
        /// Queue the node for link rebuild in the next frame.
        /// </summary>
        public void QueueNodeForLinkRebuild(RadialLayoutNode node)
        {
            if (this.linksRebuildList == null)
                this.linksRebuildList = new List<RadialLayoutNode>();

            if(!this.linksRebuildList.Contains(node))
                this.linksRebuildList.Add(node);
			//把“重建倒计时/帧计数”设置为 1，表示很快（下一帧或近期）要执行重建逻辑
			this.linksRebuildFrameCounter = 1;
			if (!this.enabled)
                this.enabled = true;
        }
        

        /// <summary>
        /// Destroys this layout and every child node
        /// </summary>
		public void Destroy()
		{
            DestroyGameObject(this.linksRoot.gameObject);
            DestroyGameObject(this.gameObject);
        }

#endregion

#region Queries and Search
        //计算某一层深度的几节点数
        public int CountNodesOfDepth(int depth)
        {
            int c = 0;

            foreach (Transform t in this.transform)
                if (t.GetComponent<RadialLayoutNode>() != null && t.GetComponent<RadialLayoutNode>().depth == depth)
                    c++;
            return c;
        }


		/// <summary>
		/// Returns he specified child node by child index (Counting only child nodes).
		/// 根据传入的childIndex找到对应的节点
		/// </summary>
		/// <param name="childIndex">Index of the node to be returned, counting only nodes objects in the Transform.</param>
		/// <returns></returns>
		public RadialLayoutNode GetNodeByChildIndex(int childIndex)
        {
            int k = 0;
            foreach (Transform t in this.transform)
            {
                var node = t.GetComponent<RadialLayoutNode>();
                if (node != null)
                {
                    if (k == childIndex)
                    {
                        return node;
                    }
                    k++;
                }
            }

            return null;
        }

		/// <summary>
		/// Returns the list of all sub-layouts.
		/// </summary>
		/// <param name="recursive">TRUE: searches in child sub-layouts also. FALSE: returns only local sub-layouts.</param>
		/// recursive递归，是的话就递归找到所有子布局，否的话找到当前这一层的所有子布局
		public RadialLayout[] GetSubLayouts(bool recursive)
        {
            List<RadialLayout> subLayouts = new List<RadialLayout>(this.subLayouts);
            if (recursive)
            {
                for(int k=0;k<this.subLayouts.Count;k++)
                    subLayouts.AddRange(this.subLayouts[k].GetSubLayouts(true));
            }

            return subLayouts.ToArray();
        }

        /// <summary>
        /// Returns the master (root) layout. Returns itself if this layout is the root.
        /// </summary>
        //找到父节点
        public RadialLayout GetMasterLayout()
        {
            RadialLayout master = this;

            while (master.ParentLayout != null)
                master = master.ParentLayout;

            return master;
        }

        /// <summary>
        /// Returns the first parent layout found, moving from this to toward the root.
        /// </summary>
        public RadialLayout GetParentLayout()
        {
            RadialLayout parent = this;

            while (parent.ParentLayout != null)
                parent = parent.ParentLayout;

            return parent;
        }

#endregion

#region Node Management

        /// <summary>
        /// Adds a new node as child of this layout.
        /// </summary>
        /// <param name="name">Name of the node, default name NODE_## will be used if left blank.</param>
        /// <param name="parentNode">Node to be used as parent, if NULL the layout will be used.</param>
        public RadialLayoutNode AddNode(string name = "",RadialLayoutNode parentNode = null)
        {
            GameObject newNode = null;
            //确定挂载目标，有父节点挂父节点，没有挂当前节点
            Transform targetTransform = parentNode == null ? this.transform : parentNode.transform;

			//如果prefab_node即预制体为空，那么就创建一个空的节点对象
			if (this.prefab_node == null)
            {
                // No prefab specified, creating a new RadialLayoutNode GameObject
                if (string.IsNullOrEmpty(name))
                {
                    //找到当前节点的同一层的节点数
                    // Finding first free number
                    int freeNumber = parentNode == null ? this.CountNodesOfDepth(0) : parentNode.GetSiblingsCount();
                    List<int> numbersFound = new List<int>();
                    foreach (Transform t in targetTransform)
                    {
                        if (t.GetComponent<RadialLayoutNode>() != null)
                        {
                            if (t.name.Contains("_"))
                            {
                                string[] splitName = t.name.Split('_');
                                if (splitName.Length == 2 && int.TryParse(splitName[1], out int parsedNumber))
                                    if (!numbersFound.Contains(parsedNumber))
                                        numbersFound.Add(parsedNumber);
                            }
                        }
                    }
                    for (int k = 0; k < freeNumber; k++)
                        if (!numbersFound.Contains(k))
                        {
                            freeNumber = k;
                            break;
                        }
                    //通过找到现有的节点的名字，用一个循环来找到最小的没用过的数字作为当前节点的名字
                    name = "Node_" + freeNumber.ToString();
                }
                newNode = new GameObject(name, typeof(RadialLayoutNode), typeof(RectTransform));

#if UNITY_EDITOR
				//把当前节点纳入Undo系统，即可撤销系统
				//第一个参数是纳入撤销记录的对象；第二个参数：这次操作在 Undo 菜单里的描述文本
				if (newNode != null)
                    UnityEditor.Undo.RegisterCreatedObjectUndo(newNode.gameObject, $"Add Node to Layout");
#endif
            }
            else
            {
                // Using the specified RadialLayoutNode
#if UNITY_EDITOR
                //判断生成的预制体节点是要和原预制体相关
                if(this.keepPrefabLinking)
                    newNode = (GameObject)UnityEditor.PrefabUtility.InstantiatePrefab(this.prefab_node.gameObject);
                else
                    newNode = GameObject.Instantiate(this.prefab_node.gameObject);

                if (newNode != null)
					UnityEditor.Undo.RegisterCreatedObjectUndo(newNode.gameObject, $"Add Node to Layout");
#else
                newNode = GameObject.Instantiate(this.prefab_node.gameObject);
#endif

                if(!string.IsNullOrEmpty(name))
                    newNode.name = name;

            }

            newNode.transform.SetParent(targetTransform);
            newNode.transform.localPosition = Vector3.zero;
            if(this.IsWorldSpace)
                newNode.transform.localRotation = Quaternion.identity;

            this.Rebuild();

            // Query system: adding target component if enabled
            if (this.UseQuerySystem)
            {
				//给新节点补上RadialLayoutQueryTarget组件
				if (newNode.GetComponent<RadialLayoutQueryTarget>() == null)
                    newNode.AddComponent<RadialLayoutQueryTarget>();
                RadialLayoutQueryManager.AssignUniqueIdentifiers(this.GetMasterLayout());
            }

            if (newNode != null)
                return newNode.GetComponent<RadialLayoutNode>();
            else
                return null;
        }

		/// <summary>
		/// Adds a new node as child of this layout using a specific prefab template.
		/// </summary>
		/// <param name="nodePrefab">Specific prefab to use as node template.</param>
		/// <param name="name">Name of the node, default name NODE_## will be used if left blank.</param>
		/// <param name="parentNode">Node to be used as parent, if NULL the layout will be used.</param>
		public RadialLayoutNode AddNodeFromPrefab(RadialLayoutNode nodePrefab,string name = "",RadialLayoutNode parentNode = null )
        {
			GameObject newNode = null;
			Transform targetTransform = parentNode == null ? this.transform : parentNode.transform;

			// Using the specified RadialLayoutNode
#if UNITY_EDITOR
			if (this.keepPrefabLinking)
				newNode = (GameObject)UnityEditor.PrefabUtility.InstantiatePrefab(nodePrefab.gameObject);
			else
				newNode = GameObject.Instantiate(nodePrefab.gameObject);

			if (newNode != null)
				UnityEditor.Undo.RegisterCreatedObjectUndo(newNode.gameObject, $"Add Node to Layout");
#else
                newNode = GameObject.Instantiate(nodePrefab.gameObject);
#endif

			if (!string.IsNullOrEmpty(name))
				newNode.name = name;

		    newNode.transform.SetParent(targetTransform);
			newNode.transform.localPosition = Vector3.zero;
			if (this.IsWorldSpace)
				newNode.transform.localRotation = Quaternion.identity;


			this.Rebuild();

            // Query system: adding target component if enabled
            if (this.UseQuerySystem)
			{
			    if (newNode.GetComponent<RadialLayoutQueryTarget>() == null)
				    newNode.AddComponent<RadialLayoutQueryTarget>();
			    RadialLayoutQueryManager.AssignUniqueIdentifiers(this.GetMasterLayout());
		    } 

            if (newNode != null)
                return newNode.GetComponent<RadialLayoutNode>();
            else
                return null;
        }

		/// <summary>
		/// Deletes the specified child node by child index (Counting only child nodes).
		/// </summary>
		/// <param name="childIndex">Index of the node to be deleted, counting only nodes objects in the Transform.</param>
        //通过节点序号删除节点，第二个参数，是否递归删除子节点
		public void DeleteNode(int childIndex,bool destroyChildren)
        {
            this.DeleteNode(this.GetNodeByChildIndex(childIndex),destroyChildren);
        }

        /// <summary>
        /// Deletes the specified node.
        /// </summary>
        /// <param name="node">Node to be deleted</param>
        /// <param name="destroyChildren">If true, child nodes of the target nodes will be destroyed recursively. Will throw an exception if false and target node has children.</param>
        public void DeleteNode(RadialLayoutNode node,bool destroyChildren)
        {
            if (node == null)
            {
                Debug.LogError("Trying do delete a NULL node.");
                return;
            }
            
            foreach (var registeredNode in this.Nodes)
            {
				//判断当前节点是否是属于本布局已登记节点
				if (registeredNode == node)
                {
                    //遍历当前节点的子节点，有子节点且true，递归删除子节点
                    // Checking for children nodes
                    foreach (Transform t in node.transform)
                    {
                        var childNode = t.GetComponent<RadialLayoutNode>();
                        if (childNode != null)
                        {
                            if (destroyChildren)
                                DeleteNode(childNode, true);
                            else
                                throw new Exceptions.DeleteNodeException(node, "Node contains child nodes and cannot be deleted. Use destroyChilderns = true if tyou want to force deletion.");
                        }
                    }
                    //存在则把节点移除
                    this.Nodes.Remove(node);

					//如果当前节点是子布局的父节点。那么把linksRoot对象（即子布局节点之前的连线）全部删除
					// If sub-layout deleting the links root
					if (node.IsSubLayout)
                        DestroyGameObject(node.GetSubLayout().linksRoot.gameObject);
                    //移除当前节点的强连的线
                    node.NotifyDestroy();

                    //子节点、连钱、强连线都删除了，最后删除节点
                    DestroyGameObject(node.gameObject,!destroyChildren);

                    break;
                }
            }
        }

		/// <summary>
		/// Deletes all node and clears the layout
		/// 倒序遍历 this.Nodes
		/// 对每个节点调用 DeleteNode(node, true)（强制递归删除）
		/// 最后 Rebuild() 重建一次干净布局状态
		/// </summary>
		public void Clear()
        {
            if (this.Nodes != null)
            {
                for (int k = this.Nodes.Count - 1; k >= 0; k--)
                    this.DeleteNode(this.Nodes[k], true);
            }

            this.Rebuild();
        }

#endregion

#region Customization

        /// <summary>
        /// Shows/Hides the graphical Inner Node of this layout.
        /// </summary>
        public void ShowInnerNode(bool show)
        {
            this.innerNode.SetActive(show);
            if (show)
                this.innerNode.transform.localScale = Vector3.one * this.innerNodeScale;
        }

		#endregion

		#region Query System

		/*enabled = true：启用查询能力-> 给布局和所有节点加 RadialLayoutQueryTarget，再分配唯一ID
        enabled = false：禁用查询能力-> 把这些 RadialLayoutQueryTarget 全部移除*/

		private void SetupQuerySystem(bool enabled)
        {
            this._useQuerySystem = enabled;

            if (enabled)
            {
				// Adding RadialLayoutQueryTarget to self
				//给主布局挂RadialLayoutQueryTarget
				if (!this.IsSubLayout && this.GetComponent<RadialLayoutQueryTarget>() == null)
                    this.gameObject.AddComponent<RadialLayoutQueryTarget>();

				// 给当前布局的节点挂 QueryTarget
				// Adding RadialLayoutQueryTarget to each node and assigning an ID
				foreach (var node in this.Nodes)
                {
                    if (node.GetComponent<RadialLayoutQueryTarget>() == null)
                    {
                        var qt = node.gameObject.AddComponent<RadialLayoutQueryTarget>();
                    }
                }
				//给所有子布局节点也挂上
				foreach (var sublayout in this.GetSubLayouts(true))
                {
                    foreach (var node in sublayout.Nodes)
                    {
                        if (node.GetComponent<RadialLayoutQueryTarget>() == null)
                        {
                            var qt = node.gameObject.AddComponent<RadialLayoutQueryTarget>();
                        }
                    }
                }
				//对这棵布局树（当前 + 子布局）分配/刷新唯一 ID
				RadialLayoutQueryManager.AssignUniqueIdentifiers(this);
            }
            else
            {
                // Removing RadialLayoutQueryTarget from self
                if (!this.IsSubLayout && this.GetComponent<RadialLayoutQueryTarget>() != null)
                {
#if UNITY_EDITOR
                    if (Application.isPlaying)
                        Destroy(this.GetComponent<RadialLayoutQueryTarget>());
                    else
                        DestroyImmediate(this.GetComponent<RadialLayoutQueryTarget>());
#else
                        Destroy(this.GetComponent<RadialLayoutQueryTarget>());
#endif
                }

                // Removing RadialLayoutQueryTarget components from nodes
                foreach (var node in this.Nodes)
                    if (node.GetComponent<RadialLayoutQueryTarget>() != null)
                    {
#if UNITY_EDITOR
                        if (Application.isPlaying)
                            Destroy(node.GetComponent<RadialLayoutQueryTarget>());
                        else
                            DestroyImmediate(node.GetComponent<RadialLayoutQueryTarget>());
#else
                        Destroy(node.GetComponent<RadialLayoutQueryTarget>());
#endif
                    }

                foreach (var sublayout in this.GetSubLayouts(true))
                {
                    foreach (var node in sublayout.Nodes)
                    {
                        if (node.GetComponent<RadialLayoutQueryTarget>() != null)
                        {
#if UNITY_EDITOR
                            if (Application.isPlaying)
                                Destroy(node.GetComponent<RadialLayoutQueryTarget>());
                            else
                                DestroyImmediate(node.GetComponent<RadialLayoutQueryTarget>());
#else
                            Destroy(node.GetComponent<RadialLayoutQueryTarget>());
#endif
                        }
                    }
                }
            }

        }

        #endregion

        #region Helper Functions

        /// <summary>
        /// Wrapper function to handle GameObject deletion in Editor vs. Play mode.
        /// </summary>
        /// <param name="gameObject"></param>
        /// <param name="undoable"></param>
        public static void DestroyGameObject(GameObject gameObject, bool undoable = false)
        {
#if UNITY_EDITOR
            //判断在编辑状态下是否点击了开始（play），即是否正在运行
            if (!Application.isPlaying)
            {
                if (!undoable)
                    //立即删除，不能撤销
                    GameObject.DestroyImmediate(gameObject);
                else
                    UnityEditor.Undo.DestroyObjectImmediate(gameObject);
            }
            else
            {
				//用运行时安全方式删除（延迟到帧末）
				GameObject.Destroy(gameObject);
            }
#else
            GameObject.Destroy(gameObject);
#endif
        }

        #endregion

        #region Misc
        /// <summary>
        /// Resets update parameters to the current state so, a refresh will not be triggered next frame.
        /// </summary>
        public void ResetUpdateParameters()
        {
            this.lastChildCount = this.transform.childCount;
            this.lastCircleRadius = this.circleRadius;
            this.lastCircleSlice = this.circleSlice;
            this.lastRotationOffset = this.rotationOffset;
            this.lastNodeScaleFactor = this.nodeScaleFactor;
            this.lastScaleNodeWithRadius = this.scaleNodeWithRadius;
            this.lastNodeScale_min = this.nodeScale_min;
            this.lastNodeScale_max = this.nodeScale_max;
            this.lastEnableSeparateExternalRadii = this.enableSeparateExternalRadii;
            this.lastNodesDistribution = this.nodesDistribution;
            this.lastFanDistributionCommonSpan = this.fanDistributionCommonSpan;
            this.lastsBranchesCommonLenght = this.branchesCommonLength;
            this.lastShowInnerNode = this.showInnerNode;
            this.lastInnerNodeScale = this.innerNodeScale;
            this.lastShowInnerLinks = this.showInnerLinks;
            this.lastRelativeRotation = this.relativeRotationMode;
        }


#if UNITY_EDITOR
        private void OnUndoPerformed()
        {
            if (this.autoRebuildMode != AutoRebuildMode.Never)
                this.Rebuild();
        }
#endif

#endregion
    }
}

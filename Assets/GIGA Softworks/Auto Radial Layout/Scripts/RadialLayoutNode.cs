using System.Collections;
using System.Collections.Generic;
using Unity.Collections;
#if UNITY_EDITOR
using UnityEditor;
#endif
using UnityEngine;
using UnityEngine.UI;
using static GIGA.AutoRadialLayout.RadialLayout;

namespace GIGA.AutoRadialLayout
{

    [ExecuteInEditMode]
    public class RadialLayoutNode : MonoBehaviour
    {
        public RadialLayout Layout { get; private set; }
        public RadialLayoutNode ParentNode { get; private set; }
        [ReadOnly]
        public int depth;
        private int lastChildCount = 0;
        
        // Offsets, etc..
        public float distanceOffset = 0;//径向距离微调
        private float fanSpan = 45;
        public float fanSpanOverride = 90;
        public float angleOffset = 0;//角度微调
		public float fanOffset = 0;//对子节点扇面整体偏移
		public float branchLength;             // Used in "branches" distribution
        public float nodeRadius;               // Used to tune links fill effect to real %

        private float lastFanSpan,lastFanSpanOverride, lastAngleOffset,lastDistanceOffset,lastFanOffset,lastBranchLength;
        private bool lastOverrideFanSpan, lastOverrideBranchLength;

        // Flags
        public bool overrideFanSpan;//覆盖默认扇面角
		public bool overrideBranchLength;//覆盖分支长度
#if UNITY_EDITOR
		public bool showNodeRadiusGizmo;
#endif

        // Getters
        public bool IgnoreLayout { get { return this.GetComponent<LayoutElement>() != null && this.GetComponent<LayoutElement>().ignoreLayout; } }
        public bool IsSubLayout {
            get {
                foreach (Transform t in this.transform)
                    if (t.GetComponent<RadialLayout>() != null)
                        return true;
                return false;
                    } }
        /// <summary>
        /// Returns true if this node has children nodes associated.
        /// </summary>
        public bool HasChildren { get {
                if (this.Layout != null)
                {
                    foreach (var n in this.Layout.Nodes)
                        if (n.ParentNode == this)
                            return true;
                }
                return false;
            } }
        public bool IsMergingNode { get { return this.GetComponent<RadialLayoutMergingNode>() != null; } }

        /// <summary>
        /// The span in degree of the branches (links) "fan" generating from this node.
        /// </summary>
        public float FanSpan { get {
                if (this.overrideFanSpan || this.Layout != null && this.Layout.nodesDistribution == RadialLayout.NodesDistribution.Branches)
                {
                    if (this.overrideFanSpan)
                        return this.fanSpanOverride;
                    else
                    {
                        return this.Layout.fanDistributionCommonSpan;
                    }
                }
                else
                    return this.fanSpan;
            } }

        /// <summary>
        /// The lenght of the branches (links) generating from this node.
        /// </summary>
        public float BranchLength
        {
            get
            {
                if (this.Layout.nodesDistribution == RadialLayout.NodesDistribution.Branches)
                {
                    if (this.overrideBranchLength)
                        return this.branchLength + this.Layout.circleRadius;
                    else
                        return this.Layout.circleRadius * this.Layout.branchesCommonLength;
                }
                else
                    return 0;
            }
        }

		/// <summary>
		/// The link that starts from this node.
		/// </summary>
		//从我出发的连线列表
		public List<RadialLayoutLink> DepartingLinks { get; private set; }

		/// <summary>
		/// The link that is going into this node.
		/// </summary>
		//到达我的主连线
		public RadialLayoutLink ArrivingLink { get; private set; }
		//合流节点（Merging Node）的额外连线
		private List<RadialLayoutLink> mergingLinks;    // Reference to the links created by a merging node

		private void OnEnable()
		{
			if(this.Layout == null)
                this.Layout = this.GetComponentInParent<RadialLayout>();
            this.ResetUpdateParameters();

#if UNITY_EDITOR
            Undo.undoRedoPerformed += Update;
#endif
        }

		private void OnDisable()
		{
#if UNITY_EDITOR
			Undo.undoRedoPerformed -= Update;
#endif
        }

		void Update()
        {
            if (this.Layout != null && (!Application.isPlaying || this.Layout.autoRebuildMode == RadialLayout.AutoRebuildMode.Always && this.Layout.enabled))
            {
                if (this.transform.childCount != this.lastChildCount ||
                    this.fanSpan != lastFanSpan ||
                    this.fanSpanOverride != lastFanSpanOverride ||
                    this.angleOffset != this.lastAngleOffset ||
                    this.distanceOffset != this.lastDistanceOffset ||
                    this.overrideFanSpan != this.lastOverrideFanSpan ||
                    this.branchLength != this.lastBranchLength ||
                    this.overrideBranchLength != this.lastOverrideBranchLength ||
                    this.fanOffset != this.lastFanOffset
                    )
                {
                    if (this.Layout == null)
                        this.Layout = this.GetComponentInParent<RadialLayout>();
                    this.Layout.Rebuild();
                    this.ResetUpdateParameters();//重置所有参数
                }
            }

#if UNITY_EDITOR
            if (this.Layout != null && this.Layout.GetMasterLayout().forceNodesSelection && Selection.activeGameObject != null && Selection.activeGameObject != this.gameObject && Selection.activeGameObject.transform.IsChildOf(this.transform) && Selection.activeGameObject.GetComponent<RadialLayout>() == null && Selection.activeGameObject.GetComponent<RadialLayoutNode>() == null && Selection.activeGameObject.GetComponentInParent<RadialLayoutNode>() != null) 
            {
                GameObject target = Selection.activeGameObject.GetComponentInParent<RadialLayoutNode>().gameObject; 

                if ((RadialLayout.forceSelectionTargetedObject == null || RadialLayout.forceSelectionTargetedObject != target) &&(RadialLayout.forceSelectionSkippedObject == null || RadialLayout.forceSelectionSkippedObject != Selection.activeGameObject))
                {
                    if(Selection.activeGameObject.GetComponent<RadialLayout>() == null && Selection.activeGameObject.GetComponent<RadialLayoutNode>() == null)
                        RadialLayout.forceSelectionSkippedObject = Selection.activeGameObject;
                    Selection.activeGameObject = Selection.activeGameObject.GetComponentInParent<RadialLayoutNode>().gameObject;
                    RadialLayout.forceSelectionTargetedObject = target;
                }
            }
#endif
        }

        #region Layout building

        /// <summary>
        /// Sets the parent layout.
        /// </summary>
        public void SetParent(RadialLayout layout)
        {
            this.Layout = layout;
        }

        /// <summary>
        /// Sets the parent node.
        /// </summary>
        public void SetParent(RadialLayoutNode parent)
        {
            this.ParentNode = parent;
        }

        /// <summary>
        /// Scans for subnodes and add them to the layout.
        /// </summary>
        /// <returns>Max depth found.</returns>
        public int ScanForSubNodes()
        {
            int maxDepth = Layout.MaxDepth;//找到当前页面的最大深度做初始值
            foreach (Transform t in this.transform)//遍历当前节点的所有子transform
            {
                RadialLayoutNode node = t.gameObject.GetComponent<RadialLayoutNode>();
                if (node != null && !node.IgnoreLayout)//判断是否是有效节点
                {
                    if(!this.Layout.Nodes.Contains(node))
                        this.Layout.Nodes.Add(node);
                    node.SetParent(this.Layout);
                    node.SetParent(this);
                    node.depth = this.depth + 1;
                    if (node.depth > maxDepth)
                        maxDepth = node.depth;
                    this.Layout.QueueNodeForLinkRebuild(node); //把节点放入重建队列里
                    int subDepth = node.ScanForSubNodes();
                    if (subDepth > maxDepth)
                        maxDepth = subDepth;
                }
                else if (t.GetComponent<RadialLayoutNode>() != null)
                    GameObject.DestroyImmediate(t.GetComponent<RadialLayoutNode>());
            }

            return maxDepth;
        }

        /// <summary>
        /// Finds all sub-layouts starting from this node (included)
        /// </summary>
        /// <returns></returns>
        //递归找到我以及我的后代节点里的所有子布局
        public RadialLayout[] ScanForSubLayouts(List<RadialLayout> excludeList)
        {
            List<RadialLayout> subLayouts = new List<RadialLayout>();

			var subLayout = this.GetSubLayout();//找到当前的子布局或者说当前节点下是否挂载了一个SubLayout
			//excludeList代表排除名单
			if (subLayout != null && !subLayouts.Contains(subLayout) && (excludeList == null || !excludeList.Contains(subLayout)))
			{
				subLayouts.Add(subLayout);

				if (subLayout.ParentLayout == null)
				{
					//如果 sub-layout 还没设 ParentLayout，会向上找真正所属父布局并绑定
					foreach (var layout in this.GetComponentsInParent<RadialLayout>())
                    {
						//哪个布局实际管理了“这个节点”，就把它设为当前 sub-layout 的父布局
						if (layout.Nodes.Contains(this))
                        {
                            subLayout.SetParentLayout(layout);
                            break;
                        }
                    }
				}
			}

			foreach (Transform t in this.transform)
            {
				
				RadialLayoutNode node = t.gameObject.GetComponent<RadialLayoutNode>();
                if (node != null)
                {
					//把一个集合里的所有元素，批量追加到 subLayouts 这个 List<RadialLayout> 末尾。
					//对每个子节点递归调用 ScanForSubLayouts(...)
					subLayouts.AddRange(node.ScanForSubLayouts(null));
                }
            }

            return subLayouts.ToArray();
        }

		/// <summary>
		/// Creates links terminating in this node.
		/// This function is automatically called on Layout.Rebuild().
		/// </summary>
		//linksRoot 是连线容器，所有查找/重建都围绕它进行。
		//linksRebuildMode 决定“强制重建”还是“尽量复用”。
        public void CreateLink()
        {
            //当前节点有归属的布局且连线预制体不为空
            if (this.Layout != null && this.Layout.prefab_link != null)
            {
                RadialLayoutLink newLink = null;
                //判断有父节点的情况
				if (this.ParentNode != null && this != null)
                {
                    // Checking if already existing
                    for (int k = this.Layout.linksRoot.transform.childCount - 1; k >= 0; k--)
                    {
						//如果已有连线，根据重建模式选择销毁还是保留
						//拿到当前布局连线容器linksRoot的 Transform并取第k个子物体，取该子物体上的连线脚本组件
						RadialLayoutLink link = this.Layout.linksRoot.transform.GetChild(k).GetComponent<RadialLayoutLink>();
                       //找到一条线，从父节点发出且到当前节点
                        if (link != null && link.from == this.ParentNode && link.to == this)
                        {
                            if(this.ParentNode.DepartingLinks != null)
                                //移除旧引用
							    this.ParentNode.DepartingLinks.Remove(link);
							//清空当前节点 ArrivingLink
							this.ArrivingLink = null;
							//Regenerate：删掉旧线对象
							//KeepExisting：复用旧线（newLink = link）
                            if (this.Layout.GetMasterLayout().linksRebuildMode == RadialLayout.LinksRebuildMode.Regenerate)
                                RadialLayout.DestroyGameObject(link.gameObject);
                            else
                                newLink = link;
                        }
                    }
                    //清理当前节点的父节点里引用为空的项
                    // Clearing all missing links
                    if (this.ParentNode.DepartingLinks != null)
                    {
                        for (int k = this.ParentNode.DepartingLinks.Count - 1; k >= 0; k--)
                            if (this.ParentNode.DepartingLinks[k] == null)
                                this.ParentNode.DepartingLinks.RemoveAt(k);
                    }
					//没有就实例化 prefab_link
					//newLink.Set(parent, this) 并登记：
                    //当前节点的 ArrivingLink
                    //父节点的 DepartingLinks

					if (newLink == null)
                        newLink = GameObject.Instantiate(this.Layout.prefab_link, this.Layout.linksRoot.transform);
                    newLink.Set(this.ParentNode, this);//从哪里来，到哪里去
                    newLink.root = this.Layout.linksRoot.GetComponent<RadialLayoutLinksRoot>();
                    this.ArrivingLink = newLink;
                    if (this.ParentNode.DepartingLinks == null)
                        this.ParentNode.DepartingLinks = new List<RadialLayoutLink>();
                    if(!this.ParentNode.DepartingLinks.Contains(newLink))
                        this.ParentNode.DepartingLinks.Add(newLink);
                }
                //没有父节点，意味着是中心--节点的连线
                else if (this.depth == 0 && this.Layout.showInnerLinks)
                {
					RadialLayoutNode subLayoutNode = null;
					if (this.Layout.IsSubLayout)
                        //判断是否是子布局，是的话找到当前布局的父节点
						subLayoutNode = this.Layout.GetComponentInParent<RadialLayoutNode>();

					// Checking if already existing
					for (int k = this.Layout.linksRoot.transform.childCount - 1; k >= 0; k--)
                    {
                        RadialLayoutLink link = this.Layout.linksRoot.transform.GetChild(k).GetComponent<RadialLayoutLink>();
						//link.from == null && link.to == this这是判断从布局中心出发的内连接线标记
						if (link != null && link.from == null && link.to == this)
                        {
							this.ArrivingLink = null;
                            if (this.Layout.GetMasterLayout().linksRebuildMode == RadialLayout.LinksRebuildMode.Regenerate)
                            {
                                // removing from departing links of sub-layout nodes
                                if (subLayoutNode != null && subLayoutNode.DepartingLinks != null)
                                {
                                    if(subLayoutNode.DepartingLinks.Contains(newLink))
									    subLayoutNode.DepartingLinks.Remove(newLink);
								}

                                RadialLayout.DestroyGameObject(link.gameObject);
                            }
                            else
                                newLink = link;
                        }
					}

					// Clearing all missing links
					if (subLayoutNode != null && subLayoutNode.DepartingLinks != null)
					{
						for (int j = subLayoutNode.DepartingLinks.Count - 1; j >= 0; j--)
							if (subLayoutNode.DepartingLinks[j] == null)
								subLayoutNode.DepartingLinks.RemoveAt(j);
					}

					if (newLink == null)
                        newLink = GameObject.Instantiate(this.Layout.prefab_link, this.Layout.linksRoot.transform);
                    newLink.Set(this.Layout, this);
                    newLink.root = this.Layout.linksRoot.GetComponent<RadialLayoutLinksRoot>();
                    this.ArrivingLink = newLink;

                    // Adding departing links to sub-layout node
                    if (subLayoutNode != null)
                    {
                        if(subLayoutNode.DepartingLinks == null)
                            subLayoutNode.DepartingLinks = new List<RadialLayoutLink>();
                        if (newLink != null &&!subLayoutNode.DepartingLinks.Contains(newLink))
                            subLayoutNode.DepartingLinks.Add(newLink);
                    }
                }

                if (this.IsMergingNode)
                    this.CreateMergingLinks();
            }
        }

        private void CreateMergingLinks()
        {
			//当前节点属于某个布局
			//布局有连线 prefab
            //当前节点是合流节点（挂了 RadialLayoutMergingNode）
			if (this.Layout != null && this.Layout.prefab_link != null && this.IsMergingNode)
			{
				//一个节点可能挂载多个RadialLayoutMergingNode组件
				RadialLayoutMergingNode[] mergeNodes = this.GetComponents<RadialLayoutMergingNode>();

                foreach (var mergeNode in mergeNodes)
                {

                    foreach (var convergingNode in mergeNode.convergingNodes)
                    {
                        // Excluding default parent node
                        //父节点到当前节点的线已经正常建立，所以跳过
                        if (this.ParentNode != null && convergingNode == this.ParentNode)
                            continue;

                        RadialLayoutLink newLink = null;

                        if (this != null && convergingNode != null)
                        {
                            // Checking if already existing
                            for (int k = this.Layout.linksRoot.transform.childCount - 1; k >= 0; k--)
                            {
                                RadialLayoutLink link = this.Layout.linksRoot.transform.GetChild(k).GetComponent<RadialLayoutLink>();
                                if (link != null && link.from == convergingNode && link.to == this)
                                {
                                    if (convergingNode.DepartingLinks != null)
                                        convergingNode.DepartingLinks.Remove(link);
									if (this.mergingLinks != null)
										this.mergingLinks.Remove(link);
									if (this.Layout.GetMasterLayout().linksRebuildMode == RadialLayout.LinksRebuildMode.Regenerate)
                                        RadialLayout.DestroyGameObject(link.gameObject);
                                    else
                                        newLink = link;
                                }
                            }

                            // Clearing all missing links
                            if (convergingNode.DepartingLinks != null)
                            {
                                for (int k = convergingNode.DepartingLinks.Count - 1; k >= 0; k--)
                                    if (convergingNode.DepartingLinks[k] == null)
                                        convergingNode.DepartingLinks.RemoveAt(k);
                            }

                            if (newLink == null)
                                newLink = GameObject.Instantiate(this.Layout.prefab_link, this.Layout.linksRoot.transform);

                            newLink.Set(convergingNode, this);
                            newLink.root = this.Layout.linksRoot.GetComponent<RadialLayoutLinksRoot>();
                            if (this.mergingLinks == null)
                                this.mergingLinks = new List<RadialLayoutLink>();
                            if (!this.mergingLinks.Contains(newLink))
                                this.mergingLinks.Add(newLink);
							//写回关系与索引
							if (convergingNode.DepartingLinks == null)
                                convergingNode.DepartingLinks = new List<RadialLayoutLink>();
                            if (!convergingNode.DepartingLinks.Contains(newLink))
                                convergingNode.DepartingLinks.Add(newLink);
                        }
                    }
                }

				// Deleting links of nodes that are no longer converging
				//删除不需要的合流线
				if (this.mergingLinks != null)
                {
					//倒序遍历当前记录的 mergingLinks
					//检查每条线的 from 是否仍在任一 mergeNode.convergingNodes 里
					for (int k = this.mergingLinks.Count - 1; k >= 0; k--)
                    {
                        bool inList = false;
                        foreach (var mergeNode in this.GetComponents<RadialLayoutMergingNode>())
                            foreach (var convergingNode in mergeNode.convergingNodes)
                            {
                                if (this.mergingLinks[k].from == convergingNode)
                                {
                                    inList = true;
                                    break;
                                }
                            }

                        if (!inList)
                        {
							//如果不在了，说明这条线已过期：
							//从来源节点 from.DepartingLinks 移除
							if (mergingLinks[k] != null)
                            {
                                if (mergingLinks[k].from.DepartingLinks != null)
                                    mergingLinks[k].from.DepartingLinks.Remove(mergingLinks[k]);
								//销毁线对象
								RadialLayout.DestroyGameObject(this.mergingLinks[k].gameObject);
                            }
							//从 this.mergingLinks 移除
							this.mergingLinks.RemoveAt(k);
                        }
                    }
                }
			}
		}    

        /// <summary>
        /// Places this Node accordingly to the parent layout settings.
        /// This function is automatically called on Layout.Rebuild().
        /// </summary>
        public void PlaceOnCircle()
        {
			//最终要写入的局部 UI 坐标
			Vector2 positionOffset = Vector2.zero;
            RectTransform rt = this.GetComponent<RectTransform>();
			//当前节点在同层同级里的索引
			int siblingIndex = this.GetSiblingIndex();
			//同层节点数量（决定角度切分）
			int siblingsCount = this.GetSiblingsCount();
			//先刷新默认扇面角 fanSpan，供后续子节点分布使用
			this.fanSpan = this.CalculateDefaultFanSpan();
            this.lastFanSpan = this.fanSpan;

            // If at first depth, it is just the circle around the center
            if (this.depth == 0)
            {
				//先拿当前布局的旋转偏移
				float rotationOffset = this.Layout.rotationOffset;
				if (this.Layout.IsSubLayout)
				{
					// Applying relative rotation offset if needed
					//模式是“相对父级旋转”
					if (this.Layout.relativeRotationMode == RadialLayout.RelativeRotation.RelativeToParent)
					{
                        RadialLayout parent = this.Layout.ParentLayout;
                        while (parent != null)
                        {
							//这会让子布局在视觉上跟随父布局旋转累计
							rotationOffset += parent.rotationOffset;
                            parent = parent.ParentLayout;
                        }
                    }
                }
                float angle = 0;
                //分为整圆和扇形分布
                if (this.Layout.circleSlice == 360)
					//每个节点角间隔 2π / 节点数
					angle = (Mathf.PI * 2 / (float)this.Layout.CountNodesOfDepth(0)) * siblingIndex + (-rotationOffset) * Mathf.Deg2Rad + this.angleOffset * Mathf.Deg2Rad;
                else
                {
					//间隔 slice / (节点数-1)，让首尾贴在扇形边界再叠加-rotationOffset（布局整体旋转）再叠加+angleOffset（当前节点个体微调）
					angle = (this.Layout.circleSlice * Mathf.Deg2Rad / ((float)this.Layout.CountNodesOfDepth(0) -1)) * (siblingIndex ) + (-rotationOffset) * Mathf.Deg2Rad + this.angleOffset * Mathf.Deg2Rad;
                }
				//通过x\y转成坐标，distanceOffset 是当前节点径向增减量
				positionOffset = new Vector2(Mathf.Sin(angle) * (this.Layout.circleRadius + this.distanceOffset), Mathf.Cos(angle) * (this.Layout.circleRadius + this.distanceOffset));
				//子布局缩放修正,避免父节点缩放把子布局第一层位置放大/缩小过头
				if (this.Layout.IsSubLayout)
                    positionOffset /= this.Layout.GetComponentInParent<RadialLayoutNode>().transform.localScale.x;

            }
            else
            {
				//父节点指向外侧的径向方向
				Vector2 bisector = Vector2.zero;
                Vector3 bisector3D = Vector3.zero;
                switch (this.Layout.nodesDistribution)
                {
					//同心/环形层级分布
					case RadialLayout.NodesDistribution.Concentric:
                        //如果布局不是世界坐标那么先找基准方向
                        if (!this.Layout.IsWorldSpace)
                        { 
                            bisector = (this.ParentNode.GetComponent<RectTransform>().position - this.Layout.GetComponent<RectTransform>().position).normalized;
                        }
                        else
                        {
							// Clearing the rotation to use the same calculations of the unrotated layout
							//世界空间下会先把坐标乘 inverseRotation 去掉布局旋转，再算方向（防旋转干扰）
							//这条 bisector 可以理解为“父节点指向外侧的径向方向”
							Quaternion inverseRotation = Quaternion.Inverse(this.Layout.transform.rotation);
                            var clearParentPos = inverseRotation * this.ParentNode.GetComponent<RectTransform>().position;
                            var clearLayoutPos = inverseRotation * this.Layout.GetComponent<RectTransform>().position;
							bisector = (clearParentPos - clearLayoutPos).normalized;
                        }

                        // Placing nodes in the fan span if more than one sibling
                        //如果同一层有多个节点
                        if (siblingsCount > 1)
                        {
							//按照 ParentNode.FanSpan 把兄弟分布在父节点扇面中
							float angleSlice = this.ParentNode.FanSpan * Mathf.Deg2Rad / (float)siblingsCount;
							//偶数个：围绕中轴对称，没有正中那个节点
							if (siblingsCount % 2 == 0)
                            {
                                // even nodes, centering around the bisector
                                int centerIndex = siblingsCount / 2;
                                int offsetIndex = siblingIndex < centerIndex ? siblingIndex - centerIndex : siblingIndex - (centerIndex - 1);

                                bisector =  bisector.Rotate(angleSlice * offsetIndex - angleSlice * 0.5f * Mathf.Sign(offsetIndex));
                            }
							//奇数个：中间节点正好在中轴，其余左右展开
							else
							{
                                // odd nodes, centering the central node on the bisector
                                int centerIndex = siblingsCount / 2;
                                if (siblingIndex != centerIndex)
                                {
                                    int distanceFromCenterIndex = siblingIndex - centerIndex;
                                    bisector = bisector.Rotate(angleSlice * distanceFromCenterIndex);
                                }
                            }
                        }
						//当前节点自身旋转偏移
						if (this.angleOffset != 0)
                            bisector = bisector.Rotate(this.angleOffset * Mathf.Deg2Rad);
						//父节点对子扇面的整体偏移
						if (this.ParentNode != null && this.ParentNode.fanOffset != 0)
                            bisector = bisector.Rotate(this.ParentNode.fanOffset * Mathf.Deg2Rad);
                        //画布缩放
                        float canvasScale = !this.Layout.IsNestedCanvas ? this.Layout.Canvas.transform.localScale.x : this.Layout.Canvas.transform.lossyScale.x;
						//径向距离 =====bisector *基础层距*层倍率*画布缩放*祖先累积偏移*当前节点偏移
						positionOffset = bisector * (this.Layout.circleRadius * (this.depth + 1) * this.Layout.GetNodesRadiusMultiplier(this.depth) * canvasScale + this.GetInheritedDistanceOffset()  + this.distanceOffset * canvasScale);
						//确认父节点在世界空间的 X 缩放不为 0
						if (this.ParentNode.transform.lossyScale.x != 0)
                            positionOffset = this.ReconstructPositionOffset(positionOffset / this.ParentNode.transform.lossyScale.x);
                        else
							//如果父节点缩放为 0，直接给 (0,0) 防止除零
							positionOffset = Vector2.zero;
                        break;
						//分支扇形分布
						//基准方向 bisector                    
   					case RadialLayout.NodesDistribution.Branches:
                        bisector = Vector2.zero;

                        if (!this.Layout.IsWorldSpace)
                        {
                            if (this.ParentNode.ParentNode == null)								
								bisector = (this.ParentNode.GetComponent<RectTransform>().position - this.Layout.GetComponent<RectTransform>().position).normalized;
                            else
                                bisector = (this.ParentNode.GetComponent<RectTransform>().position - this.ParentNode.ParentNode.GetComponent<RectTransform>().position).normalized;
                        }
                        else
                        {
							// Clearing the rotation to use the same calculations of the unrotated layout
							Quaternion inverseRotation = Quaternion.Inverse(this.Layout.transform.rotation);
							var clearParentPos = inverseRotation * this.ParentNode.GetComponent<RectTransform>().position;

                            if (this.ParentNode.ParentNode == null)
                            {
								//若父节点是第一层：方向 = 父节点相对布局中心
								var clearLayoutPos = inverseRotation * this.Layout.GetComponent<RectTransform>().position;
								bisector = (clearParentPos - clearLayoutPos).normalized;
                            }
                            else
                            {
								//否则：方向 = 父节点相对祖父节点（沿“分支延伸方向”）
								var clearParent2Pos = inverseRotation * this.ParentNode.ParentNode.GetComponent<RectTransform>().position;
								bisector = (clearParentPos - clearParent2Pos).normalized;
                            }
						}

						// Placing nodes in the fan span if more than one sibling
						if (siblingsCount > 1)
                        {
							//也是 siblings>1 时按父节点 FanSpan 展开，奇偶分支各自处理。 （偶数分支这里用了从左边界开始加 angleSlice * siblingIndex 的写法）
							if (siblingsCount % 2 == 0)
                            {
                                // even nodes, centering around the bisector
                                float angleSlice = this.ParentNode.FanSpan * Mathf.Deg2Rad / ((float)siblingsCount -1);
                                int centerIndex = siblingsCount / 2;
                                int offsetIndex = siblingIndex < centerIndex ? siblingIndex - centerIndex : siblingIndex - centerIndex +1;

                                bisector = bisector.Rotate(-this.ParentNode.FanSpan * 0.5f * Mathf.Deg2Rad + angleSlice * siblingIndex /*- angleSlice * 0.5f * Mathf.Sign(offsetIndex)*/);
                            }
                            else
                            {
                                // odd nodes, centering the central node on the bisector
                                float angleSlice = this.ParentNode.FanSpan * Mathf.Deg2Rad / ((float)siblingsCount - 1);
                                int centerIndex = siblingsCount / 2;
                                if (siblingIndex != centerIndex)
                                {
                                    int distanceFromCenterIndex = siblingIndex - centerIndex;
                                    bisector = bisector.Rotate(angleSlice * distanceFromCenterIndex);
                                }
                            }
                        }
						//同样叠加 angleOffset 与 ParentNode.fanOffset
						if (this.angleOffset != 0)
                            bisector = bisector.Rotate(this.angleOffset * Mathf.Deg2Rad);
                        if (this.ParentNode != null && this.ParentNode.fanOffset != 0)
                            bisector = bisector.Rotate(this.ParentNode.fanOffset * Mathf.Deg2Rad);

                        float branchLength = this.ParentNode.BranchLength;
						//不再用 circleRadius*(depth+1)，而是用父节点定义的分支长度推进
						positionOffset = bisector * (branchLength * this.Layout.GetNodesRadiusMultiplier(this.depth) + this.distanceOffset);

                        break;
                }
            }
			//把前面算出的偏移真正应用到 UI 节点
			rt.anchoredPosition = positionOffset;
        }
        //计算默认的扇形分布
        public float CalculateDefaultFanSpan()
        {
            if (this.depth == 0)
            {
                return Mathf.Min(60, Mathf.Abs(360f / ((float)this.GetSiblingsCount())));

            }
            else
            {
                int parentSiblingsCount = this.ParentNode != null ? this.ParentNode.GetSiblingsCount() : 0;
                return Mathf.Min(60, Mathf.Abs((ParentNode.fanSpan / ((float)parentSiblingsCount - 4))));
            }
        }

        /// <summary>
        /// Clears all offsets and restores default node position.
        /// </summary>
        public void ClearOffsets()
        {
#if UNITY_EDITOR
            Undo.RecordObject(this, "Node clear offset");
#endif
            this.distanceOffset = 0;
            this.angleOffset = 0;
            this.overrideFanSpan = false;
            this.fanOffset = 0;
        }

        private Vector2 ReconstructPositionOffset(Vector2 fromPos)
        {
            Vector2 pos = fromPos;
            RadialLayoutNode parentNode = this.ParentNode;
            while (parentNode != null)
            {
                pos -= parentNode.GetComponent<RectTransform>().anchoredPosition / parentNode.transform.localScale.x;
                parentNode = parentNode.ParentNode;
                
            }

            return pos;// / this.transform.lossyScale.x;
        }

        //计算祖先累计偏移量
        private float GetInheritedDistanceOffset()
        {
            float offset = 0;
            var parentNode = this.ParentNode;
            while (parentNode != null)
            {
                offset += parentNode.distanceOffset;
                parentNode = parentNode.ParentNode;
            }
            return offset * (!this.Layout.IsNestedCanvas ? this.Layout.Canvas.transform.localScale.x : this.Layout.Canvas.transform.lossyScale.x);
        }

        #endregion

        #region Queries and Search

        /// <summary>
        /// Returns the number of siblings of this Node (nodes with same depth).
        /// </summary>
        //找到当前节点同层的节点且返回数量
        //同一布局同一深度
        public int GetSiblingsCount()
        {
            int c = 0;
            if (this.ParentNode != null)
            {
                foreach (var child in this.ParentNode.GetComponentsInChildren<RadialLayoutNode>())

                    if (child.Layout == this.Layout && child.depth == this.depth)
                        c++;
            }
            else
            {
                // Counting nodes of the main layout
                foreach (var node in this.Layout.Nodes)
                {
                    if (node.Layout == this.Layout && node.depth == 0)
                        c++;
                }
            }
            return c;
        }

		/// <summary>
		/// Returns the index of this node in the parent hierarchy.
		/// </summary>
		//可参与布局的同级节点序列”里的索引（从 0 开始）.同一个父节点下的子节点中的索引位置
		public int GetSiblingIndex()
        {
            int siblingIndex = 0;
            foreach (Transform t in this.transform.parent)
                if (t.gameObject.activeSelf && t.GetComponent<RadialLayoutNode>() && !t.GetComponent<RadialLayoutNode>().IgnoreLayout)
                {
                    if (t.gameObject == this.gameObject)
                        break;
                    siblingIndex++;
                }
            return siblingIndex;
        }

		/// <summary>
		/// Returns all child nodes.
		/// </summary>
		/// <returns>Array containing all child nodes.</returns>
		//遍历当前节点的所有子节点，并把他们添加到children的list里面
		public RadialLayoutNode[] GetChildNodes()
        {
            List<RadialLayoutNode> children = new List<RadialLayoutNode>();
            foreach (Transform t in this.transform)
            {
                var childNode = t.GetComponent<RadialLayoutNode>();
                if (childNode != null)
                {
                    children.Add(childNode);
                }
            }

            return children.ToArray();
        }

		//----------------------------------------------

		//non-sublayout node struct:
		// node(this)
		// |-childnode0
		// |-childnode1
		// |-childnode2

		//----------------------------------------------

		//sub-layout struct:
		// node(this)
		// |--sublayout
		// |   |-childnode0
		// |   |-childnode1
		// |   |-childnode2
		// |--other

		//so: 找到当前节点下面第一个挂载RadialLayout的节点，用是否挂载来判断是否是子布局
		public RadialLayout GetSubLayout()
        {
            foreach (Transform t in this.transform)
                if (t.GetComponent<RadialLayout>() != null)
                    return t.GetComponent<RadialLayout>();
            return null;
        }

		/// <summary>
		/// Converst this node to a sub-layout.
		/// </summary>
		//强制将当前节点及其子节点转为子布局，将当前节点设为中心连接点，重新创建一个rootlinks用来管理连线
        //重新创建一个sublayout，然后把子节点的父节点全都换成子布局，再把之前的连线为空的移除
		public void ConvertToSubLayout()
        {
            if (this.IsSubLayout)
                return;
            //获取所有子节点
            var childNodes = this.GetChildNodes();
            //新创建一个layout
            RadialLayout newLayout = new GameObject("Sub-Layout",typeof(RectTransform) ,typeof(RadialLayout)).GetComponent<RadialLayout>();
            //设置当前节点为父节点
            newLayout.transform.SetParent(this.transform);
            newLayout.transform.localPosition = Vector3.zero;
            newLayout.transform.localScale = Vector3.one;
            newLayout.transform.localEulerAngles = Vector3.zero;
            newLayout.transform.SetAsLastSibling();

            GameObject linksRoot = new GameObject("LinksRoot(Sub-Layout)", typeof(LayoutElement),typeof(RadialLayoutLinksRoot));
            linksRoot.GetComponent<LayoutElement>().ignoreLayout = true;
            linksRoot.GetComponent<RadialLayoutLinksRoot>().parentLayout = newLayout;
            linksRoot.transform.SetParent(this.Layout.GetMasterLayout().transform);
            linksRoot.transform.SetSiblingIndex(this.Layout.linksRoot.transform.GetSiblingIndex() + 1);

            newLayout.linksRoot = linksRoot;

            GameObject innerNode = GameObject.Instantiate(this.Layout.innerNode);
            innerNode.transform.SetParent(newLayout.transform);
            innerNode.transform.localPosition = Vector3.zero;
            innerNode.transform.rotation = Quaternion.identity;
            newLayout.innerNode = innerNode;

            // Copying settings
            newLayout.autoRebuildMode = this.Layout.autoRebuildMode;
            newLayout.nodesDistribution = this.Layout.nodesDistribution;
            newLayout.fanDistributionCommonSpan = this.Layout.fanDistributionCommonSpan;
            newLayout.circleRadius = this.Layout.circleRadius;
            newLayout.enableSeparateExternalRadii = this.Layout.enableSeparateExternalRadii;
            newLayout.externalCircleRadiusMultiplier = this.Layout.externalCircleRadiusMultiplier;
            newLayout.rotationOffset = this.Layout.rotationOffset;
            newLayout.scaleNodeWithRadius = false;
            newLayout.nodeScaleFactor = this.Layout.nodeScaleFactor;
            newLayout.nodeScale_min = this.Layout.nodeScale_min;
            newLayout.nodeScale_max = this.Layout.nodeScale_max;
            newLayout.showInnerNode = this.Layout.showInnerNode;
            newLayout.showInnerLinks = this.Layout.showInnerLinks;
            newLayout.innerNodeScale = this.Layout.innerNodeScale;
            newLayout.prefab_node = this.Layout.prefab_node;
            newLayout.prefab_link = this.Layout.prefab_link;
            newLayout.linksProgressMode = this.Layout.linksProgressMode;
            newLayout.linksProgressSpeed = this.Layout.linksProgressSpeed;
#if UNITY_EDITOR
            // Editor only settings
            newLayout.keepPrefabLinking = this.Layout.keepPrefabLinking;
#endif

            // Moving all child nodes to new sub-layout
            foreach (var n in childNodes)
            {
                n.transform.SetParent(newLayout.transform);
                n.SetParent((RadialLayoutNode)null);
            }

            // Rearranging links
            if (this.DepartingLinks != null)
            {
                for (int k = this.DepartingLinks.Count - 1; k >= 0; k--)
                {
                    if (this.DepartingLinks[k] != null)
                    {
                        this.DepartingLinks[k].transform.SetParent(linksRoot.transform);
                        this.DepartingLinks[k].Set(newLayout, this.DepartingLinks[k].to);
                    }
				}
            }

            newLayout.Rebuild();

			// Removing deleted links from previous rebuild (became inner links)
			if (this.DepartingLinks != null)
			{
				for (int k = this.DepartingLinks.Count - 1; k >= 0; k--)
				{
					if (this.DepartingLinks[k] == null)
					{
						this.DepartingLinks.RemoveAt(k);
					}
				}
			}

			// Editor settings
#if UNITY_EDITOR
			newLayout.forceNodesSelection = this.Layout.forceNodesSelection;
            newLayout.drawEditorGizmos = this.Layout.drawEditorGizmos;
            newLayout.drawEditorGizmos_circleRadius = this.Layout.drawEditorGizmos_circleRadius;
            newLayout.drawEditorGizmos_handles = this.Layout.drawEditorGizmos_handles;
            newLayout.drawEditorGizmos_nodes = this.Layout.drawEditorGizmos_nodes;
            newLayout.handle_lockRadius = this.Layout.handle_lockRadius;
            newLayout.handle_lockRotation = this.Layout.handle_lockRotation;
            newLayout.handle_snap = this.Layout.handle_snap;
            newLayout.handle_snap_intValues = this.Layout.handle_snap_intValues;
            newLayout.handle_snap_resolution = this.Layout.handle_snap_resolution;
#endif

            // registering to parent rebuild event
            newLayout.SetParentLayout(this.Layout);


            newLayout.UseQuerySystem = this.Layout.UseQuerySystem;

        }

        /// <summary>
        /// Reconverts this node to a simple node, if a sub-layout is found;
        /// </summary>
        //遍历子布局得到当前的所有子节点，把他们父节点设定为当前节点，破坏之前的布局--子节点关系
        //把连线关系的字典内容更新为当前节点到子节点，把连线管理器和子布局删除
        public void ConvertToNode()
        {
            if (!this.IsSubLayout)
                return;

            // Moving all child outside the sublayout
            List<RadialLayoutNode> subLayoutNodes = new List<RadialLayoutNode>();
            foreach (Transform t in this.GetSubLayout().transform)
            {
                RadialLayoutNode node = t.GetComponent<RadialLayoutNode>();
                if (node != null)
                    subLayoutNodes.Add(node);
            }
            foreach (var node in subLayoutNodes)
            {
                node.transform.SetParent(this.transform);
                node.SetParent(this);
            }

            // Rearranging links
            foreach (var link in this.GetSubLayout().linksRoot.GetComponentsInChildren<RadialLayoutLink>())
            {
                link.Set(this, link.to);
            }

            // Destroy sub-layout
            RadialLayout.DestroyGameObject(this.GetSubLayout().linksRoot);
            RadialLayout.DestroyGameObject(this.GetSubLayout().gameObject);

			this.Layout.Rebuild();
        }

        #endregion

        #region Misc
        /// <summary>
        /// Resets update parameters to the current state so, a refresh will not be triggered next frame.
        /// </summary>
        public void ResetUpdateParameters()
        {
            this.lastChildCount = this.transform.childCount;
            this.lastFanSpan = this.fanSpan;
            this.lastAngleOffset = this.angleOffset;
            this.lastDistanceOffset = this.distanceOffset;
            this.lastFanSpanOverride = this.fanSpanOverride;
            this.lastOverrideFanSpan = this.overrideFanSpan;
            this.lastBranchLength = this.branchLength;
            this.lastOverrideBranchLength = this.overrideBranchLength;
            this.lastFanOffset = this.fanOffset;
        }

#if UNITY_EDITOR
        private void OnDrawGizmosSelected()
		{
            Color exColor = Gizmos.color;
			// Inspector 勾选了该开关
			if (this.showNodeRadiusGizmo)
            {
                Gizmos.color = Color.gray;
                float canvasScale = 1;
				if (this.Layout != null)
				//非嵌套 Canvas 用 Canvas.transform.localScale.x
					canvasScale = !this.Layout.IsNestedCanvas ? this.Layout.Canvas.transform.localScale.x : this.Layout.Canvas.transform.lossyScale.x;
                else
					//没有layout回退到父 Canvas 的 lossyScale.x
					canvasScale = this.GetComponentInParent<Canvas>().transform.lossyScale.x;
				//半径由三部分组成：nodeRadius（你设置的基础半径）\节点自身缩放 localScale.x\画布缩放补偿 canvasScale
				Gizmos.DrawWireSphere(this.transform.position, this.nodeRadius * this.transform.localScale.x * canvasScale);
            }

            // Links gizmo
            //当真实连线还没创建时，画连线预览
            if (this.Layout != null && this.Layout.linksRoot.transform.childCount == 0 && this.Layout.prefab_link != null)
            {
                Gizmos.color = Color.gray;
                //有子节点的话，画当前节点--其直接子节点的线
                if (this.HasChildren)
                {
                    foreach (var child in this.GetChildNodes())
                    {
                        Gizmos.DrawLine(child.transform.position, this.transform.position);
                    }
                }
                else if (this.IsSubLayout)
                {
                    foreach(var node in this.GetSubLayout().Nodes)
						//当前节点是子布局承载节点时,画“子布局第一层节点 <-> 承载节点”的线，模拟 inner links 关系
						if (node.depth == 0)
                            Gizmos.DrawLine(node.transform.position, this.transform.position);
                }
				//再画与父/中心的主连接
				//有父节点：画父 -> 当前
				//没父且在 depth==0：画布局中心 -> 当前（顶层节点主连线）
				if (this.ParentNode != null)
                    Gizmos.DrawLine(this.ParentNode.transform.position, this.transform.position);
                else if(this.depth == 0)
                    Gizmos.DrawLine(this.Layout.transform.position, this.transform.position);
            }

            Gizmos.color = exColor;
        }
#endif

        /// <summary>
        /// Inverts the order of the child nodes
        /// </summary>
        //通过获取当前节点的子节点索引作为反转区间，不断把当前子节点的最后一个节点放到第一个索引位置
        //以此来实现反转
        public void InvertChildNodes()
        {
            var children = this.GetChildNodes();
            if (children != null && children.Length > 1)
            {
				//返回当前 GameObject 在其父对象所有子对象中的顺序编号（索引）‌，索引从 ‌0 开始
				int startIndex = children[0].transform.GetSiblingIndex();
				int endIndex = children[children.Length-1].transform.GetSiblingIndex();

                for (int k = 0; k < endIndex - startIndex; k++)
                {
                    this.transform.GetChild(endIndex).transform.SetSiblingIndex(startIndex+k);
                }
			}
        }

        /// <summary>
        /// Called upon destroy to perform cleaning operations
        /// </summary>
        //清理强制连接的节点和线
        public void NotifyDestroy()
        {
			// If merging node, clearing the departing link from the converging node
            //判断有没有强制连线
			if (this.IsMergingNode)
			{
                if (this.mergingLinks != null)
                {
                    foreach (var mergingLink in this.mergingLinks)
                    {
						//DepartingLinks :强连节点的线存储容器
						//mergingLink.from == null：连线来源节点已不存在
                        //DepartingLinks == null：来源节点还没初始化出链列表
                        //Contains == false：该引用本就不在列表，避免无效操作
						if (mergingLink.from != null && mergingLink.from.DepartingLinks != null && mergingLink.from.DepartingLinks.Contains(mergingLink))
                            mergingLink.from.DepartingLinks.Remove(mergingLink);
                    }
                }
			}
		}

#endregion


    }
}

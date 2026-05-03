using System.Collections.Generic;
using System.IO;
using GIGA.AutoRadialLayout;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

public class SkillGraphPanel : MonoBehaviour
{
	public SkillDescription skillDescription = null;

#if UNITY_EDITOR
	public const string SkillGraphPanelPrefabPath = "Assets/Resources/UI/Skill/SkillGraphPanel.prefab";
	const string RadialLayoutPrefabPath = "Assets/GIGA Softworks/Auto Radial Layout/Prefabs/prefab_RadialLayout.prefab";
	const string DefaultNodePrefabPath = "Assets/Art/Prefab/UI/Node/prefab_Node_Health.prefab";
	const string RadialLinkPrefabPath = "Assets/GIGA Softworks/Auto Radial Layout/Prefabs/Links/prefab_Link_Flat.prefab";
#endif

	[SerializeField] RadialLayout skillLayout;

	readonly Dictionary<int, SkillNode> _skillNodesById = new Dictionary<int, SkillNode>();
	SkillNode _selectedNode;
	bool _initialized;

	public void OnSkillNodeButtonClicked(SkillNode node)
	{
		if (node == null)
		{
			return;
		}

		_selectedNode = node;
		if (skillDescription != null)
		{
			skillDescription.SetSkillInfo(node.SkillName, node.SkillDescription);
		}

		RefreshUnlockButtonState();
	}

	void Awake()
	{
		if (skillDescription == null)
		{
			skillDescription = GetComponentInChildren<SkillDescription>(true);
		}

		if (skillLayout == null)
		{
			skillLayout = GetComponentInChildren<RadialLayout>(true);
		}

		if (skillDescription != null && skillDescription.unlockButton != null)
		{
			skillDescription.unlockButton.onClick.AddListener(OnUnlockButtonClicked);
		}
	}

	void OnDestroy()
	{
		if (skillDescription != null && skillDescription.unlockButton != null)
		{
			skillDescription.unlockButton.onClick.RemoveListener(OnUnlockButtonClicked);
		}
	}

	void OnEnable()
	{
		if (!_initialized)
		{
			CollectSkillNodes();
			BindSheetDataToNodes();
			SyncUnlockVisuals();
			_initialized = true;
		}

		RefreshUnlockButtonState();
	}

	void CollectSkillNodes()
	{
		_skillNodesById.Clear();
		Transform scopeRoot = skillLayout != null ? skillLayout.transform : transform;
		SkillNode[] nodes = scopeRoot.GetComponentsInChildren<SkillNode>(true);
		for (int i = 0; i < nodes.Length; i++)
		{
			SkillNode node = nodes[i];
			if (node == null)
			{
				continue;
			}

			if (skillLayout != null && !node.transform.IsChildOf(skillLayout.transform))
			{
				continue;
			}

			if (node.GetComponent<RadialLayoutNode>() == null)
			{
				continue;
			}

			int id = node.SkillId;
			if (_skillNodesById.ContainsKey(id))
			{
				Debug.LogWarning($"[SkillGraphPanel] 重复的技能 id={id}：已忽略 {node.name}（保留 {_skillNodesById[id].name}）", node);
				continue;
			}

			_skillNodesById.Add(id, node);
		}
	}

	void BindSheetDataToNodes()
	{
		SkillGraphManager manager = SkillGraphManager.Instance;
		if (manager == null)
		{
			Debug.LogWarning("[SkillGraphPanel] SkillGraphManager.Instance 为 null，无法绑定技能数据。", this);
			return;
		}

		IReadOnlyDictionary<int, SkillGraphManager.SkillState> states = manager.SkillStates;
		foreach (KeyValuePair<int, SkillNode> kvp in _skillNodesById)
		{
			if (!states.TryGetValue(kvp.Key, out SkillGraphManager.SkillState state))
			{
				continue;
			}

			SkillGraphManager.SkillSheetRecord record = state.Record;
			kvp.Value.ApplyBoundData(
				record.SkillName,
				record.SkillDescription,
				record.BonusHp,
				record.BonusStamina,
				record.BonusAttack,
				record.BonusDefense,
				record.PrerequisiteSkillIds);
		}
	}

	void SyncUnlockVisuals()
	{
		SkillGraphManager manager = SkillGraphManager.Instance;
		if (manager == null)
		{
			return;
		}

		foreach (KeyValuePair<int, SkillNode> kvp in _skillNodesById)
		{
			kvp.Value.SetUnlockedVisual(manager.IsSkillUnlocked(kvp.Key));
		}

		SyncLinkHighlights();
	}

	void SyncLinkHighlights()
	{
		SkillGraphManager manager = SkillGraphManager.Instance;
		if (manager == null)
		{
			return;
		}

		foreach (KeyValuePair<int, SkillNode> kvp in _skillNodesById)
		{
			SetNodeDepartingLinksHighlight(kvp.Value, manager.IsSkillUnlocked(kvp.Key));
		}
	}

	void SetNodeDepartingLinksHighlight(SkillNode node, bool highlight)
	{
		if (node == null)
		{
			return;
		}

		RadialLayoutNode radialNode = node.GetComponent<RadialLayoutNode>();
		if (radialNode == null || radialNode.DepartingLinks == null)
		{
			return;
		}

		float progressValue = highlight ? 1f : 0f;
		for (int i = 0; i < radialNode.DepartingLinks.Count; i++)
		{
			RadialLayoutLink link = radialNode.DepartingLinks[i];
			if (link != null)
			{
				link.ProgressValue = progressValue;
			}
		}
	}

	void OnUnlockButtonClicked()
	{
		SkillGraphManager manager = SkillGraphManager.Instance;
		if (_selectedNode != null && manager != null && manager.TryUnlockSkill(_selectedNode.SkillId))
		{
			_selectedNode.SetUnlockedVisual(true);
			SetNodeDepartingLinksHighlight(_selectedNode, true);
		}

		RefreshUnlockButtonState();
	}

	void RefreshUnlockButtonState()
	{
		if (skillDescription == null || skillDescription.unlockButton == null)
		{
			return;
		}

		SkillNode node = _selectedNode;
		if (node == null)
		{
			skillDescription.unlockButton.interactable = false;
			return;
		}

		SkillGraphManager manager = SkillGraphManager.Instance;
		if (manager == null || manager.IsSkillUnlocked(node.SkillId))
		{
			skillDescription.unlockButton.interactable = false;
			return;
		}

		int[] prerequisiteSkillIds = node.PrerequisiteSkillIds;
		for (int i = 0; i < prerequisiteSkillIds.Length; i++)
		{
			if (!manager.IsSkillUnlocked(prerequisiteSkillIds[i]))
			{
				skillDescription.unlockButton.interactable = false;
				return;
			}
		}

		skillDescription.unlockButton.interactable = true;
	}

#if UNITY_EDITOR
	public static bool RebuildPanelPrefabFromBytes(string bytesFilePath, out string error)
	{
		error = null;
		if (!TryLoadSkillSheetFromFile(bytesFilePath, out SheetTableBin bin, out error))
		{
			return false;
		}

		if (!SkillGraphManager.TryValidateSkillSheetBin(bin, out error))
		{
			return false;
		}

		GameObject prefabRoot = PrefabUtility.LoadPrefabContents(SkillGraphPanelPrefabPath);
		try
		{
			SkillGraphPanel panel = prefabRoot.GetComponent<SkillGraphPanel>();
			if (panel == null)
			{
				error = $"[SkillGraphPanel] 在 {SkillGraphPanelPrefabPath} 上找不到 SkillGraphPanel。";
				return false;
			}

			if (!panel.RebuildGraphFromSheet(bin, out error))
			{
				return false;
			}

			PrefabUtility.SaveAsPrefabAsset(prefabRoot, SkillGraphPanelPrefabPath);
			AssetDatabase.SaveAssets();
			AssetDatabase.ImportAsset(SkillGraphPanelPrefabPath);
			return true;
		}
		finally
		{
			PrefabUtility.UnloadPrefabContents(prefabRoot);
		}
	}

	public bool RebuildGraphFromSheet(SheetTableBin bin, out string error)
	{
		error = null;
		if (!SkillGraphManager.TryBuildSkillRecords(bin, out List<SkillGraphManager.SkillSheetRecord> records, out error))
		{
			return false;
		}

		if (!SkillGraphManager.TryValidateDependencyGraph(records, out Dictionary<int, SkillGraphManager.SkillSheetRecord> _, out Dictionary<int, int> depthById, out error))
		{
			return false;
		}

		if (!TryEnsureSkillLayout(out RadialLayout layout, out RadialLayoutNode nodePrefab, out error))
		{
			return false;
		}

		GameObject defaultNodePrefabAsset = AssetDatabase.LoadAssetAtPath<GameObject>(DefaultNodePrefabPath);
		RadialLayoutNode defaultNodePrefab = defaultNodePrefabAsset != null
			? defaultNodePrefabAsset.GetComponent<RadialLayoutNode>()
			: nodePrefab;

		Canvas temporaryCanvas = null;
		bool hadCanvasBeforeBuild = layout.Canvas != null;
		if (!hadCanvasBeforeBuild)
		{
			temporaryCanvas = gameObject.AddComponent<Canvas>();
			temporaryCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
		}

		RemoveLegacySkillNodesOutsideLayout(layout);
		ForceRemoveAllNodesInsideLayout(layout);
		layout.Clear();

		try
		{
			List<SkillGraphManager.SkillSheetRecord> orderedRecords = new List<SkillGraphManager.SkillSheetRecord>(records);
			orderedRecords.Sort((left, right) =>
			{
				int depthCompare = depthById[left.SkillId].CompareTo(depthById[right.SkillId]);
				if (depthCompare != 0)
				{
					return depthCompare;
				}

				return left.OrderIndex.CompareTo(right.OrderIndex);
			});

			Dictionary<int, RadialLayoutNode> radialNodesById = new Dictionary<int, RadialLayoutNode>(records.Count);
			Dictionary<string, RadialLayoutNode> nodePrefabCache = new Dictionary<string, RadialLayoutNode>();
			for (int i = 0; i < orderedRecords.Count; i++)
			{
				SkillGraphManager.SkillSheetRecord record = orderedRecords[i];
				RadialLayoutNode parentNode = null;
				if (record.PrerequisiteSkillIds.Length > 0)
				{
					int primaryPrereqId = GetPrimaryPrerequisiteId(record, depthById);
					parentNode = radialNodesById[primaryPrereqId];
				}

				RadialLayoutNode thisNodePrefab = ResolveNodePrefabForRecord(record.NodePrefabPath, defaultNodePrefab, nodePrefabCache);
				RadialLayoutNode radialNode = layout.AddNodeFromPrefab(thisNodePrefab, record.SkillName, parentNode);
				if (radialNode == null)
				{
					error = $"[SkillGraphPanel] 创建技能节点失败，技能 id={record.SkillId}。";
					return false;
				}

				SetupGeneratedNode(radialNode.gameObject, record);
				radialNodesById.Add(record.SkillId, radialNode);
			}

			for (int i = 0; i < orderedRecords.Count; i++)
			{
				SkillGraphManager.SkillSheetRecord record = orderedRecords[i];
				if (record.PrerequisiteSkillIds.Length < 2)
				{
					continue;
				}

				int primaryPrereqId = GetPrimaryPrerequisiteId(record, depthById);
				RadialLayoutNode radialNode = radialNodesById[record.SkillId];
				RadialLayoutMergingNode mergingNode = radialNode.GetComponent<RadialLayoutMergingNode>();
				if (mergingNode == null)
				{
					mergingNode = radialNode.gameObject.AddComponent<RadialLayoutMergingNode>();
				}

				if (mergingNode.convergingNodes == null)
				{
					mergingNode.convergingNodes = new List<RadialLayoutNode>();
				}

				mergingNode.convergingNodes.Clear();
				for (int j = 0; j < record.PrerequisiteSkillIds.Length; j++)
				{
					int prereqId = record.PrerequisiteSkillIds[j];
					if (prereqId == primaryPrereqId)
					{
						continue;
					}

					mergingNode.convergingNodes.Add(radialNodesById[prereqId]);
				}
			}

			layout.Rebuild();
			CollectSkillNodes();
			foreach (KeyValuePair<int, SkillNode> kvp in _skillNodesById)
			{
				EditorUtility.SetDirty(kvp.Value);
			}
		}
		finally
		{
			if (temporaryCanvas != null)
			{
				DestroyImmediate(temporaryCanvas);
			}
		}

		EditorUtility.SetDirty(layout);
		EditorUtility.SetDirty(this);
		return true;
	}

	static bool TryLoadSkillSheetFromFile(string fullPath, out SheetTableBin bin, out string error)
	{
		bin = null;
		error = null;
		if (string.IsNullOrWhiteSpace(fullPath))
		{
			error = "[SkillGraphPanel] 未选择技能表文件。";
			return false;
		}

		if (!File.Exists(fullPath))
		{
			error = $"[SkillGraphPanel] 找不到技能表文件：{fullPath}";
			return false;
		}

		byte[] fileBytes = File.ReadAllBytes(fullPath);
		if (!SheetTableBin.TryLoad(fileBytes, out bin))
		{
			error = $"[SkillGraphPanel] 无法解析技能表文件：{fullPath}";
			return false;
		}

		return true;
	}

	bool TryEnsureSkillLayout(out RadialLayout layout, out RadialLayoutNode nodePrefab, out string error)
	{
		layout = skillLayout != null ? skillLayout : GetComponentInChildren<RadialLayout>(true);
		nodePrefab = null;
		error = null;

		if (layout == null)
		{
			GameObject layoutPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(RadialLayoutPrefabPath);
			if (layoutPrefab == null)
			{
				error = $"[SkillGraphPanel] 找不到径向布局 prefab：{RadialLayoutPrefabPath}";
				return false;
			}

			GameObject instantiatedLayout = PrefabUtility.InstantiatePrefab(layoutPrefab) as GameObject;
			if (instantiatedLayout == null)
			{
				instantiatedLayout = Instantiate(layoutPrefab);
			}

			instantiatedLayout.name = "SkillGraphLayout";
			instantiatedLayout.transform.SetParent(transform, false);
			layout = instantiatedLayout.GetComponent<RadialLayout>();
		}

		if (layout == null)
		{
			error = "[SkillGraphPanel] 无法创建 SkillGraph 使用的 RadialLayout。";
			return false;
		}

		skillLayout = layout;
		ConfigureLayoutRect(layout);

		GameObject nodePrefabAsset = AssetDatabase.LoadAssetAtPath<GameObject>(DefaultNodePrefabPath);
		GameObject linkPrefabAsset = AssetDatabase.LoadAssetAtPath<GameObject>(RadialLinkPrefabPath);
		if (nodePrefabAsset == null)
		{
			error = $"[SkillGraphPanel] 找不到节点 prefab：{DefaultNodePrefabPath}";
			return false;
		}

		if (linkPrefabAsset == null)
		{
			error = $"[SkillGraphPanel] 找不到连线 prefab：{RadialLinkPrefabPath}";
			return false;
		}

		nodePrefab = nodePrefabAsset.GetComponent<RadialLayoutNode>();
		RadialLayoutLink linkPrefab = linkPrefabAsset.GetComponent<RadialLayoutLink>();
		if (nodePrefab == null || linkPrefab == null)
		{
			error = "[SkillGraphPanel] GIGA prefab 缺少必需的组件引用。";
			return false;
		}

		layout.prefab_node = nodePrefab;
		layout.prefab_link = linkPrefab;
		layout.showInnerNode = true;
		layout.showInnerLinks = true;
		layout.autoRebuildMode = RadialLayout.AutoRebuildMode.EditorOnly;
		layout.ShowInnerNode(true);
		SetLayerRecursively(layout.gameObject, gameObject.layer);
		return true;
	}

	void ConfigureLayoutRect(RadialLayout layout)
	{
		RectTransform layoutRect = layout.GetComponent<RectTransform>();
		if (layoutRect == null)
		{
			return;
		}

		layoutRect.anchorMin = Vector2.zero;
		layoutRect.anchorMax = Vector2.one;
		layoutRect.pivot = new Vector2(0.5f, 0.5f);
		layoutRect.anchoredPosition = Vector2.zero;
		layoutRect.offsetMin = Vector2.zero;
		layoutRect.offsetMax = new Vector2(-300f, 0f);
		layoutRect.localScale = Vector3.one;
		layoutRect.localRotation = Quaternion.identity;
	}

	void RemoveLegacySkillNodesOutsideLayout(RadialLayout layout)
	{
		SkillNode[] nodes = GetComponentsInChildren<SkillNode>(true);
		for (int i = 0; i < nodes.Length; i++)
		{
			SkillNode node = nodes[i];
			if (node == null)
			{
				continue;
			}

			if (layout != null && node.transform.IsChildOf(layout.transform))
			{
				continue;
			}

			DestroyImmediate(node.gameObject);
		}
	}

	void ForceRemoveAllNodesInsideLayout(RadialLayout layout)
	{
		if (layout == null)
		{
			return;
		}

		RadialLayoutNode[] allNodes = layout.GetComponentsInChildren<RadialLayoutNode>(true);
		for (int i = 0; i < allNodes.Length; i++)
		{
			RadialLayoutNode node = allNodes[i];
			if (node == null)
			{
				continue;
			}

			if (node.transform == layout.transform)
			{
				continue;
			}

			DestroyImmediate(node.gameObject);
		}
	}

	void SetupGeneratedNode(GameObject nodeObject, SkillGraphManager.SkillSheetRecord record)
	{
		SetLayerRecursively(nodeObject, gameObject.layer);

		SkillNode skillNode = nodeObject.GetComponent<SkillNode>();
		if (skillNode == null)
		{
			skillNode = nodeObject.AddComponent<SkillNode>();
		}

		skillNode.SetSkillId(record.SkillId);
		skillNode.SetGeneratedLabel(null);
		skillNode.ApplyBoundData(
			record.SkillName,
			record.SkillDescription,
			record.BonusHp,
			record.BonusStamina,
			record.BonusAttack,
			record.BonusDefense,
			record.PrerequisiteSkillIds);
	}

	static void SetLayerRecursively(GameObject root, int layer)
	{
		root.layer = layer;
		for (int i = 0; i < root.transform.childCount; i++)
		{
			SetLayerRecursively(root.transform.GetChild(i).gameObject, layer);
		}
	}

	static int GetPrimaryPrerequisiteId(SkillGraphManager.SkillSheetRecord record, IReadOnlyDictionary<int, int> depthById)
	{
		int primaryPrereqId = record.PrerequisiteSkillIds[0];
		int deepestDepth = depthById[primaryPrereqId];
		for (int i = 1; i < record.PrerequisiteSkillIds.Length; i++)
		{
			int candidateId = record.PrerequisiteSkillIds[i];
			int candidateDepth = depthById[candidateId];
			if (candidateDepth > deepestDepth)
			{
				deepestDepth = candidateDepth;
				primaryPrereqId = candidateId;
			}
		}

		return primaryPrereqId;
	}

	static RadialLayoutNode ResolveNodePrefabForRecord(
		string recordPath,
		RadialLayoutNode defaultPrefab,
		Dictionary<string, RadialLayoutNode> cache)
	{
		if (string.IsNullOrWhiteSpace(recordPath))
		{
			return defaultPrefab;
		}

		if (cache.TryGetValue(recordPath, out RadialLayoutNode cached))
		{
			return cached != null ? cached : defaultPrefab;
		}

		GameObject asset = AssetDatabase.LoadAssetAtPath<GameObject>(recordPath);
		RadialLayoutNode prefab = asset != null ? asset.GetComponent<RadialLayoutNode>() : null;
		if (prefab == null)
		{
			Debug.LogWarning(
				$"[SkillGraphPanel] NodePrefab 路径无效或缺少 RadialLayoutNode 组件：{recordPath}，使用默认 prefab。");
		}

		cache[recordPath] = prefab;
		return prefab != null ? prefab : defaultPrefab;
	}
#endif
}

using System;
using System.Collections.Generic;
using UnityEngine;

public class SkillGraphManager : SingletonBehaviour<SkillGraphManager>
{
	public sealed class SkillSheetRecord
	{
		public int OrderIndex;
		public int SkillId;
		public string SkillName;
		public string SkillDescription;
		public float BonusHp;
		public float BonusStamina;
		public float BonusAttack;
		public float BonusDefense;
		public int[] PrerequisiteSkillIds;
#if UNITY_EDITOR
		public string NodePrefabPath;
#endif
	}

	public sealed class SkillState
	{
		public SkillSheetRecord Record;
		public int Depth;
		public bool Unlocked;
	}

	enum SkillVisitState
	{
		Unvisited = 0,
		Visiting = 1,
		Visited = 2,
	}

	const string SkillSheetResourcePath = "Sheets/技能表";

	readonly Dictionary<int, SkillState> _skillStates = new Dictionary<int, SkillState>();

	public IReadOnlyDictionary<int, SkillState> SkillStates => _skillStates;

	protected override bool UseDontDestroyOnLoad => true;

	void Start()
	{
		LoadSkillSheet();
	}

	public bool IsSkillUnlocked(int skillId)
	{
		return _skillStates.TryGetValue(skillId, out SkillState state) && state.Unlocked;
	}

	public bool TryUnlockSkill(int skillId)
	{
		if (!_skillStates.TryGetValue(skillId, out SkillState state))
		{
			return false;
		}

		if (state.Unlocked)
		{
			return true;
		}

		int[] prereqs = state.Record.PrerequisiteSkillIds;
		for (int i = 0; i < prereqs.Length; i++)
		{
			if (!IsSkillUnlocked(prereqs[i]))
			{
				return false;
			}
		}

		state.Unlocked = true;
		ApplyAttributeBonuses(state.Record);
		return true;
	}

	void LoadSkillSheet()
	{
		if (!TryLoadSkillSheetFromResource(out SheetTableBin bin))
		{
			Debug.LogWarning($"[SkillGraphManager] 未找到 Resources/{SkillSheetResourcePath}.bytes，请先导表。", this);
			return;
		}

		if (!TryValidateSkillSheetBin(bin, out string validateError))
		{
			Debug.LogError(validateError, this);
			return;
		}

		if (!TryBuildSkillRecords(bin, out List<SkillSheetRecord> records, out string error))
		{
			Debug.LogError(error, this);
			return;
		}

		if (!TryValidateDependencyGraph(records, out Dictionary<int, SkillSheetRecord> _, out Dictionary<int, int> depthById, out error))
		{
			Debug.LogError(error, this);
			return;
		}

		_skillStates.Clear();
		for (int i = 0; i < records.Count; i++)
		{
			SkillSheetRecord record = records[i];
			_skillStates.Add(record.SkillId, new SkillState
			{
				Record = record,
				Depth = depthById[record.SkillId],
				Unlocked = false,
			});
		}
	}

	static void ApplyAttributeBonuses(SkillSheetRecord record)
	{
		if (PlayerAttributes.Instance == null)
		{
			return;
		}

		if (record.BonusHp > 0f)
		{
			PlayerAttributes.Instance.FetchAttributeChange("Health", record.BonusHp);
		}

		if (record.BonusStamina > 0f)
		{
			PlayerAttributes.Instance.FetchAttributeChange("Stamina", record.BonusStamina);
		}

		if (record.BonusAttack > 0f)
		{
			PlayerAttributes.Instance.FetchAttributeChange("Attack", record.BonusAttack);
		}

		if (record.BonusDefense > 0f)
		{
			PlayerAttributes.Instance.FetchAttributeChange("Defense", record.BonusDefense);
		}
	}

	bool TryLoadSkillSheetFromResource(out SheetTableBin bin)
	{
		bin = null;
		TextAsset tableAsset = Resources.Load<TextAsset>(SkillSheetResourcePath);
		if (tableAsset == null)
		{
			return false;
		}

		return SheetTableBin.TryLoad(tableAsset, out bin);
	}

	public static bool TryValidateSkillSheetBin(SheetTableBin bin, out string error)
	{
		error = null;
		if (bin == null)
		{
			error = "[SkillGraphManager] 技能表对象为空。";
			return false;
		}

		if (bin.RowCount == 0)
		{
			error = "[SkillGraphManager] 技能表没有数据行。";
			return false;
		}

		(string colName, string expectedType)[] requiredCols =
		{
			("技能id",   "int"),
			("技能名",   null),
			("技能描述", null),
			("生命值",   "float"),
			("耐力值",   "float"),
			("攻击力",   "float"),
			("防御力",   "float"),
			("前置技能", "List<int>"),
		};

		for (int i = 0; i < requiredCols.Length; i++)
		{
			string colName = requiredCols[i].colName;
			string expectedType = requiredCols[i].expectedType;

			int colIndex = bin.GetColumnIndex(colName);
			if (colIndex < 0)
			{
				error = $"[SkillGraphManager] 技能表缺少必要列：{colName}。";
				return false;
			}

			if (expectedType == null)
			{
				continue;
			}

			string actualType = bin.GetColumnType(colIndex);
			bool typeMatch;
			if (string.Equals(expectedType, "float", StringComparison.OrdinalIgnoreCase))
			{
				typeMatch = string.Equals(actualType, "float", StringComparison.OrdinalIgnoreCase)
				         || string.Equals(actualType, "double", StringComparison.OrdinalIgnoreCase);
			}
			else
			{
				typeMatch = string.Equals(actualType, expectedType, StringComparison.OrdinalIgnoreCase);
			}

			if (!typeMatch)
			{
				error = $"[SkillGraphManager] 技能表列 {colName} 类型不匹配（期望 {expectedType}，实际 {actualType}）。";
				return false;
			}
		}

		return true;
	}

	public static bool TryBuildSkillRecords(SheetTableBin bin, out List<SkillSheetRecord> records, out string error)
	{
		records = new List<SkillSheetRecord>();
		error = null;
		if (bin == null)
		{
			error = "[SkillGraphManager] 技能表为空，无法绑定。";
			return false;
		}

#if UNITY_EDITOR
		bool hasNodePrefabCol = bin.RowCount > 0 && bin.GetColumnIndex("NodePrefab") >= 0;
		if (!hasNodePrefabCol)
		{
			Debug.LogWarning("[SkillGraphManager] 技能表缺少 NodePrefab 列，所有节点将使用默认 prefab。");
		}
#endif

		HashSet<int> seenSkillIds = new HashSet<int>();
		for (int rowIndex = 0; rowIndex < bin.RowCount; rowIndex++)
		{
			if (!bin.TryGetCellAsInt(rowIndex, "技能id", out int skillId))
			{
				continue;
			}

			if (!seenSkillIds.Add(skillId))
			{
				error = $"[SkillGraphManager] 技能表存在重复的技能 id={skillId}。";
				return false;
			}

			float bonusHp = 0f;
			float bonusStamina = 0f;
			float bonusAttack = 0f;
			float bonusDefense = 0f;
			bin.TryGetCellAsFloat(rowIndex, "生命值", out bonusHp);
			bin.TryGetCellAsFloat(rowIndex, "耐力值", out bonusStamina);
			bin.TryGetCellAsFloat(rowIndex, "攻击力", out bonusAttack);
			bin.TryGetCellAsFloat(rowIndex, "防御力", out bonusDefense);

			int[] prerequisiteSkillIds = System.Array.Empty<int>();
			if (bin.TryGetCellAsListInt(rowIndex, "前置技能", out int[] parsedPrerequisiteSkillIds))
			{
				prerequisiteSkillIds = parsedPrerequisiteSkillIds != null
					? (int[])parsedPrerequisiteSkillIds.Clone()
					: System.Array.Empty<int>();
			}

			SkillSheetRecord record = new SkillSheetRecord
			{
				OrderIndex = records.Count,
				SkillId = skillId,
				SkillName = bin.GetCell(rowIndex, "技能名"),
				SkillDescription = bin.GetCell(rowIndex, "技能描述"),
				BonusHp = bonusHp,
				BonusStamina = bonusStamina,
				BonusAttack = bonusAttack,
				BonusDefense = bonusDefense,
				PrerequisiteSkillIds = prerequisiteSkillIds,
			};
#if UNITY_EDITOR
			record.NodePrefabPath = hasNodePrefabCol ? (bin.GetCell(rowIndex, "NodePrefab") ?? string.Empty) : string.Empty;
#endif
			records.Add(record);
		}

		if (records.Count == 0)
		{
			error = "[SkillGraphManager] 技能表中没有可用的技能行。";
			return false;
		}

		return true;
	}

	public static bool TryValidateDependencyGraph(
		IReadOnlyList<SkillSheetRecord> records,
		out Dictionary<int, SkillSheetRecord> recordsById,
		out Dictionary<int, int> depthById,
		out string error)
	{
		recordsById = new Dictionary<int, SkillSheetRecord>(records.Count);
		depthById = new Dictionary<int, int>(records.Count);
		error = null;

		for (int i = 0; i < records.Count; i++)
		{
			SkillSheetRecord record = records[i];
			recordsById.Add(record.SkillId, record);
		}

		for (int i = 0; i < records.Count; i++)
		{
			SkillSheetRecord record = records[i];
			int[] prerequisiteSkillIds = record.PrerequisiteSkillIds;
			for (int j = 0; j < prerequisiteSkillIds.Length; j++)
			{
				int prereqId = prerequisiteSkillIds[j];
				if (!recordsById.ContainsKey(prereqId))
				{
					error = $"[SkillGraphManager] 技能 id={record.SkillId} 引用了不存在的前置技能 id={prereqId}，已取消本次重建。";
					return false;
				}
			}
		}

		Dictionary<int, SkillVisitState> visitStateById = new Dictionary<int, SkillVisitState>(records.Count);
		List<int> traversalStack = new List<int>(records.Count);
		for (int i = 0; i < records.Count; i++)
		{
			SkillSheetRecord record = records[i];
			if (!TryComputeSkillDepth(record.SkillId, recordsById, visitStateById, depthById, traversalStack, out error))
			{
				return false;
			}
		}

		return true;
	}

	static bool TryComputeSkillDepth(
		int skillId,
		IReadOnlyDictionary<int, SkillSheetRecord> recordsById,
		IDictionary<int, SkillVisitState> visitStateById,
		IDictionary<int, int> depthById,
		IList<int> traversalStack,
		out string error)
	{
		error = null;
		if (depthById.TryGetValue(skillId, out int _))
		{
			return true;
		}

		if (visitStateById.TryGetValue(skillId, out SkillVisitState visitState) && visitState == SkillVisitState.Visiting)
		{
			int cycleStartIndex = -1;
			for (int i = 0; i < traversalStack.Count; i++)
			{
				if (traversalStack[i] == skillId)
				{
					cycleStartIndex = i;
					break;
				}
			}

			string cycleText = skillId.ToString();
			if (cycleStartIndex >= 0)
			{
				List<string> cycleIds = new List<string>();
				for (int i = cycleStartIndex; i < traversalStack.Count; i++)
				{
					cycleIds.Add(traversalStack[i].ToString());
				}

				cycleIds.Add(skillId.ToString());
				cycleText = string.Join(" -> ", cycleIds);
			}

			error = $"[SkillGraphManager] 检测到循环依赖：{cycleText}，已取消本次重建。";
			return false;
		}

		visitStateById[skillId] = SkillVisitState.Visiting;
		traversalStack.Add(skillId);

		SkillSheetRecord record = recordsById[skillId];
		int depth = 0;
		int[] prerequisiteSkillIds = record.PrerequisiteSkillIds;
		for (int i = 0; i < prerequisiteSkillIds.Length; i++)
		{
			int prereqId = prerequisiteSkillIds[i];
			if (!TryComputeSkillDepth(prereqId, recordsById, visitStateById, depthById, traversalStack, out error))
			{
				return false;
			}

			int prereqDepth = depthById[prereqId] + 1;
			if (prereqDepth > depth)
			{
				depth = prereqDepth;
			}
		}

		traversalStack.RemoveAt(traversalStack.Count - 1);
		visitStateById[skillId] = SkillVisitState.Visited;
		depthById[skillId] = depth;
		return true;
	}
}

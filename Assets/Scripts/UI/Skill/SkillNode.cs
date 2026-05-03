using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class SkillNode : MonoBehaviour
{
	static readonly Color LockedColor = new Color(0.45f, 0.45f, 0.45f, 1f);
	static readonly Color UnlockedColor = Color.white;

	[SerializeField] int skillId;
	[SerializeField] Image targetImage;
	bool unlocked;

	public int SkillId => skillId;
	/// <summary>true 表示已解锁。</summary>
	public bool Unlocked => unlocked;

	[Header("表数据（运行时由 SkillGraphManager 写入，便于在 Inspector 查看）")]
	[SerializeField] string boundSkillName;
	[SerializeField, TextArea(1, 4)] string boundSkillDescription;
	[SerializeField] float boundBonusHp;
	[SerializeField] float boundBonusStamina;
	[SerializeField] float boundBonusAttack;
	[SerializeField] float boundBonusDefense;
	[SerializeField] int[] boundPrerequisiteSkillIds;

	public string SkillName => boundSkillName;
	public string SkillDescription => boundSkillDescription;
	public float BonusHp => boundBonusHp;
	public float BonusStamina => boundBonusStamina;
	public float BonusAttack => boundBonusAttack;
	public float BonusDefense => boundBonusDefense;
	public int[] PrerequisiteSkillIds => boundPrerequisiteSkillIds ?? System.Array.Empty<int>();

	Button _button;
	Image[] _visualImages;
	TextMeshProUGUI _nameLabel;
	SkillGraphPanel _panel;

	void Awake()
	{
		CacheReferences();
		RefreshVisual();
		if (_button != null)
		{
			_button.onClick.AddListener(OnButtonClicked);
		}
	}

	void OnDestroy()
	{
		if (_button != null)
		{
			_button.onClick.RemoveListener(OnButtonClicked);
		}
	}

	/// <summary>由 <see cref="SkillGraphManager"/> 根据技能表行写入。</summary>
	public void ApplyFromSheetRow(SheetTableBin sheet, int rowIndex)
	{
		float hp = boundBonusHp;
		float stamina = boundBonusStamina;
		float attack = boundBonusAttack;
		float defense = boundBonusDefense;

		if (sheet.TryGetCellAsFloat(rowIndex, "生命值", out float parsedHp))
		{
			hp = parsedHp;
		}
		if (sheet.TryGetCellAsFloat(rowIndex, "耐力值", out float parsedStamina))
		{
			stamina = parsedStamina;
		}
		if (sheet.TryGetCellAsFloat(rowIndex, "攻击力", out float parsedAttack))
		{
			attack = parsedAttack;
		}
		if (sheet.TryGetCellAsFloat(rowIndex, "防御力", out float parsedDefense))
		{
			defense = parsedDefense;
		}

		int[] prereq = System.Array.Empty<int>();
		if (sheet.TryGetCellAsListInt(rowIndex, "前置技能", out int[] parsedPrereq))
		{
			prereq = parsedPrereq != null ? (int[])parsedPrereq.Clone() : System.Array.Empty<int>();
		}

		ApplyBoundData(
			sheet.GetCell(rowIndex, "技能名"),
			sheet.GetCell(rowIndex, "技能描述"),
			hp,
			stamina,
			attack,
			defense,
			prereq);
	}

	public void SetSkillId(int value)
	{
		skillId = value;
	}

	public void SetGeneratedLabel(TextMeshProUGUI label)
	{
		_nameLabel = label;
	}

	public void ApplyBoundData(
		string skillName,
		string skillDescription,
		float bonusHp,
		float bonusStamina,
		float bonusAttack,
		float bonusDefense,
		int[] prerequisiteSkillIds)
	{
		boundSkillName = skillName ?? string.Empty;
		boundSkillDescription = skillDescription ?? string.Empty;
		boundBonusHp = bonusHp;
		boundBonusStamina = bonusStamina;
		boundBonusAttack = bonusAttack;
		boundBonusDefense = bonusDefense;
		boundPrerequisiteSkillIds = prerequisiteSkillIds != null
			? (int[])prerequisiteSkillIds.Clone()
			: System.Array.Empty<int>();

		CacheReferences();
		if (_nameLabel != null)
		{
			_nameLabel.text = boundSkillName;
		}

		RefreshVisual();
	}

	void OnButtonClicked()
	{
		if (_panel == null)
		{
			_panel = GetComponentInParent<SkillGraphPanel>();
		}

		if (_panel != null)
		{
			_panel.OnSkillNodeButtonClicked(this);
		}
	}

	/// <summary>由 <see cref="SkillGraphPanel.SyncUnlockVisuals"/> 和 <see cref="SkillGraphPanel.OnUnlockButtonClicked"/> 调用，仅更新视觉，不触发属性加成。</summary>
	public void SetUnlockedVisual(bool value)
	{
		unlocked = value;
		RefreshVisual();
	}

	void RefreshVisual()
	{
		CacheReferences();
		Color targetColor = unlocked ? UnlockedColor : LockedColor;
		if (_visualImages == null)
		{
			return;
		}

		for (int i = 0; i < _visualImages.Length; i++)
		{
			Image image = _visualImages[i];
			if (image == null)
			{
				continue;
			}

			image.color = targetColor;
		}
	}

	void CacheReferences()
	{
		if (_button == null)
		{
			_button = GetComponent<Button>();
		}

		if (_nameLabel == null)
		{
			_nameLabel = GetComponentInChildren<TextMeshProUGUI>(true);
		}

		if (_panel == null)
		{
			_panel = GetComponentInParent<SkillGraphPanel>();
		}

		if (targetImage != null)
		{
			_visualImages = new[] { targetImage };
			return;
		}

		Image[] candidateImages = GetComponentsInChildren<Image>(true);
		List<Image> scopedImages = new List<Image>(candidateImages.Length);
		for (int i = 0; i < candidateImages.Length; i++)
		{
			Image image = candidateImages[i];
			if (image == null)
			{
				continue;
			}

			SkillNode owner = image.GetComponentInParent<SkillNode>();
			if (owner == this)
			{
				scopedImages.Add(image);
			}
		}

		_visualImages = scopedImages.ToArray();
	}
}

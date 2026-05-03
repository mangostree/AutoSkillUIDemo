using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class SkillDescription : MonoBehaviour
{
	public TextMeshProUGUI skillNameText;
	public TextMeshProUGUI skillDescriptionText;
	public Button unlockButton; 

	public void SetSkillInfo(string name, string description)
	{
		if (skillNameText != null)
			skillNameText.text = name ?? string.Empty;
		if (skillDescriptionText != null)
			skillDescriptionText.text = description ?? string.Empty;
	}
}

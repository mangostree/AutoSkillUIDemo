using System.IO;
using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(SkillGraphPanel))]
public class SkillGraphPanelEditor : Editor
{
	public override void OnInspectorGUI()
	{
		DrawDefaultInspector();

		EditorGUILayout.Space();
		if (!GUILayout.Button("选择技能表 .bytes 并重建 SkillGraphPanel"))
		{
			return;
		}

		string defaultDirectory = Path.Combine(Application.dataPath, "Resources", "Sheets");
		string bytesFilePath = EditorUtility.OpenFilePanel(
			"选择技能表 .bytes",
			defaultDirectory,
			"bytes");

		if (string.IsNullOrEmpty(bytesFilePath))
		{
			return;
		}

		if (!SkillGraphPanel.RebuildPanelPrefabFromBytes(bytesFilePath, out string error))
		{
			EditorUtility.DisplayDialog("Skill Graph 重建失败", error, "确定");
			Debug.LogError(error);
			return;
		}

		EditorUtility.DisplayDialog(
			"Skill Graph 重建完成",
			$"已根据文件重建 `{SkillGraphPanel.SkillGraphPanelPrefabPath}`\n{bytesFilePath}",
			"确定");
	}
}

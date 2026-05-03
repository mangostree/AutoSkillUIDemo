#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace GIGA.AutoRadialLayout.Editor
{
	[CustomEditor(typeof(RadialLayoutFactory))]
	public class RadialLayoutFactoryEditor : UnityEditor.Editor
	{
		// Styles
		GUIStyle style_header;

		// Serialized Properties
		SerializedProperty s_nodePool;
		SerializedProperty s_destroyAfterBuild;
		SerializedProperty s_useQuerySystem;
		SerializedProperty s_prefabBasedInstancing;
		SerializedProperty s_runOnStart;
		SerializedProperty s_clearLayoutBeforeBuild;
		SerializedProperty s_buildDelegate;

		private void OnEnable()
		{
			s_nodePool = serializedObject.FindProperty("nodePool");
			s_destroyAfterBuild = serializedObject.FindProperty("destroyAfterBuild");
			s_useQuerySystem = serializedObject.FindProperty("useQuerySystem");
			s_prefabBasedInstancing = serializedObject.FindProperty("prefabBasedInstancing");
			s_runOnStart = serializedObject.FindProperty("runOnStart");
			s_clearLayoutBeforeBuild = serializedObject.FindProperty("clearLayoutBeforeBuild");
			s_buildDelegate = serializedObject.FindProperty("buildDelegate");
		}

		public override void OnInspectorGUI()
		{
			serializedObject.Update();

			var t = target as RadialLayoutFactory;

			// Creating styles if null
			if (this.style_header == null)
			{
				this.style_header = new GUIStyle("box");
				style_header.stretchWidth = true;
				style_header.normal.background = RadialLayoutEditor.MakeTex(2, 2, RadialLayoutEditor.headerColor);
				style_header.normal.textColor = Color.white;
				style_header.alignment = TextAnchor.MiddleLeft;
				style_header.fontStyle = FontStyle.Bold;
			}

			// Checking for RadialLayout component
			if(t.gameObject.GetComponent<RadialLayout>() == null)
				EditorGUILayout.HelpBox("RadialLayout component not found. Please place this component on a layout that contains the layout component.", MessageType.Error);

			EditorGUILayout.Space(10);

			// Settings
			EditorGUILayout.BeginHorizontal(style_header);
			EditorGUILayout.LabelField("Building Settings",style_header);
			EditorGUILayout.EndHorizontal();
			EditorGUI.indentLevel++;

			EditorGUILayout.LabelField("Building Behaviour", EditorStyles.boldLabel);
			EditorGUI.indentLevel++;
			EditorGUILayout.PropertyField(s_runOnStart);
			EditorGUILayout.PropertyField(s_clearLayoutBeforeBuild);
			EditorGUILayout.PropertyField(s_destroyAfterBuild);
			EditorGUI.indentLevel--;

			EditorGUILayout.Space(10);
			EditorGUILayout.PropertyField(s_nodePool);
			EditorGUILayout.Space(10);

			EditorGUILayout.LabelField("Layout Settings", EditorStyles.boldLabel);
			EditorGUI.indentLevel++;
			EditorGUILayout.PropertyField(s_useQuerySystem);
			EditorGUILayout.PropertyField(s_prefabBasedInstancing);
			EditorGUI.indentLevel--;


			EditorGUI.indentLevel--;

			// Build Delegate
			EditorGUILayout.Space(10);
			EditorGUILayout.BeginHorizontal(style_header);
			EditorGUILayout.LabelField("Build Delegate", style_header);
			EditorGUILayout.EndHorizontal();
			EditorGUILayout.HelpBox("This is the function that will be called when the building process starts and contains your building logic.",MessageType.None);
			// Checking assignment
			if (t.buildDelegate.GetPersistentEventCount() == 0 || t.buildDelegate.GetPersistentTarget(0) == null || string.IsNullOrEmpty(t.buildDelegate.GetPersistentMethodName(0)))
				EditorGUILayout.HelpBox("No delegate assigned, building process will not work.", MessageType.Warning);
			else if(t.buildDelegate.GetPersistentListenerState(0) == UnityEventCallState.Off)
				EditorGUILayout.HelpBox("Execution is set to Off, building process will not work.", MessageType.Warning);

			EditorGUILayout.PropertyField(s_buildDelegate);


			// Actions
			EditorGUILayout.BeginHorizontal(style_header);
			EditorGUILayout.LabelField("Actions",style_header);
			EditorGUILayout.EndHorizontal();
			EditorGUI.indentLevel++;

			if (GUILayout.Button("Build"))
			{
				// Checking if the listener is set
				if (t.buildDelegate.GetPersistentEventCount() == 0 || t.buildDelegate.GetPersistentTarget(0) == null || string.IsNullOrEmpty(t.buildDelegate.GetPersistentMethodName(0)))
				{
					EditorUtility.DisplayDialog("Error", "Build delegate method is not set. Assign a valid method in the Build Delegate listener above.", "Ok");
					return;
				}

				if (EditorUtility.DisplayDialog("Build Layout", "Rebuild layout?\n\nWARNING: all nodes will be destroyed if the \"Clear Layout Before Build\" option is checked.", "Yes", "No"))
				{
					if(t.buildDelegate.GetPersistentListenerState(0) != UnityEventCallState.EditorAndRuntime)
						EditorUtility.DisplayDialog("Error", $"Build delegate execution is set to: {t.buildDelegate.GetPersistentListenerState(0)}.\nMake sure to set to EditorAndRuntime if you want to enable procedural building inside the editor.", "Ok");

					t.Build();
				}
			}

			if (GUILayout.Button("Clear Layout"))
			{
				if (EditorUtility.DisplayDialog("Clear Layout", "Clear layout?\n\nWARNING: all nodes will be destroyed.", "Yes", "No"))
				{
					t.ClearLayout();
				}
			}

			EditorGUI.indentLevel--;

			// Version label
			RadialLayoutEditor.DrawVersionLabel();

			serializedObject.ApplyModifiedProperties();
		}
	}
}

#endif

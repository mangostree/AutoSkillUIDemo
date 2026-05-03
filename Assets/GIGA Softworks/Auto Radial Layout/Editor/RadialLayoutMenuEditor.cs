#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace GIGA.AutoRadialLayout.RadialMenu.Editor
{
	[CustomEditor(typeof(RadialLayoutMenu))]
	public class RadialLayoutMenuEditor : UnityEditor.Editor
	{
		// Styles
		GUIStyle style_header, style_header_gray;

		// Serialized Properties
		SerializedProperty s_startingVisibility;
		SerializedProperty s_startingOpenState;
		SerializedProperty s_childVisibility;
		SerializedProperty s_hideWhenClickingNodes; 
		SerializedProperty s_closeWhenClickingNodes;
		SerializedProperty s_showLinksForChildNodes;
		SerializedProperty s_visibilityAnimation;
		SerializedProperty s_showAnimationSpeed;
		SerializedProperty s_openingAnimation;
		SerializedProperty s_closingAnimation;
		SerializedProperty s_openAnimation_expand;
		SerializedProperty s_openAnimation_rotate;
		SerializedProperty s_openAnimation_fade;
		SerializedProperty s_openingAnimationSpeed;
		SerializedProperty s_canvasGroup;
		SerializedProperty s_layout;
		SerializedProperty s_animator;
		SerializedProperty s_labelsRoot;
		SerializedProperty s_innerNodeAction;


		private bool showReferences;

		private void OnEnable()
		{
			s_startingVisibility = serializedObject.FindProperty("startingVisibility");
			s_startingOpenState = serializedObject.FindProperty("startingOpenState");
			s_childVisibility = serializedObject.FindProperty("childVisibility");
			s_hideWhenClickingNodes = serializedObject.FindProperty("hideWhenClickingNodes");
			s_closeWhenClickingNodes = serializedObject.FindProperty("closeWhenClickingNodes");
			s_showLinksForChildNodes = serializedObject.FindProperty("showLinksForChildNodes");
			s_visibilityAnimation = serializedObject.FindProperty("visibilityAnimation");
			s_showAnimationSpeed = serializedObject.FindProperty("showAnimationSpeed");
			s_openingAnimation = serializedObject.FindProperty("openingAnimation");
			s_closingAnimation = serializedObject.FindProperty("closingAnimation");
			s_openAnimation_expand = serializedObject.FindProperty("openAnimation_expand");
			s_openAnimation_rotate = serializedObject.FindProperty("openAnimation_rotate");
			s_openAnimation_fade = serializedObject.FindProperty("openAnimation_fade");
			s_openingAnimationSpeed = serializedObject.FindProperty("openingAnimationSpeed");
			s_canvasGroup = serializedObject.FindProperty("canvasGroup");
			s_layout = serializedObject.FindProperty("layout");
			s_animator = serializedObject.FindProperty("animator");
			s_labelsRoot = serializedObject.FindProperty("labelsRoot");
			s_innerNodeAction = serializedObject.FindProperty("innerNodeAction");
		}

		public override void OnInspectorGUI()
		{
			serializedObject.Update();

			var t = target as RadialLayoutMenu;

			// Creating styles if null
			if (this.style_header == null)
			{
				this.style_header = new GUIStyle("box");
				style_header.stretchWidth = true;
				style_header.normal.background = GIGA.AutoRadialLayout.Editor.RadialLayoutEditor.MakeTex(2, 2, GIGA.AutoRadialLayout.Editor.RadialLayoutEditor.headerColor);
				style_header.normal.textColor = Color.white;
				style_header.alignment = TextAnchor.MiddleLeft;
				style_header.fontStyle = FontStyle.Bold;

				this.style_header_gray = new GUIStyle(this.style_header);
				style_header_gray.normal.background = GIGA.AutoRadialLayout.Editor.RadialLayoutEditor.MakeTex(2, 2, new Color(0.75f,0.75f,0.75f,1));
				style_header_gray.normal.textColor = Color.black;

			}

			// Initial checks
			if(t.GetComponent<RadialLayout>() == null || t.layout == null)
				EditorGUILayout.HelpBox("Radial Menu requires a RadialLayout component to work and it has to be assigned in the Layout property.", MessageType.Error);

			EditorGUILayout.Space(10);

			// Initialization
			EditorGUILayout.BeginHorizontal(style_header);
			EditorGUILayout.LabelField("Radial Menu Initialization",style_header);
			EditorGUILayout.EndHorizontal();
			EditorGUI.indentLevel++;

			s_startingVisibility.enumValueIndex = (int)(RadialLayoutMenu.VisibilityState)EditorGUILayout.EnumPopup(new GUIContent("Starting Visibility", "Determines if the menu will be visible or not at startup."), (RadialLayoutMenu.VisibilityState)s_startingVisibility.enumValueIndex);
			s_startingOpenState.enumValueIndex = (int)(RadialLayoutMenu.StartingOpenState)EditorGUILayout.EnumPopup(new GUIContent("Starting Open State", "Determines if the menu will be open or closed at startup."), (RadialLayoutMenu.StartingOpenState)s_startingOpenState.enumValueIndex);

			EditorGUI.indentLevel--;

			// Settings
			EditorGUILayout.Space(10);
			EditorGUILayout.BeginHorizontal(style_header);
			EditorGUILayout.LabelField("Radial Menu Settings", style_header);
			EditorGUILayout.EndHorizontal();
			EditorGUI.indentLevel++;

			s_childVisibility.enumValueIndex = (int)(RadialLayoutMenu.ChildNodeVisibility)EditorGUILayout.EnumPopup(new GUIContent("Show Child Nodes", "Always: child nodes are always visible.\n\nOn Mouse Over: child nodes are visible when passing mouse over parent nodes.\n\nOn Click: child nodes will show when clicking on parent nodes."), (RadialLayoutMenu.ChildNodeVisibility)s_childVisibility.enumValueIndex);

			s_hideWhenClickingNodes.boolValue = EditorGUILayout.Toggle(new GUIContent("Hide When Clicking Nodes", "Turn on if you want to hide the menu (disappear completely) when clicking on an action node."), s_hideWhenClickingNodes.boolValue);
			s_closeWhenClickingNodes.boolValue = EditorGUILayout.Toggle(new GUIContent("Close When Clicking Nodes", "Turn on if you want to close the menu when clicking on an action node."), s_closeWhenClickingNodes.boolValue);
			s_showLinksForChildNodes.boolValue = EditorGUILayout.Toggle(new GUIContent("Show Links For Child Nodes ", "Turn on if you want to add links to child nodes too, or off if you want to show only inner links. If you want to have a menu with no links at all, turn this and Show Inner Links both off, or unassign the Link Prefab property in the RadialLayout component."), s_showLinksForChildNodes.boolValue);

			s_innerNodeAction.enumValueIndex = (int)(RadialLayoutMenu.InnerNodeClickAction)EditorGUILayout.EnumPopup(new GUIContent("When Clicking Inner Node", "Open: opens / closes the menu when clicking the central node.\n\nHide: menu is hidden after clicking the central node.\n\nHide And Close: menu is hidden and closed after clicking the central node.\n\nDo Nothing: clicks on the central node are ignored."), (RadialLayoutMenu.InnerNodeClickAction)s_innerNodeAction.enumValueIndex);

			EditorGUI.indentLevel--;

			// Animations
			EditorGUILayout.Space(10);
			EditorGUILayout.BeginHorizontal(style_header);
			EditorGUILayout.LabelField("Radial Menu Animations", style_header);
			EditorGUILayout.EndHorizontal();
			EditorGUI.indentLevel++;

			// Visibility
			EditorGUILayout.LabelField("Visibility", EditorStyles.boldLabel);
			EditorGUI.indentLevel++;
			s_visibilityAnimation.enumValueIndex = (int)(RadialLayoutMenu.VisibilityAnimations)EditorGUILayout.EnumPopup(new GUIContent("Visibility Animation", "Shows/Hide the menu instantly or using a fading effect."), (RadialLayoutMenu.VisibilityAnimations)s_visibilityAnimation.enumValueIndex);
			if(s_visibilityAnimation.enumValueIndex != 0)
				s_showAnimationSpeed.floatValue = EditorGUILayout.FloatField(new GUIContent("Fading Speed", "Speed of the fading effect."), s_showAnimationSpeed.floatValue);
			EditorGUI.indentLevel--;

			// Opening
			EditorGUILayout.Space(10);
			EditorGUILayout.LabelField("Opening / Closing", EditorStyles.boldLabel);
			EditorGUI.indentLevel++;
			s_openingAnimation.enumValueIndex = (int)(RadialLayoutMenu.OpeningAnimations)EditorGUILayout.EnumPopup(new GUIContent("Opening Animation", "Opens the menu instantly or using the selected animated effects."), (RadialLayoutMenu.OpeningAnimations)s_openingAnimation.enumValueIndex);
			s_closingAnimation.enumValueIndex = (int)(RadialLayoutMenu.OpeningAnimations)EditorGUILayout.EnumPopup(new GUIContent("Closing Animation", "Closes the menu instantly or using the selected animated effects."), (RadialLayoutMenu.OpeningAnimations)s_closingAnimation.enumValueIndex);

			if (s_openingAnimation.enumValueIndex != 0 || s_closingAnimation.enumValueIndex != 0)
			{
				s_openingAnimationSpeed.floatValue = EditorGUILayout.FloatField(new GUIContent("Animation Speed", "Speed of the opening/closing animation."), s_openingAnimationSpeed.floatValue);
				s_openAnimation_expand.boolValue = EditorGUILayout.Toggle(new GUIContent("Expand Animation", ""), s_openAnimation_expand.boolValue);
				s_openAnimation_rotate.boolValue = EditorGUILayout.Toggle(new GUIContent("Rotate Animation", ""), s_openAnimation_rotate.boolValue);
				s_openAnimation_fade.boolValue = EditorGUILayout.Toggle(new GUIContent("Fade Animation", ""), s_openAnimation_fade.boolValue);

				// Showing warning if neither expand or fade is selected
				if (!s_openAnimation_fade.boolValue && !s_openAnimation_expand.boolValue)
					EditorGUILayout.HelpBox("You should activate at least expand or fade animations for a full closing effect.", MessageType.Warning);

				// Checking if layout is set to always
				if(t.layout != null && t.layout.autoRebuildMode != RadialLayout.AutoRebuildMode.Always)
					EditorGUILayout.HelpBox("Layout should be set to Auto Rebuild Mode: Always in order for animation to work.", MessageType.Warning);

			}
			EditorGUI.indentLevel--;


			EditorGUI.indentLevel--;


			// References
			EditorGUILayout.Space(10);
			EditorGUILayout.BeginHorizontal(style_header_gray);
			EditorGUILayout.LabelField("References", style_header_gray);
			EditorGUILayout.EndHorizontal();
			EditorGUI.indentLevel++;
			this.showReferences = EditorGUILayout.Toggle(new GUIContent("Show References", ""), this.showReferences);
			if (this.showReferences)
			{
				s_canvasGroup.objectReferenceValue = EditorGUILayout.ObjectField(new GUIContent("Canvas Group", ""), s_canvasGroup.objectReferenceValue, typeof(CanvasGroup), true);
				s_layout.objectReferenceValue = EditorGUILayout.ObjectField(new GUIContent("Radial Layout", ""), s_layout.objectReferenceValue, typeof(RadialLayout), true);
				s_animator.objectReferenceValue = EditorGUILayout.ObjectField(new GUIContent("Animator", ""), s_animator.objectReferenceValue, typeof(Animator), true);
				s_labelsRoot.objectReferenceValue = EditorGUILayout.ObjectField(new GUIContent("Labels Root", ""), s_labelsRoot.objectReferenceValue, typeof(GameObject), true);
			}
			EditorGUI.indentLevel--;

			serializedObject.ApplyModifiedProperties();

			// Version label
			AutoRadialLayout.Editor.RadialLayoutEditor.DrawVersionLabel();

		}
	}
}

#endif

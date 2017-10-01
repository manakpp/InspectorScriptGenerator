using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;

[CustomEditor(typeof(Main))]
public class MainEditor : Editor 
{
	private Main m_target;

	public override void OnInspectorGUI()
	{
		// This may not be used but leaving here for now (may make optional)
		m_target = (Main)target;

		serializedObject.Update();

		SerializedProperty prop = null;

		// Draw the script field
		prop = serializedObject.FindProperty("m_Script");
		EditorGUI.BeginDisabledGroup(true);
		EditorGUILayout.PropertyField(prop, true, new GUILayoutOption[0]);
		EditorGUI.EndDisabledGroup();

		// Draw all other properties
		prop = serializedObject.FindProperty("pubInt");
		EditorGUILayout.PropertyField(prop);

		prop = serializedObject.FindProperty("m_testInt");
		EditorGUILayout.PropertyField(prop);

		prop = serializedObject.FindProperty("m_testBool");
		EditorGUILayout.PropertyField(prop);

		prop = serializedObject.FindProperty("m_testFloat");
		EditorGUILayout.PropertyField(prop);

		serializedObject.ApplyModifiedProperties();
	}
}
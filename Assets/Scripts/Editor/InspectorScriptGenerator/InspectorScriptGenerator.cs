using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using System.Diagnostics;
using System.Globalization;
using System.Reflection;
using System.Runtime.InteropServices;
using System.IO;
using System.Text;

public static class InspectorScriptGenerator 
{
	private const string SCRIPT_NAME_KEY = "#SCRIPTNAME#";
	private const string SCRIPT_CONTENTS_KEY = "#SCRIPTCONTENTS#";
	private const string NEW_LINE = "\n";

	[MenuItem("Assets/Generate Inspector Script")]
	private static void GenerateInspectorScript()
	{
		Object selected = Selection.activeObject;
		MonoScript script = selected as MonoScript;
		System.Type type = script.GetClass();
		if (!IsValidType(type))
		{
			return;
		}

		// Create new script contents
		StringBuilder scriptStringBuilder = new StringBuilder();

		string templatePath = UnityEngine.Application.dataPath + "/Scripts/Editor/InspectorScriptGenerator/InspectorScriptTemplate.txt";
		using (FileStream fileStream = File.OpenRead(templatePath))
		{
			using (StreamReader streamReader = new StreamReader(fileStream))
			{
				while (streamReader.Peek() >= 0)
				{
					// Replace the name key with actual class name
					string line = streamReader.ReadLine();
					if (line.Contains(SCRIPT_NAME_KEY))
					{
						line = line.Replace(SCRIPT_NAME_KEY, type.ToString());
					}
					else if (line.Contains(SCRIPT_CONTENTS_KEY))
					{
						// Extract tabs
						int keyIndex = line.IndexOf(SCRIPT_CONTENTS_KEY);
						string tabIndentation = line.Remove(keyIndex, SCRIPT_CONTENTS_KEY.Length);

						// Replace ENTIRE line with properties (indentation is passed in to main the correct formatting)
						string propertiesString = CreatePropertiesString(type, tabIndentation);
						line = propertiesString;
					}

					scriptStringBuilder.Append(line);

					// Only add a new line if we haven't reached the end
					if (streamReader.Peek() >= 0)
					{
						scriptStringBuilder.Append(NEW_LINE);
					}
				}
			}
		}
		
		UnityEngine.Debug.Log(scriptStringBuilder.ToString());

		// Warning: Will override the existing file!! TODO: Prompt the user.
		string newScriptPath = UnityEngine.Application.dataPath + "/Scripts/Editor/" + type.ToString() + "Editor.cs";
		File.WriteAllText(newScriptPath, scriptStringBuilder.ToString());
	}

	private static string CreatePropertiesString(System.Type type, string tabIndentation)
	{
		StringBuilder scriptStringBuilder = new StringBuilder();

		List<FieldInfo> fieldInfo = GetFieldInfo(type);
		for (int i = 0; i < fieldInfo.Count; ++i)
		{
			FieldInfo info = fieldInfo[i];
			// Note: Default will render using the default PropertyField
			string line1 = tabIndentation + "prop = serializedObject.FindProperty(\"{0}\");" + NEW_LINE;
			string line2 = tabIndentation + "EditorGUILayout.PropertyField(prop);" + NEW_LINE;

			// Public fields and fields that have SerializedField will be rendered (May make this configurable later)
			bool isSerialized = info.Attributes == FieldAttributes.Public;
			if (!isSerialized)
			{
				isSerialized = info.GetCustomAttributes(typeof(SerializeField), false).Length > 0;
			}

			if (isSerialized)
			{
				// Spacing between the fields is done at the start instead of the end (just nicer formatting)
				if (i > 0)
				{
					scriptStringBuilder.Append(NEW_LINE);
				}

				// If this field is inherited, write a comment in
				if (info.DeclaringType != type)
				{
					string inheritedComment = tabIndentation + "// Inherited from class: {0}" + NEW_LINE;
					scriptStringBuilder.AppendFormat(inheritedComment, info.DeclaringType.ToString());
				}

				scriptStringBuilder.AppendFormat(line1, info.Name);
				scriptStringBuilder.Append(line2);
			}
		}

		return scriptStringBuilder.ToString();
	}

	private static List<MemberInfo> GetMemberInfo(System.Type type)
	{
		List<MemberInfo> allMemberInfo = new List<MemberInfo>();

		int iterCount = GetIterCountTillBaseClass(type);
		System.Type iterType = type;
		for (int i = 0; i < iterCount; ++i)
		{
			MemberInfo[] MemberInfo = iterType.GetMembers(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly);
			allMemberInfo.AddRange(MemberInfo);

			iterType = iterType.BaseType;

			if (type == null)
			{
				break;
			}
		}

		// Don't think the constructor is needed to removing here
		for (int i = allMemberInfo.Count - 1; i >= 0; --i)
		{
			if (allMemberInfo[i].MemberType == MemberTypes.Constructor)
			{
				allMemberInfo.Remove(allMemberInfo[i]);
			}
		}

		return allMemberInfo;
	}

	private static List<FieldInfo> GetFieldInfo(System.Type type)
	{
		List<FieldInfo> allFieldInfo = new List<FieldInfo>();

		int iterCount = GetIterCountTillBaseClass(type);
		System.Type iterType = type;
		for (int i = 0; i < iterCount; ++i)
		{
			FieldInfo[] FieldInfo = iterType.GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static);
			for (int j = 0; j < FieldInfo.Length; ++j)
			{
				FieldInfo info = FieldInfo[j];
				if (!allFieldInfo.Exists(x => x.FieldHandle == info.FieldHandle))
				{
					allFieldInfo.Add(info);
				}
			}

			iterType = iterType.BaseType;

			if (iterType == null)
			{
				break;
			}
		}

		return allFieldInfo;
	}

	private static List<PropertyInfo> GetPropertyInfo(System.Type type)
	{
		List<PropertyInfo> allPropertyInfo = new List<PropertyInfo>();

		int iterCount = GetIterCountTillBaseClass(type);
		System.Type iterType = type;
		for (int i = 0; i < iterCount; ++i)
		{
			PropertyInfo[] PropertyInfo = iterType.GetProperties(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static);
			for (int j = 0; j < PropertyInfo.Length; ++j)
			{
				PropertyInfo info = PropertyInfo[j];
				if (!allPropertyInfo.Exists(x => x.Name == info.Name))
				{
					allPropertyInfo.Add(info);
				}
			}

			iterType = iterType.BaseType;

			if (iterType == null)
			{
				break;
			}
		}

		return allPropertyInfo;
	}

	private static List<MethodInfo> GetMethodInfo(System.Type type)
	{
		List<MethodInfo> allMethodInfo = new List<MethodInfo>();

		int iterCount = GetIterCountTillBaseClass(type);
		System.Type iterType = type;
		for (int i = 0; i < iterCount; ++i)
		{
			MethodInfo[] MethodInfo = iterType.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static);
			for (int j = 0; j < MethodInfo.Length; ++j)
			{
				MethodInfo info = MethodInfo[j];
				if (!allMethodInfo.Exists(x => x.Name == info.Name))
				{
					allMethodInfo.Add(info);
				}
			}

			iterType = iterType.BaseType;

			if (iterType == null)
			{
				break;
			}
		}

		return allMethodInfo;
	}

	private static List<EventInfo> GetEventInfo(System.Type type)
	{
		List<EventInfo> allEventInfo = new List<EventInfo>();

		int iterCount = GetIterCountTillBaseClass(type);
		System.Type iterType = type;
		for (int i = 0; i < iterCount; ++i)
		{
			EventInfo[] EventInfo = iterType.GetEvents(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static);
			for (int j = 0; j < EventInfo.Length; ++j)
			{
				EventInfo info = EventInfo[j];
				if (!allEventInfo.Exists(x => x.Name == info.Name))
				{
					allEventInfo.Add(info);
				}
			}

			iterType = iterType.BaseType;

			if (iterType == null)
			{
				break;
			}
		}
		return allEventInfo;
	}
	
	/// <summary>
	/// Type will be MonoBehaviour or ScriptableObject if this was successful
	/// Log out type and base type so we know things are working (only interested in MonoBehaviour and ScriptableObject).
	/// </summary>
	/// <param name="type"></param>
	/// <returns></returns>
	private static bool IsValidType(System.Type type)
	{
		System.Type baseType = type;
		while (baseType != null)
		{
			if (baseType == typeof(MonoBehaviour) || baseType == typeof(ScriptableObject))
			{
				break;
			}

			baseType = baseType.BaseType;
		}
		
		if(baseType == null)
		{
			UnityEngine.Debug.Log(string.Format("Type {0} is not valid for generation", type));
			return false;
		}

		UnityEngine.Debug.Log(string.Format("Type: {0}, BaseType: {1}", type, baseType));
		return true;
	}

	/// <summary>
	/// Returns parent count until reaching MonoBehaviour or ScriptableObject
	/// </summary>
	/// <param name="type"></param>
	/// <returns></returns>
	private static int GetIterCountTillBaseClass(System.Type type)
	{
		int iterCount = 0;
		System.Type baseType = type.BaseType;
		while (baseType != null)
		{
			++iterCount;

			if (baseType == typeof(MonoBehaviour) || baseType == typeof(ScriptableObject))
			{
				break;
			}

			baseType = baseType.BaseType;
		}

		// deduct one because we dont include the MonoBehaviour or ScriptableObject as a step
		return iterCount;
	}
}

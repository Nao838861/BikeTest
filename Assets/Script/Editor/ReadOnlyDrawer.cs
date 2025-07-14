using UnityEngine;
using UnityEditor;

/// <summary>
/// ReadOnly属性のためのカスタムプロパティドローワー
/// </summary>
[CustomPropertyDrawer(typeof(ReadOnlyAttribute))]
public class ReadOnlyDrawer : PropertyDrawer
{
    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
    {
        // GUIを無効化して描画（読み取り専用にする）
        GUI.enabled = false;
        EditorGUI.PropertyField(position, property, label, true);
        GUI.enabled = true;
    }
}

using UnityEditor;
using UnityEngine;

namespace NTGD124
{
    // Draws a [GridAxisLabels] Vector2Int field's two components as "X"/"Z" instead of
    // Unity's default "X"/"Y" - BaseSlot.GridCoord uses its y component to store a world Z
    // coordinate, which read as "Y" in the Inspector caused real confusion when filling in
    // the 9 slots' grid coordinates by hand.
    [CustomPropertyDrawer(typeof(GridAxisLabelsAttribute))]
    public class GridAxisLabelsDrawer : PropertyDrawer
    {
        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            EditorGUI.BeginProperty(position, label, property);

            Rect fieldsRect = EditorGUI.PrefixLabel(position, label);

            SerializedProperty xProperty = property.FindPropertyRelative("x");
            SerializedProperty zProperty = property.FindPropertyRelative("y");

            float half = fieldsRect.width / 2f - 2f;
            Rect xRect = new(fieldsRect.x, fieldsRect.y, half, fieldsRect.height);
            Rect zRect = new(fieldsRect.x + half + 4f, fieldsRect.y, half, fieldsRect.height);

            float previousLabelWidth = EditorGUIUtility.labelWidth;
            EditorGUIUtility.labelWidth = 14f;
            EditorGUI.PropertyField(xRect, xProperty, new GUIContent("X"));
            EditorGUI.PropertyField(zRect, zProperty, new GUIContent("Z"));
            EditorGUIUtility.labelWidth = previousLabelWidth;

            EditorGUI.EndProperty();
        }
    }
}

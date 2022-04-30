using System.IO;
using UnityEditor;
using UnityEngine;

namespace ScriptableObjectPropertyDrawer.Editor
{
   public class EditSubAssetPopupContent : PopupWindowContent
   {
      class Styles
      {
         public readonly GUIStyle Title;
         public readonly GUIStyle Content;
         public readonly GUIStyle InlineEditorContent;
         public readonly Color32 HeaderBackgroundColor = new Color(0, 0, 0, 0.3f);

         public const float kHeaderHeight = 24f;
         public const float kLabelWidth = 60f;

         public Styles()
         {
            InlineEditorContent = new GUIStyle();
            InlineEditorContent.padding = new RectOffset(4, 4, 6, 6);

            Title = new GUIStyle(EditorStyles.boldLabel);
            Title.alignment = TextAnchor.MiddleCenter;
            Title.normal.textColor = new Color32(255, 237, 158, 255);

            Content = new GUIStyle();
            Content.padding = new RectOffset(2, 2, 4, 8);
         }
      }

      // ----------------------------------------------------------------------------------------
      // Constructors
      // ----------------------------------------------------------------------------------------

      public EditSubAssetPopupContent(Rect activatorRect, SerializedProperty editingPropery)
      {
         m_ActivatorRect = activatorRect;
         m_EditingProperty = editingPropery;
         m_EditingSubAsset = editingPropery.objectReferenceValue as ScriptableObject;
         m_InputSubAssetName = m_EditingSubAsset.name;
      }

      // ----------------------------------------------------------------------------------------
      // Overriden Methods
      // ----------------------------------------------------------------------------------------

      public override Vector2 GetWindowSize()
      {
         return new Vector2(m_ActivatorRect.width, 118);
      }

      public override void OnGUI(Rect rect)
      {
         if (s_Styles == null)
            s_Styles = new Styles();

         using (new GUILayout.AreaScope(rect))
         {
            var headerRect = GUILayoutUtility.GetRect(0, Styles.kHeaderHeight);
            EditorGUI.DrawRect(headerRect, s_Styles.HeaderBackgroundColor);
            GUI.Label(headerRect, "Edit Sub Asset", s_Styles.Title);

            using (new GUILayout.VerticalScope(s_Styles.Content))
            {
               float oldLabelWidth = EditorGUIUtility.labelWidth;
               EditorGUIUtility.labelWidth = Styles.kLabelWidth;

               m_InputSubAssetName = EditorGUILayout.TextField("Name", m_InputSubAssetName);

               bool areNameEqual = m_InputSubAssetName == m_EditingSubAsset.name;

               bool isInputNameValid = areNameEqual || !string.IsNullOrEmpty(m_InputSubAssetName);
               isInputNameValid &= m_InputSubAssetName.IndexOfAny(Path.GetInvalidFileNameChars()) < 0;

               EditorGUIUtility.labelWidth = oldLabelWidth;

               if (!isInputNameValid)
                  EditorGUILayout.HelpBox("Invalid name.", MessageType.Warning);

               using (new EditorGUI.DisabledGroupScope(areNameEqual || !isInputNameValid))
               {
                  using (new GUILayout.HorizontalScope())
                  {
                     if (GUILayout.Button("Rename"))
                        RenameSubAsset();

                     if (GUILayout.Button("Cancel"))
                     {
                        m_InputSubAssetName = m_EditingSubAsset.name;
                        GUI.FocusControl(null);
                     }
                  }
               }

               GUILayout.FlexibleSpace();

               if (m_WantsToDelete)
               {
                  GUILayout.Label($"Are you sure you want to delete \"{m_EditingSubAsset.name}\" sub asset?", EditorStyles.miniLabel);
                  using (new GUILayout.HorizontalScope())
                  {
                     if (GUILayout.Button("Yes"))
                     {
                        DeleteSubAsset();
                        editorWindow.Close();
                     }
                     if (GUILayout.Button("No"))
                        m_WantsToDelete = false;
                  }
               }
               else if (GUILayout.Button("Delete"))
                  m_WantsToDelete = true;
            }
         }
      }

      // ----------------------------------------------------------------------------------------
      // Methods
      // ----------------------------------------------------------------------------------------

      // it basically check if the object reference value is sub asset od property target object.
      public static bool IsPropertyEditable(SerializedProperty property)
      {
         if (property.objectReferenceValue == null)
            return false;

         if (!AssetDatabase.IsSubAsset(property.objectReferenceValue))
            return false;

         var targetObject = property.serializedObject.targetObject;
         if (!AssetDatabase.Contains(targetObject))
            return false;

         var targetObjectPath = AssetDatabase.GetAssetPath(targetObject);
         var objectReferencePath = AssetDatabase.GetAssetPath(property.objectReferenceValue);

         return targetObjectPath == objectReferencePath;
      }

      private void RenameSubAsset()
      {
         string path = AssetDatabase.GetAssetPath(m_EditingSubAsset);
         var mainAsset = AssetDatabase.LoadMainAssetAtPath(path);

         m_EditingSubAsset.name = m_InputSubAssetName;
         EditorUtility.SetDirty(m_EditingSubAsset);

         EditorUtility.SetDirty(mainAsset);
         AssetDatabase.SaveAssetIfDirty(mainAsset);
         AssetDatabase.ImportAsset(path);
      }

      private void DeleteSubAsset()
      {
         string path = AssetDatabase.GetAssetPath(m_EditingSubAsset);
         var mainAsset = AssetDatabase.LoadMainAssetAtPath(path);

         Object.DestroyImmediate(m_EditingSubAsset, true);
         EditorUtility.SetDirty(mainAsset);
         AssetDatabase.SaveAssetIfDirty(mainAsset);
         AssetDatabase.ImportAsset(path);

         m_EditingProperty.objectReferenceValue = null;
         m_EditingProperty.serializedObject.ApplyModifiedPropertiesWithoutUndo();
      }

      // ----------------------------------------------------------------------------------------
      // Private Fields
      // ----------------------------------------------------------------------------------------

      static Styles s_Styles;

      SerializedProperty m_EditingProperty;
      ScriptableObject m_EditingSubAsset;
      string m_InputSubAssetName;
      Rect m_ActivatorRect;
      bool m_WantsToDelete;
   }
}

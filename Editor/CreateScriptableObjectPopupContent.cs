using System;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace ScriptableObjectPropertyDrawer.Editor
{
   public class CreateScriptableObjectPopupContent : PopupWindowContent
   { 
      struct TypeItem
      {
         // initally the idea was to use GetMiniTypeThumbnail, but it didn't work as expected so I'm using the script icon.
         static Texture2D s_DefaultIcon = EditorGUIUtility.FindTexture("cs Script Icon");

         public Type Type;
         public GUIContent Content;
         public int IndexOfSearchIndexInTypename;

         public string Typename => Type.Name;

         public TypeItem(Type type) : this()
         {
            Type = type;
            Content = new GUIContent(type.Name, s_DefaultIcon, type.FullName);
         }
      }

      class Styles
      {
         public readonly GUIStyle HeaderTitle;
         public readonly GUIStyle SearchField;
         public readonly GUIStyle CreateAssetArea;
         public readonly GUIStyle TypeItem;
         public readonly GUIStyle TypeItemIcon;
         public readonly GUIStyle TypeTextField;
         public readonly Color32 SearchMatchSelectionColor;

         public const int kItemHeight = 18;

         public Styles()
         {
            SearchMatchSelectionColor = new Color32(255, 237, 158, 110);

            TypeItem = new GUIStyle("DD ItemStyle");
            TypeItem.padding = new RectOffset(20, 4, 1, 1);
            TypeItem.imagePosition = ImagePosition.TextOnly;

            TypeItemIcon = new GUIStyle();
            TypeItemIcon.padding = new RectOffset(4, 4, 2, 2);
            TypeItemIcon.imagePosition = ImagePosition.ImageOnly;

            TypeTextField = new GUIStyle(EditorStyles.textField);
            TypeTextField.imagePosition = ImagePosition.ImageLeft;

            CreateAssetArea = new GUIStyle();
            CreateAssetArea.padding = new RectOffset(12, 12, 8, 8);

            HeaderTitle = new GUIStyle(EditorStyles.boldLabel);
            HeaderTitle.alignment = TextAnchor.MiddleCenter;
            HeaderTitle.normal.textColor = new Color32(255, 237, 158, 255);

            SearchField = new GUIStyle(EditorStyles.toolbarSearchField);
            SearchField.margin = new RectOffset(12, 12, 0, 0);
         }
      }

      TypeItem SelectedTypeInfo
      {
         get
         {
            if (m_SelectedTypeItemIndex >= m_TypeItems.Length)
               return default;

            return m_TypeItems[m_SelectedTypeItemIndex];
         }
      }

      // ----------------------------------------------------------------------------------------
      // Constructors
      // ----------------------------------------------------------------------------------------

      public CreateScriptableObjectPopupContent(Rect activatorRect, Type baseType, Type[] types, SerializedProperty targetProperty, string dir)
      {
         m_TargetProperty = targetProperty;
         m_ActivatorRect = activatorRect;
         m_SaveAssetDestinationDir = dir;
         m_BaseType = baseType;
         m_SaveAssetName = $"New{baseType.Name}";
         m_SearchItemCount = int.MaxValue;

         if (baseType.IsAbstract || baseType.IsGenericType)
         {
            m_TypeItems = new TypeItem[types.Length];
            for (int i = 0; i < types.Length; i++)
               m_TypeItems[i] = new TypeItem(types[i]);
         }
         else
         {
            m_TypeItems = new TypeItem[types.Length + 1];
            m_TypeItems[0] = new TypeItem(baseType);
            for (int i = 0; i < types.Length; i++)
               m_TypeItems[i + 1] = new TypeItem(types[i]);
         }
      }

      // ----------------------------------------------------------------------------------------
      // Overriden Methods
      // ----------------------------------------------------------------------------------------

      public override Vector2 GetWindowSize()
      {
         float height = 182 + Mathf.Min(m_TypeItems.Length, 10) * 16;
         return new Vector2(m_ActivatorRect.width, height);
      }

      public override void OnGUI(Rect rect)
      {
         if (s_Styles == null)
            s_Styles = new Styles();

         using (new GUILayout.AreaScope(rect))
         {
            var headerRect = GUILayoutUtility.GetRect(0, 24);
            EditorGUI.DrawRect(headerRect, new Color(0, 0, 0, 0.3f));
            GUI.Label(headerRect, $"Create ScriptableObject ({m_BaseType.Name})", s_Styles.HeaderTitle);

            var oldLabelWidth = EditorGUIUtility.labelWidth;
            EditorGUIUtility.labelWidth = 64;

            using (new GUILayout.VerticalScope(s_Styles.CreateAssetArea))
            {
               m_SaveAssetName = EditorGUILayout.TextField("Name", m_SaveAssetName);
               m_SaveAssetDestinationDir = EditorGUILayout.TextField("Directory", m_SaveAssetDestinationDir);

               using (new GUILayout.HorizontalScope())
               {
                  EditorGUILayout.PrefixLabel("Type");

                  using (new EditorGUI.DisabledGroupScope(true))
                     EditorGUILayout.LabelField(SelectedTypeInfo.Content, s_Styles.TypeTextField);
               }

               GUILayout.Space(4f);

               using (new GUILayout.HorizontalScope())
               {
                  if (GUILayout.Button("Create"))
                  {
                     if (m_SaveAssetName.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0)
                     {
                        Debug.LogError($"\"{m_SaveAssetName}\" is not a valid asset name.");
                     }
                     else if (!AssetDatabase.IsValidFolder(m_SaveAssetDestinationDir))
                     {
                        Debug.LogError($"\"{m_SaveAssetDestinationDir}\" is not a valid path.");
                     }
                     else
                     {
                        CreateAsset(SelectedTypeInfo.Type, Path.Combine(m_SaveAssetDestinationDir, m_SaveAssetName + ".asset"));
                        editorWindow.Close();
                     }
                  }

                  if (GUILayout.Button("Create As..."))
                  {
                     var path = EditorUtility.OpenFilePanel("Create As...", m_SaveAssetDestinationDir, "asset");
                     CreateAsset(SelectedTypeInfo.Type, path);
                  }
               }

               using (new GUILayout.HorizontalScope())
               {
                  using (new EditorGUI.DisabledGroupScope(!AssetDatabase.Contains(m_TargetProperty.serializedObject.targetObject)))
                  {
                     if (GUILayout.Button("Create As SubAsset"))
                        CreateAssetAsSubAsset(SelectedTypeInfo.Type, m_SaveAssetName, m_TargetProperty.serializedObject.targetObject);
                  }
               }
            }

            EditorGUIUtility.labelWidth = oldLabelWidth;

            using (new GUILayout.HorizontalScope(EditorStyles.toolbar))
            {
               EditorGUI.BeginChangeCheck();
               m_SearchString = GUILayout.TextField(m_SearchString, s_Styles.SearchField);
               if (EditorGUI.EndChangeCheck())
               {
                  m_SearchString = m_SearchString.Trim();
                  if(!string.IsNullOrEmpty(m_SearchString))
                  {
                     m_SearchItemCount = 0;
                     for (int i = 0; i < m_TypeItems.Length; i++)
                     {
                        ref var item = ref m_TypeItems[i];
                        item.IndexOfSearchIndexInTypename = item.Typename.IndexOf(m_SearchString, StringComparison.OrdinalIgnoreCase);
                        if (item.IndexOfSearchIndexInTypename >= 0)
                           m_SearchItemCount++;
                     }
                  }
                  else 
                  {
                     m_ScrollPosition.y = Styles.kItemHeight * m_SelectedTypeItemIndex;
                     m_SearchItemCount = int.MaxValue;
                  }
               }
            }

            using (var scrollScope = new GUILayout.ScrollViewScope(m_ScrollPosition))
            {
               m_ScrollPosition = scrollScope.scrollPosition;
               
               bool hasSearch = !string.IsNullOrEmpty(m_SearchString);

               var oldSelectionColor = GUI.skin.settings.selectionColor;
               if(hasSearch)
                  GUI.skin.settings.selectionColor = s_Styles.SearchMatchSelectionColor;

               for (int i = 0, j = 0; i < m_TypeItems.Length; i++)
               {
                  if (j == m_SearchItemCount)
                     break;

                  ref var item = ref m_TypeItems[i];
                  if (hasSearch && item.IndexOfSearchIndexInTypename < 0)
                     continue;

                  j++;

                  var itemRect = GUILayoutUtility.GetRect(0, Styles.kItemHeight);
                  bool selected = m_SelectedTypeItemIndex == i;

                  switch (Event.current.type)
                  {
                     case EventType.Repaint:
                     {
                        if (hasSearch)
                        {
                           if (selected)
                              EditorGUI.DrawRect(itemRect, oldSelectionColor);

                           s_Styles.TypeItem.DrawWithTextSelection(
                              itemRect,
                              item.Content,
                              GUIUtility.keyboardControl,
                              item.IndexOfSearchIndexInTypename,
                              item.IndexOfSearchIndexInTypename + m_SearchString.Length
                           );
                        }
                        else
                        {
                           s_Styles.TypeItem.Draw(itemRect, item.Content, false, false, selected, selected);
                        }
                        
                        s_Styles.TypeItemIcon.Draw(itemRect, item.Content, false, false, false, false);
                     }
                     break;
                     case EventType.MouseDown:
                     {
                        if (itemRect.Contains(Event.current.mousePosition))
                        {
                           m_SelectedTypeItemIndex = i;
                           Event.current.Use();
                        }
                     }
                     break;
                  }
               }

               if (m_SearchItemCount == 0)
                  GUILayout.Label("No type found.", EditorStyles.centeredGreyMiniLabel);

               GUI.skin.settings.selectionColor = oldSelectionColor;
            }
         }
      }

      // ----------------------------------------------------------------------------------------
      // Methods
      // ----------------------------------------------------------------------------------------

      private ScriptableObject CreateAsset(Type type, string path)
      {
         var instance = ScriptableObject.CreateInstance(type);
         path = AssetDatabase.GenerateUniqueAssetPath(path);
         AssetDatabase.CreateAsset(instance, path);
         
         AssetDatabase.SaveAssets();

         EditorUtility.FocusProjectWindow();
         EditorGUIUtility.PingObject(instance);
         SetPropertyValue(instance);

         return instance;
      }

      private void CreateAssetAsSubAsset(Type type, string name, UnityEngine.Object mainAsset)
      {
         var instance = ScriptableObject.CreateInstance(type);
         instance.name = name;   

         AssetDatabase.AddObjectToAsset(instance, mainAsset);
         EditorUtility.SetDirty(mainAsset);
         AssetDatabase.SaveAssetIfDirty(mainAsset);
         AssetDatabase.ImportAsset(AssetDatabase.GetAssetPath(instance));

         EditorUtility.FocusProjectWindow();
         EditorGUIUtility.PingObject(mainAsset);
         SetPropertyValue(instance);
      }

      private void SetPropertyValue(ScriptableObject instance)
      {
         m_TargetProperty.objectReferenceValue = instance;
         m_TargetProperty.serializedObject.ApplyModifiedPropertiesWithoutUndo();
      }

      // ----------------------------------------------------------------------------------------
      // Private Fields
      // ----------------------------------------------------------------------------------------

      static Styles s_Styles;

      Rect m_ActivatorRect;
      TypeItem[] m_TypeItems;
      Type m_BaseType;
      SerializedProperty m_TargetProperty;
      Vector2 m_ScrollPosition;
      int m_SelectedTypeItemIndex;
      string m_SaveAssetDestinationDir;
      string m_SaveAssetName;
      int m_SearchItemCount;
      string m_SearchString;
   }
}

using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace ScriptableObjectPropertyDrawer.Editor
{
   [CustomPropertyDrawer(typeof(ScriptableObject), true)]
   public class ScriptableObjectPropertyDrawer : PropertyDrawer
   {
      const int k_ButtonWidth = 22;

      // ----------------------------------------------------------------------------------------
      // Overriden Methods
      // ----------------------------------------------------------------------------------------

      public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
      {
         EditorGUI.BeginProperty(position, label, property);
         position = EditorGUI.PrefixLabel(position, GUIUtility.GetControlID(FocusType.Passive), label);

         var indent = EditorGUI.indentLevel;
         EditorGUI.indentLevel = 0;

         bool isReferenceEditable = EditSubAssetPopupContent.IsPropertyEditable(property);

         var fieldPropertyRect = position;
         fieldPropertyRect.width -= isReferenceEditable ? k_ButtonWidth * 2 : k_ButtonWidth;
         EditorGUI.ObjectField(fieldPropertyRect, property, GUIContent.none);

         var buttonRect = position;
         buttonRect.x = fieldPropertyRect.xMax;
         buttonRect.width = k_ButtonWidth;

         if (isReferenceEditable)
         {
            if (GUI.Button(buttonRect, s_EditButtonContent, EditorStyles.centeredGreyMiniLabel))
            {
               var editSubAssetPopupContent = new EditSubAssetPopupContent(position, property);
               PopupWindow.Show(position, editSubAssetPopupContent);
            }
            buttonRect.x = buttonRect.xMax;
         }

         if (GUI.Button(buttonRect, s_AddButtonContent, EditorStyles.centeredGreyMiniLabel))
         {
            var baseType = fieldInfo.FieldType;
            var derivedTypes = GetAllTypesDerivedFrom(baseType);

            if ((baseType.IsGenericType || baseType.IsAbstract) && derivedTypes.Length == 0)
            {
               Debug.LogWarning($"No valid type was found to be create as an asset for this field. (Base Type: {baseType.Name})");
            }
            else
            {
               string dir = "Assets/";

               var targetObject = property.serializedObject.targetObject;
               if (AssetDatabase.Contains(targetObject))
               {
                  var path = AssetDatabase.GetAssetPath(targetObject.GetInstanceID());
                  dir = Path.GetDirectoryName(path);
               }

               var createSOPopupContent = new CreateScriptableObjectPopupContent(position, baseType, derivedTypes, property, dir);
               PopupWindow.Show(position, createSOPopupContent);
            }
         }

         EditorGUI.indentLevel = indent;
         EditorGUI.EndProperty();
      }

      // ----------------------------------------------------------------------------------------
      // Methods
      // ----------------------------------------------------------------------------------------

      // TODO: this method should be somewhere else
      static Type[] GetAllTypesDerivedFrom(Type baseType)
      {
         return AppDomain.CurrentDomain.GetAssemblies()
             .SelectMany(assembly => assembly.GetTypes())
             .Where(type => !type.IsAbstract && !type.IsGenericType && type.IsSubclassOf(baseType))
             .ToArray();
      }

      // ----------------------------------------------------------------------------------------
      // Private Fields
      // ----------------------------------------------------------------------------------------

      static GUIContent s_EditButtonContent = new GUIContent(EditorGUIUtility.FindTexture("editicon.sml"));
      static GUIContent s_AddButtonContent = new GUIContent(EditorGUIUtility.FindTexture("Toolbar Plus"));
   }
}

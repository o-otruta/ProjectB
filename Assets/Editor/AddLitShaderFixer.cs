// TODO(Post-MVP): Когда у всех способностей (и врагов) появятся реальные воксельные префабы, 
// fallback-код с `GameObject.CreatePrimitive` и `Shader.Find` в способностях нужно будет удалить.
// После этого ДАННЫЙ СКРИПТ ТАКЖЕ СЛЕДУЕТ УДАЛИТЬ, чтобы не тянуть лишние шейдеры в билд.

using UnityEngine;
using UnityEditor;
using UnityEngine.Rendering;

public class AddLitShaderFixer
{
    [InitializeOnLoadMethod]
    private static void AddShader()
    {
        var shader = Shader.Find("Universal Render Pipeline/Lit");
        if (shader == null) return;

        var graphicsSettings = AssetDatabase.LoadAssetAtPath<GraphicsSettings>("ProjectSettings/GraphicsSettings.asset");
        if (graphicsSettings == null) return;

        var serializedObject = new SerializedObject(graphicsSettings);
        var arrayProp = serializedObject.FindProperty("m_AlwaysIncludedShaders");

        bool hasShader = false;
        for (int i = 0; i < arrayProp.arraySize; ++i)
        {
            var arrayElem = arrayProp.GetArrayElementAtIndex(i);
            if (arrayElem.objectReferenceValue == shader)
            {
                hasShader = true;
                break;
            }
        }

        if (!hasShader)
        {
            int arrayIndex = arrayProp.arraySize;
            arrayProp.InsertArrayElementAtIndex(arrayIndex);
            var arrayElem = arrayProp.GetArrayElementAtIndex(arrayIndex);
            arrayElem.objectReferenceValue = shader;

            serializedObject.ApplyModifiedProperties();
            AssetDatabase.SaveAssets();
            Debug.Log("[Fix] Added URP Lit shader to Always Included Shaders.");
        }
    }
}

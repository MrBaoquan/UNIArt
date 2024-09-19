// using UnityEditor;
// using System.Reflection;

// internal static class CollapseExpand
// {
//     private static object[] m_ParametersExpand = new object[] { null, true };
//     private static object[] m_ParametersCollapse = new object[] { null, false };
//     private static System.Type m_SceneHierarchyWindowType = null;
//     private static System.Type SceneHierarchyWindowType
//     {
//         get
//         {
//             if (m_SceneHierarchyWindowType == null)
//             {
//                 var assembly = typeof(EditorWindow).Assembly;
//                 m_SceneHierarchyWindowType = assembly.GetType("UnityEditor.SceneHierarchyWindow");
//             }
//             return m_SceneHierarchyWindowType;
//         }
//     }
//     private static MethodInfo m_SetExpandedRecursive = null;
//     private static MethodInfo SetExpandedRecursiveImpl
//     {
//         get
//         {
//             if (m_SetExpandedRecursive == null)
//                 m_SetExpandedRecursive = m_SceneHierarchyWindowType.GetMethod(
//                     "SetExpandedRecursive"
//                 );
//             return m_SetExpandedRecursive;
//         }
//     }

//     public static void SetExpandedRecursive(int aInstanceID, bool aExpand)
//     {
//         var hierachyWindow = EditorWindow.GetWindow(SceneHierarchyWindowType);
//         if (aExpand)
//         {
//             m_ParametersExpand[0] = aInstanceID;
//             SetExpandedRecursiveImpl.Invoke(hierachyWindow, m_ParametersExpand);
//         }
//         else
//         {
//             m_ParametersCollapse[0] = aInstanceID;
//             SetExpandedRecursiveImpl.Invoke(hierachyWindow, m_ParametersCollapse);
//         }
//     }

//     [MenuItem("CONTEXT/GameObject/Expand GameObjects")]
//     [MenuItem("GameObject/Expand GameObjects", priority = 40)]
//     private static void ExpandGameObjects()
//     {
//         SetExpandedRecursive(Selection.activeGameObject.GetInstanceID(), true);
//     }

//     [MenuItem("CONTEXT/GameObject/Collapse GameObjects")]
//     [MenuItem("GameObject/Collapse GameObjects", priority = 40)]
//     private static void CollapseGameObjects()
//     {
//         SetExpandedRecursive(Selection.activeGameObject.GetInstanceID(), false);
//     }

//     [MenuItem("GameObject/Expand GameObjects", validate = true)]
//     [MenuItem("GameObject/Collapse GameObjects", validate = true)]
//     private static bool CanExpandOrCollapse()
//     {
//         return Selection.activeGameObject != null;
//     }
// }

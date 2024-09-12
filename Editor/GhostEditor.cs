using System;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using UNIArt.Runtime;

namespace UNIArt.Editor
{
    [CustomEditor(typeof(Ghost))]
    public class GhostEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            base.OnInspectorGUI();
            if (!GhostManager.IsReady())
                return;

            EditorGUILayout.Space();

            Ghost ghostComponent = (Ghost)target;
            var _isRestored = GhostManager.IsGhostRestored(ghostComponent);

            GUI.color = Color.green;
            if (_isRestored)
            {
                if (GUILayout.Button("Make Ghost"))
                {
                    GhostManager.GenerateGhostEntity(ghostComponent);
                }
            }
            else
            {
                if (GUILayout.Button("Restore Entity"))
                {
                    GhostManager.RestoreGhostEntity(ghostComponent);
                }
            }
            GhostManager.CheckRemoveNonBuiltinComponents(ghostComponent);
        }
    }

    public static class GhostManager
    {
        public static bool IsReady()
        {
            return GhostManagerPrototype != null;
        }

        public static bool IsGhostRestored(Ghost component)
        {
            return (bool)IsGhostRestoredMethod.Invoke(null, new object[] { component });
        }

        public static void GenerateGhostEntity(Ghost component)
        {
            GenerateGhostEntityMethod.Invoke(null, new object[] { component });
        }

        public static void RestoreGhostEntity(Ghost component)
        {
            RestoreGhostEntityMethod.Invoke(null, new object[] { component });
        }

        public static void CheckRemoveNonBuiltinComponents(Ghost component)
        {
            CheckRemoveNonBuiltinComponentsMethod.Invoke(null, new object[] { component });
        }

        private static Type ghostManager = null;
        private static Type GhostManagerPrototype
        {
            get
            {
                if (ghostManager == null)
                {
                    try
                    {
                        var _assembly = Assembly.Load("UNIHper.Ghost.Editor");
                        if (_assembly is null)
                            return null;

                        var _manager = _assembly.GetType("UNIHper.Ghost.Editor.GhostManager");
                        if (_manager is null)
                            return null;

                        ghostManager = _manager;
                    }
                    catch (System.Exception)
                    {
                        ghostManager = null;
                    }
                }
                return ghostManager;
            }
        }

        private static MethodInfo GenerateGhostEntityMethod =>
            GhostManagerPrototype.GetMethod(
                "GenerateGhostEntity",
                BindingFlags.Public | BindingFlags.Static
            );
        private static MethodInfo RestoreGhostEntityMethod =>
            GhostManagerPrototype.GetMethod(
                "RestoreGhostEntity",
                BindingFlags.Public | BindingFlags.Static
            );
        private static MethodInfo IsGhostRestoredMethod =>
            GhostManagerPrototype.GetMethod(
                "IsGhostRestored",
                BindingFlags.Public | BindingFlags.Static
            );
        private static MethodInfo CheckRemoveNonBuiltinComponentsMethod =>
            GhostManagerPrototype.GetMethod(
                "CheckRemoveNonBuiltinComponents",
                BindingFlags.Public | BindingFlags.Static
            );
    }
}

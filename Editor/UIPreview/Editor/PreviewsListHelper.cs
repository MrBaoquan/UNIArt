using System;
using System.Collections;
using System.Linq;
using UnityEngine;
using UnityEditor;

namespace Kamgam.UIPreview
{
    /// <summary>
    /// Uses reflection to get the List<IPreviewable> m_Previews from the InspectorWindow.
    /// The list is then reordered to make the preview of type T the first in the list.
    /// 
    /// This is necessary because the CustomPreview attribute does not have a mechanism
    /// for ordering the previews.
    /// </summary>
    public static class PreviewsListHelper<T> where T : class
    {
        public static bool CacheBuilt = false;
        public static bool TypesFound = false;

        private static Type[] s_editorWindowTypes;

        public static void ScheduleReorderPreviews()
        {
            // Call delayed to avoid changing the collection while iterating.
            EditorApplication.update -= ReorderPreviews;
            EditorApplication.update += ReorderPreviews;
        }

        public static void ReorderPreviews()
        {
            EditorApplication.update -= ReorderPreviews;

            if (!CacheBuilt) 
                BuildReflectionCache();

            if (!TypesFound)
                return;

            IList allInspectors = (IList)AllInspectorsInfo.GetValue(null);
            foreach (var inspectorWindow in allInspectors)
            {
                IList previews = (IList)PreviewsInfo.GetValue(inspectorWindow);
                if (previews != null && previews.Count > 0)
                {
                    // find UIPreview
                    int index = -1;
                    T firstPreview = null;
                    for (int i = 0; i < previews.Count; i++)
                    {
                        firstPreview = previews[i] as T;
                        if (firstPreview != null)
                        {
                            index = i;
                            break;
                        }
                    }

                    // make uiPreview the first
                    if (firstPreview != null)
                    {
                        var tmp = previews[0];
                        previews[0] = previews[index];
                        previews[index] = tmp;
                    }
                }
            }
        }

        #region Reflection Cache

        private static Type InspectorWindowType;
        private static System.Reflection.FieldInfo AllInspectorsInfo; // List<InspectorWindow> m_AllInspectors
        private static System.Reflection.FieldInfo PreviewsInfo; //  List<IPreviewable> m_Previews

        public static void BuildReflectionCache()
        {
            if (CacheBuilt)
                return;

            CacheBuilt = true;

            try
            {
                s_editorWindowTypes = typeof(EditorWindow).Assembly.GetTypes();
                if (s_editorWindowTypes != null && s_editorWindowTypes.Length > 0)
                {
                    InspectorWindowType = s_editorWindowTypes.FirstOrDefault(t => t.Name == "InspectorWindow");
                    if (InspectorWindowType != null)
                    {
                        AllInspectorsInfo = InspectorWindowType.GetField(
                            "m_AllInspectors",
                            System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Public);

                        PreviewsInfo = InspectorWindowType.GetField(
                            "m_Previews",
                            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

                        TypesFound = AllInspectorsInfo != null && PreviewsInfo != null;
                    }
                }
            }
            catch (Exception)
            {
                // fail silently
                TypesFound = false;
            }
        }

        #endregion
    }
}
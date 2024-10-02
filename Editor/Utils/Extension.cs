using UnityEngine;

namespace UNIArt.Editor
{
    public static class Extension
    {
        public static T AddOrGetComponent<T>(this GameObject go)
            where T : Component
        {
            if (go.GetComponent<T>() == null)
            {
                return go.AddComponent<T>();
            }
            return go.GetComponent<T>();
        }
    }
}

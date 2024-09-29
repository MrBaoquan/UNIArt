using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace PluginMaster
{
    [ExecuteInEditMode]
    public class ScaleWithScreen : MonoBehaviour
    {
        private void Update()
        {
            var unitsPerPixel = 9.6f / Screen.width;
            var desiredHalfHeight = 0.5f * unitsPerPixel * Screen.height;
            var yOffset = 2.7f - desiredHalfHeight;
            Camera.main.orthographicSize = desiredHalfHeight;
            Camera.main.transform.position = new Vector3(0f, yOffset, -10);
        }
    }
}
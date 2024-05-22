//----------------------------------------------
// Ruler 2D
// Copyright © 2015-2020 Pixel Fire™
//----------------------------------------------
namespace R2D
{   
    using UnityEngine;
    using UnityEditor;

    public class R2DD_VirtualCam
    {
        Camera cam;
        float scale;

        public R2DD_VirtualCam(Camera sceneCam)
        {
            cam = sceneCam;
            scale = EditorGUIUtility.pixelsPerPoint;
        }

        public Vector3 WorldToScreenPoint(Vector3 pos)
        {
            Vector3 screenPoint = cam.WorldToScreenPoint(pos);
            return screenPoint / scale;
        }

        public Vector3 ScreenToWorldPoint(Vector3 pos)
        {
            Vector3 worldPoint = cam.ScreenToWorldPoint(pos * scale);
            return worldPoint;
        }

        public float orthographicSize 
        {
            get { return cam.orthographicSize; }
        }

        public Rect pixelRect {
            get 
            { 
                Rect r = cam.pixelRect;
                r.Set(
                    r.x / scale,
                    r.y / scale,
                    r.width / scale,
                    r.height / scale
                    
                );

                return r;
            }
        }
    }
}


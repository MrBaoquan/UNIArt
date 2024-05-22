//----------------------------------------------
// Ruler 2D
// Copyright © 2015-2020 Pixel Fire™
//----------------------------------------------

namespace R2D
{
    using UnityEngine;
    using System.Collections.Generic;
    using System.Linq;

    public class R2DC_Settings
    {
        static R2DC_Settings instance;

        public static R2DC_Settings Instance
        {
            get
            {
                if (instance == null)
                {
                    instance = new R2DC_Settings();
                }

                return instance;
            }
        }

        public int contextIndex;
        public List<string> contextNames = new List<string>();
        public List<Context> availableContexts = new List<Context>();
        R2DD_State state;

        private R2DC_Settings()
        {
            state = R2DD_State.Instance;

            // Contexts list
            UpdateContextsList();
        }

        public void UpdateContextsList()
        {
            //// Context names drop down
            availableContexts.Clear();
            availableContexts.Add(new Context(ContextType.EditorScene, null));
            contextNames.Clear();
            contextNames.Add(R2DD_Lang.editorScene);

            object[] objs = GameObject.FindObjectsOfType(typeof(GameObject));

            var _defaultCanvas = GameObject.FindObjectOfType<Canvas>();
            if (_defaultCanvas == null)
            {
                _defaultCanvas = new GameObject("R2D Canvas").AddComponent<Canvas>();
                _defaultCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
                contextNames.Add(_defaultCanvas.name);
                availableContexts.Add(new Context(ContextType.Canvas, _defaultCanvas.gameObject));
            }
            else
            {
                // 默认Canvas设置
                var _allCanvas = GameObject.FindObjectsOfType<Canvas>();
                if (_allCanvas.Count() > 1)
                {
                    _allCanvas
                        .Where(_canvas => _canvas.gameObject.name == "R2D Canvas")
                        .ToList()
                        .ForEach(_canvas =>
                        {
                            GameObject.DestroyImmediate(_canvas.gameObject);
                        });
                }
                GameObject
                    .FindObjectsOfType<Canvas>()
                    .ToList()
                    .ForEach(_canvas =>
                    {
                        contextNames.Add(_canvas.name);
                        availableContexts.Add(new Context(ContextType.Canvas, _canvas.gameObject));
                    });

                // foreach (object obj in objs)
                // {
                //     GameObject gameObj = (GameObject)obj;

                //     // add any canvas to the list
                //     if (gameObj.GetComponent<Canvas>() != null)
                //     {
                //         availableContexts.Add(new Context(ContextType.Canvas, gameObj));
                //         contextNames.Add(gameObj.name);
                //     }
                //     // add any UIRoot to the list
                //     else if (R2DC_NGUI.Instance.HasNGUIRoot(gameObj))
                //     {
                //         availableContexts.Add(new Context(ContextType.NGUI, gameObj));
                //         contextNames.Add(gameObj.name);
                //     }
                // }
            }

            int contextIndex = 0;

            for (int i = 0; i < availableContexts.Count; i++)
            {
                if (state.context.instanceId == availableContexts[i].instanceId)
                {
                    contextIndex = i;
                    break;
                }

                if (
                    availableContexts[i].gameObject
                    && availableContexts[i].gameObject.GetComponent<Canvas>() != null
                )
                {
                    contextIndex = i;
                    break;
                }
            }

            SetContext(contextIndex);
        }

        public void SetContext(int pContextIndex)
        {
            contextIndex = pContextIndex;
            state.context = availableContexts[contextIndex];
            R2DC_Movement.Instance.error = R2DC_Movement.ADError.None;
        }
    }
}

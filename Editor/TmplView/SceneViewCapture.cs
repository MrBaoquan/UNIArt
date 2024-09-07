using System;
using UnityEditor;
using UnityEngine;

namespace UNIHper.Art.Editor
{
    public class SceneViewCapture
    {
        private static Color _rectangleColor = Color.green;
        private static float _rectangleWidth = 360;
        private static float _rectangleHeight = 240;
        private static Rect _rectangle;
        private static bool _isDrawing = false;

        // 控制拖动的变量
        private static bool _isDraggingEdge = false;
        private static Vector2 _dragStartPos;
        private static Vector2 _dragStartSize;
        private static int _selectedEdge = -1; // 0: left, 1: top, 2: right, 3: bottom

        private static Action<Rect> onCapture;

        public static void OnCapture(Action<Rect> onCapture)
        {
            SceneViewCapture.onCapture = onCapture;
        }

        public static void ShowCapture()
        {
            // 设置默认矩形的位置
            _rectangle = new Rect(100, 100, _rectangleWidth, _rectangleHeight);
            _isDrawing = true;
            SceneView.duringSceneGui += OnSceneGUI;
        }

        public static void HideCapture()
        {
            _isDrawing = false;
            SceneView.duringSceneGui -= OnSceneGUI;
        }

        private static int btnActionID = 0; // 1 确认 2取消

        private static void OnSceneGUI(SceneView sceneView)
        {
            // 如果按了esc，则隐藏截图窗口
            if (Event.current.type == EventType.KeyDown && Event.current.keyCode == KeyCode.Escape)
            {
                HideCapture();
                return;
            }

            if (!_isDrawing)
                return;

            // 如果按下了enter，则开始截图
            if (Event.current.type == EventType.KeyDown && Event.current.keyCode == KeyCode.Return)
            {
                btnActionID = 1;
            }

            Handles.BeginGUI();

            // 获取当前窗口的大小
            Vector2 windowSize = sceneView.position.size;

            // 捕获鼠标事件
            Event e = Event.current;

            // 定义矩形边缘的拖拽区域
            float edgeSize = 10f;
            Rect leftEdge = new Rect(
                _rectangle.x - edgeSize / 2,
                _rectangle.y,
                edgeSize,
                _rectangle.height
            );
            Rect rightEdge = new Rect(
                _rectangle.xMax - edgeSize / 2,
                _rectangle.y,
                edgeSize,
                _rectangle.height
            );
            Rect topEdge = new Rect(
                _rectangle.x,
                _rectangle.y - edgeSize / 2,
                _rectangle.width,
                edgeSize
            );
            Rect bottomEdge = new Rect(
                _rectangle.x,
                _rectangle.yMax - edgeSize / 2,
                _rectangle.width,
                edgeSize
            );

            // 绘制矩形边框（不填充）
            Handles.DrawSolidRectangleWithOutline(
                _rectangle,
                new Color(0, 0, 0, 0),
                _rectangleColor
            );

            // 在矩形右下角绘制一个按钮
            Rect confirmRect = new Rect(_rectangle.xMax - 40, _rectangle.yMax + 5, 40, 20);
            if (GUI.Button(confirmRect, "确定"))
            {
                btnActionID = 1;
            }

            Rect cancelRect = new Rect(_rectangle.xMax - 90, _rectangle.yMax + 5, 40, 20);
            if (GUI.Button(cancelRect, "取消"))
            {
                btnActionID = 2;
            }

            if (e.type == EventType.MouseDown && e.button == 0)
            {
                // 检查是否点击在矩形的边缘
                if (leftEdge.Contains(e.mousePosition))
                {
                    StartEdgeDrag(0, e);
                }
                else if (rightEdge.Contains(e.mousePosition))
                {
                    StartEdgeDrag(2, e);
                }
                else if (topEdge.Contains(e.mousePosition))
                {
                    StartEdgeDrag(1, e);
                }
                else if (bottomEdge.Contains(e.mousePosition))
                {
                    StartEdgeDrag(3, e);
                }
                else if (_rectangle.Contains(e.mousePosition))
                {
                    // 如果点击在矩形内部，则移动整个矩形
                    GUIUtility.hotControl = GUIUtility.GetControlID(FocusType.Passive);
                    _dragStartPos = e.mousePosition;
                    _dragStartSize = new Vector2(_rectangle.width, _rectangle.height);
                    e.Use();
                }
            }
            else if (e.type == EventType.MouseDrag && GUIUtility.hotControl != 0 && e.button == 0)
            {
                if (_isDraggingEdge)
                {
                    AdjustRectangleSize(e, windowSize);
                    e.Use();
                }
                else
                {
                    MoveRectangle(e, windowSize);
                    e.Use();
                }
            }
            else if (e.type == EventType.MouseUp && GUIUtility.hotControl != 0 && e.button == 0)
            {
                GUIUtility.hotControl = 0;
                _isDraggingEdge = false;
                _selectedEdge = -1;
                e.Use();
            }

            Handles.EndGUI();
            // 强制重绘SceneView
            sceneView.Repaint();

            if (btnActionID == 1)
            {
                btnActionID = 0;

                var _windowRect = sceneView.position;
                _windowRect.height += 21; // 实际测试， 窗口高度比实际测量少21像素

                var _deltaY =
                    sceneView.position.y
                    + (_windowRect.height - SceneView.currentDrawingSceneView.camera.pixelHeight);

                var _captureRect = _rectangle;
                _captureRect.y += _deltaY;

                HideCapture();

                onCapture.Invoke(_captureRect);
            }
            else if (btnActionID == 2)
            {
                HideCapture();
                btnActionID = 0;
            }
        }

        private static void StartEdgeDrag(int edge, Event e)
        {
            _isDraggingEdge = true;
            _selectedEdge = edge;
            _dragStartPos = e.mousePosition;
            _dragStartSize = new Vector2(_rectangle.width, _rectangle.height);
            GUIUtility.hotControl = GUIUtility.GetControlID(FocusType.Passive);
            e.Use();
        }

        private static void AdjustRectangleSize(Event e, Vector2 windowSize)
        {
            Vector2 delta = e.mousePosition - _dragStartPos;

            switch (_selectedEdge)
            {
                case 0: // Left
                    _rectangle.x = e.mousePosition.x;
                    _rectangle.width = Mathf.Max(10, _dragStartSize.x - delta.x);
                    if (_rectangle.x < 0) // 左边界限制
                    {
                        _rectangle.width += _rectangle.x;
                        _rectangle.x = 0;
                    }
                    break;
                case 1: // Top
                    _rectangle.y = e.mousePosition.y;
                    _rectangle.height = Mathf.Max(10, _dragStartSize.y - delta.y);
                    if (_rectangle.y < 0) // 上边界限制
                    {
                        _rectangle.height += _rectangle.y;
                        _rectangle.y = 0;
                    }
                    break;
                case 2: // Right
                    _rectangle.width = Mathf.Max(10, _dragStartSize.x + delta.x);
                    if (_rectangle.xMax > windowSize.x) // 右边界限制
                    {
                        _rectangle.width = windowSize.x - _rectangle.x;
                    }
                    break;
                case 3: // Bottom
                    _rectangle.height = Mathf.Max(10, _dragStartSize.y + delta.y);
                    if (_rectangle.yMax > windowSize.y) // 下边界限制
                    {
                        _rectangle.height = windowSize.y - _rectangle.y;
                    }
                    break;
            }
        }

        private static void MoveRectangle(Event e, Vector2 windowSize)
        {
            Vector2 newPosition = _rectangle.position + e.delta;

            // 确保矩形不会超出窗口范围
            newPosition.x = Mathf.Clamp(newPosition.x, 0, windowSize.x - _rectangle.width);
            newPosition.y = Mathf.Clamp(newPosition.y, 0, windowSize.y - _rectangle.height);

            _rectangle.position = newPosition;
        }

        public static void TakeScreenshot(Rect captureRect, string savedPath, Action onSaved = null)
        {
            EditorApplication.delayCall += () =>
            {
                var _targetWidth = (int)captureRect.width - 1;
                var _targetHeight = (int)captureRect.height - 1;

                Color[] pixels = UnityEditorInternal.InternalEditorUtility.ReadScreenPixel(
                    captureRect.position,
                    _targetWidth,
                    _targetHeight
                );

                Texture2D inspectorTexture = new Texture2D(
                    _targetWidth,
                    _targetHeight,
                    TextureFormat.RGB24,
                    false
                );
                inspectorTexture.SetPixels(pixels);

                byte[] bytes = inspectorTexture.EncodeToPNG();

                System.IO.File.WriteAllBytes(savedPath, bytes);
                AssetDatabase.Refresh();

                onSaved?.Invoke();
            };
        }
    }
}

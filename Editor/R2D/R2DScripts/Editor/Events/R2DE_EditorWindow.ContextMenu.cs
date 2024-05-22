//----------------------------------------------
// Ruler 2D
// Copyright © 2015-2020 Pixel Fire™ 
//----------------------------------------------

namespace R2D 
{
    using UnityEngine;
    using UnityEditor;
    using System;

    partial class R2DE_EditorWindow : EditorWindow 
    { 
        // Context menu entries
        [MenuItem(R2DD_Lang.contextAlignTopEdges, false, 49)]
        static void AlignTopEdges(MenuCommand menuCommand)
        {
            R2DC_Movement movement = GetAlignTool(menuCommand);
            if (movement != null) movement.AlignTop();
        }
            
        [MenuItem(R2DD_Lang.contextAlignVerticalCenters, false, 49)]
        static void AlignVerticalCenters(MenuCommand menuCommand)
        {
            R2DC_Movement movement = GetAlignTool(menuCommand);
            if (movement != null) movement.AlignYMid();
        }

        [MenuItem(R2DD_Lang.contextAlignBottomEdges, false, 49)]
        static void AlignBottomEdges(MenuCommand menuCommand)
        {
            R2DC_Movement movement = GetAlignTool(menuCommand);
            if (movement != null) movement.AlignBot();
        }
            
        [MenuItem(R2DD_Lang.contextAlignLeftEdges, false, 49)]
        static void AlignLeftEdges(MenuCommand menuCommand)
        {
            R2DC_Movement movement = GetAlignTool(menuCommand);
            if (movement != null) movement.AlignLeft();
        }

        [MenuItem(R2DD_Lang.contextAlignHorizontalCenters, false, 49)]
        static void AlignHorizontalCenters(MenuCommand menuCommand)
        {
            R2DC_Movement movement = GetAlignTool(menuCommand);
            if (movement != null) movement.AlignXMid();
        }
            
        [MenuItem(R2DD_Lang.contextAlignRightEdges, false, 49)]
        static void AlignRightEdges(MenuCommand menuCommand)
        {
            R2DC_Movement movement = GetAlignTool(menuCommand);
            if (movement != null) movement.AlignRight();
        }
            
        [MenuItem(R2DD_Lang.contextDistributeTopEdges, false, 49)]
        static void DistroTopEdges(MenuCommand menuCommand)
        {
            R2DC_Movement movement = GetDistroTool(menuCommand);
            if (movement != null) movement.DistroTop();
        }
            
        [MenuItem(R2DD_Lang.contextDistributeVerticalCenters, false, 49)]
        static void DistroVerticalCenters(MenuCommand menuCommand)
        {
            R2DC_Movement movement = GetDistroTool(menuCommand);
            if (movement != null) movement.DistroYMid();
        }
            
        [MenuItem(R2DD_Lang.contextDistributeBottomEdges, false, 49)]
        static void DistroBotEdges(MenuCommand menuCommand)
        {
            R2DC_Movement movement = GetDistroTool(menuCommand);
            if (movement != null) movement.DistroBot();
        }
            
        [MenuItem(R2DD_Lang.contextDistributeLeftEdges, false, 49)]
        static void DistroLeftEdges(MenuCommand menuCommand)
        {
            R2DC_Movement movement = GetDistroTool(menuCommand);
            if (movement != null) movement.DistroLeft();
        }

        [MenuItem(R2DD_Lang.contextDistributeHorizontalCenters, false, 49)]
        static void DistroHorizontalCenters(MenuCommand menuCommand)
        {
            R2DC_Movement movement = GetDistroTool(menuCommand);
            if (movement != null) movement.DistroXMid();
        }
            
        [MenuItem(R2DD_Lang.contextDistributeRightEdges, false, 49)]
        static void DistroRightEdges(MenuCommand menuCommand)
        {
            R2DC_Movement movement = GetDistroTool(menuCommand);
            if (movement != null) movement.DistroRight();
        }
            
        [MenuItem(R2DD_Lang.contextSnapToGuideLeft, false, 49)]
        static void SnapToGuideLeft(MenuCommand menuCommand)
        {
            R2DC_Movement movement = GetSnapTool(menuCommand);
            if (movement != null) movement.SnapLeft();
        }
            
        [MenuItem(R2DD_Lang.contextSnapToGuideRight, false, 49)]
        static void SnapToGuideRight(MenuCommand menuCommand)
        {
            R2DC_Movement movement = GetSnapTool(menuCommand);
            if (movement != null) movement.SnapRight();
        }
            
        [MenuItem(R2DD_Lang.contextSnapToGuideUp, false, 49)]
        static void SnapToGuideUp(MenuCommand menuCommand)
        {
            R2DC_Movement movement = GetSnapTool(menuCommand);
            if (movement != null) movement.SnapTop();
        }

        [MenuItem(R2DD_Lang.contextSnapToGuideDown, false, 49)]
        static void SnapToGuideDown(MenuCommand menuCommand)
        {
            R2DC_Movement movement = GetSnapTool(menuCommand);
            if (movement != null) movement.SnapBot();
        }

        static R2DC_Movement GetAlignTool(MenuCommand menuCommand)
        {
            if (CommandHandled(menuCommand))
            {
                return null;
            }

            R2DC_Movement movement = R2DC_Movement.Instance;
            R2DC_Selection.Instance.UpdateSelection();

            if (!movement.alignEnabled)
            {
                return null;
            }

            return movement;
        }

        static R2DC_Movement GetDistroTool(MenuCommand menuCommand)
        {
            if (CommandHandled(menuCommand))
            {
                return null;
            }
                
            R2DC_Movement movement = R2DC_Movement.Instance;
            R2DC_Selection.Instance.UpdateSelection();

            if (!movement.distroEnabled)
            {
                return null;
            }

            return movement;
        }

        static R2DC_Movement GetSnapTool(MenuCommand menuCommand)
        {
            if (CommandHandled(menuCommand))
            {
                return null;
            }
               
            R2DC_Movement movement = R2DC_Movement.Instance;
            R2DC_Selection.Instance.UpdateSelection();

            if (!movement.guideSnapEnabled)
            {
                return null;
            }

            return movement;
        }

        static bool CommandHandled(MenuCommand menuCommand)
        {
            return (Selection.objects.Length > 1 && menuCommand.context != Selection.objects[0]);
        }
    }
}

//----------------------------------------------
// Ruler 2D
// Copyright © 2015-2020 Pixel Fire™ 
//----------------------------------------------

namespace R2D {

	using UnityEngine;
	using UnityEditor;
	using System;
	using System.IO;
	
	public class R2DD_Resources 
    {
		static R2DD_Resources instance;
		
		public static R2DD_Resources Instance 
        {
			get 
            {
				if (instance == null) 
                {
					instance = new R2DD_Resources();
				}

				return instance;
			}
		}

		public Texture2D rulerHBg;
		public Texture2D rulerVBg;
		public Texture2D rulerLinePixel;
		public Texture2D corner;
        public Texture2D lockActive;
        public Texture2D lockInactive;
		public Texture2D pixel;
		public Texture logo;
		public Texture windowIcon;
		public Texture panelSettings;
		public Texture panelGrid; 
		public Texture panelToolbox;
		public Texture barToolbox;
		public Texture toolDistribute;
		public Texture toolPresets;
		public Texture2D help;
		public Font regularFont;
		public Texture2D crossHairPixel;
		public Texture2D liveGuidePixel;
        public Texture2D lockGuidePixel;
		public Texture2D guidePixel;
		public Texture guideIcon;
		public Texture2D coordBg;
		public Texture2D measureAlertBg;
		public Texture alignTop;
		public Texture alignYMid;
		public Texture alignBot;
		public Texture alignLeft;
		public Texture alignXMid;
		public Texture alignRight;
		public Texture distroTop;
		public Texture distroYMid;
		public Texture distroBot;
		public Texture distroLeft;
		public Texture distroXMid;
		public Texture distroRight;
		public Texture snapLeft;
		public Texture snapRight;
		public Texture snapTop;
		public Texture snapBot;
		public Texture spaceX; 
		public Texture spaceY;
		public Texture measureActive;
		public Texture measureInactive;
		public Texture clearMeasure;
		public Texture measureObj;
		public Texture2D measurePixel;
		public Texture2D measureCross;
		public Texture2D gridBorder;

		string r2dResourcesPath; 

		private R2DD_Resources() 
        {
			string[] dirs = Directory.GetDirectories(Environment.CurrentDirectory, 
			                                          pathResourcesDir,
			                                          SearchOption.AllDirectories);
			r2dResourcesPath = dirs[0].Replace(Environment.CurrentDirectory + Path.DirectorySeparatorChar, "") + Path.DirectorySeparatorChar;

			string skinPrefix = "";
		
            if (EditorGUIUtility.isProSkin) 
            {
				skinPrefix = pathPro + Path.DirectorySeparatorChar;
			}
			else 
            {
				skinPrefix = pathStandard + Path.DirectorySeparatorChar;
			}

			rulerHBg 		= LoadTexture2D(skinPrefix + pathRulerHBg);
			rulerVBg 		= LoadTexture2D(skinPrefix + pathRulerVBg);
			rulerLinePixel 	= LoadTexture2D(pathRulerLinePixel);
			corner			= LoadTexture2D(skinPrefix + pathImgCorner);
            lockActive      = LoadTexture2D(skinPrefix + pathImgLockActive);
            lockInactive      = LoadTexture2D(skinPrefix + pathImgLockInactive);
			pixel 			= LoadTexture2D(pathImgPixel);
			help			= LoadTexture2D(pathHelp);
			crossHairPixel 	= LoadTexture2D(pathCrossHairPixel);
			liveGuidePixel	= LoadTexture2D(pathLiveGuidePixel);
            lockGuidePixel  = LoadTexture2D(pathLockGuidePixel);
			guidePixel		= LoadTexture2D(pathGuidePixel);
			windowIcon		= LoadTexture(skinPrefix + pathWindowIcon);
			panelSettings	= LoadTexture(skinPrefix + pathToolSettings);
			panelGrid		= LoadTexture(skinPrefix + pathToolGrids);
			panelToolbox	= LoadTexture(skinPrefix + pathToolbox);
			toolDistribute	= LoadTexture(skinPrefix + pathToolDistribute);
			toolPresets		= LoadTexture(skinPrefix + pathToolPresets);
			guideIcon 		= LoadTexture(pathGuideIcon);
			coordBg 		= LoadTexture2D(skinPrefix + pathCoordBg);
			alignTop		= LoadTexture(skinPrefix + pathAlignTop);
			alignYMid		= LoadTexture(skinPrefix + pathAlignYMid); 
			alignBot		= LoadTexture(skinPrefix + pathAlignBot);
			alignLeft		= LoadTexture(skinPrefix + pathAlignLeft);
			alignXMid		= LoadTexture(skinPrefix + pathAlignXMid);
			alignRight		= LoadTexture(skinPrefix + pathAlignRight);
			distroTop		= LoadTexture(skinPrefix + pathDistroTop);
			distroYMid		= LoadTexture(skinPrefix + pathDistroYMid);
			distroBot		= LoadTexture(skinPrefix + pathDistroBot);
			distroLeft		= LoadTexture(skinPrefix + pathDistroLeft);
			distroXMid		= LoadTexture(skinPrefix + pathDistroXMid);
			distroRight		= LoadTexture(skinPrefix + pathDistroRight);
			snapLeft		= LoadTexture(skinPrefix + pathSnapLeft);
			snapRight		= LoadTexture(skinPrefix + pathSnapRight);
			snapBot			= LoadTexture(skinPrefix + pathSnapBot);
			snapTop			= LoadTexture(skinPrefix + pathSnapTop);
			spaceX			= LoadTexture(skinPrefix + pathSpaceX);
			spaceY			= LoadTexture(skinPrefix + pathSpaceY);
			barToolbox		= LoadTexture(skinPrefix + pathBarToolbox);
			measureActive	= LoadTexture(skinPrefix + pathMeasureActive);
			measureInactive = LoadTexture(skinPrefix + pathMeasureInactive); 
			clearMeasure	= LoadTexture(skinPrefix + pathClearMeasure);
			measureObj		= LoadTexture(skinPrefix + pathMeasureObj);
			measurePixel	= LoadTexture2D(pathMeasurePixel);
			measureCross	= LoadTexture2D(pathMeasureCross);
			measureAlertBg	= LoadTexture2D(skinPrefix + pathMeasureAlertBg);
			gridBorder		= LoadTexture2D(pathGridBorder);
			logo			= LoadTexture(skinPrefix + pathLogo);

			regularFont = LoadFont(pathFntRegular);
		}

		Texture2D LoadTexture2D(string textureName) 
        {
			return AssetDatabase.LoadAssetAtPath(r2dResourcesPath + textureName, typeof(Texture2D)) as Texture2D;
		}

		Texture LoadTexture(string textureName) 
        {
			return AssetDatabase.LoadAssetAtPath(r2dResourcesPath + textureName, typeof(Texture)) as Texture;
		}

		Font LoadFont(string fontName) 
        {
			return AssetDatabase.LoadAssetAtPath(r2dResourcesPath + fontName, typeof(Font)) as Font;
		}

		Sprite LoadSprite(string spriteName) 
        {
			return AssetDatabase.LoadAssetAtPath(r2dResourcesPath + spriteName, typeof(Sprite)) as Sprite;
		}
		
		const string pathResourcesDir = "R2DResources";
		const string pathPro = "Pro";
		const string pathStandard = "Personal";
		const string pathRulerHBg = "R2DImgRulerHBg.png";
		const string pathRulerVBg = "R2DImgRulerVBg.png";
		const string pathRulerLinePixel = "R2DImgRulerLinePixel.png";
		const string pathImgCorner = "R2DImgCorner.png";
        const string pathImgLockActive = "R2DImgLockActive.png";
        const string pathImgLockInactive = "R2DImgLockInactive.png";
		const string pathImgPixel = "R2DImgPixel.png";
		const string pathFntRegular = "R2DFntMain.ttf";
		const string pathWindowIcon = "R2DImgLogo.png";
		const string pathToolSettings = "R2DImgSettings.png";
		const string pathToolGrids = "R2DImgGrid.png";
		const string pathToolbox = "R2DImgToolbox.png";
		const string pathToolDistribute	= "R2DImgDistribute.png";
		const string pathToolPresets = "R2DImgPresets.png";
		const string pathHelp = "R2DImgHelp.png";
		const string pathCrossHairPixel	= "R2DImgCrossHairPixel.png";
		const string pathGuideIcon = "R2DImgGuideIcon.png";
		const string pathLiveGuidePixel	= "R2DImgLiveGuidePixel.png";
        const string pathLockGuidePixel = "R2DImgLockGuidePixel.png";
		const string pathGuidePixel = "R2DImgGuidePixel.png";
		const string pathCoordBg = "R2DImgCoordBg.png";
		const string pathMeasureAlertBg	= "R2DImgMeasureAlertBg.png";
		const string pathAlignTop = "R2DImgAlignTop.png";
		const string pathAlignYMid = "R2DImgAlignHMid.png";
		const string pathAlignBot = "R2DImgAlignBot.png";
		const string pathAlignLeft = "R2DImgAlignLeft.png";
		const string pathAlignXMid = "R2DImgAlignVMid.png";
		const string pathAlignRight = "R2DImgAlignRight.png";
		const string pathDistroTop = "R2DImgDistroTop.png";
		const string pathDistroYMid = "R2DImgDistroYMid.png";
		const string pathDistroBot = "R2DImgDistroBot.png";
		const string pathDistroLeft = "R2DImgDistroLeft.png";
		const string pathDistroXMid = "R2DImgDistroXMid.png";
		const string pathDistroRight = "R2DImgDistroRight.png";
		const string pathSnapLeft = "R2DImgSnapLeft.png";
		const string pathSnapRight = "R2DImgSnapRight.png";
		const string pathSnapTop = "R2DImgSnapTop.png";
		const string pathSnapBot = "R2DImgSnapBot.png";
		const string pathSpaceX = "R2DImgSpaceX.png";
		const string pathSpaceY = "R2DImgSpaceY.png";
		const string pathBarToolbox = "R2DImgToolboxBar.png";
		const string pathMeasureActive = "R2DImgMeasureActive.png";
		const string pathMeasureInactive = "R2DImgMeasureInactive.png";
		const string pathClearMeasure = "R2DImgClearMeasure.png";
		const string pathMeasurePixel = "R2DImgMeasurePixel.png";
		const string pathMeasureCross = "R2DImgMeasureCross.png"; 
		const string pathMeasureObj = "R2DImgMeasureObj.png";
		const string pathGridBorder = "R2DImgGridBorder.png";
		const string pathLogo = "R2DImgPFLogo.png";

		public const string urlSettingsHelp	= "http://pixelfire.co/ruler-2d-guides-grid-and-alignment-tools-for-unity/#settings"; 
		public const string urlGridHelp = "http://pixelfire.co/ruler-2d-guides-grid-and-alignment-tools-for-unity/#grid";
		public const string urlToolboxHelp = "http://pixelfire.co/ruler-2d-guides-grid-and-alignment-tools-for-unity/#toolbox";
	}
}

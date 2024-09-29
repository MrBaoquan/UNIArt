/*
Copyright (c) 2020 Omar Duarte
Unauthorized copying of this file, via any medium is strictly prohibited.
Writen by Omar Duarte, 2020.

This file incorporates work by The Code Corsair
http://www.elopezr.com/photoshop-blend-modes-in-unity/

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN
THE SOFTWARE.
*/

Shader "PluginMaster/PsBlendModeFast" 
{
	Properties 
	{
		[PerRendererData] _MainTex ("Texture", 2D) = "white" {}

		[HideInInspector] _blendMode("BlendMode", Int) = 0

		[HideInInspector] _blendOp1("__op1", Float) = 0.0
		[HideInInspector] _blendSrc1("__src1", Float) = 1.0
		[HideInInspector] _blendDst1("__dst1", Float) = 0.0
		[HideInInspector] _blendSrcAlpha1("__src_alpha1", Float) = 1.0
		[HideInInspector] _blendDstAlpha1("__dst_alpha1", Float) = 0.0

		[HideInInspector] _blendOp2("__op2", Float) = 0.0
		[HideInInspector] _blendSrc2("__src2", Float) = 1.0
		[HideInInspector] _blendDst2("__dst2", Float) = 0.0
		[HideInInspector] _blendSrcAlpha2("__src_alpha2", Float) = 1.0
		[HideInInspector] _blendDstAlpha2("__dst_alpha2", Float) = 0.0

		[HideInInspector] _visible("Visible", Range(0, 1)) = 0
		[HideInInspector] _opacity("Opacity", Range(0.0, 1.0)) = 1.0

		_Color ("Tint", Color) = (1,1,1,1)

        _StencilComp ("Stencil Comparison", Float) = 8
        _Stencil ("Stencil ID", Float) = 0
        _StencilOp ("Stencil Operation", Float) = 0
        _StencilWriteMask ("Stencil Write Mask", Float) = 255
        _StencilReadMask ("Stencil Read Mask", Float) = 255

        _ColorMask ("Color Mask", Float) = 15

        [Toggle(UNITY_UI_ALPHACLIP)] _UseUIAlphaClip ("Use Alpha Clip", Float) = 0
	}
	
	CGINCLUDE
	#include "UnityCG.cginc"
    #include "UnityUI.cginc"

    #pragma multi_compile_local _ UNITY_UI_CLIP_RECT
    #pragma multi_compile_local _ UNITY_UI_ALPHACLIP

	#include "BlendMode.cginc"

	struct appdata_t
    {
        float4 vertex   : POSITION;
        float4 color    : COLOR;
        float2 texcoord : TEXCOORD0;
        UNITY_VERTEX_INPUT_INSTANCE_ID
    };

    struct v2f
    {
        float4 vertex   : SV_POSITION;
        fixed4 color    : COLOR;
        float2 texcoord  : TEXCOORD0;
        float4 worldPosition : TEXCOORD1;
        UNITY_VERTEX_OUTPUT_STEREO
    };
	ENDCG
	
	SubShader
	{
		Tags
        {
            "Queue"="Transparent"
            "IgnoreProjector"="True"
            "RenderType"="Transparent"
            "PreviewType"="Plane"
            "CanUseSpriteAtlas"="True"
        }

        Stencil
        {
            Ref [_Stencil]
            Comp [_StencilComp]
            Pass [_StencilOp]
            ReadMask [_StencilReadMask]
            WriteMask [_StencilWriteMask]
        }

        Cull Off
        Lighting Off
        ZWrite Off
        ZTest [unity_GUIZTestMode]
        ColorMask [_ColorMask]
        Fog{ Mode Off }

		Pass
		{
			BlendOp[_blendOp1]
			Blend[_blendSrc1][_blendDst1]

			CGPROGRAM
			#pragma vertex vert
			#pragma fragment frag
			#pragma target 2.0

			sampler2D _MainTex;
			fixed4 _Color;
            fixed4 _TextureSampleAdd;
            float4 _ClipRect;
            float4 _MainTex_ST;

			int _blendMode;
			int _visible;
			float _opacity;

			v2f vert(appdata_t v)
            {
                v2f OUT;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(OUT);
                OUT.worldPosition = v.vertex;
                OUT.vertex = UnityObjectToClipPos(OUT.worldPosition);

                OUT.texcoord = TRANSFORM_TEX(v.texcoord, _MainTex);

                OUT.color = v.color * _Color;
                return OUT;
            }

			float4 frag(v2f IN) : SV_Target
			{
				float4 color = (tex2D(_MainTex, IN.texcoord) + _TextureSampleAdd) * IN.color;
				color.a *= _opacity;
				if (_visible == 0)
				{
					color.rgba = float4(0, 0, 0, 0);
				}
				else if (_blendMode == DISSOLVE)
				{
					float randAlpha = color.a;
					if (frac(sin(dot(IN.texcoord, float2(12.9898, 78.233))) * 43758.5453123) > randAlpha)
					{
						randAlpha = 0;
					}
					color.a *= randAlpha;
				}
				else if (_blendMode == DARKEN)
				{
					color.rgb = lerp(float3(1, 1, 1), color.rgb, color.a);
				}
				else if (_blendMode == MULTIPLY)
				{
					color.rgb *= color.a;
				}
				else if (_blendMode == COLOR_BURN)
				{
					color.rgb = 1.0 - (1.0 / max(0.001, color.rgb * color.a + 1.0 - color.a));
				}
				else if (_blendMode == LINEAR_BURN)
				{
					color.rgb = (color.rgb - 1.0) * color.a;
				}
				else if (_blendMode == LIGHTEN)
				{
					color.rgb = lerp(float3(0, 0, 0), color.rgb, color.a);
				}
				else if (_blendMode == SCREEN)
				{
					color.rgb *= color.a;
				}
				else if (_blendMode == COLOR_DODGE)
				{
					color.rgb = 1.0 / max(0.001, (1.0 - color.rgb * color.a));
				}
				else if (_blendMode == OVERLAY)
				{
					if (color.a == 0.5) color.a = 0.4999;
					
					color.rgb *= color.a;

					fixed3 desiredValue = (4.0 * color.rgb - 1.0) / (2.0 - 4.0 * color.rgb);
					fixed3 backgroundValue = (1.0 - color.a) / ((2.0 - 4.0 * color.rgb) * max(0.001, color.a));

					color.rgb = desiredValue + backgroundValue;
				}
				else if (_blendMode == SOFT_LIGHT)
				{
					if (color.a == 0.5) color.a = 0.4999;
					float3 desiredValue = 2.0 * color.rgb * color.a / (1.0 - 2.0 * color.rgb * color.a);
					float3 backgroundValue = (1.0 - color.a) / ((1.0 - 2.0 * color.rgb * color.a) * max(0.001, color.a));

					color.rgb = desiredValue + backgroundValue;
				}
				else if (_blendMode == HARD_LIGHT)
				{
					float3 numerator = (2.0 * color.rgb * color.rgb - color.rgb) * (color.a);
					float3 denominator = max(0.001, (4.0 * color.rgb - 4.0 * color.rgb * color.rgb) * (color.a) + 1.0 - color.a);
					color.rgb = numerator / denominator;
				}
				else if (_blendMode == VIVID_LIGHT)
				{
					color.rgb *= color.a;
					color.rgb = color.rgb >= 0.5 ? (1.0 / max(0.001, 2.0 - 2.0 * color.rgb)) : 1.0;
				}
				else if (_blendMode == LINEAR_LIGHT)
				{
					color.rgb = (2 * color.rgb - 1.0) * color.a;
				}
				else if (_blendMode == EXCLUSION)
				{
					color.rgb *= 2.0 * color.a;
				}
				else if (_blendMode == DIVIDE)
				{
					color.rgb = color.a / max(0.001, color.rgb);
				}
				
				#ifdef UNITY_UI_CLIP_RECT
                color.a *= UnityGet2DClipping(IN.worldPosition.xy, _ClipRect);
                #endif

                #ifdef UNITY_UI_ALPHACLIP
                clip (color.a - 0.001);
                #endif

				return color;
			}
			
			ENDCG
		}

		Pass
		{
			BlendOp[_blendOp2]
			Blend[_blendSrc2][_blendDst2]

			CGPROGRAM
			#pragma vertex vert
			#pragma fragment frag
			#pragma target 2.0
			
			sampler2D _MainTex;
            float4 _MainTex_ST;

			float _blendMode;
			float _opacity;

			v2f vert(appdata_t v)
            {
                v2f OUT;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(OUT);
                OUT.worldPosition = v.vertex;
                OUT.vertex = UnityObjectToClipPos(OUT.worldPosition);

                OUT.texcoord = TRANSFORM_TEX(v.texcoord, _MainTex);

                OUT.color = v.color;
                return OUT;
            }

			float4 frag(v2f IN) : SV_Target
			{	
				float4 color = tex2D(_MainTex, IN.texcoord);

				color.rgb *= IN.color.rgb;
				color.a *= IN.color.a * _opacity;
				
				if (_blendMode == OVERLAY)
				{
					if (color.a == 0.5) color.a = 0.4999;
					color.rgb *= color.a; 
					
					float3 value = (2.0 - 4.0 * color.rgb);
					color.rgb = value * max(0.001, color.a);
				}
				else if(_blendMode == SOFT_LIGHT)
				{
					if (color.a == 0.5) color.a = 0.4999;
					color.rgb = (1.0 - 2.0 * color.rgb * color.a) * max(0.001, color.a);
				}
				else if (_blendMode == HARD_LIGHT)
				{
					color.rgb = max(0.001, (4.0 * color.rgb - 4.0 * color.rgb * color.rgb) * (color.a) + 1.0 - color.a); 
				}
				else if (_blendMode == VIVID_LIGHT)
				{
					color.rgb = color.rgb < 0.5 ? (color.a - color.a / max(0.0001, 2.0 * color.rgb)) : 0.0;
				}
				
				return color;
			}
			
			ENDCG
		}
	}
}

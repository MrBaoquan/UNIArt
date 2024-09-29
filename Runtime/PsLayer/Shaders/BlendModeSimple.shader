/*
Copyright (c) 2020 Omar Duarte
Unauthorized copying of this file, via any medium is strictly prohibited.
Writen by Omar Duarte, 2020.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN
THE SOFTWARE.
*/

Shader "PluginMaster/PsBlendModeSimple"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}

        [HideInInspector] _blendMode("BlendMode", Int) = 0

		[HideInInspector] _blendOp1("__op1", Float) = 0.0
		[HideInInspector] _blendSrc1("__src1", Float) = 1.0
		[HideInInspector] _blendDst1("__dst1", Float) = 0.0
		[HideInInspector] _blendSrcAlpha1("__src_alpha1", Float) = 1.0
		[HideInInspector] _blendDstAlpha1("__dst_alpha1", Float) = 0.0

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

            sampler2D _MainTex;
            fixed4 _Color;
            fixed4 _TextureSampleAdd;
            float4 _ClipRect;
            float4 _MainTex_ST;
            
            int _blendMode;
			int _visible;
			half _opacity;

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
	                color.a = (frac(sin(dot(IN.texcoord, float2(12.9898, 78.233))) * 43758.5453123) + 0.001 > color.a) ? 0 : 1;
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
                else if (_blendMode == LINEAR_LIGHT)
                {
	                color.rgb = (2 * color.rgb - 1.0) * color.a;
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
    }
}
Shader "Party Cart/Racer Materials"
{
    Properties
    {
        [MainColor] _BaseColor("Body paint / HUD color", Color) = (.2,.5,.5,1)
        _TrimColor("Chassis and trousers", Color) = (.02,.07,.08,1)
        _AccentColor("Wing and steering wheel", Color) = (.6,.2,.1,1)
        _JacketColor("Canvas jacket", Color) = (.6,.2,.1,1)
        _PotColor("Pot enamel / steel", Color) = (.9,.85,.7,1)
        _GloveColor("Fabric gloves", Color) = (.9,.85,.7,1)
        _StripeColor("Racing stripe and placket", Color) = (.9,.85,.7,1)
        _MetalPot("Steel pot", Range(0,1)) = 0
        [HideInInspector] _ModelOffset("Model offset for separated liners", Vector) = (0,0,0,0)
        [HideInInspector] _FenderPart("Separated liner", Float) = 0
        [HideInInspector] _BaseMap("Base Map", 2D) = "white" {}
        [HideInInspector] _Cutoff("Cutoff", Float) = .5
        [HideInInspector] _Cull("Cull", Float) = 2
        [HideInInspector] _Surface("Surface", Float) = 0
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" "RenderPipeline"="UniversalPipeline" "UniversalMaterialType"="Lit" }
        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/SurfaceInput.hlsl"
        CBUFFER_START(UnityPerMaterial)
            float4 _BaseMap_ST;
            half4 _BaseColor, _TrimColor, _AccentColor, _JacketColor, _PotColor, _GloveColor, _StripeColor;
            half _MetalPot, _Cutoff, _Surface;
            float4 _ModelOffset;
            half _FenderPart;
        CBUFFER_END
        ENDHLSL
        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode"="UniversalForwardOnly" }
            Cull [_Cull]
            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_instancing
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS
            #pragma multi_compile_fragment _ _ADDITIONAL_LIGHT_SHADOWS
            #pragma multi_compile_fragment _ _SHADOWS_SOFT _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH
            #pragma multi_compile_fragment _ _REFLECTION_PROBE_BLENDING
            #pragma multi_compile_fragment _ _REFLECTION_PROBE_BOX_PROJECTION
            #pragma multi_compile_fragment _ _REFLECTION_PROBE_ATLAS
            #pragma multi_compile_fragment _ _SCREEN_SPACE_OCCLUSION
            #pragma multi_compile _ _CLUSTER_LIGHT_LOOP
            #pragma multi_compile _ LOD_FADE_CROSSFADE
            #pragma multi_compile_fog
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/LODCrossFade.hlsl"
            struct Attributes { float4 positionOS:POSITION; float3 normalOS:NORMAL; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct Varyings
            {
                float4 positionCS:SV_POSITION;
                float3 positionWS:TEXCOORD0;
                float3 positionOS:TEXCOORD1;
                half3 normalWS:TEXCOORD2;
                half3 normalOS:TEXCOORD3;
                half4 fogAndLight:TEXCOORD4;
                UNITY_VERTEX_INPUT_INSTANCE_ID
                UNITY_VERTEX_OUTPUT_STEREO
            };
            Varyings Vert(Attributes v)
            {
                Varyings o = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(v); UNITY_TRANSFER_INSTANCE_ID(v,o); UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                VertexPositionInputs pos = GetVertexPositionInputs(v.positionOS.xyz);
                o.positionCS = pos.positionCS; o.positionWS = pos.positionWS; o.positionOS = v.positionOS.xyz + _ModelOffset.xyz;
                o.normalWS = TransformObjectToWorldNormal(v.normalOS); o.normalOS = v.normalOS;
                o.fogAndLight = half4(ComputeFogFactor(pos.positionCS.z), VertexLighting(pos.positionWS,o.normalWS));
                return o;
            }
            // Mesh-local coordinates keep the finish attached to the car at every native mesh LOD.
            // The original atlas has overlapping islands; no old color or UV edge is sampled here.
            int Region(float3 p, half3 n)
            {
                float ax=abs(p.x);
                if(_FenderPart>.5 && ax>.415) return 1;
                if(p.y>.282 && p.z>-.67)
                {
                    if(p.y>.537 && ax<.07) return 11;
                    if(ax>.176) return 12;
                    if(p.y<.301 || (p.y>.463 && p.y<.486)) return 10;
                    return 9;
                }
                if(p.z<-.745 && p.y>-.01) return ax>.455 ? 1 : ax<.055 ? 2 : 3;
                float3 grip=float3((ax-.173)/.060,(p.y-.168)/.060,(p.z-.061)/.071);
                if(dot(grip,grip)<1) return 8;
                if(p.z>-.12 && p.z<.18 && p.y>-.035 && p.y<.27) return ax>.105 || p.y>.20 || p.y<.065 ? 13 : 12;
                if(p.z>-.74 && p.z<.065 && p.y>-.025 && ax<.29) return p.z>-.47 && ax<.028 ? 6 : 5;
                if(ax<.27 && p.z>-.42 && p.z<.43 && p.y>-.29 && p.y<=-.025) return 7;
                if(p.z<-.49 && p.z>-.73 && p.y>-.20 && ax<.255) return 4;
                if(p.z>.945 && p.y>-.37 && p.y<-.13)
                {
                    float d=length(float2(ax-.188,p.y+.251));
                    if(d<.068) return 14;
                    if(d<.089) return 3;
                }
                if(p.y<-.325) return 1;
                if(ax<.049 && p.z>.40 && n.y>-.15) return 2;
                return 0;
            }
            half4 Frag(Varyings i):SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(i); UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);
                #ifdef LOD_FADE_CROSSFADE
                    LODFadeCrossFade(i.positionCS);
                #endif
                float3 p=i.positionOS;
                int r=Region(p,i.normalOS);
                half3 color=_BaseColor.rgb;
                half metal=.05, smoothness=.48;
                if(r==1 || r==7) color=_TrimColor.rgb;
                if(r==2 || r==6) color=_StripeColor.rgb;
                if(r==3 || r==11 || r==13) color=_AccentColor.rgb;
                if(r==4) { color=_AccentColor.rgb*.72; smoothness=.26; metal=0; }
                if(r==5) color=_JacketColor.rgb;
                if(r==8) color=_GloveColor.rgb;
                if(r==9) { color=_PotColor.rgb; metal=lerp(.08,.72,_MetalPot); smoothness=.5; }
                if(r==10) color=_BaseColor.rgb;
                if(r==12) { color=half3(.47,.51,.49); metal=.78; smoothness=.46; }
                if(r==14) { color=half3(.007,.025,.03); metal=.15; smoothness=.18; }
                if(r==1) { metal=.12; smoothness=.33; }
                if(r==11 || r==13) { metal=0; smoothness=.35; }
                if(r==5 || r==6 || r==7 || r==8)
                {
                    metal=0; smoothness=.13;
                    // Derivative attenuation prevents fine canvas from shimmering at race-camera distance.
                    float footprint=max(length(ddx(p)),length(ddy(p)));
                    float weave=sin((p.x+p.z)*2100)*sin(p.y*2400);
                    color*=1+weave*.022*saturate(1-footprint*1000);
                    if(r==6 && abs(p.x)<.009 && frac((p.y+.018)/.064)<.265) color=_TrimColor.rgb;
                }
                SurfaceData surface=(SurfaceData)0;
                surface.albedo=color; surface.metallic=metal; surface.smoothness=smoothness;
                surface.normalTS=half3(0,0,1); surface.occlusion=1; surface.alpha=1;
                InputData data=(InputData)0;
                data.positionWS=i.positionWS; data.normalWS=NormalizeNormalPerPixel(i.normalWS);
                data.viewDirectionWS=GetWorldSpaceNormalizeViewDir(i.positionWS);
                #if defined(_MAIN_LIGHT_SHADOWS_SCREEN)
                    data.shadowCoord=ComputeScreenPos(TransformWorldToHClip(i.positionWS));
                #else
                    data.shadowCoord=TransformWorldToShadowCoord(i.positionWS);
                #endif
                data.bakedGI=SampleSH(data.normalWS); data.shadowMask=half4(1,1,1,1);
                data.vertexLighting=i.fogAndLight.yzw; data.fogCoord=i.fogAndLight.x;
                data.normalizedScreenSpaceUV=GetNormalizedScreenSpaceUV(i.positionCS);
                half4 colorOut=UniversalFragmentPBR(data,surface);
                colorOut.rgb=MixFog(colorOut.rgb,data.fogCoord);
                return colorOut;
            }
            ENDHLSL
        }
        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode"="ShadowCaster" }
            ZWrite On ZTest LEqual ColorMask 0 Cull [_Cull]
            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex ShadowPassVertex
            #pragma fragment ShadowPassFragment
            #pragma multi_compile_instancing
            #pragma multi_compile_vertex _ _CASTING_PUNCTUAL_LIGHT_SHADOW
            #pragma multi_compile _ LOD_FADE_CROSSFADE
            #include "Packages/com.unity.render-pipelines.universal/Shaders/ShadowCasterPass.hlsl"
            ENDHLSL
        }
        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode"="DepthOnly" }
            ZWrite On ColorMask R Cull [_Cull]
            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex DepthOnlyVertex
            #pragma fragment DepthOnlyFragment
            #pragma multi_compile_instancing
            #pragma multi_compile _ LOD_FADE_CROSSFADE
            #include "Packages/com.unity.render-pipelines.universal/Shaders/DepthOnlyPass.hlsl"
            ENDHLSL
        }
        Pass
        {
            Name "DepthNormals"
            Tags { "LightMode"="DepthNormalsOnly" }
            ZWrite On Cull [_Cull]
            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex DepthNormalsVertex
            #pragma fragment DepthNormalsFragment
            #pragma multi_compile_instancing
            #pragma multi_compile_fragment _ _GBUFFER_NORMALS_OCT
            #pragma multi_compile _ LOD_FADE_CROSSFADE
            #include "Packages/com.unity.render-pipelines.universal/Shaders/DepthNormalsPass.hlsl"
            ENDHLSL
        }
    }
    FallBack "Hidden/Universal Render Pipeline/FallbackError"
}

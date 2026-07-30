Shader "FX/PSX Retro Surface"
{
    Properties
    {
        _MainTex("Albedo", 2D) = "white" {}
        _Color("Tint", Color) = (1,1,1,1)

        [Toggle(PSX_NORMALMAP)] _UseNormalMap("Use Normal Map", Float) = 0
        _BumpMap("Normal Map", 2D) = "bump" {}
        _NormalScale("Normal Scale", Range(0,2)) = 1

        _PsxSpecColor("Spec Color", Color) = (1,1,1,1)
        _PsxSpecPower("Spec Power", Range(4,128)) = 24
        _PsxSpecSteps("Spec Steps", Range(1,16)) = 6

        [Header(Local Multipliers)]
        _LocalSnapStrength("Local Snap Strength", Range(0,1)) = 1
        _LocalUvSnapStrength("Local UV Snap Strength", Range(0,1)) = 1
        [HideInInspector] _LocalDepthStrength("Local Depth Strength", Range(0,1)) = 0
    }

        SubShader
        {
            Tags
            {
                "RenderPipeline" = "UniversalPipeline"
                "Queue" = "Geometry"
                "RenderType" = "Opaque"
            }
            Pass
            {
                Name "Forward"
                Tags { "LightMode" = "UniversalForward" }

                HLSLPROGRAM
                #pragma target 3.0
                #pragma vertex Vert
                #pragma fragment Frag

                #pragma multi_compile_instancing
                #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE
                #pragma multi_compile _ _SHADOWS_SOFT
                #pragma multi_compile _ PSX_AFFINE
                #pragma shader_feature_local _ PSX_NORMALMAP

                #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
                #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

                #if (defined(SHADER_API_D3D11) || defined(SHADER_API_GLCORE) || defined(SHADER_API_METAL) || defined(SHADER_API_VULKAN))
                    #define PSX_HAS_NOPERSP 1
                #else
                    #define PSX_HAS_NOPERSP 0
                #endif

                #if defined(PSX_AFFINE) && PSX_HAS_NOPERSP
                    #define PSX_INTERP noperspective
                #else
                    #define PSX_INTERP
                #endif

                TEXTURE2D(_MainTex);
                SAMPLER(sampler_MainTex);

                TEXTURE2D(_BumpMap);
                SAMPLER(sampler_BumpMap);

                CBUFFER_START(UnityPerMaterial)
                    float4 _MainTex_ST;
                    float4 _MainTex_TexelSize;
                    float4 _Color;

                    float _NormalScale;

                    float4 _PsxSpecColor;
                    float _PsxSpecPower;
                    float _PsxSpecSteps;

                    float _LocalSnapStrength;
                    float _LocalUvSnapStrength;
                    float _LocalDepthStrength;
                CBUFFER_END

                float _PSX_Enabled;

                float4 _PSX_SnapResolution;
                float _PSX_UseScreenResolution;
                float _PSX_SnapStrength;
                float _PSX_JitterSpeed;
                float _PSX_JitterPixels;

                float _PSX_DepthSteps;
                float _PSX_DepthStrength;
                float _PSX_DepthNoise;

                float _PSX_UvPrecision;
                float _PSX_UvSnapStrength;
                float _PSX_UvWarpStrength;
                float _PSX_AffineSwim;

                float _PSX_MipBias;

                float _PSX_VertexLighting;
                float _PSX_LightSteps;

                float _PSX_ColorBits;
                float _PSX_DitherStrength;

                float Hash12(float2 p)
                {
                    float h = dot(p, float2(127.1, 311.7));
                    return frac(sin(h) * 43758.5453123);
                }

                float Bayer4(float2 pixel)
                {
                    float2 p = floor(pixel);
                    float x = fmod(p.x, 4.0);
                    float y = fmod(p.y, 4.0);

                    float v0 = (x == 0.0) ? 0.0 : ((x == 1.0) ? 8.0 : ((x == 2.0) ? 2.0 : 10.0));
                    float v1 = (x == 0.0) ? 12.0 : ((x == 1.0) ? 4.0 : ((x == 2.0) ? 14.0 : 6.0));
                    float v2 = (x == 0.0) ? 3.0 : ((x == 1.0) ? 11.0 : ((x == 2.0) ? 1.0 : 9.0));
                    float v3 = (x == 0.0) ? 15.0 : ((x == 1.0) ? 7.0 : ((x == 2.0) ? 13.0 : 5.0));

                    float v = (y == 0.0) ? v0 : ((y == 1.0) ? v1 : ((y == 2.0) ? v2 : v3));
                    return (v / 16.0) - 0.5;
                }

                float2 GetSnapResolution(float enabled)
                {
                    float2 baseRes = max(_PSX_SnapResolution.xy, 1.0);
                    float2 screenRes = max(_ScreenParams.xy, 1.0);
                    float useScreen = lerp(0.0, saturate(_PSX_UseScreenResolution), enabled);
                    return lerp(baseRes, screenRes, useScreen);
                }

                float2 GetFrameJitterOffsetNdc(float2 res, float enabled)
                {
                    return 0.0.xx;
                }

                float2 SnapNdc(float2 ndc, float2 res)
                {
                    float2 pixelStep = 2.0 / res;
                    return floor(ndc / pixelStep + 0.5) * pixelStep;
                }

                float Quantize01(float v, float steps)
                {
                    float s = max(steps, 1.0);
                    return floor(v * s + 0.5) / s;
                }

                float3 QuantizeRgb(float3 rgb, float bits, float dither)
                {
                    float b = clamp(bits, 1.0, 8.0);
                    float levels = (pow(2.0, b) - 1.0);

                    float3 x = saturate(rgb);
                    x = (x * levels) + dither;
                    x = floor(x + 0.5);
                    return saturate(x / levels);
                }

                float3 UnpackNormalSimple(float4 packedNormal, float scale)
                {
                    float3 n = packedNormal.xyz * 2.0 - 1.0;
                    n.xy *= scale;
                    return normalize(n);
                }

                float4 SampleMainTex(float2 uv, float mipBias)
                {
                    #if defined(SHADER_API_GLES) || defined(SHADER_API_GLES3) || defined(SHADER_API_WEBGL)
                        return SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, uv);
                    #else
                        return SAMPLE_TEXTURE2D_BIAS(_MainTex, sampler_MainTex, uv, mipBias);
                    #endif
                }

                struct Attributes
                {
                    float4 positionOS : POSITION;
                    float3 normalOS : NORMAL;
                    float4 tangentOS : TANGENT;
                    float2 uv : TEXCOORD0;
                    UNITY_VERTEX_INPUT_INSTANCE_ID
                };

                struct Varyings
                {
                    float4 positionCS : SV_POSITION;
                    PSX_INTERP float2 uv : TEXCOORD0;

                    float3 positionWS : TEXCOORD1;
                    float3 normalWS : TEXCOORD2;
                    float3 tangentWS : TEXCOORD3;
                    float3 bitangentWS : TEXCOORD4;

                    float3 vtxLight : TEXCOORD5;
                    float clipW : TEXCOORD6;

                    UNITY_VERTEX_INPUT_INSTANCE_ID
                    UNITY_VERTEX_OUTPUT_STEREO
                };

                Varyings Vert(Attributes v)
                {
                    Varyings o;

                    UNITY_SETUP_INSTANCE_ID(v);
                    UNITY_TRANSFER_INSTANCE_ID(v, o);
                    UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);

                    float enabled = step(0.5, _PSX_Enabled);

                    float2 res = GetSnapResolution(enabled);
                    float2 jitter = GetFrameJitterOffsetNdc(res, enabled);

                    float snapStrength = saturate(_PSX_SnapStrength * _LocalSnapStrength) * enabled;

                    float depthStrength = saturate(_PSX_DepthStrength * _LocalDepthStrength) * enabled;
                    float depthSteps = max(_PSX_DepthSteps, 1.0);

                    float3 positionWS = TransformObjectToWorld(v.positionOS.xyz);
                    float3 positionVS = TransformWorldToView(positionWS);

                    float viewZ = positionVS.z;
                    float farClip = max(_ProjectionParams.z, 0.001);
                    float viewZ01 = saturate((-viewZ) / farClip);
                    float quantZ01 = Quantize01(viewZ01, depthSteps);
                    float quantZ = -quantZ01 * farClip;

                    float depthNoise = (Hash12(v.positionOS.xy * 17.13) - 0.5) * _PSX_DepthNoise * enabled;
                    float finalZ = lerp(viewZ, quantZ + depthNoise, depthStrength);

                    positionVS.z = finalZ;

                    float4 positionCS = TransformWViewToHClip(positionVS);

                    float w = max(abs(positionCS.w), 1e-6);
                    float2 ndc = positionCS.xy / w;

                    float2 snappedNdc = SnapNdc(ndc + jitter, res);
                    float2 finalNdc = lerp(ndc, snappedNdc, snapStrength);

                    positionCS.xy = finalNdc * w;

                    o.positionCS = positionCS;
                    o.clipW = positionCS.w;

                    o.uv = TRANSFORM_TEX(v.uv, _MainTex);

                    VertexNormalInputs normalInputs = GetVertexNormalInputs(v.normalOS, v.tangentOS);
                    o.normalWS = normalInputs.normalWS;
                    o.tangentWS = normalInputs.tangentWS;
                    o.bitangentWS = normalInputs.bitangentWS;

                    o.positionWS = positionWS;

                    float3 N = normalize(o.normalWS);

                    float4 shadowCoord = TransformWorldToShadowCoord(positionWS);
                    Light mainLight = GetMainLight(shadowCoord);

                    float3 L = normalize(mainLight.direction);

                    float ndotl = saturate(dot(N, L));
                    float lightSteps = max(_PSX_LightSteps, 1.0);
                    float stepped = Quantize01(ndotl, lightSteps);

                    float3 ambient = SampleSH(N);
                    float3 diffuse = mainLight.color * (stepped * mainLight.distanceAttenuation * mainLight.shadowAttenuation);

                    float useVertex = step(0.5, _PSX_VertexLighting) * enabled;
                    float3 vtxLit = (ambient + diffuse);

                    o.vtxLight = lerp(float3(1.0, 1.0, 1.0), vtxLit, useVertex);

                    return o;
                }

                float4 Frag(Varyings i) : SV_Target
                {
                    UNITY_SETUP_INSTANCE_ID(i);
                    UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);

                    float enabled = step(0.5, _PSX_Enabled);

                    float2 pixel = i.positionCS.xy;
                    float dither = Bayer4(pixel) * saturate(_PSX_DitherStrength);

                    float2 uv = i.uv;

                    float2 texSize = max(_MainTex_TexelSize.zw, 1.0);
                    float texScale = max(texSize.x, texSize.y) / 256.0;
                    float uvPrecision = max(_PSX_UvPrecision * texScale, 1.0);

                    float uvSnapStrength = saturate(_PSX_UvSnapStrength) * saturate(_LocalUvSnapStrength) * enabled;

                    float2 snappedUv = floor(uv * uvPrecision + 0.5) / uvPrecision;
                    uv = lerp(uv, snappedUv, uvSnapStrength);

                    float uvWarpStrength = saturate(_PSX_UvWarpStrength) * enabled;
                    float2 cell = floor(uv * uvPrecision + 0.5);
                    float2 warp = float2(Hash12(cell), Hash12(cell + 19.31)) - 0.5;
                    uv += warp * uvWarpStrength * (1.0 / uvPrecision);

                    float swim = saturate(_PSX_AffineSwim) * enabled;
                    uv += (Hash12(float2(i.clipW, uv.x) * 3.1) - 0.5) * swim * (1.0 / uvPrecision);

                    float4 albedoTex = SampleMainTex(uv, _PSX_MipBias);
                    float3 albedo = albedoTex.rgb * _Color.rgb;

                    float3 N = normalize(i.normalWS);

                    #if defined(PSX_NORMALMAP)
                        float4 nSample = SAMPLE_TEXTURE2D(_BumpMap, sampler_BumpMap, uv);
                        float3 nTS = UnpackNormalSimple(nSample, _NormalScale);

                        float3 T = normalize(i.tangentWS);
                        float3 B = normalize(i.bitangentWS);
                        float3 Nw = normalize(i.normalWS);

                        N = normalize(T * nTS.x + B * nTS.y + Nw * nTS.z);
                    #endif

                    float3 V = normalize(GetCameraPositionWS() - i.positionWS);

                    float4 shadowCoord = TransformWorldToShadowCoord(i.positionWS);
                    Light mainLight = GetMainLight(shadowCoord);

                    float3 L = normalize(mainLight.direction);
                    float3 H = normalize(L + V);

                    float ndotl = saturate(dot(N, L));
                    float ndoth = saturate(dot(N, H));

                    float useVertex = step(0.5, _PSX_VertexLighting) * enabled;
                    float3 vtxLight = i.vtxLight;

                    float3 ambient = SampleSH(N);
                    float3 perPixelDiffuse = (mainLight.color * (ndotl * mainLight.distanceAttenuation * mainLight.shadowAttenuation)) + ambient;
                    float3 diffuseLight = lerp(perPixelDiffuse, vtxLight, useVertex);

                    float specPow = pow(ndoth, _PsxSpecPower);
                    float specSteps = max(_PsxSpecSteps, 1.0);
                    float specStepped = Quantize01(specPow, specSteps);
                    float3 spec = _PsxSpecColor.rgb * specStepped;

                    float3 color = albedo * diffuseLight + spec;

                    color = QuantizeRgb(color, _PSX_ColorBits, dither * enabled);

                    return float4(color, 1.0);
                }
                ENDHLSL
            }
        }
}

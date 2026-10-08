// Shader estilo PS1 para URP: vértices que tremem (precisão baixa), textura "torta" (affine),
// textura sempre pixelada, luz por pixel com sombras, luzes extras e neblina.
Shader "VampireShooter/PSX Lit"
{
    Properties
    {
        [MainTexture] _BaseMap("Textura", 2D) = "white" {}
        [MainColor] _BaseColor("Cor", Color) = (1, 1, 1, 1)
        _EmissionMap("Mapa de emissão", 2D) = "white" {}
        [HDR] _EmissionColor("Emissão", Color) = (0, 0, 0, 0)

        [Toggle(_ALPHATEST_ON)] _AlphaClip("Recorte (alpha clip)", Float) = 0
        _Cutoff("Limite do recorte", Range(0, 1)) = 0.5
        [Enum(UnityEngine.Rendering.CullMode)] _Cull("Faces (Cull)", Float) = 2

        [Header(PS1)]
        _Jitter("Tremer vertices (grade, 0 desliga)", Float) = 120
        _Affine("Textura torta (affine)", Range(0, 1)) = 0.4
        _LightSteps("Faixas de luz (0 = suave)", Float) = 0
    }

    HLSLINCLUDE
    #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

    TEXTURE2D(_BaseMap);
    TEXTURE2D(_EmissionMap);
    // "point" no nome do sampler força filtro sem suavização (texturas pixeladas)
    SAMPLER(sampler_point_repeat);

    CBUFFER_START(UnityPerMaterial)
        float4 _BaseMap_ST;
        half4 _BaseColor;
        half4 _EmissionColor;
        half _Cutoff;
        half _Cull;
        half _AlphaClip;
        float _Jitter;
        half _Affine;
        half _LightSteps;
    CBUFFER_END

    // Prende o vértice numa grade de baixa resolução na tela (o "tremido" do PS1)
    float4 TremerVertice(float4 positionCS)
    {
        // Vértices atrás ou colados na câmera não entram na grade (eles fariam a peça inteira chacoalhar)
        if (_Jitter <= 0 || positionCS.w < 1.0) return positionCS;
        float2 grade = float2(_Jitter * (_ScreenParams.x / _ScreenParams.y), _Jitter) * 0.5;
        float2 ndc = positionCS.xy / positionCS.w;
        ndc = round(ndc * grade) / grade;
        positionCS.xy = ndc * positionCS.w;
        return positionCS;
    }

    half4 AmostrarBase(float2 uv)
    {
        return SAMPLE_TEXTURE2D(_BaseMap, sampler_point_repeat, uv) * _BaseColor;
    }

    void RecortarAlpha(half alpha)
    {
        #if defined(_ALPHATEST_ON)
            clip(alpha - _Cutoff);
        #endif
    }
    ENDHLSL

    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" "Queue" = "Geometry" }
        Cull [_Cull]

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma shader_feature_local _ALPHATEST_ON
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS
            #pragma multi_compile_fragment _ _ADDITIONAL_LIGHT_SHADOWS
            #pragma multi_compile_fragment _ _SHADOWS_SOFT
            #pragma multi_compile_fog
            #pragma multi_compile_instancing

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float2 uv : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                float3 uvAffine : TEXCOORD1;   // uv * w, w  (interpolado sem correção de perspectiva)
                float3 positionWS : TEXCOORD2;
                float3 normalWS : TEXCOORD3;
                half fogFactor : TEXCOORD4;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            Varyings Vert(Attributes input)
            {
                Varyings output;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);

                VertexPositionInputs pos = GetVertexPositionInputs(input.positionOS.xyz);
                output.positionCS = TremerVertice(pos.positionCS);
                output.positionWS = pos.positionWS;
                output.normalWS = TransformObjectToWorldNormal(input.normalOS);
                output.uv = TRANSFORM_TEX(input.uv, _BaseMap);
                output.uvAffine = float3(output.uv * output.positionCS.w, output.positionCS.w);
                output.fogFactor = ComputeFogFactor(output.positionCS.z);
                return output;
            }

            half3 Iluminar(float3 positionWS, half3 normalWS)
            {
                float4 shadowCoord = TransformWorldToShadowCoord(positionWS);
                Light principal = GetMainLight(shadowCoord);
                half3 luz = principal.color * (principal.distanceAttenuation * principal.shadowAttenuation)
                          * saturate(dot(normalWS, principal.direction));

                #if defined(_ADDITIONAL_LIGHTS) || defined(_ADDITIONAL_LIGHTS_VERTEX)
                uint quantidade = GetAdditionalLightsCount();
                for (uint i = 0u; i < quantidade; ++i)
                {
                    Light extra = GetAdditionalLight(i, positionWS, half4(1, 1, 1, 1));
                    luz += extra.color * (extra.distanceAttenuation * extra.shadowAttenuation)
                         * saturate(dot(normalWS, extra.direction));
                }
                #endif

                // Faixas de luz opcionais (sombreamento "em degraus")
                if (_LightSteps > 0)
                    luz = floor(luz * _LightSteps + 0.5) / _LightSteps;

                return luz + SampleSH(normalWS);
            }

            half4 Frag(Varyings input, FRONT_FACE_TYPE face : FRONT_FACE_SEMANTIC) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);

                float2 uv = lerp(input.uv, input.uvAffine.xy / input.uvAffine.z, _Affine);
                half4 cor = AmostrarBase(uv);
                RecortarAlpha(cor.a);

                // Folhas e planos de dois lados: a face de trás usa a normal invertida
                half3 normalWS = normalize(input.normalWS) * IS_FRONT_VFACE(face, 1.0, -1.0);
                half3 final = cor.rgb * Iluminar(input.positionWS, normalWS);
                final += SAMPLE_TEXTURE2D(_EmissionMap, sampler_point_repeat, uv).rgb * _EmissionColor.rgb;

                final = MixFog(final, input.fogFactor);
                return half4(final, 1);
            }
            ENDHLSL
        }

        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode" = "ShadowCaster" }
            ZWrite On
            ZTest LEqual
            ColorMask 0

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma shader_feature_local _ALPHATEST_ON
            #pragma multi_compile_vertex _ _CASTING_PUNCTUAL_LIGHT_SHADOW
            #pragma multi_compile_instancing

            #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/CommonMaterial.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Shadows.hlsl"

            float3 _LightDirection;
            float3 _LightPosition;

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float2 uv : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
            };

            Varyings Vert(Attributes input)
            {
                Varyings output;
                UNITY_SETUP_INSTANCE_ID(input);
                float3 positionWS = TransformObjectToWorld(input.positionOS.xyz);
                float3 normalWS = TransformObjectToWorldNormal(input.normalOS);
                #if defined(_CASTING_PUNCTUAL_LIGHT_SHADOW)
                    float3 direcaoLuz = normalize(_LightPosition - positionWS);
                #else
                    float3 direcaoLuz = _LightDirection;
                #endif
                output.positionCS = TransformWorldToHClip(ApplyShadowBias(positionWS, normalWS, direcaoLuz));
                #if UNITY_REVERSED_Z
                    output.positionCS.z = min(output.positionCS.z, UNITY_NEAR_CLIP_VALUE);
                #else
                    output.positionCS.z = max(output.positionCS.z, UNITY_NEAR_CLIP_VALUE);
                #endif
                output.uv = TRANSFORM_TEX(input.uv, _BaseMap);
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                RecortarAlpha(AmostrarBase(input.uv).a);
                return 0;
            }
            ENDHLSL
        }

        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode" = "DepthOnly" }
            ZWrite On
            ColorMask R

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma shader_feature_local _ALPHATEST_ON
            #pragma multi_compile_instancing

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
            };

            Varyings Vert(Attributes input)
            {
                Varyings output;
                UNITY_SETUP_INSTANCE_ID(input);
                // Mesmo tremido do passe de cor, para a profundidade (decals de sangue) bater
                output.positionCS = TremerVertice(TransformObjectToHClip(input.positionOS.xyz));
                output.uv = TRANSFORM_TEX(input.uv, _BaseMap);
                return output;
            }

            half Frag(Varyings input) : SV_Target
            {
                RecortarAlpha(AmostrarBase(input.uv).a);
                return input.positionCS.z;
            }
            ENDHLSL
        }

        Pass
        {
            Name "DepthNormals"
            Tags { "LightMode" = "DepthNormals" }
            ZWrite On

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma shader_feature_local _ALPHATEST_ON
            #pragma multi_compile_instancing

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float2 uv : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                float3 normalWS : TEXCOORD1;
            };

            Varyings Vert(Attributes input)
            {
                Varyings output;
                UNITY_SETUP_INSTANCE_ID(input);
                output.positionCS = TremerVertice(TransformObjectToHClip(input.positionOS.xyz));
                output.normalWS = TransformObjectToWorldNormal(input.normalOS);
                output.uv = TRANSFORM_TEX(input.uv, _BaseMap);
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                RecortarAlpha(AmostrarBase(input.uv).a);
                return half4(normalize(input.normalWS), 0);
            }
            ENDHLSL
        }
    }

    FallBack "Hidden/Universal Render Pipeline/FallbackError"
}

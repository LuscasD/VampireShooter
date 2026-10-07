// Pós-processamento estilo PS1: reduz a quantidade de cores com dithering (padrão Bayer 4x4),
// como o gradiente "quebrado" dos jogos de PlayStation. Usado pelo Full Screen Pass Renderer Feature.
Shader "VampireShooter/PSX Pos"
{
    Properties
    {
        _Niveis("Níveis de cor por canal", Float) = 24
        _Dither("Força do dithering", Range(0, 2)) = 1
    }

    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" }
        ZWrite Off ZTest Always Cull Off Blend Off

        Pass
        {
            Name "PSXPos"

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"

            float _Niveis;
            float _Dither;

            static const float bayer[16] =
            {
                 0,  8,  2, 10,
                12,  4, 14,  6,
                 3, 11,  1,  9,
                15,  7, 13,  5
            };

            half4 Frag(Varyings input) : SV_Target
            {
                float2 uv = input.texcoord;
                half4 cor = SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_PointClamp, uv);

                // Um valor do padrão por pixel da imagem (a imagem já é de baixa resolução)
                uint2 pixel = uint2(uv * _ScreenParams.xy) % 4;
                float limite = bayer[pixel.y * 4 + pixel.x] / 16.0 - 0.5;

                cor.rgb += limite * _Dither / _Niveis;
                cor.rgb = floor(saturate(cor.rgb) * _Niveis + 0.5) / _Niveis;
                return half4(cor.rgb, 1);
            }
            ENDHLSL
        }
    }
}

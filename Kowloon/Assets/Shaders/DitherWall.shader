// Walls render on both sides. The "front" side (defined by the inward-pointing
// mesh normals — see TileMeshBuilder) is the side a viewer sees from inside the
// room; it renders fully opaque. The "back" side (the outward-facing surface
// closest to the camera) renders with a 4×4 Bayer screen-space dither, so the
// player sees through to the room interior without losing the cue that a wall
// exists there.
Shader "Kowloon/DitherWall"
{
    Properties
    {
        [MainColor] _BaseColor ("Base Color", Color) = (1, 1, 1, 1)
        [Range(0.0, 1.0)] _DitherDensity ("Front-Side Dither Density", Float) = 0.45
    }

    SubShader
    {
        Tags
        {
            "RenderType"      = "Opaque"
            "RenderPipeline"  = "UniversalPipeline"
            "Queue"           = "Geometry"
        }
        LOD 100

        Pass
        {
            Name "ForwardUnlit"
            Tags { "LightMode" = "UniversalForward" }
            Cull Off

            HLSLPROGRAM
            #pragma vertex   vert
            #pragma fragment frag
            #pragma multi_compile_instancing

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                UNITY_VERTEX_INPUT_INSTANCE_ID
                UNITY_VERTEX_OUTPUT_STEREO
            };

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseColor;
                float  _DitherDensity;
            CBUFFER_END

            UNITY_INSTANCING_BUFFER_START(Props)
                UNITY_DEFINE_INSTANCED_PROP(float4, _BaseColor)
            UNITY_INSTANCING_BUFFER_END(Props)

            Varyings vert(Attributes IN)
            {
                Varyings OUT;
                UNITY_SETUP_INSTANCE_ID(IN);
                UNITY_TRANSFER_INSTANCE_ID(IN, OUT);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(OUT);
                OUT.positionCS = TransformObjectToHClip(IN.positionOS.xyz);
                return OUT;
            }

            // 4x4 Bayer matrix, normalized to [0, 1).
            static const float kBayer4x4[16] =
            {
                0.0000,  0.5000,  0.1250,  0.6250,
                0.7500,  0.2500,  0.8750,  0.3750,
                0.1875,  0.6875,  0.0625,  0.5625,
                0.9375,  0.4375,  0.8125,  0.3125
            };

            half4 frag(Varyings IN, FRONT_FACE_TYPE isFrontFace : FRONT_FACE_SEMANTIC) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(IN);
                float4 baseColor = UNITY_ACCESS_INSTANCED_PROP(Props, _BaseColor);

                bool front = IS_FRONT_VFACE(isFrontFace, true, false);
                if (!front)
                {
                    int x = ((int)IN.positionCS.x) & 3;
                    int y = ((int)IN.positionCS.y) & 3;
                    float threshold = kBayer4x4[y * 4 + x];
                    if (threshold >= _DitherDensity) discard;
                }

                return half4(baseColor.rgb, 1.0);
            }
            ENDHLSL
        }
    }

    Fallback Off
}

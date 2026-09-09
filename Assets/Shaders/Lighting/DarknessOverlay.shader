// 全屏黑暗层：基础暗幕 + 最多 32 个软边光源挖洞。
// 内置管线 Unlit，与默认精灵材质兼容；光源数据由 DarknessOverlayView
// 通过 MaterialPropertyBlock 写入（_LightData：xy=世界坐标，z=半径，w=强度；
// _LightShapeData：x=形状，yz=竖向光带半轴）。
Shader "Memorial Archive/Lighting/Darkness Overlay"
{
    Properties
    {
        _DarknessColor ("Darkness Color", Color) = (0, 0, 0, 1)
        _DarknessAlpha ("Darkness Alpha", Range(0, 1)) = 0.95
        _FalloffExponent ("Falloff Exponent", Range(1, 6)) = 2
        _Dim ("Dim Factor", Range(0, 1)) = 1
    }

    SubShader
    {
        Tags { "Queue" = "Transparent" "RenderType" = "Transparent" "IgnoreProjector" = "True" }

        Blend SrcAlpha OneMinusSrcAlpha
        ZWrite Off
        Cull Off

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.0

            #define MAX_LIGHTS 32

            fixed4 _DarknessColor;
            float _DarknessAlpha;
            float _FalloffExponent;
            float _Dim;
            float4 _LightData[MAX_LIGHTS];
            float4 _LightShapeData[MAX_LIGHTS];
            int _LightCount;

            struct v2f
            {
                float4 vertex : SV_POSITION;
                float2 worldPosition : TEXCOORD0;
            };

            v2f vert(float4 vertex : POSITION)
            {
                v2f output;
                output.vertex = UnityObjectToClipPos(vertex);
                output.worldPosition = mul(unity_ObjectToWorld, vertex).xy;
                return output;
            }

            fixed4 frag(v2f input) : SV_Target
            {
                float lightAmount = 0.0;
                int lightCount = min(_LightCount, MAX_LIGHTS);
                for (int index = 0; index < lightCount; index++)
                {
                    float4 lightData = _LightData[index];
                    float4 shapeData = _LightShapeData[index];
                    float contribution;
                    if (shapeData.x > 0.5)
                    {
                        // A vertical ellipse keeps the light narrow at the
                        // sides while covering the full character height.
                        float2 halfExtents = max(shapeData.yz, float2(0.0001, 0.0001));
                        float2 normalizedOffset = abs(input.worldPosition - lightData.xy) / halfExtents;
                        contribution = saturate(1.0 - length(normalizedOffset));
                    }
                    else
                    {
                        float distanceToLight = distance(input.worldPosition, lightData.xy);
                        contribution = saturate(1.0 - distanceToLight / max(lightData.z, 0.0001));
                    }
                    contribution = pow(contribution, _FalloffExponent) * saturate(lightData.w);
                    lightAmount = max(lightAmount, contribution);
                }

                float alpha = _DarknessAlpha * _Dim * (1.0 - saturate(lightAmount));
                return fixed4(_DarknessColor.rgb, alpha);
            }
            ENDCG
        }
    }

    Fallback Off
}

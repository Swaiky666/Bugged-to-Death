Shader "Custom/OutlineViewSpace"
{
    Properties
    {
        _Color ("Main Color", Color) = (1,1,1,1)
        _OutlineColor ("Outline Color", Color) = (0,0,0,1)
        _OutlineWidth ("Outline Width", Range(0.0, 0.1)) = 0.02
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" }

        // Pass 1: Outline
        Pass
        {
            Name "OUTLINE"
            Cull Front         // �޳����棬ֻ������
            ZWrite On
            ZTest LEqual
            ColorMask RGB

            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            fixed4 _OutlineColor;
            float _OutlineWidth;

            struct appdata
            {
                float4 vertex : POSITION;
                float3 normal : NORMAL;
            };

            struct v2f
            {
                float4 pos : SV_POSITION;
            };

            v2f vert (appdata v)
            {
                v2f o;

                // ����ռ䷨��
                float3 worldNormal = UnityObjectToWorldNormal(v.normal);
                // ������ת���ӽǿռ�
                float3 viewNormal = mul((float3x3)UNITY_MATRIX_V, worldNormal);

                // ������ת���ӽǿռ�
                float4 worldPos = mul(unity_ObjectToWorld, v.vertex);
                float4 viewPos = mul(UNITY_MATRIX_V, worldPos);

                // ���ӽǿռ䷨�߷�������
                viewPos.xyz += viewNormal * _OutlineWidth;

                // ����ͶӰ
                o.pos = mul(UNITY_MATRIX_P, viewPos);
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                return _OutlineColor;
            }
            ENDCG
        }

        // Pass 2: ������Ⱦģ��
        Pass
        {
            Name "BASE"
            Tags { "LightMode"="ForwardBase" }
            Cull Back

            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            fixed4 _Color;

            struct appdata
            {
                float4 vertex : POSITION;
            };

            struct v2f
            {
                float4 pos : SV_POSITION;
            };

            v2f vert (appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                return _Color;
            }
            ENDCG
        }
    }
}

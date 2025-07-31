Shader "BugFixerGame/URP_MissingMaterial"
{
    Properties
    {
        [Header(Missing Material Effect)]
        _BaseColor ("Missing Material Color", Color) = (1, 0, 1, 1)
        _SecondaryColor ("Secondary Color", Color) = (0.6, 0, 0.6, 1)
        
        [Header(Pattern Settings)]
        _PatternScale ("Pattern Scale", Range(1, 50)) = 8
        _PatternType ("Pattern Type", Range(0, 2)) = 1
        // 0 = Solid, 1 = Checkerboard, 2 = Stripes
        
        [Header(Animation)]
        _FlashSpeed ("Flash Speed", Range(0, 10)) = 3
        _FlashIntensity ("Flash Intensity", Range(0, 1)) = 0.4
        _PulseSpeed ("Pulse Speed", Range(0, 5)) = 1
        
        [Header(Advanced)]
        _EmissionIntensity ("Emission Intensity", Range(0, 2)) = 0.3
        _Metallic ("Metallic", Range(0, 1)) = 0
        _Smoothness ("Smoothness", Range(0, 1)) = 0.2
    }

    SubShader
    {
        Tags 
        { 
            "RenderType" = "Opaque" 
            "RenderPipeline" = "UniversalPipeline" 
            "Queue" = "Geometry"
        }
        
        LOD 300

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }
            
            HLSLPROGRAM
            #pragma prefer_hlslcc gles
            #pragma exclude_renderers d3d11_9x
            #pragma target 2.0

            // Universal Pipeline keywords
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS_CASCADE
            #pragma multi_compile _ _SHADOWS_SOFT
            #pragma multi_compile_fog

            #pragma vertex vert
            #pragma fragment frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
                float3 normalOS : NORMAL;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                float3 positionWS : TEXCOORD1;
                float3 normalWS : TEXCOORD2;
                float fogCoord : TEXCOORD3;
                float4 shadowCoord : TEXCOORD4;
            };

            // Properties
            CBUFFER_START(UnityPerMaterial)
                half4 _BaseColor;
                half4 _SecondaryColor;
                float _PatternScale;
                int _PatternType;
                float _FlashSpeed;
                float _FlashIntensity;
                float _PulseSpeed;
                float _EmissionIntensity;
                float _Metallic;
                float _Smoothness;
            CBUFFER_END

            // Utility functions
            float Checkerboard(float2 uv, float scale)
            {
                float2 c = floor(uv * scale);
                return fmod(c.x + c.y, 2.0);
            }

            float Stripes(float2 uv, float scale)
            {
                return step(0.5, fmod(uv.x * scale + uv.y * scale * 0.5, 1.0));
            }

            float PulseEffect(float3 worldPos, float time)
            {
                float dist = length(worldPos - GetCameraPositionWS());
                return sin(time * _PulseSpeed * 3.14159 + dist * 0.1) * 0.5 + 0.5;
            }

            Varyings vert(Attributes input)
            {
                Varyings output = (Varyings)0;
                
                // Transform positions
                VertexPositionInputs vertexInput = GetVertexPositionInputs(input.positionOS.xyz);
                output.positionCS = vertexInput.positionCS;
                output.positionWS = vertexInput.positionWS;
                
                // Transform normals
                VertexNormalInputs normalInput = GetVertexNormalInputs(input.normalOS);
                output.normalWS = normalInput.normalWS;
                
                // UV and fog
                output.uv = input.uv;
                output.fogCoord = ComputeFogFactor(output.positionCS.z);
                
                // Shadow coordinates
                output.shadowCoord = GetShadowCoord(vertexInput);
                
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                // Base color
                half4 baseColor = _BaseColor;
                
                // Pattern generation
                float pattern = 1.0;
                if (_PatternType == 1) // Checkerboard
                {
                    pattern = Checkerboard(input.uv, _PatternScale);
                }
                else if (_PatternType == 2) // Stripes
                {
                    pattern = Stripes(input.uv, _PatternScale);
                }
                
                // Mix colors based on pattern
                half4 finalColor = lerp(_SecondaryColor, baseColor, pattern);
                
                // Flash effect
                float time = _Time.y;
                float flash = sin(time * _FlashSpeed * 6.28318) * 0.5 + 0.5;
                finalColor.rgb += flash * _FlashIntensity * baseColor.rgb;
                
                // Pulse effect
                float pulse = PulseEffect(input.positionWS, time);
                finalColor.rgb += pulse * 0.1 * baseColor.rgb;
                
                // Emission
                half3 emission = finalColor.rgb * _EmissionIntensity;
                
                // Simple lighting (since this is a "broken" material, we keep it simple)
                Light mainLight = GetMainLight(input.shadowCoord);
                half3 lighting = mainLight.color * mainLight.distanceAttenuation * mainLight.shadowAttenuation;
                
                // Fresnel rim lighting for extra "broken" effect
                float3 viewDir = normalize(GetCameraPositionWS() - input.positionWS);
                float fresnel = 1.0 - saturate(dot(input.normalWS, viewDir));
                fresnel = pow(fresnel, 2.0);
                emission += fresnel * baseColor.rgb * 0.2;
                
                // Final color composition
                half3 color = finalColor.rgb * lighting + emission;
                color = saturate(color);
                
                // Apply fog
                color = MixFog(color, input.fogCoord);
                
                return half4(color, 1.0);
            }
            ENDHLSL
        }
        
        // Shadow caster pass
        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode" = "ShadowCaster" }
            
            ZWrite On
            ZTest LEqual
            ColorMask 0
            Cull Back
            
            HLSLPROGRAM
            #pragma prefer_hlslcc gles
            #pragma exclude_renderers d3d11_9x
            #pragma target 2.0
            
            #pragma vertex ShadowPassVertex
            #pragma fragment ShadowPassFragment
            
            #include "Packages/com.unity.render-pipelines.universal/Shaders/LitInput.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/Shaders/ShadowCasterPass.hlsl"
            ENDHLSL
        }
        
        // Depth only pass
        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode" = "DepthOnly" }
            
            ZWrite On
            ColorMask 0
            Cull Back
            
            HLSLPROGRAM
            #pragma prefer_hlslcc gles
            #pragma exclude_renderers d3d11_9x
            #pragma target 2.0
            
            #pragma vertex DepthOnlyVertex
            #pragma fragment DepthOnlyFragment
            
            #include "Packages/com.unity.render-pipelines.universal/Shaders/LitInput.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/Shaders/DepthOnlyPass.hlsl"
            ENDHLSL
        }
    }
    
    FallBack "Hidden/Universal Render Pipeline/FallbackError"
}
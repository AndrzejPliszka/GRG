//THIS WAS COMPLETLY WRITTEN IN AI, AS I DONT KNOW ANYTHING ABOUT SHADERS!!!!!

//
Shader "Lit/SigamTestShader_Lit"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
        _Color ("Tint Color", Color) = (1,1,1,1)
    }
    SubShader
    {
        Tags { "RenderType"="Transparent" "Queue"="Transparent" }
        LOD 100

        Pass
        {
            Offset 1, 1
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off

            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_fog
            #pragma multi_compile_fwdbase     // <-- oœwietlenie w passie ForwardBase
            #include "UnityCG.cginc"
            #include "Lighting.cginc"         // <-- modele oœwietlenia

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
                float3 normal : NORMAL;
            };

            struct v2f
            {
                float2 uv : TEXCOORD0;
                UNITY_FOG_COORDS(1)
                float4 vertex : SV_POSITION;
                float3 worldNormal : TEXCOORD2;
                float3 worldPos : TEXCOORD3;
            };

            sampler2D _MainTex;
            float4 _MainTex_ST;
            fixed4 _Color;

            v2f vert (appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = TRANSFORM_TEX(v.uv, _MainTex);
                o.worldNormal = UnityObjectToWorldNormal(v.normal);
                o.worldPos = mul(unity_ObjectToWorld, v.vertex).xyz;
                UNITY_TRANSFER_FOG(o, o.vertex);
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                // tekstura i kolor
                fixed4 texCol = tex2D(_MainTex, i.uv) * _Color;

                // œwiat³o kierunkowe (Lambert)
                fixed3 normal = normalize(i.worldNormal);
                fixed3 lightDir = normalize(_WorldSpaceLightPos0.xyz);
                fixed NdotL = max(0, dot(normal, lightDir));
                fixed3 diffuse = _LightColor0.rgb * NdotL;

                // Ambient (globalne oœwietlenie)
                fixed3 ambient = UNITY_LIGHTMODEL_AMBIENT.rgb;

                // koñcowy kolor = (ambient + diffuse) * kolor_teksturowy
                fixed3 litCol = texCol.rgb * (ambient + diffuse);

                fixed4 finalCol = fixed4(litCol, texCol.a);

                UNITY_APPLY_FOG(i.fogCoord, finalCol);
                return finalCol;
            }
            ENDCG
        }
    }
}

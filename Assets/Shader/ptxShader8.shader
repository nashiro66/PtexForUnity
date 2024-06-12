Shader "Unlit/ptxShader8"
{
    Properties
    {
        _tex ("Texture", 2D) = "white" {}
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" }
        LOD 100

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
                float2 uv_local : TEXCOORD1;
            };

            struct v2f
            {
                float2 uv : TEXCOORD0;
                float2 uv_local : TEXCOORD1;
                float4 vertex : SV_POSITION;
            };

            sampler2D _tex;

            v2f vert (appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                o.uv_local = v.uv_local;
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {   
                int res = 8;
                int resm1 = res - 1;
                float ut = i.uv_local.x * (float)res;
                float vt = i.uv_local.y * (float)res;
                int ui = clamp(int(ut), 0, resm1);
                int vi = clamp(int(vt), 0, resm1);
                float uf = ut - (float)ui;
                float vf = vt - (float)vi;
                fixed4 col;
                if (uf + vf <= 1.0) {
                    //col = fixed4(0.0, 0.0, 0.0, 1.0);
                    //col = float4(ui / (float)resm1, vi / (float)resm1, 0.0, 1.0);
                    //col = float4(i.uv.x, i.uv.y, 0.0, 1.0);
                    col = tex2D(_tex, float2(i.uv.x + ui / 4096.0, i.uv.y + vi / 4096.0));
                }
                else {
                    //col = fixed4(0.0, 0.0, 0.0, 1.0);
                    col = float4((resm1 - vi) / (float)resm1, (resm1 - ui) / (float)resm1, 0.0, 1.0);
                    //col = float4(i.uv.x, i.uv.y, 0.0, 1.0);
                    col = tex2D(_tex, float2(i.uv.x + (resm1 - vi) / 4096.0, i.uv.y + (resm1 - ui) / 4096.0));
                }
                return col;
            }
            ENDCG
        }
    }
}

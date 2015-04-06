// Unlit shader. Simplest possible textured shader.
// - no lighting
// - no lightmap support
// - no per-material color
//
// Texture atlas
// - supports tiling of subtexture without seams on border
// - uses the 2nd uv space for storing position of subtexture in atlas
// - uses vertex color for storing tiling scale (rg), texture size (b) and if has scaling (a)

Shader "Mobile/Unlit Tileable Atlas" {
Properties {
	_MainTex ("Base (RGB)", 2D) = "white" {}
}

SubShader {
	Tags { "RenderType"="Opaque" }
	LOD 100
	
	Pass {  
		CGPROGRAM
			#pragma vertex vert
			#pragma fragment frag
			#pragma target 3.0
			
			#include "UnityCG.cginc"

			struct appdata_t {
				float4 vertex : POSITION;
				float2 texcoord : TEXCOORD0;
				float2 texcoord1 : TEXCOORD1;
				float4 color : COLOR;
			};

			struct v2f {
				float4 vertex : SV_POSITION;
				half2 texcoord : TEXCOORD0;
				half2 texcoord1 : TEXCOORD1;
				float4 color : COLOR;
			};

			sampler2D _MainTex;
			float4 _MainTex_ST;
			
			v2f vert (appdata_t v)
			{
				v2f o;
				o.vertex = mul(UNITY_MATRIX_MVP, v.vertex);
				o.texcoord = TRANSFORM_TEX(v.texcoord, _MainTex);
				o.texcoord1 = v.texcoord1;
				o.color = v.color;
				return o;
			}
			
			fixed4 fourTapSample(float2 tileUV, float2 tileOffset, float tileSize, sampler2D atlas)
			{
				float4 color = float4(0.0,0.0,0.0,0.0);
				float totalWeight = 0.0;
				
				for(int dx=0; dx<2; ++dx) {
					for(int dy=0; dy<2; ++dy) {
						//Compute coordinate in 2x2 tile patch
						float2 tileCoord = 2.0 * frac(0.5 * (tileUV + float2(dx,dy)));

					    //Weight sample based on distance to center
					    float w = pow(1.0 - max(abs(tileCoord.x-1.0), abs(tileCoord.y-1.0)), 16.0);

					    //Compute atlas coord
					    float2 atlasUV = tileOffset + tileSize * tileCoord;

					    //Sample and accumulate
					    color += w * tex2D(atlas, atlasUV);
					    totalWeight += w;
				    }
			    }
				
				return color / totalWeight;
			}
			
			fixed4 frag (v2f i) : SV_Target
			{
				float2 tileUV = i.texcoord * i.color.rg * 128;
				if(i.color.a > 0) 
				{
					return fourTapSample(tileUV, i.texcoord1, i.color.b, _MainTex);
				}
				else
				{
					tileUV = frac(tileUV) * i.color.b + i.texcoord1;
					return tex2D(_MainTex, tileUV);
				}
			}
		ENDCG
	}
}

}

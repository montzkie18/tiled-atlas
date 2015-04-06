// Simplified Diffuse shader. Differences from regular Diffuse one:
// - no Main Color
// - fully supports only 1 directional light. Other lights can affect it, but it will be per-vertex/SH.
//
// Texture atlas
// - supports tiling of subtexture without seams on border
// - uses the 2nd uv space for storing position of subtexture in atlas
// - uses vertex color for storing tiling scale (rg), texture size (b) and if has scaling (a)

Shader "Mobile/Diffuse Tileable Atlas" {
Properties {
	_MainTex ("Base (RGB)", 2D) = "white" {}
	_BumpTex ("Bump (RGB)", 2D) = "white" {}
}
SubShader {
	Tags { "RenderType"="Opaque" }
	LOD 150

CGPROGRAM
#pragma surface surf Lambert noforwardadd
#pragma target 3.0

sampler2D _MainTex;
sampler2D _BumpTex;

struct Input {
	float2 uv_MainTex;
	float2 uv2_BumpTex;
	float4 color : COLOR;
};

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

void surf (Input IN, inout SurfaceOutput o) {
	float2 tileUV = IN.uv_MainTex * IN.color.rg * 128;
	fixed4 color;
	if(IN.color.a > 0) 
	{
		color = fourTapSample(tileUV, IN.uv2_BumpTex, IN.color.b, _MainTex);
	}
	else
	{
		tileUV = frac(tileUV) * IN.color.b + IN.uv2_BumpTex;
		color = tex2D(_MainTex, tileUV);
	}
	o.Albedo = color.rgb;
}
ENDCG
}

Fallback "Mobile/Diffuse"
}

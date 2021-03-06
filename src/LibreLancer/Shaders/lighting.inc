﻿#define MAX_LIGHTS 9

uniform ivec4 LightingParameters;
#define LightingEnabled (LightingParameters.x == 1)
#define LightCount (LightingParameters.y)
#define FogMode (LightingParameters.z)
#define NumberOfTilesX (LightingParameters.w)

uniform vec4 LightsPos[MAX_LIGHTS];
uniform vec4 LightsColorRange[MAX_LIGHTS]; //xyz: color, w: range
uniform vec3 LightsAttenuation[MAX_LIGHTS];
#ifdef SPOTLIGHT
uniform vec3 SpotlightParams[MAX_LIGHTS]; //x: falloff. y: cos(theta/2), z: cos(phi/2)
uniform vec3 LightsDir[MAX_LIGHTS];
#endif
uniform vec4 AmbientColor;
uniform vec4 FogColor;
uniform vec2 FogRange;

#ifdef FEATURES430
#define MAX_NUM_LIGHTS_PER_TILE 512

struct PointLight {
	vec4 position;
	vec4 colorRange;
	vec4 attenuation;
	vec4 blank;
};

struct VisibleIndex {
	int index;
};

// Shader storage buffer objects
layout(std430, binding = 0) readonly buffer LightBuffer {
	PointLight data[];
} lightBuffer;

layout(std430, binding = 1) readonly buffer VisibleLightIndicesBuffer {
	VisibleIndex data[];
} visibleLightIndicesBuffer;
#endif
/* 
 * approximate function given in [IGraph]
 * ax^2 + b^x + c
 */
float fquadratic(float x, vec3 params)
{
	return x * x * params.x + x * params.y + params.z;
}

#define FOGMODE_LINEAR 3
#define FOGMODE_EXP 1
#define FOGMODE_EXP2 2

vec3 apply_fog(vec4 view_position, vec3 object_color)
{
	float fogFactor;
	float dist = length(view_position);
	if(FogMode == FOGMODE_EXP) {
		//FogRange - x: density
		fogFactor = 1.0 / exp(dist * FogRange.x);
	} else if (FogMode == FOGMODE_EXP2) {
		//FogRange - x: density
		fogFactor = 1.0 / exp((dist * FogRange.x) * (dist * FogRange.x));
	} else {
		//FogRange - x: near, y: far
		fogFactor = (FogRange.y - dist) / (FogRange.y - FogRange.x);
	}
	fogFactor = clamp(fogFactor, 0.0, 1.0);
	return mix(FogColor.rgb, object_color, fogFactor);
}

/*
 * ac : Ambient Color (material)
 * ec : Emissive Color
 * dc : Diffuse Color
 * tex : Diffuse Texture
 * position: World Position
 * view_position: View Position
 * normal: World Normal
 */
vec4 light(vec4 ac, vec4 ec, vec4 dc, vec4 tex, vec3 position, vec4 view_position, vec3 normal)
{
	if(!LightingEnabled)
		return dc * tex;
	vec3 color = AmbientColor.rgb * ac.rgb;
	vec3 n = normalize(normal);
	for(int i = 0; i < MAX_LIGHTS; i++)
	{
		if(i >= LightCount) break;
		vec3 surfaceToLight;
		float attenuation;
		//LightsPos[i].w is the type of light
		//0: directional, 1: point, 2: pointattencurve
		if (LightsPos[i].w == 0.0) { 
			//directional light: LightsPos[i].xyz is direction
			surfaceToLight = normalize(-LightsPos[i].xyz);
			attenuation = 1.;
		} else { 
			//point light
			surfaceToLight = normalize(LightsPos[i].xyz - position);
			float distanceToLight = length(LightsPos[i].xyz - position);
			vec3 curve = LightsAttenuation[i];
            float atten0 = attenuation = 1.0 / (curve.x + curve.y * distanceToLight + curve.z * (distanceToLight * distanceToLight));
            float atten1 = fquadratic(distanceToLight / max(LightsColorRange[i].w,1.),curve);
            attenuation = mix(atten0, atten1, LightsPos[i].w - 1.0); //choose correct attenuation
			#ifdef SPOTLIGHT
			if(SpotlightParams[i].x > 0.0) { //It's a spotlight
				float NdotL = max(dot(n, LightsDir[i]), 0.0);
				if(NdotL > 0.0) {
					float rho = dot(surfaceToLight, -LightsDir[i]);
					attenuation *= pow(clamp((rho - SpotlightParams[i].z) / (SpotlightParams[i].y - SpotlightParams[i].z),0.,1.), SpotlightParams[i].x);
				} else {
					attenuation = 0.0;
				}

			}
			#endif
		}
		//color calculation
		float diffuseCoefficient = max(dot(n, surfaceToLight), 0.0);
		vec3 diffuse = diffuseCoefficient * dc.rgb * LightsColorRange[i].xyz;
		color += attenuation * diffuse;
	}
	#ifdef FEATURES430
	if(NumberOfTilesX > -1) {

	ivec2 location = ivec2(gl_FragCoord.xy);
	ivec2 tileID = location / ivec2(16, 16);
	uint index = tileID.y * NumberOfTilesX + tileID.x;
	uint offset = index * MAX_NUM_LIGHTS_PER_TILE;
	for (uint i = 0; i < MAX_NUM_LIGHTS_PER_TILE && visibleLightIndicesBuffer.data[offset + i].index != -1; i++) {
		uint lightIndex = visibleLightIndicesBuffer.data[offset + i].index;
		PointLight light = lightBuffer.data[lightIndex];
		float attenuation;
		vec3 surfaceToLight = normalize(light.position.xyz - position);
		float distanceToLight = length(light.position.xyz - position);
		if(light.position.w == 1) {
			vec4 curve = light.attenuation;
			attenuation = 1.0 / (curve.x + curve.y * distanceToLight + curve.z * (distanceToLight * distanceToLight));
		} else {
			attenuation = fquadratic(distanceToLight / max(light.colorRange.w,1.), light.attenuation.xyz);
		}
		if(distanceToLight >  light.colorRange.w) attenuation = 0;
		float diffuseCoefficient = max(dot(n, surfaceToLight), 0.0);
		vec3 diffuse = diffuseCoefficient * dc.rgb * light.colorRange.xyz;
		color += attenuation * diffuse;

	}

	}
	#endif
	color = clamp(color,0.0,1.0);
	vec3 object_color = (ec.rgb * tex.rgb) + (tex.rgb * color);
	if(FogMode > 0) {
		object_color = apply_fog(view_position, object_color);
	}
	return vec4(object_color, tex.a);
}
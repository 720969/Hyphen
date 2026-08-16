// HyphenLabelCommon.cginc — 1:1 port of cocos2dx ccShader_Label shaders.
// All fragment shaders match cocos2dx exactly:
//   - v_fragmentColor = input.color (vertex color)
//   - u_textColor = _TextColor (material uniform)
//   - u_effectColor = _EffectColor (material uniform)
//   - Normal/DF: texture .a = font alpha
//   - Outline: texture .r = outline alpha, .a = font alpha (RGBA32: R=outline, A=font)
#ifndef HYPHEN_LABEL_COMMON_INCLUDED
#define HYPHEN_LABEL_COMMON_INCLUDED

#include "UnityCG.cginc"

sampler2D _MainTex;
float4 _MainTex_ST;
float4 _ClipRect;
float4 _TextColor;
float4 _EffectColor;
float _GlowRange = 0.8;

struct HyphenVertexInput
{
    float4 vertex   : POSITION;
    float4 color    : COLOR;
    float2 texCoord : TEXCOORD0;
};

struct HyphenVertexOutput
{
    float4 vertex  : SV_POSITION;
    float4 color    : COLOR;
    float2 texCoord : TEXCOORD0;
};

HyphenVertexOutput vert_normal(HyphenVertexInput input)
{
    HyphenVertexOutput o;
    o.vertex = UnityObjectToClipPos(input.vertex);
    o.color = input.color;
    o.texCoord = input.texCoord * _MainTex_ST.xy + _MainTex_ST.zw;
    return o;
}

// 1:1 port of ccLabelNormal_frag:
//   gl_FragColor = v_fragmentColor * vec4(u_textColor.rgb, u_textColor.a * texture.a)
half4 frag_normal(HyphenVertexOutput input) : SV_Target
{
    half a = tex2D(_MainTex, input.texCoord).a;
    return half4(input.color.rgb * _TextColor.rgb, input.color.a * _TextColor.a * a);
}

// 1:1 port of ccLabelDistanceFieldNormal_frag:
//   float dist = color.a;
//   float alpha = smoothstep(0.5-width, 0.5+width, dist) * u_textColor.a;
//   gl_FragColor = v_fragmentColor * vec4(u_textColor.rgb, alpha);
half4 frag_distancefield(HyphenVertexOutput input) : SV_Target
{
    float dist = tex2D(_MainTex, input.texCoord).a;
    float width = fwidth(dist) * 0.3;
    width = max(width, 0.005);
    float alpha = smoothstep(0.5 - width, 0.5 + width, dist);
    return half4(input.color.rgb * _TextColor.rgb, input.color.a * _TextColor.a * alpha);
}

// 1:1 port of ccLabelOutline_frag:
//   vec4 sample = texture2D(CC_Texture0, v_texCoord);
//   float fontAlpha = sample.a;
//   float outlineAlpha = sample.r;
//   vec4 color = u_textColor * fontAlpha + u_effectColor * (1.0 - fontAlpha);
//   gl_FragColor = v_fragmentColor * vec4(color.rgb, max(fontAlpha,outlineAlpha)*color.a);
half4 frag_outline(HyphenVertexOutput input) : SV_Target
{
    half4 texSample = tex2D(_MainTex, input.texCoord);
    float fontAlpha = texSample.a;
    float outlineAlpha = texSample.r;
    if ((fontAlpha + outlineAlpha) <= 0.0)
    {
        discard;
        return half4(0,0,0,0);
    }
    // Shadow quads use same path as text — only vertex color differs (shadowColor vs white).
    // This matches cocos2dx where shadow pass uses the same frag shader with v_fragmentColor = shadowColor.
    // 1:1 port of ccLabelOutline_frag. color.a mixes text & effect alpha
    half4 color = half4(
        _TextColor.rgb * fontAlpha + _EffectColor.rgb * (1.0 - fontAlpha),
        _TextColor.a * fontAlpha + _EffectColor.a * (1.0 - fontAlpha));
    half a = max(fontAlpha, outlineAlpha) * color.a;
    return input.color * half4(color.rgb, a);
}

// 1:1 port of ccLabelDistanceFieldGlow_frag:
//   float dist = texture2D(CC_Texture0, v_texCoord).a;
//   float alpha = smoothstep(0.5-width, 0.5+width, dist);
//   float mu = smoothstep(0.5, 1.0, sqrt(dist));
//   vec4 color = u_effectColor*(1.0-alpha) + u_textColor*alpha;
//   gl_FragColor = v_fragmentColor * vec4(color.rgb, max(alpha,mu)*color.a);
half4 frag_glow(HyphenVertexOutput input) : SV_Target
{
    float dist = tex2D(_MainTex, input.texCoord).a;
    float width = fwidth(dist) * 0.3;
    width = max(width, 0.005);
    float alpha = smoothstep(0.5 - width, 0.5 + width, dist);
    float mu = smoothstep(0.5, 1.0, sqrt(dist));
    half3 color = _EffectColor.rgb * (1.0 - alpha) + _TextColor.rgb * alpha;
    half a = max(alpha * _TextColor.a, mu * _EffectColor.a);
    return input.color * half4(color, a);
}

#endif

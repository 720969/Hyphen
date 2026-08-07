Shader "Hyphen/Label Outline"
{
    Properties
    {
        _MainTex ("Atlas Texture (RGBA32: R=outline, A=font)", 2D) = "white" {}
        _TextColor ("Text Color", Color) = (1,1,1,1)
        _EffectColor ("Effect Color", Color) = (0,0,0,1)
        _ClipRect ("Clip Rect", Vector) = (-32767, -32767, 32767, 32767)
    }

    SubShader
    {
        Tags
        {
            "Queue" = "Transparent"
            "IgnoreProjector" = "True"
            "RenderType" = "Transparent"
        }

        Blend SrcAlpha OneMinusSrcAlpha
        ZTest Always
        ZWrite Off
        Cull Off

        Pass
        {
            CGPROGRAM
            #pragma vertex vert_normal
            #pragma fragment frag_outline
            #include "HyphenLabelCommon.cginc"
            ENDCG
        }
    }
}

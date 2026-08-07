Shader "Hyphen/Label Glow"
{
    Properties
    {
        _MainTex ("Atlas Texture (SDF)", 2D) = "white" {}
        _TextColor ("Text Color", Color) = (1,1,1,1)
        _EffectColor ("Glow Color", Color) = (0,0,1,1)
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
            #pragma target 3.0
            #pragma vertex vert_normal
            #pragma fragment frag_glow
            #include "HyphenLabelCommon.cginc"
            ENDCG
        }
    }
}

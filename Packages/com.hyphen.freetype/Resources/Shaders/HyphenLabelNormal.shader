Shader "Hyphen/Label Normal"
{
    Properties
    {
        _MainTex ("Atlas Texture", 2D) = "white" {}
        _TextColor ("Text Color", Color) = (1,1,1,1)
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
            #pragma fragment frag_normal
            #include "HyphenLabelCommon.cginc"
            ENDCG
        }
    }
}

using System;
using System.Runtime.InteropServices;

namespace Hyphen.FreeType
{
    /// <summary>
    /// P/Invoke declarations for FreeType 2.14.3 + edtaa3 (combined in freetype.dll).
    /// Only declares functions actually called from C# — outline rendering is done
    /// entirely in hyphen_helper.c (native side).
    /// </summary>
    internal static class FreeTypeNative
    {
#if UNITY_EDITOR_WIN || UNITY_STANDALONE_WIN
        private const string DLL = "freetype";
#elif UNITY_ANDROID
        private const string DLL = "freetype";
#elif UNITY_IOS
        private const string DLL = "__Internal";
#else
        private const string DLL = "freetype";
#endif

        // --- FT_Library ---
        [DllImport(DLL, CallingConvention = CallingConvention.Cdecl)]
        public static extern int FT_Init_FreeType(out IntPtr library);

        [DllImport(DLL, CallingConvention = CallingConvention.Cdecl)]
        public static extern int FT_Done_FreeType(IntPtr library);

        // --- FT_Face ---
        [DllImport(DLL, CallingConvention = CallingConvention.Cdecl)]
        public static extern int FT_New_Memory_Face(IntPtr library, byte[] fileData, long dataSize, int faceIndex, out IntPtr face);

        [DllImport(DLL, CallingConvention = CallingConvention.Cdecl)]
        public static extern int FT_Done_Face(IntPtr face);

        [DllImport(DLL, CallingConvention = CallingConvention.Cdecl)]
        public static extern int FT_Set_Char_Size(IntPtr face, long charWidth, long charHeight, uint hDpi, uint vDpi);

        [DllImport(DLL, CallingConvention = CallingConvention.Cdecl)]
        public static extern int FT_Select_Charmap(IntPtr face, uint encoding);

        // --- FT_Glyph loading ---
        [DllImport(DLL, CallingConvention = CallingConvention.Cdecl)]
        public static extern uint FT_Get_Char_Index(IntPtr face, uint charCode);

        [DllImport(DLL, CallingConvention = CallingConvention.Cdecl)]
        public static extern int FT_Load_Char(IntPtr face, uint charCode, int loadFlags);

        [DllImport(DLL, CallingConvention = CallingConvention.Cdecl)]
        public static extern int FT_Get_Kerning(IntPtr face, uint leftGlyph, uint rightGlyph, uint kernMode, out FT_Vector kerning);

        // --- FT_Stroker ---
        [DllImport(DLL, CallingConvention = CallingConvention.Cdecl)]
        public static extern int FT_Stroker_New(IntPtr library, out IntPtr stroker);

        [DllImport(DLL, CallingConvention = CallingConvention.Cdecl)]
        public static extern void FT_Stroker_Set(IntPtr stroker, long radius, int lineCap, int lineJoin, long miterLimit);

        [DllImport(DLL, CallingConvention = CallingConvention.Cdecl)]
        public static extern void FT_Stroker_Done(IntPtr stroker);

        // --- edtaa3 ---
        [DllImport(DLL, CallingConvention = CallingConvention.Cdecl)]
        public static extern void computegradient(double[] img, int w, int h, double[] gx, double[] gy);

        [DllImport(DLL, CallingConvention = CallingConvention.Cdecl)]
        public static extern void edtaa3(double[] img, double[] gx, double[] gy, int w, int h, short[] distx, short[] disty, double[] dist);

        // --- Hyphen helper functions (from hyphen_helper.c) ---
        [DllImport(DLL, CallingConvention = CallingConvention.Cdecl)]
        public static extern int hyphen_get_ascender(IntPtr face);

        [DllImport(DLL, CallingConvention = CallingConvention.Cdecl)]
        public static extern int hyphen_get_line_height(IntPtr face);

        [DllImport(DLL, CallingConvention = CallingConvention.Cdecl)]
        public static extern void hyphen_get_glyph_metrics(IntPtr face,
            out int width, out int height,
            out int bearingX, out int bearingY,
            out int advance);

        [DllImport(DLL, CallingConvention = CallingConvention.Cdecl)]
        public static extern void hyphen_get_bitmap_info(IntPtr face,
            out int width, out int rows, out IntPtr buffer);

        [DllImport(DLL, CallingConvention = CallingConvention.Cdecl)]
        public static extern int hyphen_render_outline_bitmap(IntPtr library, IntPtr face, IntPtr stroker,
            out int outWidth, out int outRows, out IntPtr outBuffer,
            out int bboxXMin, out int bboxYMin,
            out int bboxXMax, out int bboxYMax);

        [DllImport(DLL, CallingConvention = CallingConvention.Cdecl)]
        public static extern void hyphen_free(IntPtr ptr);

        // --- Constants ---
        public const int FT_LOAD_NO_HINTING = 0x02;
        public const int FT_LOAD_NO_BITMAP = 0x08;
        public const int FT_LOAD_NO_AUTOHINT = 0x8000;
        public const int FT_LOAD_RENDER = 0x04;

        public const uint FT_ENCODING_UNICODE = 0x756E6963; // 'unic' as FT_ULong
        public const uint FT_KERNING_DEFAULT = 0;

        public const int FT_STROKER_LINECAP_ROUND = 1;
        public const int FT_STROKER_LINEJOIN_ROUND = 0; // FreeType enum: ROUND=0, BEVEL=1, MITER=2
    }

    // --- Structs (only those actually used by P/Invoke) ---

    [StructLayout(LayoutKind.Sequential)]
    public struct FT_Vector
    {
        public int x;
        public int y;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct FT_BBox
    {
        public int xMin;
        public int yMin;
        public int xMax;
        public int yMax;
    }
}

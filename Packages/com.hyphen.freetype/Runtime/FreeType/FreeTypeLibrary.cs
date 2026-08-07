using System;
using UnityEngine;

namespace Hyphen.FreeType
{
    /// <summary>
    /// Manages the FreeType library lifecycle.
    /// Port of FontFreeType::_FTlibrary / initFreeType / shutdownFreeType.
    /// </summary>
    public sealed class FreeTypeLibrary : IDisposable
    {
        private static FreeTypeLibrary _instance;
        private static readonly object _lock = new object();

        private IntPtr _handle;
        private bool _disposed;

        public IntPtr Handle => _handle;
        public bool IsValid => _handle != IntPtr.Zero;

        /// <summary>
        /// Gets the shared FreeTypeLibrary instance (singleton).
        /// Thread-safe initialization.
        /// </summary>
        public static FreeTypeLibrary Instance
        {
            get
            {
                if (_instance == null || !_instance.IsValid)
                {
                    lock (_lock)
                    {
                        if (_instance == null || !_instance.IsValid)
                        {
                            _instance = new FreeTypeLibrary();
                        }
                    }
                }
                return _instance;
            }
        }

        private FreeTypeLibrary()
        {
            int err = FreeTypeNative.FT_Init_FreeType(out _handle);
            if (err != 0)
            {
                Debug.LogError($"[Hyphen] FT_Init_FreeType failed with error {err}");
                _handle = IntPtr.Zero;
            }
        }

        public void Dispose()
        {
            if (!_disposed && _handle != IntPtr.Zero)
            {
                FreeTypeNative.FT_Done_FreeType(_handle);
                _handle = IntPtr.Zero;
                _disposed = true;
            }
        }
    }
}

# Hyphen Native Library Build

Hyphen renders text via a hand-built FreeType 2.14.3 shared library that also
contains `edtaa3` (distance field) and `hyphen_helper.c` (safe metric accessors).

## Layout

```
Assets/Plugins/
├── Windows/x86_64/freetype.dll   # Windows x64 (built from freetype-2.14.3/)
├── Android/...                    # libfreetype.so (not yet built)
└── iOS/...                        # static lib / source (not yet built)
```

## Windows (already built)

The DLL is produced from the in-repo FreeType source:

```bash
cmake -B build -S freetype-2.14.3 \
  -DBUILD_SHARED_LIBS=ON \
  -DFT_DISABLE_ZLIB=ON -DFT_DISABLE_BZIP2=ON -DFT_DISABLE_PNG=ON -DFT_DISABLE_HARFBUZZ=ON \
  -G "Visual Studio 17 2022" -A x64
cmake --build build --config Release
# copy build/Release/freetype.dll -> Assets/Plugins/Windows/x86_64/
```

`hyphen_helper.c` and `edtaa3func.c` are compiled together into the same DLL.
The helper must export the functions used by `FreeTypeNative.cs` (see that file).

## Android

Requires Android NDK. Build `libfreetype.so` for `arm64-v8a`, `armeabi-v7a`,
`x86_64` and place under `Assets/Plugins/Android/<abi>/libfreetype.so`.

Example NDK toolchain build (adjust NDK path):

```bash
export ANDROID_NDK=/path/to/ndk
export CROSS=$ANDROID_NDK/toolchains/llvm/prebuilt/linux-x86_64/bin
cmake -B build-android-arm64 -S freetype-2.14.3 -DCMAKE_TOOLCHAIN_FILE=$ANDROID_NDK/build/cmake/android.toolchain.cmake \
  -DANDROID_ABI=arm64-v8a -DANDROID_PLATFORM=android-24 -DBUILD_SHARED_LIBS=ON \
  -DFT_DISABLE_ZLIB=ON -DFT_DISABLE_BZIP2=ON -DFT_DISABLE_PNG=ON -DFT_DISABLE_HARFBUZZ=ON
cmake --build build-android-arm64 --config Release
```

The C# layer uses `#if UNITY_ANDROID` → DLL name `"freetype"`, matching
`libfreetype.so`. Ensure the `.so` is placed with the correct ABI subfolders.

## iOS

Requires macOS + Xcode. Build a static library for arm64 + x86_64 simulator and
link it, or compile the sources into the Xcode project. The C# layer uses
`#if UNITY_IOS` → `"__Internal"` (static link, no dylib).

**IMPORTANT for iOS:** `hyphen_helper.c` and `edtaa3func.c` must be added to the
Xcode project's Compile Sources so their symbols are available to the `__Internal`
static link.
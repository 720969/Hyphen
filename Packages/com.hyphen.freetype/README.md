# Hyphen
A FreeType-based text rendering engine for Unity UGUI

## Requirements
- Unity 2021.3 or later
- URP render pipeline
- Windows: freetype.dll included (x86_64)
- Android/iOS: Native library build required (see [README-Native.md](README-Native.md))

## Installation
### Via UnityPackage
1. Download `Hyphen.unitypackage` from the [Releases](../../releases) page
2. In Unity: `Assets > Import Package > Custom Package`
3. Select the downloaded `.unitypackage` file and import
### Via Git
1. In Unity: `Window > Package Manager > + > Add package from git URL`
2. Enter the repository URL

## Quick Start
1. In Hierarchy, right-click → `UI (Canvas) > Text (Hyphen)`
2. If no Canvas exists, one will be created automatically
3. In the Inspector, set:
   - **Font Asset**: A `.bytes` font file (TTF/OTF/TTC converted to bytes)
   - **Font Size**: Desired pixel size
   - **Text**: Your text content
4. Adjust alignment, wrapping, and effects as needed

### Creating Font Asset
To create your own font assets:
1. Select a `.ttf`, `.otf`, or `.ttc` file in the Project window
2. Right-click → `Assets > Create > Hyphen > Font Asset`
3. A `.bytes` copy will be created in the same folder
4. Assign this `.bytes` file to the **Font Asset** field on your Hyphen Label
A default **Noto Sans SC** font is bundled with the package.

### Effects
- **Shadow**: Enable with custom color and offset
- **Outline**: FreeType stroker-based outline with adjustable size and color (mutually exclusive with SDF/Glow)
- **Glow**: SDF soft glow halo with custom color (requires SDF, mutually exclusive with Outline)

## Credits
- **FreeType 2.14.3** — Copyright (C) 1996-2024 David Turner, Robert Wilhelm, and Werner Lemberg. [https://freetype.org](https://freetype.org)
- **edtaa3** — Copyright (C) 2009 Stefan Gustavson (stefan.gustavson@gmail.com). From the freetype-gl project.
- **Cocos2d-x** — CCLabel/CCFontAtlas/CCFontFreeType architecture reference. Copyright (C) Chukong Technologies Inc.
- **Noto Sans SC** — Copyright (C) Google Inc. Licensed under the SIL Open Font License (OFL).

## License
This project uses the **FreeType License (FTL)** for the FreeType engine and native library components. The Hyphen C# source code is licensed under the **MIT License**. See [LICENSE](LICENSE) for full details.
The bundled edtaa3 distance field algorithm is licensed under a BSD 2-clause license (see source headers).
The bundled Noto Sans SC font is licensed under the SIL Open Font License 1.1.

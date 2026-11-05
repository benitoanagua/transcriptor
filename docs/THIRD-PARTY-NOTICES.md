# Third-party notices

This file summarizes third-party notices currently reviewed for the Windows release. Each release ZIP also contains JSON inventories of direct and transitive NuGet packages for the desktop, recorder, and transcription projects under `dependency-inventory/`. Those package inventories are not a complete SBOM of the .NET runtime, Windows components, or separately obtained Whisper binaries/models; review the applicable upstream notices and licenses as well.

## NAudio 2.2.1

Transcriptor references the NAudio 2.2.1 NuGet meta-package from the recording and transcription projects. NAudio is licensed under the MIT License. The package brings in the NAudio components required for the selected target frameworks.

Project: https://github.com/naudio/NAudio  
Package: https://www.nuget.org/packages/NAudio/2.2.1  
License: MIT

### MIT License

Copyright (c) Mark Heath

Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (the "Software"), to deal
in the Software without restriction, including without limitation the rights
to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
copies of the Software, and to permit persons to whom the Software is
furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in all
copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
SOFTWARE.

## Avalonia 12.1.0

The desktop project directly references the Avalonia core package. It is licensed under the MIT License.

Project: https://github.com/AvaloniaUI/Avalonia  
Package: https://www.nuget.org/packages/Avalonia/12.1.0  
License: MIT

## Avalonia.Desktop 12.1.0

The desktop project directly references the Avalonia desktop platform integration package. It is licensed under the MIT License.

Project: https://github.com/AvaloniaUI/Avalonia  
Package: https://www.nuget.org/packages/Avalonia.Desktop/12.1.0  
License: MIT

## Avalonia.Themes.Fluent 12.1.0

The desktop project directly references the Avalonia Fluent theme package. It is licensed under the MIT License.

Project: https://github.com/AvaloniaUI/Avalonia  
Package: https://www.nuget.org/packages/Avalonia.Themes.Fluent/12.1.0  
License: MIT

### Avalonia MIT License

Copyright (c) AvaloniaUI OÜ
All Rights Reserved

Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (the "Software"), to deal
in the Software without restriction, including without limitation the rights
to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
copies of the Software, and to permit persons to whom the Software is
furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in all
copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
SOFTWARE.

## .NET 10 runtime

The Windows release is published as a self-contained application and includes .NET runtime components. The .NET product distribution for Windows is governed by the .NET Library License and includes components with their own notices. Review the applicable license and notice files supplied with the .NET distribution:

- .NET license information: https://github.com/dotnet/core/blob/main/license-information.md
- .NET Library License: https://dotnet.microsoft.com/dotnet_library_license

This project-level notice does not replace or override the license and notices shipped by the .NET runtime or any other component.

## Whisper.cpp and model files

The official Transcriptor release archives do not bundle `whisper-cli.exe` or GGML model files. Users obtain and configure those separately; their applicable licenses and notices are determined by the specific build and model they choose.

## Embedded fonts

The UI embeds the following unmodified Google Fonts TTF assets as Avalonia resources:

- Inter variable font — Copyright 2020 The Inter Project Authors. https://github.com/rsms/inter
- JetBrains Mono variable font — Copyright 2020 The JetBrains Mono Project Authors. https://github.com/JetBrains/JetBrainsMono
- Chakra Petch Bold — Copyright 2018 The Chakra Petch Project Authors. https://github.com/m4rc1e/Chakra-Petch

Each font is licensed under the SIL Open Font License, Version 1.1 (OFL-1.1): https://scripts.sil.org/OFL

### SIL OPEN FONT LICENSE Version 1.1 - 26 February 2007

PREAMBLE

The goals of the Open Font License (OFL) are to stimulate worldwide development of collaborative font projects, to support the font creation efforts of academic and linguistic communities, and to provide a free and open framework in which fonts may be shared and improved in partnership with others.

The OFL allows the licensed fonts to be used, studied, modified and redistributed freely as long as they are not sold by themselves. The fonts, including any derivative works, can be bundled, embedded, redistributed and/or sold with any software provided that any reserved names are not used by derivative works. The fonts and derivatives, however, cannot be released under any other type of license. The requirement for fonts to remain under this license does not apply to any document created using the fonts or their derivatives.

DEFINITIONS

"Font Software" refers to the set of files released by the Copyright Holder(s) under this license and clearly marked as such. This may include source files, build scripts and documentation.

"Reserved Font Name" refers to any names specified as such after the copyright statement(s).

"Original Version" refers to the collection of Font Software components as distributed by the Copyright Holder(s).

"Modified Version" refers to any derivative made by adding to, deleting, or substituting -- in part or in whole -- any of the components of the Original Version, by changing formats or by porting the Font Software to a new environment.

"Author" refers to any designer, engineer, programmer, technical writer or other person who contributed to the Font Software.

PERMISSION & CONDITIONS

Permission is hereby granted, free of charge, to any person obtaining a copy of the Font Software, to use, study, copy, merge, embed, modify, redistribute, and sell modified and unmodified copies of the Font Software, subject to the following conditions:

1. Neither the Font Software nor any of its individual components, in Original or Modified Versions, may be sold by itself.
2. Original or Modified Versions of the Font Software may be bundled, redistributed and/or sold with any software, provided that each copy contains the above copyright notice and this license. These can be included either as stand-alone text files, human-readable headers or in the appropriate machine-readable metadata fields within text or binary files as long as those fields can be easily viewed by the user.
3. No Modified Version of the Font Software may use the Reserved Font Name(s) unless explicit written permission is granted by the corresponding Copyright Holder. This restriction only applies to the primary font name as presented to the users.
4. The name(s) of the Copyright Holder(s) or the Author(s) of the Font Software shall not be used to promote, endorse or advertise any Modified Version, except to acknowledge the contribution(s) of the Copyright Holder(s) and the Author(s) or with their explicit written permission.
5. The Font Software, modified or unmodified, in part or in whole, must be distributed entirely under this license, and must not be distributed under any other license. The requirement for fonts to remain under this license does not apply to any document created using the Font Software.

TERMINATION

This license becomes null and void if any of the above conditions are not met.

DISCLAIMER

THE FONT SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED TO ANY WARRANTIES OF MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT OF COPYRIGHT, PATENT, TRADEMARK, OR OTHER RIGHT. IN NO EVENT SHALL THE COPYRIGHT HOLDER BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY, INCLUDING ANY GENERAL, SPECIAL, INDIRECT, INCIDENTAL, OR CONSEQUENTIAL DAMAGES, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM, OUT OF THE USE OR INABILITY TO USE THE FONT SOFTWARE OR FROM OTHER DEALINGS IN THE FONT SOFTWARE.

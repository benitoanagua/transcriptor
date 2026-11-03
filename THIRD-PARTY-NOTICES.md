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

## .NET 10 runtime

The Windows release is published as a self-contained application and includes .NET runtime components. The .NET product distribution for Windows is governed by the .NET Library License and includes components with their own notices. Review the applicable license and notice files supplied with the .NET distribution:

- .NET license information: https://github.com/dotnet/core/blob/main/license-information.md
- .NET Library License: https://dotnet.microsoft.com/dotnet_library_license

This project-level notice does not replace or override the license and notices shipped by the .NET runtime or any other component.

## Whisper.cpp and model files

The official Transcriptor release archives do not bundle `whisper-cli.exe` or GGML model files. Users obtain and configure those separately; their applicable licenses and notices are determined by the specific build and model they choose.

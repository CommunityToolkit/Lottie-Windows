# LottieRuntime

A native runtime interpreter for Lottie animations.

`LottieRuntime` builds a `Windows.UI.Composition` visual tree from a serialized
composition. The DLL consumes FlatBuffer data through a classic COM interface.
Applications can also link the implementation directly.

## How it fits in

Lottie-Windows already turns a Lottie file into a `WinCompData` graph, which is a
description of a composition tree. Three consumers use that graph:

| Consumer | When it runs | Cost |
| --- | --- | --- |
| `Instantiator` | Runtime, in the app | Ships the whole JSON reader and translator |
| `LottieGen` code generators | Build time | No runtime cost, but one class per animation |
| `LottieRuntime` | Runtime, in the app | One interpreter shared by every animation |

`CompositionSerializer` writes the `WinCompData` graph into a
FlatBuffer at build time, and `LottieRuntime` reads that buffer at runtime. The
animation stays data, so it can be swapped, downloaded or themed without a rebuild,
and the code that reads it is the same size no matter how many animations an
application has.

```
Lottie JSON --> LottieCompositionReader --> LottieData
                                              |
                                              v
                                       LottieToWinComp
                                              |
                                              v
                                        WinCompData graph
                                        /      |        \
                                       /       |         \
                              Instantiator  UIDataCodeGen  CompositionSerializer
                              (runtime C#)   (C#/C++/CX)          |
                                                                  v
                                                            .lcomp buffer
                                                                  |
                                                                  v
                                                            LottieRuntime
                                                            (runtime C++)
```

Because the FlatBuffer is written from the same graph the other two consume, all
three produce the same visual tree. The round trip tests in
`tests/CompDataFlatbuffer.Tests` assert exactly that.

## Producing a buffer

```
LottieGen -InputFile animation.json -Language flatbuffer -OutputFolder .
```

This writes `Animation.lcomp`. The format is described by
[`source/LottieFlatbuffer/lottie_comp.fbs`](../../source/LottieFlatbuffer/lottie_comp.fbs).

## Using the DLL

```cpp
#include <LottieRuntimeCom.h>

auto bytes = ReadFileBytes(L"Animation.lcomp");
winrt::com_ptr<ILottieCompositionLoader> loader;
winrt::check_hresult(CoCreateInstance(
    CLSID_LottieCompositionLoader,
    nullptr,
    CLSCTX_INPROC_SERVER,
    __uuidof(ILottieCompositionLoader),
    loader.put_void()));

winrt::Windows::UI::Composition::Visual root{ nullptr };
winrt::check_hresult(loader->LoadComposition(
    static_cast<UINT32>(bytes.size()),
    reinterpret_cast<BYTE const*>(bytes.data()),
    winrt::guid_of<decltype(root)>(),
    winrt::put_abi(root)));
```

`LottieRuntime.manifest` declares the private COM assembly. Activate that manifest
before `CoCreateInstance`; `LottieRuntime.exe` contains the complete activation
sequence. No system registration is required. The calling thread must own a
`DispatcherQueue` that outlives the returned visual tree.

`LottieRuntime.h` is the C++ API for applications linking the implementation
directly. It is not exported by `LottieRuntime.dll`.

## Design

**Least derived interfaces.** A node's concrete type is known only inside the
function that creates it. The realization caches hold `Visual`,
`CompositionShape`, `CompositionBrush`, `CompositionGeometry` and so on, and every
call the interpreter makes afterwards is declared on those types. There is no point
at which the interpreter asks what a node actually is, because the buffer already
said, and the answer was consumed at the moment of creation. The one exception is
`ObjectCategory::Surface` in an object reference, where the buffer permits an image
surface that is not a `CompositionObject`; that is a validation check, not a
downcast in the normal path.

**Size first.** There is one function per node *category*, not per node type, and
the state shared by every node is applied by a single function that takes
`CompositionObject`. Everything is in one translation unit, so `/OPT:REF` discards
the code for whatever an application never reaches.

**Runtime cost second.** Each node and string is realized at most once. Lookups use
O(1) bounds-checked array indexing into the FlatBuffer's node vectors. The buffer
is read in place; no intermediate object model is built.

**Transient memory third.** Realization caches are dense vectors matching the
FlatBuffer's node vectors. D2D geometries are cached while the graph is built so
shared geometry remains shared and cycles are rejected.

**Untrusted input.** A buffer may have come from a file or a download. It is checked
for the `LCMP` identifier, run through the FlatBuffers verifier, and every index
read out of it is range checked. A malformed buffer throws
`winrt::hresult_invalid_argument`; it never reads outside the buffer. The declared
Universal API contract is checked before graph hydration.

## Dependencies

Direct2D, for path geometry, and the composition effect interop interface, for the
two effects the translator emits. There is no dependency on Win2D: the interpreter
implements `IGeometrySource2DInterop` and `IGraphicsEffectD2D1Interop` itself, which
is a few dozen lines and avoids pulling a large component into every application.

The FlatBuffers C++ headers come from the pinned `external/flatbuffers` submodule.
The checked-in C++ binding is generated by `flatc` from that commit. The checked-in
C# binding remains on `Google.FlatBuffers` 25.2.10 from Microsoft's configured
NuGet source. `build/RegenerateFlatbuffers.ps1` validates both generator versions.

## Incorporate it in a build

`LottieGen.nupkg` contains the complete native source set under
`LottieRuntime/`.

### C++/WinRT

Add `LottieRuntime.cpp` to the consuming native project. Add `LottieRuntime/`
and `LottieRuntime/flatbuffers/include/` to its include paths. Build as C++20
and link:

```text
d2d1.lib
dxguid.lib
shcore.lib
shlwapi.lib
windowsapp.lib
```

Include `LottieRuntime.h` and call `LoadComposition` with the application's
`Compositor`. This direct-link path does not use COM activation.

### Managed applications

A managed application can also use the C# interpreter in
[`source/LottieRuntime`](../../source/LottieRuntime/README.md), which reads the same
buffer and performs the same validation, and needs no COM activation. Use the DLL
instead when the application should not take a Win2D dependency, or when the same
animation is loaded from both native and managed code.

Add the packaged `LottieRuntime.vcxproj` to the solution. The project builds
`LottieRuntime.dll`, including the `ILottieCompositionLoader` implementation.
Copy both files into every managed application output directory:

```text
LottieRuntime.dll
LottieRuntime.manifest
```

The managed application activates `LottieRuntime.manifest`, calls
`CoCreateInstance(CLSID_LottieCompositionLoader)`, and invokes
`ILottieCompositionLoader::LoadComposition`. The calling thread owns the
`DispatcherQueue` for the lifetime of the returned visual tree.

## Status

Debug and Release x64 builds are supported. `LottieRuntime.exe` validates, dumps,
or displays a composition through registration-free activation.
`tests/CompDataFlatbuffer.Tests` verifies serializer round trips.

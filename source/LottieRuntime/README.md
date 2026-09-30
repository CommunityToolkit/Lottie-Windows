# LottieRuntime (managed)

A managed runtime interpreter for Lottie animations.

`CompositionInterpreter` builds a `Windows.UI.Composition` visual tree from a
serialized composition. It is the C# counterpart of the native interpreter in
[`dlls/LottieRuntime`](../../dlls/LottieRuntime/README.md): both read the same
`.lcomp` buffer, make the same composition calls, and perform the same validation,
so an application can choose whichever one suits its language and packaging without
changing what appears on screen.

## How it fits in

Lottie-Windows turns a Lottie file into a `WinCompData` graph, which is a
description of a composition tree. Four consumers use that graph:

| Consumer | When it runs | Cost |
| --- | --- | --- |
| `Instantiator` | Runtime, in the app | Ships the whole JSON reader and translator |
| `LottieGen` code generators | Build time | No runtime cost, but one class per animation |
| `LottieRuntime` (C++) | Runtime, in the app | One interpreter shared by every animation |
| `CompositionInterpreter` (C#) | Runtime, in the app | One interpreter shared by every animation |

`CompositionSerializer` writes the graph into a FlatBuffer at build time and the
interpreter reads that buffer at runtime. The animation stays data, so it can be
swapped, downloaded or themed without a rebuild, and the amount of code in the
application does not grow with the number of animations.

Note the difference from `CompositionDeserializer`, which is also managed and also
reads the same buffer: the deserializer rebuilds a `WinCompData` graph, which is the
object model that the translator and the code generators work in, while the
interpreter creates live composition objects and no intermediate model at all. The
deserializer is a tool; the interpreter is a player.

## Producing a buffer

```
LottieGen -InputFile animation.json -Language flatbuffer -OutputFolder .
```

This writes `Animation.lcomp`. The format is described by
[`source/LottieFlatbuffer/lottie_comp.fbs`](../LottieFlatbuffer/lottie_comp.fbs).

## Using it

```csharp
var bytes = File.ReadAllBytes("Animation.lcomp");
var root = CompositionInterpreter.LoadComposition(compositor, bytes);

ElementCompositionPreview.SetElementChildVisual(element, root);

// Drive the animation by setting Progress between 0 and 1.
CompositionInterpreter.ProgressPropertySet(root).InsertScalar("Progress", 0.5f);
```

The `Compositor` must belong to the calling thread, which must own a
`DispatcherQueue` that outlives the returned visual tree.

## Design

**One function per category, not per type.** A node's concrete type is known only
inside the function that creates it. The realization caches hold `Visual`,
`CompositionShape`, `CompositionBrush`, `CompositionGeometry` and so on, and
everything the interpreter does afterwards is declared on those types, so there is
no point at which it has to ask what a node actually is. The state that every
`CompositionObject` has is applied by one function that takes `CompositionObject`.

**Runtime cost second.** Each node and string is realized at most once. Lookups are
bounds-checked array indexing into the buffer's node vectors, because the caches are
indexed identically to those vectors. The buffer is read in place.

**Untrusted input.** A buffer may have come from a file or a download. It is checked
for the `LCMP` identifier, run through the FlatBuffers verifier, and every index read
out of it is range checked. Canvas geometries are checked for cycles and paths for
figures that do not begin and end. A malformed buffer is reported as a
`FlatBufferFormatException` and never reads outside the buffer. A buffer that needs a
newer schema, or a Universal API contract that this version of Windows does not have,
is reported as a `NotSupportedException` before any object is created.

## Dependencies

`Windows.UI.Composition`, Win2D for path geometry and the two effects that the
translator emits, and `Google.FlatBuffers` for the generated schema binding. The
native interpreter avoids Win2D by implementing the two Direct2D interop interfaces
itself; that is not available to managed code, so a consumer of this interpreter
takes the Win2D dependency, exactly as a consumer of `Instantiator` or of
`LottieGen`'s C# output already does.

## Incorporate it in a build

`LottieRuntime.projitems` is a shared project. Import it into the application
project and add a package reference to `Google.FlatBuffers`. The runtime imports
the generated schema binding and format constants without bringing in the
tool-side serializer, deserializer, or `WinCompData` model.

Define `WINAPPSDK` when building for the Windows App SDK, which switches the
interpreter from `Windows.UI.Composition` to `Microsoft.UI.Composition`. Define
`PUBLIC_LottieRuntime` to make the type public.

## Tests

`tests/CompDataFlatbuffer.Tests` dumps the interpreted tree in the same canonical
text as the `WinCompData` graph that the buffer was written from and asserts that the
two are equal, for every animation in the sample corpus and for a synthetic graph
that contains one of every node type. The same tests check that malformed, truncated
and corrupted buffers are rejected. The composition APIs only exist on Windows, so
the tests run against stand-ins for them, which is what makes the interpreter
testable on any platform.

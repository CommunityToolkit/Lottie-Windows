# LottieFlatbuffer

`lottie_comp.fbs` is the schema for the serialized form of a `WinCompData` graph.

## What is serialized, and why that

A Lottie file goes through several stages before anything is drawn:

```
Lottie JSON --> LottieData --> WinCompData graph --> composition tree
```

The interesting choice is which of those to serialize. The Lottie authoring format
would be the obvious answer, but it is the wrong one: reading it back still requires
the whole translator, which is by far the largest part of Lottie-Windows and the
part that changes most often. The `WinCompData` graph is much closer to what the
composition engine needs, so a reader for it is small, and it is a closed object
model, so a reader for it is *complete* rather than approximate.

This is the same seam that the code generators use. `Instantiator.cs`, the C#, C++/CX
and C++/WinRT generators, and this format are all consumers of the same graph, which
is why they all produce the same visual tree.

## Layout

The graph is a DAG with sharing, so it is stored as a set of parallel node vectors,
one per category, and every reference is a `uint32` index into one of them.
`0xFFFFFFFF` means no node. Sharing therefore costs nothing: two references to the
same node are two copies of the same index.

Three fields can point at more than one category — a reference parameter's target, a
property set's owner and a controller's target. Those pack the category into the top
four bits, so they are the only places a reader has to decode a category. Everywhere
else the field's type already says which vector to index.

Strings are interned into a single vector and referenced by index, which is what
makes the format small: the graph is full of repeated property names such as
`Offset`, `Progress` and `TrimEnd`.

Path geometry is flattened into a `[ubyte]` of opcodes and a `[float]` of operands
rather than a table per command, because a path can have thousands of commands and a
FlatBuffers table has a fixed overhead that would dominate.

Serialization is deterministic: the same graph always produces the same bytes, so
output can be diffed and cached.

## Versioning

The rules are at the top of `lottie_comp.fbs`. In short: fields may only be added,
never removed or reordered, and `SCHEMA_VERSION` is bumped when they are. A reader
refuses a buffer whose version is higher than the one it was built for, because such
a buffer may carry meaning in fields the reader does not look at.

## Regenerating

`flatc` output for both C# and C++ is checked in, so contributors do not need `flatc`
installed. After changing the schema:

```powershell
.\build\RegenerateFlatbuffers.ps1
```

The script restores vcpkg dependencies, then uses the restored host
`tools\flatbuffers\flatc.exe` to generate both bindings. It uses `VCPKG_ROOT`
when set and otherwise bootstraps the repo-local `.vcpkg` checkout. Override
`VcpkgRoot`, `InstalledDir`, or `HostTriplet` for a different installation.
Updating the FlatBuffers dependency requires updating the manifest and
the `Google.FlatBuffers` NuGet references, then regenerating the bindings.

## Consumers

| | |
| --- | --- |
| `source/CompDataFlatbuffer/CompositionSerializer.cs` | Writes a graph |
| `source/CompDataFlatbuffer/CompositionDeserializer.cs` | Reads one back, in C# |
| `source/LottieRuntime` | Reads one back using statically compiled WinUI 2 or WinUI 3 C++/WinRT source |
| `LottieGen -Language flatbuffer` | Produces a `.lcomp` file |
| `tests/CompDataFlatbuffer.Tests` | Asserts a graph survives the round trip unchanged |

When the requested UAP range needs different translated graphs, LottieGen writes
one `.lcomp` file per range. The file names contain the supported range, such as
`.uap12-plus.lcomp` or `.uap7-11.lcomp`; the consumer selects the appropriate
variant before calling the native reader.

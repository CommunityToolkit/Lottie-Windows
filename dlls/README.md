# Lottie-Windows/dlls directory

This directory contains projects that build DLL versions of the Lottie-Windows code. These exist in order to
have the compiler and build system enforce modularity, and the DLLs are never expected to be consumed by
any product (although you could consume them if you really wanted to).

## Modules
Conceptually, there are 4 *modules* in Lottie-Windows:
* LottieData
* LottieReader
* LottieToWinComp
* WinCompData

These modules exist to help abstract the design, and dependencies between each module must always
form a DAG (Directed Acyclic Graph).

When we build our binaries (Lottie-Windows nupkg, LottieGen command line tool, and the Lottie Viewer app) 
we include the source code from each module using shared projects, rather than consuming the modules' DLLs.
While we *could* have LottieGen and Lottie Viewer consume the Lottie-Windows nuget, that would require 
an extra unnecessary step, and in the case of LottieGen it would require an extra binary to be copied with
the tool.

Modularity is enforced by building the code as a set of dlls. This makes the compiler and build system
enforce modularity boundaries. If the DAG is broken, the build will break.

### Bottom line: 
Shared projects is how we do static libs in C#, and .NET assemblies is how we enforce modularity.

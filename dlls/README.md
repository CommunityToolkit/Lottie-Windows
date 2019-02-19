# Lottie-Windows/dlls directory

This directory contains projects that build DLL versions of the Lottie-Windows code. These exist in order to
have the compiler and build system enforce modularity, and the DLLs are not consumed by any other project.

The reason they exist is for code quality rather than packaging.

## Modular design
Conceptually, there are 5 *modules* in Lottie-Windows:


| Module          | Purpose          | Depends-on  |
| ----------      |:------------------| -----:|
| Lottie          | .json file to AnimatedVisual translator | LottieData, LottieReader, LottieToWinComp, WinCompData |
| LottieData      | data model for Lottie compositions |   - |
| LottieReader    | .json files to LottieComposition object | LottieData |
| LottieToWinComp | LottieComposition to WinCompData Visual tree translator |   LottieData, WinCompData |
| WinCompData     | data model for Windows.UI.Composition  |    - |

The modules help abstract the design, and dependencies between modules always form a DAG (Directed Acyclic Graph). 
The DAG is enforced by building each module as an assembly (DLL) so that **if the DAG is broken the build of the DLLs will break**.

But we don't actually use the DLLs in any of the other projects; instead, we mush all of the source code together using shared projects. This saves the extra step of building the Lottie-Windows nuget project before any other projects, and it means there are fewer binaries to be packaged up and copied around.

### Summary
* Shared projects give us the equivalent of C++ static libs.
* The dlls directory enforces modularity.

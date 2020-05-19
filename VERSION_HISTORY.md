# Version History

Lottie-Windows aligns its major version with the Windows Community Toolkit.

## 6.1.0
This release brings a new theme property binding feature, better Lottie feature support, performance improvements and bug fixes.

**Theme property binding**

This feature enables properties in Lottie files to be bound to values at runtime. This enables scenarios such as synchronizing colors in your Lottie file to the Windows theme colors.

Theme property binding was developed to support fill color binding, however it also supports StrokeWidth and opens the door to enabling other property bindings.

To make an After Effects color fill bound to a property at runtime, the name of the fill needs to include (CSS-like) syntax such as the following: "{color:var(Foreground)}". This will create a property in the generated code called "Foreground".

Property binding default values are exposed as constants. This allows these default values to be used in supporting code without having to instantiate the IAnimatedVisualSource object.

The ColorAsVector4(…) method in generated code is to help developers set color properties. Due to a limitation of the Windows.UI.Composition APIs, we can't use the Windows.UI.Xaml.Color type for doing property binding. We use Vector4 instead and this method ensures the conversion from color to Vector4 is done the same way by developers as is expected by generated code.

**Lottie feature support**
* Handle opacity and color animated on the same element.

**Code generation**
* Rich generated comments about the Lottie contents, and the code generation.
    * We now generate comments that identify the version of the tool, when it was run, the input file, the command line arguments, etc., etc., so that it's easier to keep track of how the file was generated, and to regenerate with identical options when the Lottie file changes.
    * We generate various tables in the comments to help developers understand the contents of the generated file. These include information about markers, and counts of some important objects. Object counts are useful for determining whether a change to a Lottie file may have regressed performance.

* New code generation options.
    * Added "-Namespace" option to put the generated code into a user's namespace.
    * Added "-Public" switch to generate public classes. Generated code defaults to internal, but can be overridden with this switch.
    * Added "-TestMode" switch to generate code that doesn't change given the same inputs (e.g. no timestamps are included in the output). Used for regression testing.

* Code quality improvements.
    * Abstracted common patterns (e.g. creating a shape and setting its fill) into methods. This results in smaller code that is easier to read.
    * Canonicalize progress remapping variables. These are the "t1", "t2", etc. variables that are used to support spatial Beziers. Canonicalizing can reduce the number of variables (and their associated costs).
    * Use "Visual.IsVisible" property for visibility control. This was previously not possible due to requirements to run downlevel (where this property was not available).
    * Use "CompositionShape.Scale" for visibility on shapes instead of TransformMatrix. This generates more efficient code and enables more optimizations.
    * Gradient stop optimization. After Effects does not remove redundant gradient stops, and simple gradients often have a redundant "middle" gradient stop. We can now detect and remove redundant gradient stops.
    * Eliminate redundant "Position" animations.
    * Compact expression strings to save some bytes.
    * Readability improvements for generated code.
    * Method naming improvements.
    * Matrices now have comments explaining what they do.

* C++/WinRT
    * Limited support for C++/WinRT. This is enough support to satisfy the needs of some Microsoft teams, but should not be considered general support yet. Bug reports are welcome though and we will work towards full support.

**Metadata handling**
* Added a general metadata facility for all CompositionObjects. This allows arbitrary data to be passed through the translator. It is used for Lottie metadata (e.g. markers), property binding, object names, and object descriptions.

**Parser performance**
* Removed dependency on the Newtonsoft JSON parser. We now use the System.Text.Json parser. Parsing performance improved ~50%.
* Parsing is more resilient to malformed and invalid Lottie files.

**Lottie Viewer**
* Remove support for code generation from the Lottie Viewer tool. Code generation can now only be done by the LottieGen.exe tool. This is so we can add lots of options to the code generator without complicating the Lottie Viewer UI. It also means there is only one way to generate code, which allows for more repeatable code generation in production environments.

**Bug fixes**
* Prevent use of PathKeyFrameAnimations before UAP v11. PathKeyFrameAnimations are unreliable on earlier versions and may result in a crash.
* Miter limit on strokes was being calculated incorrectly.

---
## 6.0.0

Support for ARM64.

The following Adobe After Effects are also supported:

**Layer types**
* Image (codegen only)

**Fill**
* Linear and Radial Gradient

**Mask**
* Subtract

**Track mattes**
* Alpha matte

Lottie-Windows is version-adaptive when animation features require different UAP versions.

---
## 5.1.1

Support for Bodymovin v5.5.0 with changes to the JSON schema.

---
## 5.1.0 — Initial Release

The following Adobe After Effects features are supported:

**Layer types**
* Precomp
* Null
* Solid
* Shape

**Transforms**
* Anchor Point
* Position
* Scale
* Rotation
* Opacity *
* Parenting

**Shape**
* Ellipse
* Rectangle
* Rounded Rectangle
* Group
* Trim Path
    * Simultaneously
* Merge Paths **
    * Merge
    * Add
    * Subtract
    * Intersect
    * Exclude Intersection

**Fill**
* Color
* Opacity *
* Fill Rule

**Stroke**
* Color
* Opacity *
* Width
* Line Cap
* Line Join
* Miter Limit
* Dashes

**Mask**
* Mask Path
* Add

**Interpolation**
* Linear 
* Cubic Bezier
* Hold
* Spatial Bezier
* Rove Across Time


\* Group opacity is not always rendered accurately as a performance trade-off. If color and opacity are both animated on the same object, opacity may be incorrectly rendered.

\** Merge Paths is not supported when the shapes being merged are themselves animated.

# Version History

Lottie-Windows aligns its major version with the Windows Community Toolkit.

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

## 5.1.1

Support for Bodymovin v5.5.0 with changes to the JSON schema.

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





// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

// A runtime interpreter for serialized Lottie compositions.
//
// This is the native counterpart of Instantiator.cs. It walks the FlatBuffer produced
// by CompositionSerializer and issues the same sequence of Compositor calls that the
// managed instantiator and the generated code issue, so the three produce the same
// visual tree.
//
// Three things shape the code:
//
//  * Size first. There is one function per node category rather than one per node
//    type, and the shared state of every node is applied by a single function that
//    takes the least derived type. Everything lives in one translation unit so that
//    the linker can discard what an application does not reach.
//
//  * Runtime cost second. Nodes and strings are realized once, then found by indexing
//    dense arrays. The FlatBuffer remains the object model.
//
//  * Transient memory third. Cache sizes come directly from the buffer. D2D geometry
//    remains cached during interpretation so shared subgraphs stay shared.
//
// Calls are made through the least derived interface that has the member being used.
// A node is only ever known by its concrete type inside the function that creates it;
// everywhere else it is held as Visual, CompositionShape, CompositionBrush and so on.
// That is what keeps the interpreter free of casts: there is no point at which it has
// to ask what a node actually is, because the buffer already said, and the answer was
// consumed at the moment of creation.

// C++/WinRT requires <unknwn.h> before projected headers.
#include <unknwn.h>

#include "LottieRuntime.h"

#include <cstdint>
#include <cstring>
#include <optional>
#include <string>
#include <vector>

#include <d2d1_1.h>
#include <shcore.h>
#include <shlwapi.h>
#include <windows.graphics.effects.interop.h>
#include <Windows.Graphics.Interop.h>

#include <winrt/Windows.Foundation.h>
#include <winrt/Windows.Foundation.Collections.h>
#include <winrt/Windows.Foundation.Metadata.h>
#include <winrt/Windows.Foundation.Numerics.h>
#include <winrt/Windows.Graphics.Effects.h>
#include <winrt/Windows.Storage.Streams.h>
#include <winrt/Windows.UI.h>
#include <winrt/Windows.UI.Composition.h>

#undef GetCurrentTime
#include <winrt/Windows.UI.Xaml.Media.h>

#include "Generated/lottie_comp_generated.h"

namespace Fb = CommunityToolkit::WinUI::Lottie::CompDataFlatbuffer::Schema;

using namespace winrt::Windows::Foundation::Numerics;
using namespace winrt::Windows::UI::Composition;

namespace
{
    // The index that the format uses for "no node". It is the same value in every
    // field that can hold an index.
    constexpr uint32_t NullIndex = 0xFFFFFFFFu;

    // The schema version this build understands. A buffer that declares a higher
    // version is rejected, because it may use fields that are not read here.
    constexpr uint16_t SupportedSchemaVersion = 1;

    // Object references that can point at more than one category pack the category
    // into the top four bits.
    constexpr uint32_t CategoryShift = 28;
    constexpr uint32_t IndexMask = (1u << CategoryShift) - 1u;

    [[noreturn]] void ThrowMalformed()
    {
        throw winrt::hresult_invalid_argument(L"The buffer is not a well formed Lottie composition.");
    }

    [[noreturn]] void ThrowUnsupported()
    {
        throw winrt::hresult_not_implemented(L"The composition uses a feature this build does not support.");
    }

    void Check(bool condition)
    {
        if (!condition)
        {
            ThrowMalformed();
        }
    }

    // Reads an entry from one of the buffer's node vectors. A vector that is absent
    // is treated as empty, so an index into it is out of range rather than a crash.
    template<typename T>
    T const* At(::flatbuffers::Vector<::flatbuffers::Offset<T>> const* vector, uint32_t index)
    {
        Check(vector != nullptr && index < vector->size());
        auto const* result = vector->Get(index);
        Check(result != nullptr);
        return result;
    }

    float3 ToFloat3(Fb::Vec3 const* value)
    {
        return { value->x(), value->y(), value->z() };
    }

    float2 ToFloat2(Fb::Vec2 const* value)
    {
        return { value->x(), value->y() };
    }

    float3x2 ToFloat3x2(Fb::Mat3x2 const* value)
    {
        return {
            value->m11(), value->m12(),
            value->m21(), value->m22(),
            value->m31(), value->m32(),
        };
    }

    float4x4 ToFloat4x4(Fb::Mat4x4 const* value)
    {
        return {
            value->m11(), value->m12(), value->m13(), value->m14(),
            value->m21(), value->m22(), value->m23(), value->m24(),
            value->m31(), value->m32(), value->m33(), value->m34(),
            value->m41(), value->m42(), value->m43(), value->m44(),
        };
    }

    winrt::Windows::UI::Color ToColor(Fb::Color const* value)
    {
        if (value == nullptr)
        {
            return { 0, 0, 0, 0 };
        }

        return { value->a(), value->r(), value->g(), value->b() };
    }

    // CanvasGeometryCombine and D2D1_COMBINE_MODE do not agree on the order of their
    // members, so the value from the buffer cannot simply be cast.
    D2D1_COMBINE_MODE ToCombineMode(uint8_t value)
    {
        switch (value)
        {
        case 0: return D2D1_COMBINE_MODE_UNION;
        case 1: return D2D1_COMBINE_MODE_EXCLUDE;
        case 2: return D2D1_COMBINE_MODE_INTERSECT;
        case 3: return D2D1_COMBINE_MODE_XOR;
        default: ThrowMalformed();
        }
    }

    // ---------------------------------------------------------------------------
    // Geometry.
    //
    // A CompositionPath is built from anything that implements IGeometrySource2D,
    // and the only way to give one real content is the interop interface that hands
    // out a D2D geometry. This is the smallest object that does that.
    // ---------------------------------------------------------------------------
    struct GeometrySource : winrt::implements<GeometrySource, winrt::Windows::Graphics::IGeometrySource2D, ABI::Windows::Graphics::IGeometrySource2DInterop>
    {
        GeometrySource(winrt::com_ptr<ID2D1Geometry> geometry) :
            m_geometry(std::move(geometry))
        {
        }

        IFACEMETHODIMP GetGeometry(ID2D1Geometry** value) noexcept override
        {
            m_geometry.copy_to(value);
            return S_OK;
        }

        IFACEMETHODIMP TryGetGeometryUsingFactory(ID2D1Factory*, ID2D1Geometry** value) noexcept override
        {
            *value = nullptr;
            return E_NOTIMPL;
        }

    private:
        winrt::com_ptr<ID2D1Geometry> m_geometry;
    };

    ID2D1Factory* D2DFactory()
    {
        // One factory for the lifetime of the process. The geometries it makes are
        // device independent, so nothing here depends on a device and the factory
        // never has to be recreated.
        static ID2D1Factory* const factory = []
        {
            ID2D1Factory* result = nullptr;
            winrt::check_hresult(D2D1CreateFactory(D2D1_FACTORY_TYPE_MULTI_THREADED, &result));
            return result;
        }();

        return factory;
    }

    // ---------------------------------------------------------------------------
    // Effects.
    //
    // Windows.UI.Composition accepts any IGraphicsEffect that also implements the
    // D2D interop interface, which is how it learns which D2D effect to instantiate
    // and what to set on it. Implementing that directly avoids taking a dependency
    // on Win2D for the two effects the translator emits.
    // ---------------------------------------------------------------------------
    struct Effect : winrt::implements<
        Effect,
        winrt::Windows::Graphics::Effects::IGraphicsEffect,
        winrt::Windows::Graphics::Effects::IGraphicsEffectSource,
        ABI::Windows::Graphics::Effects::IGraphicsEffectD2D1Interop>
    {
        Effect(GUID const& id, winrt::Windows::Foundation::IInspectable property) :
            m_id(id),
            m_property(std::move(property))
        {
        }

        winrt::hstring Name() const noexcept
        {
            return m_name;
        }

        void Name(winrt::hstring const& value)
        {
            m_name = value;
        }

        void AddSource(winrt::Windows::Graphics::Effects::IGraphicsEffectSource const& source)
        {
            m_sources.push_back(source);
        }

        IFACEMETHODIMP GetEffectId(GUID* id) noexcept override
        {
            *id = m_id;
            return S_OK;
        }

        IFACEMETHODIMP GetPropertyCount(UINT* count) noexcept override
        {
            *count = 1;
            return S_OK;
        }

        IFACEMETHODIMP GetProperty(UINT index, ABI::Windows::Foundation::IPropertyValue** value) noexcept override
        {
            *value = nullptr;

            if (index != 0)
            {
                return E_BOUNDS;
            }

            auto pv = m_property.try_as<ABI::Windows::Foundation::IPropertyValue>();
            if (pv)
            {
                *value = pv.detach();
                return S_OK;
            }
            return E_NOINTERFACE;
        }

        IFACEMETHODIMP GetSourceCount(UINT* count) noexcept override
        {
            *count = static_cast<UINT>(m_sources.size());
            return S_OK;
        }

        IFACEMETHODIMP GetSource(UINT index, ABI::Windows::Graphics::Effects::IGraphicsEffectSource** source) noexcept override
        {
            *source = nullptr;

            if (index >= m_sources.size())
            {
                return E_BOUNDS;
            }

            auto value = m_sources[index];
            *source = reinterpret_cast<ABI::Windows::Graphics::Effects::IGraphicsEffectSource*>(
                winrt::detach_abi(value));
            return S_OK;
        }

        IFACEMETHODIMP GetNamedPropertyMapping(
            LPCWSTR,
            UINT*,
            ABI::Windows::Graphics::Effects::GRAPHICS_EFFECT_PROPERTY_MAPPING*) noexcept override
        {
            return E_INVALIDARG;
        }

    private:
        GUID m_id;
        winrt::Windows::Foundation::IInspectable m_property;
        winrt::hstring m_name;
        std::vector<winrt::Windows::Graphics::Effects::IGraphicsEffectSource> m_sources;
    };
}

namespace
{
    // ---------------------------------------------------------------------------
    // The interpreter.
    //
    // One instance exists for the duration of one call to LoadComposition. It owns
    // the realization caches, which is the only state the walk needs: every other
    // question is answered by the buffer.
    // ---------------------------------------------------------------------------
    class Interpreter
    {
    public:
        Interpreter(Compositor const& compositor, Fb::LottieComposition const& root) :
            m_compositor(compositor),
            m_root(root),
            m_strings(Count(root.strings())),
            m_stringCached(Count(root.strings())),
            m_visuals(Count(root.visuals()), nullptr),
            m_shapes(Count(root.shapes()), nullptr),
            m_geometries(Count(root.geometries()), nullptr),
            m_paths(Count(root.canvas_geometries()), nullptr),
            m_canvasGeometries(Count(root.canvas_geometries())),
            m_canvasGeometryStates(Count(root.canvas_geometries())),
            m_brushes(Count(root.brushes()), nullptr),
            m_gradientStops(Count(root.gradient_stops()), nullptr),
            m_viewBoxes(Count(root.view_boxes()), nullptr),
            m_clips(Count(root.clips()), nullptr),
            m_shadows(Count(root.shadows()), nullptr),
            m_surfaces(Count(root.surfaces()), nullptr),
            m_effects(Count(root.effects()), nullptr),
            m_easings(Count(root.easings()), nullptr),
            m_animations(Count(root.animations()), nullptr),
            m_propertySets(Count(root.property_sets()), nullptr),
            m_controllers(Count(root.controllers()), nullptr)
        {
        }

        Visual Run()
        {
            auto result = GetVisual(m_root.root_visual());
            Check(result != nullptr);
            return result;
        }

    private:
        template<typename T>
        static size_t Count(T const* vector)
        {
            return vector == nullptr ? 0 : vector->size();
        }

        winrt::hstring GetString(uint32_t index)
        {
            if (index == NullIndex)
            {
                return {};
            }

            auto const* strings = m_root.strings();
            Check(strings != nullptr && index < strings->size());
            if (!m_stringCached[index])
            {
                auto const* value = strings->Get(index);
                Check(value != nullptr);

                m_strings[index] =
                    winrt::to_hstring(std::string_view(value->c_str(), value->size()));
                m_stringCached[index] = true;
            }

            return m_strings[index];
        }

        // -----------------------------------------------------------------------
        // Shared state.
        //
        // This is the reason the caches hold base types: everything an interpreter
        // does to every node is available on CompositionObject.
        // -----------------------------------------------------------------------
        void Initialize(CompositionObject const& target, Fb::CompObj const* source)
        {
            InitializeProperties(target, source);
            StartAnimations(target, source);
        }

        void InitializeProperties(CompositionObject const& target, Fb::CompObj const* source)
        {
            if (source == nullptr)
            {
                return;
            }

            if (auto comment = source->comment(); comment != NullIndex)
            {
                target.Comment(GetString(comment));
            }

            // The property set is realized through its owner rather than on its own,
            // so that a property set and the object that owns it cannot both create
            // it. Doing it here also breaks the cycle between the two.
            if (auto properties = source->properties(); properties != NullIndex)
            {
                RealizePropertySet(properties, target.Properties());
            }

        }

        void StartAnimations(CompositionObject const& target, Fb::CompObj const* source)
        {
            if (source == nullptr)
            {
                return;
            }

            auto const* animators = source->animators();
            if (animators == nullptr)
            {
                return;
            }

            for (uint32_t i = 0; i != animators->size(); ++i)
            {
                auto const* animator = animators->Get(i);
                Check(animator != nullptr);

                auto property = GetString(animator->property());
                auto animation = GetAnimation(animator->animation());
                Check(animation != nullptr);

                auto controllerIndex = animator->controller();
                auto customController =
                    controllerIndex != NullIndex && IsCustomController(controllerIndex)
                    ? GetController(controllerIndex)
                    : nullptr;

                if (customController != nullptr &&
                    animation.try_as<ExpressionAnimation>() != nullptr)
                {
                    ThrowMalformed();
                }

                if (customController != nullptr)
                {
                    target.StartAnimation(property, animation, customController);
                }
                else
                {
                    target.StartAnimation(property, animation);
                }

                if (controllerIndex != NullIndex && customController == nullptr)
                {
                    RealizeController(controllerIndex, target, property);
                }
            }
        }

        bool IsCustomController(uint32_t index) const
        {
            auto const* controller = At(m_root.controllers(), index);
            return controller->is_custom();
        }

        // -----------------------------------------------------------------------
        // Property sets.
        // -----------------------------------------------------------------------
        void RealizePropertySet(uint32_t index, CompositionPropertySet const& target)
        {
            Check(index < m_propertySets.size());

            if (m_propertySets[index] != nullptr)
            {
                return;
            }

            // Cached before the values are applied, because a value may be an
            // expression that refers back to this property set.
            m_propertySets[index] = target;

            auto const* source = At(m_root.property_sets(), index);

            if (auto const* values = source->values(); values != nullptr)
            {
                for (uint32_t i = 0; i != values->size(); ++i)
                {
                    auto const* value = values->Get(i);
                    Check(value != nullptr);

                    auto name = GetString(value->name());

                    switch (value->type())
                    {
                    case Fb::PropertyValueType::Color:
                        target.InsertColor(name, ToColor(value->color()));
                        break;
                    case Fb::PropertyValueType::Scalar:
                        target.InsertScalar(name, value->scalar());
                        break;
                    case Fb::PropertyValueType::Vector2:
                    {
                        auto const* vector = Vector(value);
                        target.InsertVector2(name, { vector->x(), vector->y() });
                        break;
                    }
                    case Fb::PropertyValueType::Vector3:
                    {
                        auto const* vector = Vector(value);
                        target.InsertVector3(name, { vector->x(), vector->y(), vector->z() });
                        break;
                    }
                    case Fb::PropertyValueType::Vector4:
                    {
                        auto const* vector = Vector(value);
                        target.InsertVector4(
                            name,
                            { vector->x(), vector->y(), vector->z(), vector->w() });
                        break;
                    }
                    case Fb::PropertyValueType::None:
                        break;
                    default:
                        ThrowMalformed();
                    }
                }
            }

            StartAnimations(target, source->base());
        }

        static Fb::Vec4 const* Vector(Fb::PropertyValue const* value)
        {
            auto const* result = value->vector();
            Check(result != nullptr);
            return result;
        }

        CompositionPropertySet GetPropertySet(uint32_t index)
        {
            Check(index < m_propertySets.size());

            if (m_propertySets[index] == nullptr)
            {
                auto const* source = At(m_root.property_sets(), index);

                if (auto owner = source->owner(); owner != NullIndex)
                {
                    // Realizing the owner populates the cache as a side effect,
                    // because Initialize realizes the owner's property set.
                    GetObjectReference(owner);
                }
                else
                {
                    // A property set with no owner is a standalone one, which is how
                    // the translator exposes theming properties.
                    RealizePropertySet(index, m_compositor.CreatePropertySet());
                }
            }

            Check(m_propertySets[index] != nullptr);
            return m_propertySets[index];
        }

        // -----------------------------------------------------------------------
        // Controllers.
        // -----------------------------------------------------------------------
        void RealizeController(uint32_t index, AnimationController const& controller)
        {
            Check(index < m_controllers.size());

            if (m_controllers[index] != nullptr)
            {
                return;
            }

            m_controllers[index] = controller;

            auto const* source = At(m_root.controllers(), index);

            if (source->is_paused())
            {
                controller.Pause();
            }

            Initialize(controller, source->base());
        }

        void RealizeController(
            uint32_t index,
            CompositionObject const& target,
            winrt::hstring const& property)
        {
            auto controller = target.TryGetAnimationController(property);
            Check(controller != nullptr);
            RealizeController(index, controller);
        }

        AnimationController GetController(uint32_t index)
        {
            Check(index < m_controllers.size());
            auto const* source = At(m_root.controllers(), index);

            if (m_controllers[index] == nullptr)
            {
                if (source->is_custom())
                {
                    RealizeController(index, m_compositor.CreateAnimationController());
                }
                else
                {
                    GetObjectReference(source->target_object());
                }
            }

            Check(m_controllers[index] != nullptr);
            return m_controllers[index];
        }

        // -----------------------------------------------------------------------
        // Object references.
        //
        // Only the three fields that can point at more than one kind of node are
        // packed this way, so this is the only place a category has to be decoded.
        // -----------------------------------------------------------------------
        CompositionObject GetObjectReference(uint32_t reference)
        {
            if (reference == NullIndex)
            {
                return nullptr;
            }

            auto const index = reference & IndexMask;

            switch (static_cast<Fb::ObjectCategory>(reference >> CategoryShift))
            {
            case Fb::ObjectCategory::Visual: return GetVisual(index);
            case Fb::ObjectCategory::Shape: return GetShape(index);
            case Fb::ObjectCategory::Geometry: return GetGeometry(index);
            case Fb::ObjectCategory::Brush: return GetBrush(index);
            case Fb::ObjectCategory::Animation: return GetAnimation(index);
            case Fb::ObjectCategory::Easing: return GetEasing(index);
            case Fb::ObjectCategory::PropertySet: return GetPropertySet(index);
            case Fb::ObjectCategory::Clip: return GetClip(index);
            case Fb::ObjectCategory::Shadow: return GetShadow(index);
            case Fb::ObjectCategory::GradientStop: return GetGradientStop(index);
            case Fb::ObjectCategory::ViewBox: return GetViewBox(index);
            case Fb::ObjectCategory::Surface:
            {
                // Only a visual surface is a CompositionObject; an image surface is
                // not, and nothing can legitimately reference one this way.
                auto surface = GetSurface(index);
                auto result = surface.try_as<CompositionObject>();
                Check(result != nullptr);
                return result;
            }
            case Fb::ObjectCategory::Controller: return GetController(index);
            default:
                ThrowMalformed();
            }
        }

        // -----------------------------------------------------------------------
        // Visuals.
        // -----------------------------------------------------------------------
        Visual GetVisual(uint32_t index)
        {
            if (index == NullIndex)
            {
                return nullptr;
            }

            Check(index < m_visuals.size());

            if (m_visuals[index] != nullptr)
            {
                return m_visuals[index];
            }

            auto const* source = At(m_root.visuals(), index);

            // The concrete type is known only here. Everything below this point,
            // including the recursive walk into the children, uses the base type.
            ContainerVisual container{ nullptr };

            switch (source->kind())
            {
            case Fb::VisualKind::Container:
                container = m_compositor.CreateContainerVisual();
                break;
            case Fb::VisualKind::Sprite:
            {
                auto sprite = m_compositor.CreateSpriteVisual();
                m_visuals[index] = sprite;
                if (auto brush = source->brush(); brush != NullIndex)
                {
                    sprite.Brush(GetBrush(brush));
                }

                if (auto shadow = source->shadow(); shadow != NullIndex)
                {
                    sprite.Shadow(GetShadow(shadow));
                }

                container = sprite;
                break;
            }
            case Fb::VisualKind::Shape:
            {
                auto shapeVisual = m_compositor.CreateShapeVisual();
                m_visuals[index] = shapeVisual;

                if (auto viewBox = source->view_box(); viewBox != NullIndex)
                {
                    shapeVisual.ViewBox(GetViewBox(viewBox));
                }

                if (auto const* shapes = source->shapes(); shapes != nullptr)
                {
                    auto collection = shapeVisual.Shapes();
                    for (uint32_t i = 0; i != shapes->size(); ++i)
                    {
                        auto shape = GetShape(shapes->Get(i));
                        Check(shape != nullptr);
                        collection.Append(shape);
                    }
                }

                container = shapeVisual;
                break;
            }
            case Fb::VisualKind::Layer:
            {
                auto layer = m_compositor.CreateLayerVisual();
                m_visuals[index] = layer;

                if (auto shadow = source->shadow(); shadow != NullIndex)
                {
                    layer.Shadow(GetShadow(shadow));
                }

                container = layer;
                break;
            }
            default:
                ThrowMalformed();
            }

            // Any case that resolves referenced objects must populate m_visuals
            // first. The assignment below only precedes the child walk.
            // Cached before the children are walked, so that a cycle terminates.
            m_visuals[index] = container;

            ApplyVisualProperties(container, source);
            InitializeProperties(container, source->base());

            if (auto const* children = source->children(); children != nullptr)
            {
                auto collection = container.Children();
                for (uint32_t i = 0; i != children->size(); ++i)
                {
                    auto child = GetVisual(children->Get(i));
                    Check(child != nullptr);
                    collection.InsertAtTop(child);
                }
            }

            StartAnimations(container, source->base());

            return container;
        }

        // Takes a Visual rather than any of the four concrete kinds, because every
        // property here is declared on Visual.
        void ApplyVisualProperties(Visual const& target, Fb::Visual const* source)
        {
            if (auto value = source->border_mode())
            {
                target.BorderMode(static_cast<CompositionBorderMode>(*value));
            }

            if (auto const* value = source->center_point())
            {
                target.CenterPoint(ToFloat3(value));
            }

            if (auto value = source->clip(); value != NullIndex)
            {
                target.Clip(GetClip(value));
            }

            if (auto value = source->is_visible())
            {
                target.IsVisible(*value);
            }

            if (auto const* value = source->offset())
            {
                target.Offset(ToFloat3(value));
            }

            if (auto value = source->opacity())
            {
                target.Opacity(*value);
            }

            if (auto value = source->rotation_angle_in_degrees())
            {
                target.RotationAngleInDegrees(*value);
            }

            if (auto const* value = source->rotation_axis())
            {
                target.RotationAxis(ToFloat3(value));
            }

            if (auto const* value = source->scale())
            {
                target.Scale(ToFloat3(value));
            }

            if (auto const* value = source->size())
            {
                target.Size(ToFloat2(value));
            }

            if (auto const* value = source->transform_matrix())
            {
                target.TransformMatrix(ToFloat4x4(value));
            }
        }

        // -----------------------------------------------------------------------
        // Shapes.
        // -----------------------------------------------------------------------
        CompositionShape GetShape(uint32_t index)
        {
            if (index == NullIndex)
            {
                return nullptr;
            }

            Check(index < m_shapes.size());

            if (m_shapes[index] != nullptr)
            {
                return m_shapes[index];
            }

            auto const* source = At(m_root.shapes(), index);

            CompositionShape result{ nullptr };

            switch (source->kind())
            {
            case Fb::ShapeKind::Container:
            {
                auto container = m_compositor.CreateContainerShape();
                m_shapes[index] = container;

                if (auto const* shapes = source->shapes(); shapes != nullptr)
                {
                    auto collection = container.Shapes();
                    for (uint32_t i = 0; i != shapes->size(); ++i)
                    {
                        auto shape = GetShape(shapes->Get(i));
                        Check(shape != nullptr);
                        collection.Append(shape);
                    }
                }

                result = container;
                break;
            }
            case Fb::ShapeKind::Sprite:
            {
                auto sprite = m_compositor.CreateSpriteShape();
                m_shapes[index] = sprite;
                ApplySpriteShapeProperties(sprite, source);
                result = sprite;
                break;
            }
            default:
                ThrowMalformed();
            }

            // Any case that resolves referenced objects must populate m_shapes
            // first. The assignment below cannot terminate recursive graphs.
            m_shapes[index] = result;

            ApplyShapeProperties(result, source);
            Initialize(result, source->base());

            return result;
        }

        void ApplyShapeProperties(CompositionShape const& target, Fb::Shape const* source)
        {
            if (auto const* value = source->center_point())
            {
                target.CenterPoint(ToFloat2(value));
            }

            if (auto const* value = source->offset())
            {
                target.Offset(ToFloat2(value));
            }

            if (auto value = source->rotation_angle_in_degrees())
            {
                target.RotationAngleInDegrees(*value);
            }

            if (auto const* value = source->scale())
            {
                target.Scale(ToFloat2(value));
            }

            if (auto const* value = source->transform_matrix())
            {
                target.TransformMatrix(ToFloat3x2(value));
            }
        }

        void ApplySpriteShapeProperties(CompositionSpriteShape const& target, Fb::Shape const* source)
        {
            if (auto value = source->geometry(); value != NullIndex)
            {
                target.Geometry(GetGeometry(value));
            }

            if (auto value = source->fill_brush(); value != NullIndex)
            {
                target.FillBrush(GetBrush(value));
            }

            if (auto value = source->stroke_brush(); value != NullIndex)
            {
                target.StrokeBrush(GetBrush(value));
            }

            if (auto value = source->is_stroke_non_scaling())
            {
                target.IsStrokeNonScaling(*value);
            }

            if (auto value = source->stroke_dash_offset())
            {
                target.StrokeDashOffset(*value);
            }

            if (auto const* values = source->stroke_dash_array(); values != nullptr)
            {
                auto dashes = target.StrokeDashArray();
                for (uint32_t i = 0; i != values->size(); ++i)
                {
                    dashes.Append(values->Get(i));
                }
            }

            if (auto value = source->stroke_dash_cap())
            {
                target.StrokeDashCap(static_cast<CompositionStrokeCap>(*value));
            }

            if (auto value = source->stroke_start_cap())
            {
                target.StrokeStartCap(static_cast<CompositionStrokeCap>(*value));
            }

            if (auto value = source->stroke_end_cap())
            {
                target.StrokeEndCap(static_cast<CompositionStrokeCap>(*value));
            }

            if (auto value = source->stroke_line_join())
            {
                target.StrokeLineJoin(static_cast<CompositionStrokeLineJoin>(*value));
            }

            if (auto value = source->stroke_miter_limit())
            {
                target.StrokeMiterLimit(static_cast<float>(*value));
            }

            if (auto value = source->stroke_thickness())
            {
                target.StrokeThickness(*value);
            }
        }

        // -----------------------------------------------------------------------
        // Geometries.
        // -----------------------------------------------------------------------
        CompositionGeometry GetGeometry(uint32_t index)
        {
            if (index == NullIndex)
            {
                return nullptr;
            }

            Check(index < m_geometries.size());

            if (m_geometries[index] != nullptr)
            {
                return m_geometries[index];
            }

            auto const* source = At(m_root.geometries(), index);

            CompositionGeometry result{ nullptr };

            switch (source->kind())
            {
            case Fb::GeometryKind::Path:
            {
                auto path = source->path();
                result = m_compositor.CreatePathGeometry(
                    path == NullIndex ? nullptr : GetPath(path));
                break;
            }
            case Fb::GeometryKind::Rectangle:
            {
                auto rectangle = m_compositor.CreateRectangleGeometry();
                if (auto const* value = source->offset())
                {
                    rectangle.Offset(ToFloat2(value));
                }

                if (auto const* value = source->size())
                {
                    rectangle.Size(ToFloat2(value));
                }

                result = rectangle;
                break;
            }
            case Fb::GeometryKind::RoundedRectangle:
            {
                auto rounded = m_compositor.CreateRoundedRectangleGeometry();
                if (auto const* value = source->offset())
                {
                    rounded.Offset(ToFloat2(value));
                }

                if (auto const* value = source->size())
                {
                    rounded.Size(ToFloat2(value));
                }

                if (auto const* value = source->corner_radius())
                {
                    rounded.CornerRadius(ToFloat2(value));
                }

                result = rounded;
                break;
            }
            case Fb::GeometryKind::Ellipse:
            {
                auto ellipse = m_compositor.CreateEllipseGeometry();
                if (auto const* value = source->center())
                {
                    ellipse.Center(ToFloat2(value));
                }

                if (auto const* value = source->radius())
                {
                    ellipse.Radius(ToFloat2(value));
                }

                result = ellipse;
                break;
            }
            default:
                ThrowMalformed();
            }

            m_geometries[index] = result;

            if (auto value = source->trim_start())
            {
                result.TrimStart(*value);
            }

            if (auto value = source->trim_end())
            {
                result.TrimEnd(*value);
            }

            if (auto value = source->trim_offset())
            {
                result.TrimOffset(*value);
            }

            Initialize(result, source->base());

            return result;
        }

        // -----------------------------------------------------------------------
        // Paths.
        //
        // A path is not a CompositionObject, so it has no shared state and no
        // animators; it is only ever a value.
        // -----------------------------------------------------------------------
        CompositionPath GetPath(uint32_t index)
        {
            Check(index < m_paths.size());

            if (m_paths[index] == nullptr)
            {
                auto geometry = BuildGeometry(index);
                m_paths[index] = CompositionPath(winrt::make<GeometrySource>(std::move(geometry)));
            }

            return m_paths[index];
        }

        winrt::com_ptr<ID2D1Geometry> BuildGeometry(uint32_t index)
        {
            Check(index < m_canvasGeometries.size());

            switch (m_canvasGeometryStates[index])
            {
            case CanvasGeometryState::Realized:
                return m_canvasGeometries[index];
            case CanvasGeometryState::Realizing:
                ThrowMalformed();
            case CanvasGeometryState::Unrealized:
                break;
            }

            m_canvasGeometryStates[index] = CanvasGeometryState::Realizing;
            auto result = BuildGeometryCore(index);
            Check(result != nullptr);
            m_canvasGeometries[index] = result;
            m_canvasGeometryStates[index] = CanvasGeometryState::Realized;
            return result;
        }

        winrt::com_ptr<ID2D1Geometry> BuildGeometryCore(uint32_t index)
        {
            auto const* source = At(m_root.canvas_geometries(), index);

            switch (source->kind())
            {
            case Fb::CanvasGeometryKind::Combination:
            {
                auto a = BuildGeometry(Required(source->a()));
                auto b = BuildGeometry(Required(source->b()));

                winrt::com_ptr<ID2D1PathGeometry> result;
                winrt::check_hresult(D2DFactory()->CreatePathGeometry(result.put()));

                winrt::com_ptr<ID2D1GeometrySink> sink;
                winrt::check_hresult(result->Open(sink.put()));

                auto const matrix = source->matrix();
                auto const transform = matrix == nullptr
                    ? D2D1::Matrix3x2F::Identity()
                    : D2D1::Matrix3x2F(
                        matrix->m11(), matrix->m12(),
                        matrix->m21(), matrix->m22(),
                        matrix->m31(), matrix->m32());

                winrt::check_hresult(a->CombineWithGeometry(
                    b.get(),
                    ToCombineMode(source->combine_mode()),
                    transform,
                    sink.get()));

                winrt::check_hresult(sink->Close());
                return result;
            }
            case Fb::CanvasGeometryKind::Ellipse:
            {
                winrt::com_ptr<ID2D1EllipseGeometry> result;
                winrt::check_hresult(D2DFactory()->CreateEllipseGeometry(
                    D2D1::Ellipse(
                        D2D1::Point2F(source->x(), source->y()),
                        source->radius_x(),
                        source->radius_y()),
                    result.put()));
                return result;
            }
            case Fb::CanvasGeometryKind::RoundedRectangle:
            {
                winrt::com_ptr<ID2D1RoundedRectangleGeometry> result;
                winrt::check_hresult(D2DFactory()->CreateRoundedRectangleGeometry(
                    D2D1::RoundedRect(
                        D2D1::RectF(
                            source->x(),
                            source->y(),
                            source->x() + source->w(),
                            source->y() + source->h()),
                        source->radius_x(),
                        source->radius_y()),
                    result.put()));
                return result;
            }
            case Fb::CanvasGeometryKind::TransformedGeometry:
            {
                auto inner = BuildGeometry(Required(source->source()));

                auto const* matrix = source->matrix();
                Check(matrix != nullptr);

                winrt::com_ptr<ID2D1TransformedGeometry> result;
                winrt::check_hresult(D2DFactory()->CreateTransformedGeometry(
                    inner.get(),
                    D2D1::Matrix3x2F(
                        matrix->m11(), matrix->m12(),
                        matrix->m21(), matrix->m22(),
                        matrix->m31(), matrix->m32()),
                    result.put()));
                return result;
            }
            case Fb::CanvasGeometryKind::Group:
            {
                auto const* geometries = source->geometries();
                Check(geometries != nullptr);

                std::vector<winrt::com_ptr<ID2D1Geometry>> owned;
                std::vector<ID2D1Geometry*> raw;
                owned.reserve(geometries->size());
                raw.reserve(geometries->size());

                for (uint32_t i = 0; i != geometries->size(); ++i)
                {
                    owned.push_back(BuildGeometry(geometries->Get(i)));
                    raw.push_back(owned.back().get());
                }

                winrt::com_ptr<ID2D1GeometryGroup> result;
                winrt::check_hresult(D2DFactory()->CreateGeometryGroup(
                    static_cast<D2D1_FILL_MODE>(source->fill_rule()),
                    raw.data(),
                    static_cast<UINT32>(raw.size()),
                    result.put()));
                return result;
            }
            case Fb::CanvasGeometryKind::Path:
                return BuildPath(source);
            default:
                ThrowMalformed();
            }
        }

        // Replays the flattened command stream. The operands of every command are
        // consecutive in one array, which is why there is no table per command.
        winrt::com_ptr<ID2D1Geometry> BuildPath(Fb::CanvasGeometry const* source)
        {
            winrt::com_ptr<ID2D1PathGeometry> result;
            winrt::check_hresult(D2DFactory()->CreatePathGeometry(result.put()));

            winrt::com_ptr<ID2D1GeometrySink> sink;
            winrt::check_hresult(result->Open(sink.put()));

            sink->SetFillMode(static_cast<D2D1_FILL_MODE>(source->fill_rule()));

            auto const* ops = source->ops();
            auto const* operands = source->operands();

            uint32_t next = 0;
            bool inFigure = false;

            auto take = [&](uint32_t count) -> float const*
            {
                Check(operands != nullptr && next + count <= operands->size());
                auto const* values = operands->data() + next;
                next += count;
                return values;
            };

            for (uint32_t i = 0; ops != nullptr && i != ops->size(); ++i)
            {
                switch (static_cast<Fb::PathOp>(ops->Get(i)))
                {
                case Fb::PathOp::BeginFigure:
                {
                    Check(!inFigure);
                    auto const* v = take(2);

                    // Every figure the translator emits is filled; a hollow figure
                    // would need a begin flag in the format, which it does not have
                    // because the object model has no way to express one.
                    sink->BeginFigure(D2D1::Point2F(v[0], v[1]), D2D1_FIGURE_BEGIN_FILLED);
                    inFigure = true;
                    break;
                }
                case Fb::PathOp::EndFigure:
                {
                    Check(inFigure);
                    auto const* v = take(1);
                    sink->EndFigure(v[0] != 0 ? D2D1_FIGURE_END_CLOSED : D2D1_FIGURE_END_OPEN);
                    inFigure = false;
                    break;
                }
                case Fb::PathOp::AddLine:
                {
                    Check(inFigure);
                    auto const* v = take(2);
                    sink->AddLine(D2D1::Point2F(v[0], v[1]));
                    break;
                }
                case Fb::PathOp::AddCubicBezier:
                {
                    Check(inFigure);
                    auto const* v = take(6);
                    sink->AddBezier(D2D1::BezierSegment(
                        D2D1::Point2F(v[0], v[1]),
                        D2D1::Point2F(v[2], v[3]),
                        D2D1::Point2F(v[4], v[5])));
                    break;
                }
                default:
                    ThrowMalformed();
                }
            }

            Check(!inFigure);
            winrt::check_hresult(sink->Close());

            return result;
        }

        static uint32_t Required(uint32_t index)
        {
            Check(index != NullIndex);
            return index;
        }

        // -----------------------------------------------------------------------
        // Brushes.
        // -----------------------------------------------------------------------
        CompositionBrush GetBrush(uint32_t index)
        {
            if (index == NullIndex)
            {
                return nullptr;
            }

            Check(index < m_brushes.size());

            if (m_brushes[index] != nullptr)
            {
                return m_brushes[index];
            }

            auto const* source = At(m_root.brushes(), index);

            CompositionBrush result{ nullptr };

            switch (source->kind())
            {
            case Fb::BrushKind::Color:
                result = m_compositor.CreateColorBrush(ToColor(source->color()));
                break;
            case Fb::BrushKind::LinearGradient:
            {
                auto brush = m_compositor.CreateLinearGradientBrush();
                m_brushes[index] = brush;

                if (auto const* value = source->start_point())
                {
                    brush.StartPoint(ToFloat2(value));
                }

                if (auto const* value = source->end_point())
                {
                    brush.EndPoint(ToFloat2(value));
                }

                ApplyGradientBrushProperties(brush, source);
                result = brush;
                break;
            }
            case Fb::BrushKind::RadialGradient:
            {
                auto brush = m_compositor.CreateRadialGradientBrush();
                m_brushes[index] = brush;

                if (auto const* value = source->ellipse_center())
                {
                    brush.EllipseCenter(ToFloat2(value));
                }

                if (auto const* value = source->ellipse_radius())
                {
                    brush.EllipseRadius(ToFloat2(value));
                }

                if (auto const* value = source->gradient_origin_offset())
                {
                    brush.GradientOriginOffset(ToFloat2(value));
                }

                ApplyGradientBrushProperties(brush, source);
                result = brush;
                break;
            }
            case Fb::BrushKind::Surface:
                result = m_compositor.CreateSurfaceBrush(GetSurface(Required(source->surface())));
                break;
            case Fb::BrushKind::Mask:
            {
                auto brush = m_compositor.CreateMaskBrush();
                m_brushes[index] = brush;
                brush.Source(GetBrush(source->source()));
                brush.Mask(GetBrush(source->mask()));
                result = brush;
                break;
            }
            case Fb::BrushKind::Effect:
            {
                auto effect = GetEffect(Required(source->effect()));
                auto brush = m_compositor.CreateEffectFactory(effect).CreateBrush();
                m_brushes[index] = brush;

                if (auto const* parameters = source->source_parameters(); parameters != nullptr)
                {
                    for (uint32_t i = 0; i != parameters->size(); ++i)
                    {
                        auto const* parameter = parameters->Get(i);
                        Check(parameter != nullptr);
                        brush.SetSourceParameter(
                            GetString(parameter->name()),
                            GetBrush(parameter->brush()));
                    }
                }

                result = brush;
                break;
            }
            default:
                ThrowMalformed();
            }

            // Any case that resolves referenced objects must populate m_brushes
            // first. The assignment below cannot terminate recursive graphs.
            m_brushes[index] = result;

            Initialize(result, source->base());

            return result;
        }

        void ApplyGradientBrushProperties(CompositionGradientBrush const& target, Fb::Brush const* source)
        {
            if (auto const* value = source->anchor_point())
            {
                target.AnchorPoint(ToFloat2(value));
            }

            if (auto const* value = source->center_point())
            {
                target.CenterPoint(ToFloat2(value));
            }

            if (auto value = source->extend_mode())
            {
                target.ExtendMode(static_cast<CompositionGradientExtendMode>(*value));
            }

            if (auto value = source->interpolation_space())
            {
                target.InterpolationSpace(static_cast<CompositionColorSpace>(*value));
            }

            if (auto value = source->mapping_mode())
            {
                target.MappingMode(static_cast<CompositionMappingMode>(*value));
            }

            if (auto const* value = source->offset())
            {
                target.Offset(ToFloat2(value));
            }

            if (auto value = source->rotation_angle_in_degrees())
            {
                target.RotationAngleInDegrees(*value);
            }

            if (auto const* value = source->scale())
            {
                target.Scale(ToFloat2(value));
            }

            if (auto const* value = source->transform_matrix())
            {
                target.TransformMatrix(ToFloat3x2(value));
            }

            if (auto const* stops = source->color_stops(); stops != nullptr)
            {
                auto collection = target.ColorStops();
                for (uint32_t i = 0; i != stops->size(); ++i)
                {
                    collection.Append(GetGradientStop(stops->Get(i)));
                }
            }
        }

        CompositionColorGradientStop GetGradientStop(uint32_t index)
        {
            Check(index < m_gradientStops.size());

            if (m_gradientStops[index] == nullptr)
            {
                auto const* source = At(m_root.gradient_stops(), index);

                auto stop = m_compositor.CreateColorGradientStop(
                    source->offset(),
                    ToColor(source->color()));

                m_gradientStops[index] = stop;
                Initialize(stop, source->base());
            }

            return m_gradientStops[index];
        }

        // -----------------------------------------------------------------------
        // Clips, view boxes, shadows and surfaces.
        // -----------------------------------------------------------------------
        CompositionClip GetClip(uint32_t index)
        {
            Check(index < m_clips.size());

            if (m_clips[index] != nullptr)
            {
                return m_clips[index];
            }

            auto const* source = At(m_root.clips(), index);

            CompositionClip result{ nullptr };

            switch (source->kind())
            {
            case Fb::ClipKind::Inset:
            {
                auto clip = m_compositor.CreateInsetClip();

                if (auto value = source->left_inset())
                {
                    clip.LeftInset(*value);
                }

                if (auto value = source->right_inset())
                {
                    clip.RightInset(*value);
                }

                if (auto value = source->top_inset())
                {
                    clip.TopInset(*value);
                }

                if (auto value = source->bottom_inset())
                {
                    clip.BottomInset(*value);
                }

                result = clip;
                break;
            }
            case Fb::ClipKind::Geometric:
            {
                auto clip = m_compositor.CreateGeometricClip();
                m_clips[index] = clip;
                clip.Geometry(GetGeometry(source->geometry()));
                result = clip;
                break;
            }
            default:
                ThrowMalformed();
            }

            // Any case that resolves referenced objects must populate m_clips
            // first. The assignment below cannot terminate recursive graphs.
            m_clips[index] = result;

            if (auto const* value = source->center_point())
            {
                result.CenterPoint(ToFloat2(value));
            }

            if (auto const* value = source->scale())
            {
                result.Scale(ToFloat2(value));
            }

            Initialize(result, source->base());

            return result;
        }

        CompositionViewBox GetViewBox(uint32_t index)
        {
            Check(index < m_viewBoxes.size());

            if (m_viewBoxes[index] == nullptr)
            {
                auto const* source = At(m_root.view_boxes(), index);

                auto viewBox = m_compositor.CreateViewBox();
                m_viewBoxes[index] = viewBox;

                if (auto const* value = source->size())
                {
                    viewBox.Size(ToFloat2(value));
                }

                Initialize(viewBox, source->base());
            }

            return m_viewBoxes[index];
        }

        CompositionShadow GetShadow(uint32_t index)
        {
            Check(index < m_shadows.size());

            if (m_shadows[index] != nullptr)
            {
                return m_shadows[index];
            }

            auto const* source = At(m_root.shadows(), index);
            Check(source->kind() == Fb::ShadowKind::Drop);

            auto shadow = m_compositor.CreateDropShadow();
            m_shadows[index] = shadow;

            if (auto value = source->blur_radius())
            {
                shadow.BlurRadius(*value);
            }

            if (auto const* value = source->color())
            {
                shadow.Color(ToColor(value));
            }

            if (auto mask = source->mask(); mask != NullIndex)
            {
                shadow.Mask(GetBrush(mask));
            }

            if (auto const* value = source->offset())
            {
                shadow.Offset(ToFloat3(value));
            }

            if (auto value = source->opacity())
            {
                shadow.Opacity(*value);
            }

            if (auto value = source->source_policy())
            {
                shadow.SourcePolicy(static_cast<CompositionDropShadowSourcePolicy>(*value));
            }

            Initialize(shadow, source->base());

            return shadow;
        }

        ICompositionSurface GetSurface(uint32_t index)
        {
            Check(index < m_surfaces.size());

            if (m_surfaces[index] != nullptr)
            {
                return m_surfaces[index];
            }

            auto const* source = At(m_root.surfaces(), index);

            switch (source->kind())
            {
            case Fb::SurfaceKind::VisualSurface:
            {
                auto surface = m_compositor.CreateVisualSurface();
                m_surfaces[index] = surface;

                if (auto value = source->source_visual(); value != NullIndex)
                {
                    surface.SourceVisual(GetVisual(value));
                }

                if (auto const* value = source->source_size())
                {
                    surface.SourceSize(ToFloat2(value));
                }

                if (auto const* value = source->source_offset())
                {
                    surface.SourceOffset(ToFloat2(value));
                }

                Initialize(surface, source->base());
                break;
            }
            case Fb::SurfaceKind::LoadedImageFromUri:
            {
                auto const uri = source->uri();
                Check(uri != NullIndex);
                m_surfaces[index] =
                    winrt::Windows::UI::Xaml::Media::LoadedImageSurface::StartLoadFromUri(
                        winrt::Windows::Foundation::Uri(GetString(uri)));
                break;
            }
            case Fb::SurfaceKind::LoadedImageFromStream:
            {
                auto const* bytes = source->bytes();
                Check(bytes != nullptr);

                winrt::com_ptr<IStream> byteStream;
                byteStream.attach(SHCreateMemStream(bytes->data(), bytes->size()));
                Check(byteStream != nullptr);

                winrt::Windows::Storage::Streams::IRandomAccessStream stream{ nullptr };
                winrt::check_hresult(CreateRandomAccessStreamOverStream(
                    byteStream.get(),
                    BSOS_DEFAULT,
                    winrt::guid_of<decltype(stream)>(),
                    winrt::put_abi(stream)));

                m_surfaces[index] =
                    winrt::Windows::UI::Xaml::Media::LoadedImageSurface::StartLoadFromStream(
                        stream);
                break;
            }
            default:
                ThrowMalformed();
            }

            return m_surfaces[index];
        }

        winrt::Windows::Graphics::Effects::IGraphicsEffect GetEffect(uint32_t index)
        {
            Check(index < m_effects.size());

            if (m_effects[index] != nullptr)
            {
                return m_effects[index];
            }

            auto const* source = At(m_root.effects(), index);

            winrt::com_ptr<Effect> effect;

            switch (source->kind())
            {
            case Fb::EffectKind::Composite:
                // CanvasComposite and D2D1_COMPOSITE_MODE agree on every member, so
                // the mode from the buffer is the D2D value.
                effect = winrt::make_self<Effect>(
                    CLSID_D2D1Composite,
                    winrt::box_value(static_cast<uint32_t>(source->mode())));
                break;
            case Fb::EffectKind::GaussianBlur:
                effect = winrt::make_self<Effect>(
                    CLSID_D2D1GaussianBlur,
                    winrt::box_value(source->blur_amount()));
                break;
            default:
                ThrowMalformed();
            }

            // The sources are the names the brush later binds real brushes to. The
            // composition engine matches them up by name.
            if (auto const* sources = source->sources(); sources != nullptr)
            {
                for (uint32_t i = 0; i != sources->size(); ++i)
                {
                    effect->AddSource(CompositionEffectSourceParameter(GetString(sources->Get(i))));
                }
            }

            auto result = effect.as<winrt::Windows::Graphics::Effects::IGraphicsEffect>();
            m_effects[index] = result;
            return result;
        }

        // -----------------------------------------------------------------------
        // Easings.
        // -----------------------------------------------------------------------
        CompositionEasingFunction GetEasing(uint32_t index)
        {
            if (index == NullIndex)
            {
                return nullptr;
            }

            Check(index < m_easings.size());

            if (m_easings[index] != nullptr)
            {
                return m_easings[index];
            }

            auto const* source = At(m_root.easings(), index);

            CompositionEasingFunction result{ nullptr };

            switch (source->kind())
            {
            case Fb::EasingKind::Linear:
                result = m_compositor.CreateLinearEasingFunction();
                break;
            case Fb::EasingKind::CubicBezier:
            {
                auto const* first = source->control_point_1();
                auto const* second = source->control_point_2();
                result = m_compositor.CreateCubicBezierEasingFunction(
                    first == nullptr ? float2{ 0, 0 } : ToFloat2(first),
                    second == nullptr ? float2{ 0, 0 } : ToFloat2(second));
                break;
            }
            case Fb::EasingKind::Step:
            {
                auto stepCount = source->step_count();
                auto step = m_compositor.CreateStepEasingFunction(
                    stepCount ? *stepCount : 1);

                if (auto value = source->initial_step())
                {
                    step.InitialStep(*value);
                }

                if (auto value = source->final_step())
                {
                    step.FinalStep(*value);
                }

                if (auto value = source->is_initial_step_single_frame())
                {
                    step.IsInitialStepSingleFrame(*value);
                }

                if (auto value = source->is_final_step_single_frame())
                {
                    step.IsFinalStepSingleFrame(*value);
                }

                result = step;
                break;
            }
            default:
                ThrowMalformed();
            }

            m_easings[index] = result;
            Initialize(result, source->base());

            return result;
        }

        // -----------------------------------------------------------------------
        // Animations.
        // -----------------------------------------------------------------------
        CompositionAnimation GetAnimation(uint32_t index)
        {
            if (index == NullIndex)
            {
                return nullptr;
            }

            Check(index < m_animations.size());

            if (m_animations[index] != nullptr)
            {
                return m_animations[index];
            }

            auto const* source = At(m_root.animations(), index);

            CompositionAnimation result = source->kind() == Fb::AnimationKind::Expression
                ? CreateExpressionAnimation(source)
                : CreateKeyFrameAnimation(source);

            m_animations[index] = result;

            if (auto target = source->target(); target != NullIndex)
            {
                result.Target(GetString(target));
            }

            if (auto const* parameters = source->reference_parameters(); parameters != nullptr)
            {
                for (uint32_t i = 0; i != parameters->size(); ++i)
                {
                    auto const* parameter = parameters->Get(i);
                    Check(parameter != nullptr);

                    auto value = GetObjectReference(parameter->target());
                    Check(value != nullptr);
                    result.SetReferenceParameter(GetString(parameter->name()), value);
                }
            }

            Initialize(result, source->base());

            return result;
        }

        CompositionAnimation CreateExpressionAnimation(Fb::Animation const* source)
        {
            return m_compositor.CreateExpressionAnimation(GetString(Required(source->expression())));
        }

        // Creates the animation and inserts its key frames in one place, because the
        // concrete type is only needed in order to insert a value key frame and it
        // would otherwise have to be recovered with a cast.
        CompositionAnimation CreateKeyFrameAnimation(Fb::Animation const* source)
        {
            auto const* frames = source->key_frames();

            KeyFrameAnimation result{ nullptr };

            switch (source->kind())
            {
            case Fb::AnimationKind::Scalar:
            {
                auto animation = m_compositor.CreateScalarKeyFrameAnimation();
                ForEachValueKeyFrame(frames, [&](Fb::KeyFrame const* frame)
                {
                    animation.InsertKeyFrame(frame->progress(), frame->scalar(), GetEasing(frame->easing()));
                });
                result = animation;
                break;
            }
            case Fb::AnimationKind::Vector2:
            {
                auto animation = m_compositor.CreateVector2KeyFrameAnimation();
                ForEachValueKeyFrame(frames, [&](Fb::KeyFrame const* frame)
                {
                    auto const* v = RequireVector(frame);
                    animation.InsertKeyFrame(frame->progress(), { v->x(), v->y() }, GetEasing(frame->easing()));
                });
                result = animation;
                break;
            }
            case Fb::AnimationKind::Vector3:
            {
                auto animation = m_compositor.CreateVector3KeyFrameAnimation();
                ForEachValueKeyFrame(frames, [&](Fb::KeyFrame const* frame)
                {
                    auto const* v = RequireVector(frame);
                    animation.InsertKeyFrame(frame->progress(), { v->x(), v->y(), v->z() }, GetEasing(frame->easing()));
                });
                result = animation;
                break;
            }
            case Fb::AnimationKind::Vector4:
            {
                auto animation = m_compositor.CreateVector4KeyFrameAnimation();
                ForEachValueKeyFrame(frames, [&](Fb::KeyFrame const* frame)
                {
                    auto const* v = RequireVector(frame);
                    animation.InsertKeyFrame(frame->progress(), { v->x(), v->y(), v->z(), v->w() }, GetEasing(frame->easing()));
                });
                result = animation;
                break;
            }
            case Fb::AnimationKind::Color:
            {
                auto animation = m_compositor.CreateColorKeyFrameAnimation();

                if (auto value = source->interpolation_color_space())
                {
                    animation.InterpolationColorSpace(static_cast<CompositionColorSpace>(*value));
                }

                ForEachValueKeyFrame(frames, [&](Fb::KeyFrame const* frame)
                {
                    animation.InsertKeyFrame(frame->progress(), ToColor(frame->color()), GetEasing(frame->easing()));
                });
                result = animation;
                break;
            }
            case Fb::AnimationKind::Boolean:
            {
                auto animation = m_compositor.CreateBooleanKeyFrameAnimation();

                // A boolean cannot be interpolated, so it has no easing.
                ForEachValueKeyFrame(frames, [&](Fb::KeyFrame const* frame)
                {
                    animation.InsertKeyFrame(frame->progress(), frame->scalar() != 0);
                });
                result = animation;
                break;
            }
            case Fb::AnimationKind::Path:
            {
                auto animation = m_compositor.CreatePathKeyFrameAnimation();
                ForEachValueKeyFrame(frames, [&](Fb::KeyFrame const* frame)
                {
                    animation.InsertKeyFrame(frame->progress(), GetPath(Required(frame->path())), GetEasing(frame->easing()));
                });
                result = animation;
                break;
            }
            default:
                ThrowMalformed();
            }

            // Expression key frames are inserted through the base type, so they do
            // not have to be repeated in every branch above. They are inserted after
            // the value key frames, which is safe because a key frame animation is a
            // map keyed on progress rather than a list.
            if (frames != nullptr)
            {
                for (uint32_t i = 0; i != frames->size(); ++i)
                {
                    auto const* frame = frames->Get(i);
                    Check(frame != nullptr);

                    if (frame->kind() == Fb::KeyFrameKind::Expression)
                    {
                        result.InsertExpressionKeyFrame(
                            frame->progress(),
                            GetString(Required(frame->expression())),
                            GetEasing(frame->easing()));
                    }
                }
            }

            if (auto ticks = source->duration_ticks(); ticks != 0)
            {
                result.Duration(winrt::Windows::Foundation::TimeSpan{ ticks });
            }

            return result;
        }

        template<typename F>
        void ForEachValueKeyFrame(::flatbuffers::Vector<::flatbuffers::Offset<Fb::KeyFrame>> const* frames, F&& apply)
        {
            if (frames == nullptr)
            {
                return;
            }

            for (uint32_t i = 0; i != frames->size(); ++i)
            {
                auto const* frame = frames->Get(i);
                Check(frame != nullptr);

                if (frame->kind() == Fb::KeyFrameKind::Value)
                {
                    apply(frame);
                }
            }
        }

        static Fb::Vec4 const* RequireVector(Fb::KeyFrame const* frame)
        {
            auto const* result = frame->vector();
            Check(result != nullptr);
            return result;
        }

        Compositor const& m_compositor;
        Fb::LottieComposition const& m_root;

        enum class CanvasGeometryState : uint8_t
        {
            Unrealized,
            Realizing,
            Realized,
        };

        // The caches. Each is exactly as long as the matching vector in the buffer,
        // so a lookup is a bounds check and an index. Each holds the least derived
        // type that any reference to that category needs, which is what stops a
        // realized node from ever having to be cast back to what it was.
        std::vector<winrt::hstring> m_strings;
        std::vector<uint8_t> m_stringCached;
        std::vector<Visual> m_visuals;
        std::vector<CompositionShape> m_shapes;
        std::vector<CompositionGeometry> m_geometries;
        std::vector<CompositionPath> m_paths;
        std::vector<winrt::com_ptr<ID2D1Geometry>> m_canvasGeometries;
        std::vector<CanvasGeometryState> m_canvasGeometryStates;
        std::vector<CompositionBrush> m_brushes;
        std::vector<CompositionColorGradientStop> m_gradientStops;
        std::vector<CompositionViewBox> m_viewBoxes;
        std::vector<CompositionClip> m_clips;
        std::vector<CompositionShadow> m_shadows;
        std::vector<ICompositionSurface> m_surfaces;
        std::vector<winrt::Windows::Graphics::Effects::IGraphicsEffect> m_effects;
        std::vector<CompositionEasingFunction> m_easings;
        std::vector<CompositionAnimation> m_animations;
        std::vector<CompositionPropertySet> m_propertySets;
        std::vector<AnimationController> m_controllers;
    };
}

namespace CommunityToolkit::WinUI::Lottie
{
    Visual LoadComposition(Compositor const& compositor, std::span<std::byte const> buffer)
    {
        // The buffer is untrusted, so nothing is read out of it until the verifier
        // has agreed that every offset in it is inside it.
        ::flatbuffers::Verifier verifier(
            reinterpret_cast<uint8_t const*>(buffer.data()),
            buffer.size());

        if (buffer.size() < sizeof(uint32_t) + 4 ||
            !Fb::LottieCompositionBufferHasIdentifier(buffer.data()) ||
            !Fb::VerifyLottieCompositionBuffer(verifier))
        {
            ThrowMalformed();
        }

        auto const* root = Fb::GetLottieComposition(buffer.data());
        Check(root != nullptr);

        // A newer schema may store things in fields this build does not read, so a
        // buffer that declares one is refused rather than silently misinterpreted.
        if (root->schema_version() > SupportedSchemaVersion)
        {
            ThrowUnsupported();
        }

        auto const requiredUapVersion = root->required_uap_version();
        if (requiredUapVersion != 0 &&
            !winrt::Windows::Foundation::Metadata::ApiInformation::IsApiContractPresent(
                L"Windows.Foundation.UniversalApiContract",
                requiredUapVersion))
        {
            ThrowUnsupported();
        }

        return Interpreter(compositor, *root).Run();
    }

    CompositionPropertySet ProgressPropertySet(Visual const& root)
    {
        return root.Properties();
    }
}

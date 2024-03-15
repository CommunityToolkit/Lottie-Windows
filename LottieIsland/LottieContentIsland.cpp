#include "pch.h"
#include "LottieContentIsland.h"
#include "LottieContentIsland.g.cpp"

namespace winrt::LottieIsland::implementation
{
    LottieContentIsland::LottieContentIsland(
        const winrt::Compositor& compositor)
        : m_compositor(compositor)
    {
        m_rootVisual = m_compositor.CreateContainerVisual();
        m_island = winrt::ContentIsland::Create(m_rootVisual);

        InitializeTree();
    }

    int32_t LottieContentIsland::MyProperty()
    {
        return m_myProperty;
    }

    void LottieContentIsland::MyProperty(int32_t value)
    {
        m_myProperty = value;
    }

    winrt::Microsoft::UI::Xaml::Controls::IAnimatedVisualSource LottieContentIsland::AnimatedVisualSource() const
    {
        // Return the AnimatedVisualSource
        return m_animatedVisualSource;
    }

    void LottieContentIsland::AnimatedVisualSource(winrt::Microsoft::UI::Xaml::Controls::IAnimatedVisualSource const& value)
    {
        // Set the AnimatedVisualSource
        m_animatedVisualSource = value;
        winrt::Windows::Foundation::IInspectable diagnostics;
        winrt::Microsoft::UI::Xaml::Controls::IAnimatedVisual animatedVisual = m_animatedVisualSource.TryCreateAnimatedVisual(m_compositor, diagnostics);

        // Set up lottie
        m_rootVisual.Children().InsertAtTop(animatedVisual.RootVisual());
        auto animation = m_compositor.CreateScalarKeyFrameAnimation();
        animation.Duration(animatedVisual.Duration());
        auto linearEasing = m_compositor.CreateLinearEasingFunction();
        animation.InsertKeyFrame(0, 0);
        animation.InsertKeyFrame(1, 1, linearEasing);
        animation.IterationBehavior(winrt::Microsoft::UI::Composition::AnimationIterationBehavior::Forever);
        animatedVisual.RootVisual().Properties().StartAnimation(L"Progress", animation);
    }

    winrt::Windows::Foundation::TimeSpan LottieContentIsland::Duration() const
    {
        if (m_animatedVisualSource == nullptr)
        {
            return 0ms;
        }

        throw winrt::hresult_not_implemented{};
    }

    bool LottieContentIsland::IsAnimationLoaded() const
    {
        if (m_animatedVisualSource == nullptr)
        {
            return false;
        }

        throw winrt::hresult_not_implemented{};
    }

    bool LottieContentIsland::IsPlaying() const
    {
        if (m_animatedVisualSource == nullptr)
        {
            return false;
        }

        throw winrt::hresult_not_implemented{};
    }

    void LottieContentIsland::Pause()
    {
        throw winrt::hresult_not_implemented{};
    }

    winrt::Windows::Foundation::IAsyncAction LottieContentIsland::PlayAsync(double fromProgress, double toProgress, bool looped)
    {
        throw winrt::hresult_not_implemented{};
    }

    void LottieContentIsland::Resume()
    {
        throw winrt::hresult_not_implemented{};
    }

    void LottieContentIsland::Stop()
    {
        throw winrt::hresult_not_implemented{};
    }

    void LottieContentIsland::InitializeTree()
    {
        // Make a blue square with a red square inside of it.
        // Add some animations to the red square

        // 300 x 300 blue background
        auto blueVisual = m_compositor.CreateSpriteVisual();
        auto blueBrush = m_compositor.CreateColorBrush(winrt::Windows::UI::Colors::Blue());
        blueVisual.Brush(blueBrush);
        blueVisual.Size({ 300, 300 });

        m_rootVisual.Children().InsertAtTop(blueVisual);

        // 50 x 50 red square
        auto redVisual = m_compositor.CreateSpriteVisual();
        auto redBrush = m_compositor.CreateColorBrush(winrt::Windows::UI::Colors::Red());
        redVisual.Brush(redBrush);
        redVisual.Size({ 50, 50 });

        m_rootVisual.Children().InsertAtTop(redVisual);

        // Setup an animation

        auto keyFrameAnimation = m_compositor.CreateVector3KeyFrameAnimation();
        keyFrameAnimation.InsertKeyFrame(0.0f, { 0, 0, 0 });
        keyFrameAnimation.InsertKeyFrame(1.0f, { 250.f, 250.f, 0 });

        // Bounce back and forth forever
        keyFrameAnimation.Duration(2000ms);
        keyFrameAnimation.Direction(winrt::AnimationDirection::Alternate);
        keyFrameAnimation.IterationBehavior(winrt::AnimationIterationBehavior::Forever);

        // Start animation
        redVisual.StartAnimation(L"Offset", keyFrameAnimation);
    }
}

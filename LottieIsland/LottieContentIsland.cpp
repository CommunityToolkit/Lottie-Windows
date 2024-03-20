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

        m_island.StateChanged({ get_weak(), &LottieContentIsland::OnIslandStateChanged });
    }

    winrt::Microsoft::UI::Xaml::Controls::IAnimatedVisualSource LottieContentIsland::AnimatedVisualSource() const
    {
        // Return the AnimatedVisualSource
        return m_animatedVisualSource;
    }

    void LottieContentIsland::AnimatedVisualSource(winrt::Microsoft::UI::Xaml::Controls::IAnimatedVisualSource const& value)
    {
        if (m_animatedVisualSource == value)
        {
            return;
        }

        if (m_animatedVisualSource != nullptr)
        {
            StopAnimation();
            m_rootVisual.Children().RemoveAll();
            m_animatedVisual = nullptr;
            m_animatedVisualSource = nullptr;
        }

        if (value != nullptr)
        {
            // Set the AnimatedVisualSource
            m_animatedVisualSource = value;
            winrt::Windows::Foundation::IInspectable diagnostics;
            m_animatedVisual = m_animatedVisualSource.TryCreateAnimatedVisual(m_compositor, diagnostics);

            // Set up lottie
            m_rootVisual.Children().InsertAtTop(m_animatedVisual.RootVisual());

            // Tell our hosting environment that our size changed, and ask for confirmation of our ActualSize.
            // Any changes will come back through a StateChanged notification
            m_island.RequestSize(m_animatedVisual.Size());

            // While that request is propagating, resize ourselves to fill the island's current size
            Resize(m_island.ActualSize());

            StartAnimation(0.0, 1.0, true /*loop*/);
        }
    }

    winrt::Windows::Foundation::TimeSpan LottieContentIsland::Duration() const
    {
        if (m_animatedVisual == nullptr)
        {
            return 0ms;
        }

        return m_animatedVisual.Duration();
    }

    bool LottieContentIsland::IsAnimationLoaded() const
    {
        // Revisit this when we get JSON loading to work.
        return m_animatedVisual != nullptr;
    }

    bool LottieContentIsland::IsPlaying() const
    {
        return m_progressPropertySet != nullptr;
    }

    double LottieContentIsland::PlaybackRate() const
    {
        return m_playbackRate;
    }

    void LottieContentIsland::PlaybackRate(double rate)
    {
        m_playbackRate = rate;
        if (m_animationController != nullptr)
        {
            m_animationController.PlaybackRate(m_playbackRate);
        }
    }

    void LottieContentIsland::Pause()
    {
        if (m_animationController != nullptr)
        {
            m_animationController.Pause();
        }
    }

    winrt::Windows::Foundation::IAsyncAction LottieContentIsland::PlayAsync(double fromProgress, double toProgress, bool looped)
    {
        // Stop any existing animation
        StopAnimation();

        // TODO: actually implement the async portion of this properly using composition batches.

        StartAnimation(fromProgress, toProgress, looped);
        co_return;
    }

    void LottieContentIsland::Resume()
    {
        if (m_animationController != nullptr)
        {
            m_animationController.Resume();
        }
    }

    void LottieContentIsland::Stop()
    {
        StopAnimation();
    }

    void LottieContentIsland::StartAnimation(double fromProgress, double toProgress, bool loop)
    {
        if (m_animatedVisual == nullptr)
        {
            throw winrt::hresult_illegal_method_call{ L"Cannot start an animation before the animation is loaded." };
        }

        auto animation = m_compositor.CreateScalarKeyFrameAnimation();
        animation.Duration(m_animatedVisual.Duration());
        auto linearEasing = m_compositor.CreateLinearEasingFunction();
        animation.InsertKeyFrame(0, fromProgress);
        animation.InsertKeyFrame(1, toProgress, linearEasing);
        if (loop)
        {
            animation.IterationBehavior(winrt::AnimationIterationBehavior::Forever);
        }
        else
        {
            animation.IterationBehavior(winrt::AnimationIterationBehavior::Count);
            animation.IterationCount(1);
        }

        m_progressPropertySet = m_animatedVisual.RootVisual().Properties();
        m_progressPropertySet.StartAnimation(L"Progress", animation);
        m_animationController = m_progressPropertySet.TryGetAnimationController(L"Progress");
        m_animationController.PlaybackRate(m_playbackRate);
        m_previousFromProgress = fromProgress;
    }

    void LottieContentIsland::StopAnimation()
    {
        if (!IsPlaying())
        {
            // No-op
            return;
        }

        // Stop and snap to the beginning of the animation
        m_progressPropertySet.StopAnimation(L"Progress");
        m_progressPropertySet.InsertScalar(L"Progress", m_previousFromProgress);

        // Cleanup
        m_previousFromProgress = 0.0;
        m_animationController = nullptr;
        m_progressPropertySet = nullptr;
    }

    void LottieContentIsland::OnIslandStateChanged(const winrt::ContentIsland& /*island*/, const winrt::ContentIslandStateChangedEventArgs& args)
    {
        if (args.DidActualSizeChange() && IsAnimationLoaded())
        {
            Resize(m_island.ActualSize());
        }
    }

    void LottieContentIsland::Resize(const float2& newSize)
    {
        float2 desiredSize = m_animatedVisual.Size();
        if (newSize.x == 0 || newSize.y == 0 || desiredSize.x == 0 || desiredSize.y == 0)
        {
            // Don't try to scale (and hit fun divide by 0) if we have no effective size
            m_rootVisual.Size({ 0, 0 });
        }
        else
        {
            // We implement Uniform stretching here, where we don't overflow bounds,
            // but keep aspect ratio.
            float2 scale = newSize / m_animatedVisual.Size();

            // Take the smaller scale and set both axes to that.
            if (scale.x < scale.y)
            {
                scale.y = scale.x;
            }
            else
            {
                scale.x = scale.y;
            }

            m_rootVisual.Size(desiredSize);
            m_rootVisual.Scale({ scale.x, scale.y, 1.f });
        }
    }
}
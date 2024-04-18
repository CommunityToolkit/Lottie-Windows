#pragma once

#include "LottieContentIsland.g.h"
#include "winrt/CommunityToolkit.WinAppSDK.LottieIsland.h"

namespace winrt
{
    using namespace ::winrt::CommunityToolkit::WinAppSDK::LottieIsland;
}

namespace winrt::CommunityToolkit::WinAppSDK::LottieIsland::implementation
{
    struct LottieContentIsland : LottieContentIslandT<LottieContentIsland>
    {
        using PointerEventHandler = Windows::Foundation::TypedEventHandler<winrt::LottieContentIsland, winrt::PointerEventArgs>;

        static winrt::LottieContentIsland Create(const winrt::Compositor& compositor);

        LottieContentIsland(const winrt::Compositor& compositor);
        ~LottieContentIsland();

        winrt::ContentIsland Island() const
        {
            return m_island;
        }

        winrt::IAnimatedVisualSource AnimatedVisualSource() const;
        void AnimatedVisualSource(const winrt::IAnimatedVisualSource& source);

        winrt::Windows::Foundation::TimeSpan Duration() const;

        bool IsAnimationLoaded() const;

        bool IsPlaying() const;

        float PlaybackRate() const;
        void PlaybackRate(float rate);

        winrt::event_token PointerEntered(const PointerEventHandler& handler) { return m_pointerEnteredEvent.add(handler); }
        void PointerEntered(winrt::event_token const& token) noexcept { m_pointerEnteredEvent.remove(token); }

        winrt::event_token PointerExited(const PointerEventHandler& handler) { return m_pointerExitedEvent.add(handler); }
        void PointerExited(winrt::event_token const& token) noexcept { m_pointerExitedEvent.remove(token); }

        winrt::event_token PointerMoved(const PointerEventHandler& handler) { return m_pointerMovedEvent.add(handler); }
        void PointerMoved(winrt::event_token const& token) noexcept { m_pointerMovedEvent.remove(token); }

        winrt::event_token PointerPressed(const PointerEventHandler& handler) { return m_pointerPressedEvent.add(handler); }
        void PointerPressed(winrt::event_token const& token) noexcept { m_pointerPressedEvent.remove(token); }

        winrt::event_token PointerReleased(const PointerEventHandler& handler) { return m_pointerReleasedEvent.add(handler); }
        void PointerReleased(winrt::event_token const& token) noexcept { m_pointerReleasedEvent.remove(token); }

        void Pause();

        winrt::Windows::Foundation::IAsyncAction PlayAsync(float fromProgress, float toProgress, bool looped);

        void Resume();

        void Stop();

    private:
        void StartAnimation(float fromProgress, float toProgress, bool loop);
        void StopAnimation();

        void OnIslandStateChanged(const winrt::ContentIsland& island, const winrt::ContentIslandStateChangedEventArgs& args);

        void Resize(const float2& size);

        void InitializeInputHandlers();

        winrt::event<PointerEventHandler> m_pointerEnteredEvent;
        winrt::event<PointerEventHandler> m_pointerExitedEvent;
        winrt::event<PointerEventHandler> m_pointerMovedEvent;
        winrt::event<PointerEventHandler> m_pointerPressedEvent;
        winrt::event<PointerEventHandler> m_pointerReleasedEvent;

        winrt::Compositor m_compositor{ nullptr };
        winrt::ContainerVisual m_rootVisual{ nullptr };
        winrt::ContentIsland m_island{ nullptr };
        winrt::InputPointerSource m_inputPointerSource{ nullptr };
        winrt::IAnimatedVisualSource m_animatedVisualSource{ nullptr };
        winrt::IAnimatedVisual m_animatedVisual{ nullptr };
        winrt::CompositionPropertySet m_progressPropertySet{ nullptr };
        winrt::AnimationController m_animationController{ nullptr };
        float m_previousFromProgress = 0.0;
        float m_playbackRate = 1.0f;
        winrt::handle m_animationCompletionEvent{ nullptr };
        bool m_looped;
    };
}

namespace winrt::CommunityToolkit::WinAppSDK::LottieIsland::factory_implementation
{
    struct LottieContentIsland : LottieContentIslandT<LottieContentIsland, implementation::LottieContentIsland>
    {
    };
}

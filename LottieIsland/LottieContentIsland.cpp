#include "pch.h"
#include "LottieContentIsland.h"
#include "LottieContentIsland.g.cpp"

namespace winrt::CommunityToolkit::WinAppSDK::LottieIsland::implementation
{
    winrt::LottieContentIsland LottieContentIsland::Create(const winrt::Compositor& compositor)
    {
        return winrt::make<LottieContentIsland>(compositor);
    }

    LottieContentIsland::LottieContentIsland(
        const winrt::Compositor& compositor)
        : m_compositor(compositor)
    {
        m_rootVisual = m_compositor.CreateContainerVisual();
        m_island = winrt::ContentIsland::Create(m_rootVisual);
        m_island.AppData(get_strong().as<winrt::Windows::Foundation::IInspectable>());

        m_island.AutomationProviderRequested({ get_weak(), &LottieContentIsland::OnIslandAutomationProviderRequested });
        m_island.StateChanged({ get_weak(), &LottieContentIsland::OnIslandStateChanged });

        // Once it's not experimental, we should use InputPointerSource::GetForVisual on our root visual.
        // This will give us automatic hittesting for whatever content and shape the Lottie animation has.
        // Currently hittesting will just be a rectangle the size of the island, regardless of content.
        m_inputPointerSource = winrt::Microsoft::UI::Input::InputPointerSource::GetForIsland(m_island);

        InitializeInputHandlers();
    }

    LottieContentIsland::~LottieContentIsland()
    {
        // Dispose (Close) our island. This will revoke any event handlers from it or sub-objects, which
        // is why the LottieContentIsland doesn't need to manually revoke event handlers.
        m_island.Close();
    }

    winrt::IAnimatedVisualFrameworkless LottieContentIsland::AnimatedVisual() const
    {
        // Return the AnimatedVisualSource
        return m_animatedVisual;
    }

    void LottieContentIsland::AnimatedVisual(winrt::IAnimatedVisualFrameworkless const& value)
    {
        if (m_animatedVisual == value)
        {
            return;
        }

        if (m_animatedVisual != nullptr)
        {
            StopAnimation();
            m_rootVisual.Children().RemoveAll();
            m_animatedVisual = nullptr;
        }

        if (value != nullptr)
        {
            // Set the AnimatedVisualSource
            m_animatedVisual = value;

            // Set up lottie
            winrt::Visual lottieVisual = m_animatedVisual.RootVisual();
            m_rootVisual.Children().InsertAtTop(lottieVisual);

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

    float LottieContentIsland::PlaybackRate() const
    {
        return m_playbackRate;
    }

    void LottieContentIsland::PlaybackRate(float rate)
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

    winrt::Windows::Foundation::IAsyncAction LottieContentIsland::PlayAsync(float fromProgress, float toProgress, bool looped)
    {
        if (m_animationCompletionEvent.get() == nullptr)
        {
            m_animationCompletionEvent = winrt::handle(CreateEvent(nullptr, false, false, nullptr));
        }

        // Stop any existing animation
        StopAnimation();

        auto batch = m_compositor.CreateScopedBatch(CompositionBatchTypes::Animation);

        StartAnimation(fromProgress, toProgress, looped);

        // Keep track of whether the animation is looped, since we will have to
        // manually fire the event if Stop() is called in the non-looped case.
        // We don't hook up the event here in the looped case, because ScopedBatches
        // complete immediately if their animation is looped.
        m_looped = looped;
        if (!looped)
        {
            // Hook up an event handler to the Completed event of the batch
            batch.Completed([&](auto&&, auto&&)
                {
                    // Set the completion event when the batch completes
                    SetEvent(m_animationCompletionEvent.get());
                });
        }

        // Commit the batch
        batch.End();

        // Wait for the completion event asynchronously
        co_await winrt::resume_on_signal(m_animationCompletionEvent.get()); // Wait for the event to be signaled
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

    winrt::Windows::Graphics::RectInt32 LottieContentIsland::GetBoundingRectangleInScreenSpaceForAutomation(
        ::IUnknown const* const /*sender*/) const
    {
        // Convert our local bounds to screen space and return it to UI Automation.
        auto coordinateConverter = m_island.CoordinateConverter();
        auto actualSize = m_island.ActualSize();
        winrt::Windows::Foundation::Rect islandLocalBounds{ 0, 0, actualSize.x, actualSize.y };
        auto islandScreenBounds = coordinateConverter.ConvertLocalToScreen(islandLocalBounds);
        return islandScreenBounds;
    }

    void LottieContentIsland::HandleSetFocusForAutomation(
        ::IUnknown const* const /*sender*/)
    {
        // No-op.
    }

    winrt::com_ptr<::IRawElementProviderFragment> LottieContentIsland::GetFragmentFromPointForAutomation(
        double /*x*/,
        double /*y*/,
        ::IUnknown const* const /*sender*/) const
    {
        // No child automation fragments.
        return nullptr;
    }

    winrt::com_ptr<::IRawElementProviderFragment> LottieContentIsland::GetFragmentInFocusForAutomation(
        ::IUnknown const* const /*sender*/) const
    {
        // No child automation fragments.
        return nullptr;
    }

    void LottieContentIsland::HandleInvokeForAutomation(
        ::IUnknown const* const /*sender*/)
    {
        if (nullptr != m_animatedVisual)
        {
            IsPlaying() ? StopAnimation() : StartAnimation(0.0f, 1.0f, false);
        }
    }

    void LottieContentIsland::StartAnimation(float fromProgress, float toProgress, bool loop)
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

        if (m_looped)
        {
            SetEvent(m_animationCompletionEvent.get());
        }

        // Cleanup
        m_previousFromProgress = 0.0;
        m_animationController = nullptr;
        m_progressPropertySet = nullptr;
    }

    void LottieContentIsland::OnIslandAutomationProviderRequested(
        const winrt::ContentIsland& island,
        const winrt::ContentIslandAutomationProviderRequestedEventArgs& args)
    {
        if (nullptr == m_automationProvider)
        {
            // We need to create the automation provider.
            m_automationProvider = winrt::make_self<LottieIslandInternal::LottieIslandAutomationProvider>();
            m_automationProvider->Name(L"Lottie");

            // Register ourselves as the callback for our automation provider.
            m_fragmentCallbackRevoker = m_automationProvider->SetFragmentCallbackHandler(this);
            m_fragmentRootCallbackRevoker = m_automationProvider->SetFragmentRootCallbackHandler(this);
            m_invokeCallbackRevoker = m_automationProvider->SetInvokeCallbackHandler(this);

            // Set up the host provider.
            auto hostProviderAsIInspectable = island.GetAutomationHostProvider();
            m_automationProvider->HostProvider(hostProviderAsIInspectable.try_as<::IRawElementProviderSimple>());
        }

        args.AutomationProvider(m_automationProvider.as<IInspectable>());
        args.Handled(true);
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

    void LottieContentIsland::InitializeInputHandlers()
    {
        m_inputPointerSource.PointerEntered([this](auto& /*sender*/, auto& args) {
            m_pointerEnteredEvent(*this, args);
        });

        m_inputPointerSource.PointerExited([this](auto& /*sender*/, auto& args) {
            m_pointerExitedEvent(*this, args);
        });

        m_inputPointerSource.PointerMoved([this](auto& /*sender*/, auto& args) {
            m_pointerMovedEvent(*this, args);
        });

        m_inputPointerSource.PointerPressed([this](auto& /*sender*/, auto& args) {
            m_pointerPressedEvent(*this, args);
        });

        m_inputPointerSource.PointerReleased([this](auto& /*sender*/, auto& args) {
            m_pointerReleasedEvent(*this, args);
        });
    }
}

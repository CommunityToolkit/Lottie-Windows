// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

// SimpleLottieIslandApp.cpp : Defines the entry point for the application.

#include "pch.h"
#include "SimpleLottieIslandApp.h"

#include <Microsoft.UI.Dispatching.Interop.h> // For ContentPreTranslateMessage
#include <winrt/CommunityToolkit.WinAppSDK.LottieIsland.h>
#include <winrt/LottieWinRT.h>

namespace winrt
{
    using namespace winrt::Windows::Foundation;
    using namespace winrt::Microsoft::UI;
    using namespace winrt::Microsoft::UI::Content;
    using namespace winrt::Microsoft::UI::Dispatching;
    using namespace winrt::LottieWinRT;
    using namespace winrt::CommunityToolkit::WinAppSDK::LottieIsland;
    using float2 = winrt::Windows::Foundation::Numerics::float2;
}

// Forward declarations of functions included in this code module:
void                MyRegisterClass(HINSTANCE hInstance, const wchar_t* szWindowClass);
HWND                InitInstance(HINSTANCE, int, const wchar_t* szTitle, const wchar_t* szWindowClass);
LRESULT CALLBACK    WndProc(HWND, UINT, WPARAM, LPARAM);
INT_PTR CALLBACK    About(HWND, UINT, WPARAM, LPARAM);
bool                ProcessMessageForTabNavigation(const HWND topLevelWindow, MSG* msg);

// Extra state for our top-level window, we point to from GWLP_USERDATA.
struct WindowInfo
{
    // winrt::DesktopWindowXamlSource DesktopWindowXamlSource{ nullptr };
    winrt::Microsoft::UI::Composition::Compositor Compositor{};
    winrt::DesktopChildSiteBridge Bridge{ nullptr };
    winrt::event_token TakeFocusRequestedToken{};
    HWND LastFocusedWindow{ NULL };
    winrt::LottieContentIsland LottieIsland{ nullptr };
    bool isPaused = false;
};

enum class ButtonType
{
    PlayButton = 1,
    PauseButton,
    StopButton,
    ReverseButton
};

constexpr int k_padding = 10;
constexpr int k_buttonWidth = 150;
constexpr int k_buttonHeight = 40;

void LayoutButton(ButtonType type, int tlwWidth, int tlwHeight, HWND topLevelWindow);
void CreateWin32Button(ButtonType type, const std::wstring_view& text, HWND parentHwnd);
void OnButtonClicked(ButtonType type, WindowInfo* windowInfo, HWND topLevelWindow);
void SetButtonText(ButtonType type, const std::wstring_view& text, HWND topLevelWindow);
void SetPauseState(WindowInfo* windowInfo, bool isPaused, HWND topLevelWindow);

int APIENTRY wWinMain(_In_ HINSTANCE hInstance,
    _In_opt_ HINSTANCE hPrevInstance,
    _In_ LPWSTR    lpCmdLine,
    _In_ int       nCmdShow)
{
    UNREFERENCED_PARAMETER(hPrevInstance);
    UNREFERENCED_PARAMETER(lpCmdLine);

    try
    {
        // Island-support: Call init_apartment to initialize COM and WinRT for the thread.
        winrt::init_apartment(winrt::apartment_type::single_threaded);

        // Island-support: We must start a DispatcherQueueController before we can create an island or use Xaml.
        auto dispatcherQueueController{ winrt::DispatcherQueueController::CreateOnCurrentThread() };

        // The title bar text
        WCHAR szTitle[100];
        winrt::check_bool(LoadStringW(hInstance, IDS_APP_TITLE, szTitle, ARRAYSIZE(szTitle)) != 0);

        // The main window class name
        WCHAR szWindowClass[100];
        winrt::check_bool(LoadStringW(hInstance, IDC_SIMPLELOTTIEISLANDAPP, szWindowClass, ARRAYSIZE(szWindowClass)) != 0);

        MyRegisterClass(hInstance, szWindowClass);

        // Perform application initialization:
        InitInstance(hInstance, nCmdShow, szTitle, szWindowClass);

        HACCEL hAccelTable = LoadAccelerators(hInstance, MAKEINTRESOURCE(IDC_SIMPLELOTTIEISLANDAPP));

        MSG msg{};

        // Main message loop:
        while (GetMessage(&msg, nullptr, 0, 0))
        {
            // Island-support: It's important to call ContentPreTranslateMessage in the event loop so that WinAppSDK can be aware of
            // the messages.  If you don't need to use an accelerator table, you could just call DispatcherQueue.RunEventLoop
            // to do the message pump for you (it will call ContentPreTranslateMessage automatically).
            if (::ContentPreTranslateMessage(&msg))
            {
                continue;
            }

            if (TranslateAccelerator(msg.hwnd, hAccelTable, &msg))
            {
                continue;
            }

            TranslateMessage(&msg);
            DispatchMessage(&msg);
        }

        // Island-support: To properly shut down after using a DispatcherQueue, call ShutdownQueue[Aysnc]().
        dispatcherQueueController.ShutdownQueue();
    }
    catch (const winrt::hresult_error& exception)
    {
        // An exception was thrown, let's make the exit code the HR value of the exception.
        return exception.code().value;
    }

    return 0;
}

//
//  FUNCTION: MyRegisterClass()
//
//  PURPOSE: Registers the window class.
//
void MyRegisterClass(HINSTANCE hInstance, const wchar_t* szWindowClass)
{
    WNDCLASSEXW wcex;

    wcex.cbSize = sizeof(WNDCLASSEX);

    wcex.style          = CS_HREDRAW | CS_VREDRAW;
    wcex.lpfnWndProc    = WndProc;
    wcex.cbClsExtra     = 0;
    wcex.cbWndExtra     = 0;
    wcex.hInstance      = hInstance;
    wcex.hIcon          = LoadIcon(hInstance, MAKEINTRESOURCE(IDI_SIMPLELOTTIEISLANDAPP));
    wcex.hCursor        = LoadCursor(nullptr, IDC_ARROW);
    wcex.hbrBackground  = (HBRUSH)(COLOR_WINDOW+1);
    wcex.lpszMenuName   = MAKEINTRESOURCEW(IDC_SIMPLELOTTIEISLANDAPP);
    wcex.lpszClassName  = szWindowClass;
    wcex.hIconSm        = LoadIcon(wcex.hInstance, MAKEINTRESOURCE(IDI_SMALL));

    winrt::check_bool(RegisterClassExW(&wcex) != 0);
}

//
//   FUNCTION: InitInstance(HINSTANCE, int)
//
//   PURPOSE: Saves instance handle and creates main window
//
//   COMMENTS:
//
//        In this function, we save the instance handle in a global variable and
//        create and display the main program window.
//
HWND InitInstance(HINSTANCE /*hInstance*/, int nCmdShow, const wchar_t* szTitle, const wchar_t* szWindowClass)
{
   HWND hWnd = CreateWindowW(szWindowClass, szTitle, WS_OVERLAPPEDWINDOW,
      CW_USEDEFAULT, 0, CW_USEDEFAULT, 0, nullptr, nullptr, ::GetModuleHandle(NULL), nullptr);
   winrt::check_bool(hWnd != NULL);

   ShowWindow(hWnd, nCmdShow);
   UpdateWindow(hWnd);
   return hWnd;
}

//
//  FUNCTION: WndProc(HWND, UINT, WPARAM, LPARAM)
//
//  PURPOSE: Processes messages for the main window.
//
//  WM_COMMAND  - process the application menu
//  WM_PAINT    - Paint the main window
//  WM_DESTROY  - post a quit message and return
//
//
LRESULT CALLBACK WndProc(HWND hWnd, UINT message, WPARAM wParam, LPARAM lParam)
{
    WindowInfo* windowInfo = reinterpret_cast<WindowInfo*>(::GetWindowLongPtr(hWnd, GWLP_USERDATA));

    switch (message)
    {
    case WM_CREATE:
        {
            windowInfo = new WindowInfo();
            ::SetWindowLongPtr(hWnd, GWLP_USERDATA, reinterpret_cast<LONG_PTR>(windowInfo));

            // Create the DesktopChildSiteBridge
            windowInfo->Bridge = winrt::DesktopChildSiteBridge::Create(
                windowInfo->Compositor,
                winrt::GetWindowIdFromWindow(hWnd));

            // Create the LottieIsland, which is a WinRT wrapper for hosting a Lottie animation in a ContentIsland
            windowInfo->LottieIsland = winrt::LottieContentIsland::Create(windowInfo->Compositor);

            // Connect the ContentIsland to the DesktopChildSiteBridge
            windowInfo->Bridge.Connect(windowInfo->LottieIsland.Island());
            windowInfo->Bridge.Show();

            winrt::LottieVisualSourceWinRT lottieVisualSource = winrt::LottieVisualSourceWinRT::CreateFromString(L"ms-appx:///LottieLogo1.json");
            lottieVisualSource.AnimatedVisualInvalidated([windowInfo, lottieVisualSource](const winrt::IInspectable&, auto&&)
                {
                    windowInfo->Compositor.DispatcherQueue().TryEnqueue([windowInfo, lottieVisualSource]()
                        {
                            windowInfo->LottieIsland.AnimatedVisualSource(lottieVisualSource.as<winrt::IAnimatedVisualSourceFrameworkless>());
                        });
                    
                });

            windowInfo->LottieIsland.PointerPressed([=](auto&...) {
                // Clicking on the Lottie animation acts like clicking "Pause/Resume"
                OnButtonClicked(ButtonType::PauseButton, windowInfo, hWnd);
                });

            // Add some Win32 controls to allow the app to play with the animation
            CreateWin32Button(ButtonType::PlayButton, L"Play", hWnd);
            CreateWin32Button(ButtonType::PauseButton, L"Pause", hWnd);
            CreateWin32Button(ButtonType::StopButton, L"Stop", hWnd);
            CreateWin32Button(ButtonType::ReverseButton, L"Reverse", hWnd);
        }
        break;
    case WM_SIZE:
        {
            const int width = LOWORD(lParam);
            const int height = HIWORD(lParam);

            if (windowInfo->Bridge)
            {
                // Layout our bridge: we want to use all available height (minus a button and some padding),
                // but respect the ratio that the LottieIsland wants to display at. This can be accessed through
                // the "RequestedSize" property on the ContentSiteView.

                int availableHeight = height - (k_padding * 3) - k_buttonHeight;
                int availableWidth = width - (k_padding * 2);

                // Check what size the lottie wants to be
                winrt::float2 requestedSize = windowInfo->Bridge.SiteView().RequestedSize();

                // Scale the width to be the ratio the lottie wants
                int bridgeWidth = 0;
                if (requestedSize.y > 0) // Guard against divide-by-zero
                {
                    bridgeWidth = static_cast<int>((requestedSize.x / requestedSize.y) * availableHeight);
                }

                // ... but don't overflow the width we have available
                bridgeWidth = std::min(availableWidth, bridgeWidth);

                windowInfo->Bridge.MoveAndResize({ k_padding, k_padding, bridgeWidth, availableHeight });
            }

            LayoutButton(ButtonType::PlayButton, width, height, hWnd);
            LayoutButton(ButtonType::PauseButton, width, height, hWnd);
            LayoutButton(ButtonType::StopButton, width, height, hWnd);
            LayoutButton(ButtonType::ReverseButton, width, height, hWnd);
        }
        break;
    case WM_ACTIVATE:
        {
            // Make focus work nicely when the user presses alt+tab to activate a different window, and then alt+tab
            // again to come back to this window.  We want the focus to go back to the same child HWND that was focused
            // before.
            const bool isGettingDeactivated = (LOWORD(wParam) == WA_INACTIVE);
            if (isGettingDeactivated)
            {
                // Remember the HWND that had focus.
                windowInfo->LastFocusedWindow = ::GetFocus();
            }
            else if (windowInfo->LastFocusedWindow != NULL)
            {
                ::SetFocus(windowInfo->LastFocusedWindow);
            }
        }
        break;
    case WM_COMMAND:
        {
            int wmId = LOWORD(wParam);
            int wmCode = HIWORD(wParam);
            // Parse the menu selections:
            switch (wmId)
            {
            case IDM_ABOUT:
                DialogBox(::GetModuleHandle(NULL), MAKEINTRESOURCE(IDD_ABOUTBOX), hWnd, About);
                break;
            case IDM_EXIT:
                DestroyWindow(hWnd);
                break;
            case 501: // Buttons
            case 502:
            case 503:
            case 504:
                if (wmCode == BN_CLICKED)
                {
                    ButtonType type = static_cast<ButtonType>(wmId - 500);
                    OnButtonClicked(type, windowInfo, hWnd);
                }
                break;
            default:
                return DefWindowProc(hWnd, message, wParam, lParam);
            }
        }
        break;
    case WM_PAINT:
        {
            PAINTSTRUCT ps;
            HDC hdc = BeginPaint(hWnd, &ps);
            // TODO: Add any drawing code that uses hdc here...
            UNREFERENCED_PARAMETER(hdc);
            EndPaint(hWnd, &ps);
        }
        break;
    case WM_DESTROY:
        PostQuitMessage(0);
        break;
    case WM_NCDESTROY:
        delete windowInfo;
        ::SetWindowLong(hWnd, GWLP_USERDATA, NULL);
        break;
    default:
        return DefWindowProc(hWnd, message, wParam, lParam);
    }
    return 0;
}

// Message handler for about box.
INT_PTR CALLBACK About(HWND hDlg, UINT message, WPARAM wParam, LPARAM lParam)
{
    UNREFERENCED_PARAMETER(lParam);
    switch (message)
    {
    case WM_INITDIALOG:
        return (INT_PTR)TRUE;

    case WM_COMMAND:
        if (LOWORD(wParam) == IDOK || LOWORD(wParam) == IDCANCEL)
        {
            EndDialog(hDlg, LOWORD(wParam));
            return (INT_PTR)TRUE;
        }
        break;
    }
    return (INT_PTR)FALSE;
}

void LayoutButton(ButtonType type, int /*tlwWidth*/, int tlwHeight, HWND topLevelWindow)
{
    int buttonIndex = static_cast<int>(type);

    int xPos = ((buttonIndex - 1) * (k_buttonWidth + k_padding)) + k_padding;
    int yPos = tlwHeight - k_buttonHeight - k_padding;

    HWND buttonHwnd = ::GetDlgItem(topLevelWindow, 500 + buttonIndex);
    ::SetWindowPos(buttonHwnd, NULL, xPos, yPos, k_buttonWidth, k_buttonHeight, SWP_NOZORDER);
}

void CreateWin32Button(ButtonType type, const std::wstring_view& text, HWND parentHwnd)
{
    int buttonIndex = static_cast<int>(type);

    int xPos = ((buttonIndex - 1) * (k_buttonWidth + k_padding)) + k_padding;

    const HINSTANCE hInst = (HINSTANCE)GetWindowLongPtr(parentHwnd, GWLP_HINSTANCE);
    HMENU fakeHMenu = reinterpret_cast<HMENU>(static_cast<intptr_t>(500 + buttonIndex));
    ::CreateWindowW(
        L"BUTTON",
        text.data(),
        WS_TABSTOP | WS_VISIBLE | WS_CHILD,
        xPos, 250, k_buttonWidth, k_buttonHeight,
        parentHwnd,
        fakeHMenu,
        hInst,
        NULL);
}

void OnButtonClicked(ButtonType type, WindowInfo* windowInfo, HWND topLevelWindow)
{
    winrt::Windows::Foundation::IAsyncAction asyncAction{ nullptr };
    switch (type)
    {
    case ButtonType::PlayButton:
        asyncAction = windowInfo->LottieIsland.PlayAsync(0.0, 1.0, true);
        asyncAction.Completed([](auto&&, auto&& asyncStatus)
            {
                // Check if the async operation was successfully completed
                if (asyncStatus == winrt::Windows::Foundation::AsyncStatus::Completed)
                {
                    OutputDebugString(L"Async operation completed successfully.\n");
                }
                else
                {
                    OutputDebugString(L"Async operation failed or was canceled.\n");
                }
            });


        SetPauseState(windowInfo, false, topLevelWindow);
        break;
    case ButtonType::PauseButton:
        if (windowInfo->isPaused)
        {
            windowInfo->LottieIsland.Resume();
        }
        else
        {
            windowInfo->LottieIsland.Pause();
        }
        SetPauseState(windowInfo, !windowInfo->isPaused, topLevelWindow);
        break;
    case ButtonType::StopButton:
        windowInfo->LottieIsland.Stop();
        SetPauseState(windowInfo, false, topLevelWindow);
        break;
    case ButtonType::ReverseButton:
        if (windowInfo->LottieIsland.PlaybackRate() == 1.0)
        {
            windowInfo->LottieIsland.PlaybackRate(-1.0);
        }
        else
        {
            windowInfo->LottieIsland.PlaybackRate(1.0);
        }
        break;
    default:
        throw winrt::hresult_invalid_argument{ L"Invalid button type." };
    }
}

void SetButtonText(ButtonType type, const std::wstring_view& text, HWND topLevelWindow)
{
    int buttonIndex = static_cast<int>(type);
    HWND buttonHwnd = ::GetDlgItem(topLevelWindow, 500 + buttonIndex);
    ::SendMessageW(buttonHwnd, WM_SETTEXT, 0, reinterpret_cast<LPARAM>(text.data()));
}

void SetPauseState(WindowInfo* windowInfo, bool isPaused, HWND topLevelWindow)
{
    if (windowInfo->isPaused == isPaused)
    {
        return;
    }

    SetButtonText(ButtonType::PauseButton,
        isPaused ? L"Resume" : L"Pause",
        topLevelWindow);

    windowInfo->isPaused = isPaused;
}

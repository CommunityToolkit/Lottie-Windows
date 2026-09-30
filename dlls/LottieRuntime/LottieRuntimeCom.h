// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

#pragma once

#include <unknwn.h>
#include <Windows.h>

// COM interface for DLL activation. LottieRuntime.manifest declares the class for
// registration-free COM activation.
//
// riid selects the interface returned for the root Visual.
MIDL_INTERFACE("3874B71B-05E6-42A9-B838-1AE7F0BA4E1C")
ILottieCompositionLoader : public IUnknown
{
public:
    // buffer contains LottieGen -Language flatbuffer output. Validation precedes
    // every read; malformed data returns E_INVALIDARG.
    STDMETHOD(LoadComposition)(UINT32 length, BYTE const* buffer, REFIID riid, void** result) = 0;
};

// Registration-free clients do not link an import library.
constexpr CLSID CLSID_LottieCompositionLoader = {
    0x9f7b7089, 0xf512, 0x4bbc, { 0x81, 0xfe, 0x4f, 0xf1, 0x2b, 0x4d, 0xa1, 0x59 } };

#include <windows.h>
#include <mfapi.h>
#include <mfidl.h>
#include <mferror.h>
#include <wrl.h>
#include <cstdio>
#include <stdexcept>
#include <string>
#include <vector>
#include <process.h>
#include "FrameBridge.h"
using namespace Microsoft::WRL;
static int checks = 0;
static void Check(bool ok, const char* text) { if (!ok) throw std::runtime_error(text); ++checks; printf("PASS %s\n", text); }
static void Ok(HRESULT hr) { if (FAILED(hr)) { char text[64]; sprintf_s(text, "HRESULT 0x%08lX", hr); throw std::runtime_error(text); } }
template<class T> static T Function(HMODULE dll, const char* name) { auto p = GetProcAddress(dll, name); if (!p) throw std::runtime_error(name); return reinterpret_cast<T>(p); }
static ComPtr<IMFMediaEvent> Event(IMFMediaEventGenerator* generator, MediaEventType expected) {
    auto until = GetTickCount64() + 3000;
    while (GetTickCount64() < until) {
        ComPtr<IMFMediaEvent> event;
        HRESULT hr = generator->GetEvent(MF_EVENT_FLAG_NO_WAIT, &event);
        if (hr == MF_E_NO_EVENTS_AVAILABLE) { Sleep(2); continue; }
        Ok(hr); MediaEventType type; Ok(event->GetType(&type));
        HRESULT status; Ok(event->GetStatus(&status)); Ok(status);
        if (type == expected) return event;
    }
    throw std::runtime_error("Event timeout");
}
static ComPtr<IMFMediaStream> Start(IMFMediaSource* source) {
    ComPtr<IMFPresentationDescriptor> pd; Ok(source->CreatePresentationDescriptor(&pd));
    DWORD count = 0; Ok(pd->GetStreamDescriptorCount(&count)); Check(count == 1, "single independent video stream");
    PROPVARIANT position{}; Ok(source->Start(pd.Get(), &GUID_NULL, &position));
    auto event = Event(source, MENewStream);
    PROPVARIANT value{}; Ok(event->GetValue(&value));
    ComPtr<IMFMediaStream> stream; Ok(value.punkVal->QueryInterface(IID_PPV_ARGS(&stream))); PropVariantClear(&value);
    Event(source, MESourceStarted); Event(stream.Get(), MEStreamStarted);
    return stream;
}
static ComPtr<IMFSample> Sample(IMFMediaStream* stream, IUnknown* token = nullptr) {
    Ok(stream->RequestSample(token));
    auto event = Event(stream, MEMediaSample); PROPVARIANT value{}; Ok(event->GetValue(&value));
    ComPtr<IMFSample> sample; Ok(value.punkVal->QueryInterface(IID_PPV_ARGS(&sample))); PropVariantClear(&value); return sample;
}
static BYTE Pixel(IMFSample* sample, DWORD index) {
    ComPtr<IMFMediaBuffer> buffer; Ok(sample->ConvertToContiguousBuffer(&buffer));
    DWORD size = 0; Ok(buffer->GetCurrentLength(&size)); Check(size == wt::FrameBytes, "NV12 frame length");
    BYTE* bytes = nullptr; Ok(buffer->Lock(&bytes, nullptr, nullptr)); BYTE value = bytes[index]; Ok(buffer->Unlock()); return value;
}
int wmain(int argc, wchar_t** argv) {
    HMODULE dll = nullptr;
    ComPtr<IMFMediaSource> a, b;
    void* writerA = nullptr; void* writerB = nullptr;
    using Close = void(__stdcall*)(void*);
    Close close = nullptr;
    std::wstring pathA, pathB;
    try {
        Ok(CoInitializeEx(nullptr, COINIT_MULTITHREADED)); Ok(MFStartup(MF_VERSION));
        wchar_t exe[MAX_PATH]{}; GetModuleFileNameW(nullptr, exe, MAX_PATH);
        std::wstring folder(exe); folder.resize(folder.find_last_of(L"\\") + 1);
        dll = LoadLibraryW((folder + L"WTMultiCam.VirtualCamera.dll").c_str()); Check(dll != nullptr, "load native x64 provider");
        auto activate = Function<HRESULT(__stdcall*)(LPCWSTR, IMFActivate**)>(dll, "WT_TestActivation");
        auto open = Function<HRESULT(__stdcall*)(LPCWSTR, void**)>(dll, "WT_OpenPublisher");
        auto publish = Function<HRESULT(__stdcall*)(void*, const BYTE*, DWORD, DWORD, DWORD)>(dll, "WT_Publish");
        auto clear = Function<Close>(dll, "WT_Clear"); close = Function<Close>(dll, "WT_ClosePublisher");
        auto unload = Function<HRESULT(__stdcall*)()>(dll, "DllCanUnloadNow");
        if (argc == 4 && wcscmp(argv[1], L"--read") == 0) {
            ComPtr<IMFActivate> activation;
            Ok(activate(argv[2], &activation)); Ok(activation->ActivateObject(IID_PPV_ARGS(&a)));
            auto stream = Start(a.Get()); auto sample = Sample(stream.Get());
            constexpr DWORD center = wt::Width * (wt::Height / 2) + wt::Width / 2;
            Check(Pixel(sample.Get(), center) == _wtoi(argv[3]), "separate reader process receives publisher frame");
            a->Shutdown(); activation->ShutdownObject(); stream.Reset(); sample.Reset(); activation.Reset(); a.Reset();
            FreeLibrary(dll); MFShutdown(); CoUninitialize(); return 0;
        }
        Check(unload() == S_OK, "no initial COM objects");
        wchar_t temp[MAX_PATH]{}; GetTempPathW(MAX_PATH, temp);
        pathA = std::wstring(temp) + L"wt-vcam-a-" + std::to_wstring(GetCurrentProcessId()) + L".bin";
        pathB = std::wstring(temp) + L"wt-vcam-b-" + std::to_wstring(GetCurrentProcessId()) + L".bin";
        Ok(open(pathA.c_str(), &writerA)); Ok(open(pathB.c_str(), &writerB));
        std::vector<BYTE> red(16), green(16); for (int i = 0; i < 4; ++i) { red[i*4+2] = 255; green[i*4+1] = 255; }
        Check(publish(writerA, red.data(), 0, 2, 8) == E_INVALIDARG, "reject invalid frame geometry");
        Ok(publish(writerA, red.data(), 2, 2, 8)); Ok(publish(writerB, green.data(), 2, 2, 8));
        {
            ComPtr<IMFActivate> temporaryActivation;
            Ok(activate(pathA.c_str(), &temporaryActivation));
            ComPtr<IMFMediaSource> survivingSource;
            Ok(temporaryActivation->ActivateObject(IID_PPV_ARGS(&survivingSource)));
            temporaryActivation.Reset();
            DWORD characteristics = 0;
            HRESULT stillAlive = survivingSource->GetCharacteristics(&characteristics);
            printf("Activation release regression HRESULT: 0x%08lX\n", stillAlive);
            Check(SUCCEEDED(stillAlive), "releasing activation does not shut down an externally owned source");
            auto survivingStream = Start(survivingSource.Get());
            Check(Pixel(Sample(survivingStream.Get()).Get(), wt::Width * (wt::Height / 2) + wt::Width / 2) == 82,
                "source still streams after activation release");
            survivingSource->Shutdown();
        }
        Check(_wspawnl(_P_WAIT, exe, exe, L"--read", pathA.c_str(), L"82", nullptr) == 0, "cross-process bridge and source activation");
        ComPtr<IMFActivate> activationA, activationB;
        Ok(activate(pathA.c_str(), &activationA)); Ok(activate(pathB.c_str(), &activationB));
        Ok(activationA->SetUINT32(MF_LOW_LATENCY, TRUE));
        Ok(activationA->ActivateObject(IID_PPV_ARGS(&a))); Ok(activationB->ActivateObject(IID_PPV_ARGS(&b)));
        ComPtr<IMFMediaSourceEx> extended; Ok(a.As(&extended));
        ComPtr<IMFAttributes> attrs; Ok(extended->GetStreamAttributes(0, &attrs));
        ComPtr<IMFAttributes> sourceAttrs; Ok(extended->GetSourceAttributes(&sourceAttrs));
        UINT32 lowLatency = 0; Ok(sourceAttrs->GetUINT32(MF_LOW_LATENCY, &lowLatency)); Check(lowLatency == TRUE, "activation attributes propagate into source");
        UINT32 streamId = 99; Ok(attrs->GetUINT32(MF_DEVICESTREAM_STREAM_ID, &streamId)); Check(streamId == 0, "frame server stream attributes");
        auto streamA = Start(a.Get()); auto streamB = Start(b.Get());
        ComPtr<IMFMediaType> token; Ok(MFCreateMediaType(&token));
        auto sampleA = Sample(streamA.Get(), token.Get()); auto sampleB = Sample(streamB.Get());
        constexpr DWORD center = wt::Width * (wt::Height / 2) + wt::Width / 2;
        Check(Pixel(sampleA.Get(), center) == 82, "camera A red luminance");
        Check(Pixel(sampleB.Get(), center) == 144, "camera B green independent from A");
        Check(Pixel(sampleA.Get(), 0) == 16, "aspect preserved with black bars");
        ComPtr<IUnknown> returned; Ok(sampleA->GetUnknown(MFSampleExtension_Token, IID_PPV_ARGS(&returned)));
        Check(returned.Get() == static_cast<IUnknown*>(token.Get()), "request sample token preserved");
        LONGLONG first = 0, second = 0; Ok(sampleA->GetSampleTime(&first));
        auto next = Sample(streamA.Get()); Ok(next->GetSampleTime(&second)); Check(second > first, "timestamps increase");
        clear(writerA); auto black = Sample(streamA.Get()); Check(Pixel(black.Get(), center) == 16, "stop publishing clears image");
        Check(Pixel(Sample(streamB.Get()).Get(), center) == 144, "clearing A leaves B intact");
        Ok(a->Stop()); Event(a.Get(), MESourceStopped); Event(streamA.Get(), MEStreamStopped);
        Check(streamA->RequestSample(nullptr) == MF_E_INVALIDREQUEST, "reject request while stopped");
        ComPtr<IMFPresentationDescriptor> pd; Ok(a->CreatePresentationDescriptor(&pd)); PROPVARIANT position{};
        Ok(a->Start(pd.Get(), &GUID_NULL, &position)); Event(a.Get(), MEUpdatedStream); Event(a.Get(), MESourceStarted); Event(streamA.Get(), MEStreamStarted);
        Ok(publish(writerA, red.data(), 2, 2, 8)); Check(Pixel(Sample(streamA.Get()).Get(), center) == 82, "stop and restart restores current frame");
        close(writerA); writerA = nullptr;
        Ok(open(pathA.c_str(), &writerA)); Ok(publish(writerA, green.data(), 2, 2, 8));
        Check(Pixel(Sample(streamA.Get()).Get(), center) == 144, "publisher reconnects while consumer stays open");
        Sleep(2050);
        Check(Pixel(Sample(streamA.Get()).Get(), center) == 16, "expired publisher frame becomes black");
        Ok(a->Shutdown()); Ok(b->Shutdown());
        Check(streamA->RequestSample(nullptr) == MF_E_SHUTDOWN, "shutdown rejects requests");
        Ok(activationA->ShutdownObject()); Ok(activationB->ShutdownObject());
        activationA.Reset(); activationB.Reset(); sourceAttrs.Reset();
        streamA.Reset(); streamB.Reset(); extended.Reset(); attrs.Reset(); a.Reset(); b.Reset();
        Check(unload() == S_OK, "COM source and stream released without reference cycle");
        close(writerA); writerA = nullptr; close(writerB); writerB = nullptr;
        FreeLibrary(dll); dll = nullptr; MFShutdown(); CoUninitialize();
        DeleteFileW(pathA.c_str()); DeleteFileW(pathB.c_str());
        printf("%d native checks passed. No devices registered.\n", checks); return 0;
    } catch (const std::exception& error) {
        fprintf(stderr, "FAIL %s\n", error.what());
        if (a) a->Shutdown(); if (b) b->Shutdown();
        if (close && writerA) close(writerA); if (close && writerB) close(writerB);
        if (!pathA.empty()) DeleteFileW(pathA.c_str()); if (!pathB.empty()) DeleteFileW(pathB.c_str());
        return 1;
    }
}

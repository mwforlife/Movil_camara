#include <windows.h>
#include <mfapi.h>
#include <mfidl.h>
#include <mferror.h>
#include <mfvirtualcamera.h>
#include <ks.h>
#include <ksmedia.h>
#include <ksproxy.h>
#include <wrl.h>
#include <atomic>
#include <condition_variable>
#include <deque>
#include <mutex>
#include <thread>
#include <chrono>
#include "FrameBridge.h"
using namespace Microsoft::WRL;
#define HR(call) do { HRESULT result_ = (call); if (FAILED(result_)) return result_; } while (0)

static std::atomic<long> objects{0}, locks{0};
struct Counted { Counted() { ++objects; } ~Counted() { --objects; } };
class Events {
    std::mutex mutex;
    ComPtr<IMFMediaEventQueue> queue;
public:
    HRESULT Init() { return MFCreateEventQueue(&queue); }
    ComPtr<IMFMediaEventQueue> Get() { std::lock_guard guard(mutex); return queue; }
    void Shutdown() { auto q = Get(); if (q) q->Shutdown(); std::lock_guard guard(mutex); queue.Reset(); }
    HRESULT Send(MediaEventType type, const PROPVARIANT* value = nullptr) {
        auto q = Get(); return q ? q->QueueEventParamVar(type, GUID_NULL, S_OK, value) : MF_E_SHUTDOWN;
    }
};
#define EVENT_METHODS \
 HRESULT STDMETHODCALLTYPE GetEvent(DWORD f, IMFMediaEvent** e) override { auto q = events.Get(); return q ? q->GetEvent(f,e) : MF_E_SHUTDOWN; } \
 HRESULT STDMETHODCALLTYPE BeginGetEvent(IMFAsyncCallback* c, IUnknown* s) override { auto q = events.Get(); return q ? q->BeginGetEvent(c,s) : MF_E_SHUTDOWN; } \
 HRESULT STDMETHODCALLTYPE EndGetEvent(IMFAsyncResult* r, IMFMediaEvent** e) override { auto q = events.Get(); return q ? q->EndGetEvent(r,e) : MF_E_SHUTDOWN; } \
 HRESULT STDMETHODCALLTYPE QueueEvent(MediaEventType t, REFGUID g, HRESULT h, const PROPVARIANT* v) override { auto q = events.Get(); return q ? q->QueueEventParamVar(t,g,h,v) : MF_E_SHUTDOWN; }

class Stream final : public RuntimeClass<RuntimeClassFlags<ClassicCom>, ChainInterfaces<IMFMediaStream2, IMFMediaStream, IMFMediaEventGenerator>>, public Counted {
    std::mutex mutex;
    std::condition_variable wake;
    ComPtr<IMFMediaSource> source;
    ComPtr<IMFStreamDescriptor> descriptor;
    ComPtr<IMFAttributes> attributes;
    Events events;
    std::thread worker;
    std::deque<ComPtr<IUnknown>> requests;
    MF_STREAM_STATE state = MF_STREAM_STATE_STOPPED;
    bool shutdown = false;
    wt::Bridge bridge;
    std::wstring path;
    std::chrono::steady_clock::time_point next{};
    LONGLONG lastTimestamp = 0, sampleIndex = 0;

    HRESULT Sample(IUnknown* token) {
        ComPtr<IMFSample> sample;
        ComPtr<IMFMediaBuffer> buffer;
        HR(MFCreateSample(&sample));
        HR(MFCreateMemoryBuffer(wt::FrameBytes, &buffer));
        BYTE* bytes = nullptr;
        HR(buffer->Lock(&bytes, nullptr, nullptr));
        if (!bridge.Read(bytes)) {
            // Open lazily, including after the receiver restarts or publishes its first frame.
            bridge.Open(path, false);
            if (!bridge.Read(bytes)) {
                memset(bytes, 16, wt::Width * wt::Height);
                memset(bytes + wt::Width * wt::Height, 128, wt::FrameBytes - wt::Width * wt::Height);
            }
        }
        buffer->Unlock();
        HR(buffer->SetCurrentLength(wt::FrameBytes));
        HR(sample->AddBuffer(buffer.Get()));
        auto timestamp = std::max(MFGetSystemTime(), lastTimestamp + 1);
        lastTimestamp = timestamp;
        ++sampleIndex;
        HR(sample->SetSampleTime(timestamp));
        HR(sample->SetSampleDuration(10000000 / 30));
        HR(sample->SetUINT32(MFSampleExtension_CleanPoint, TRUE));
        if (sampleIndex == 1) HR(sample->SetUINT32(MFSampleExtension_Discontinuity, TRUE));
        if (token) HR(sample->SetUnknown(MFSampleExtension_Token, token));
        auto q = events.Get();
        return q ? q->QueueEventParamUnk(MEMediaSample, GUID_NULL, S_OK, sample.Get()) : MF_E_SHUTDOWN;
    }
    void Run() {
        CoInitializeEx(nullptr, COINIT_MULTITHREADED);
        std::unique_lock guard(mutex);
        while (!shutdown) {
            wake.wait(guard, [&] { return shutdown || (state == MF_STREAM_STATE_RUNNING && !requests.empty()); });
            if (shutdown) break;
            if (wake.wait_until(guard, next, [&] { return shutdown || state != MF_STREAM_STATE_RUNNING; })) continue;
            if (shutdown || state != MF_STREAM_STATE_RUNNING || requests.empty()) continue;
            auto token = requests.front(); requests.pop_front();
            // Serialize Stop/Shutdown with emission so no old-session sample appears after a restart.
            HRESULT hr = Sample(token.Get());
            if (FAILED(hr)) QueueEvent(MEError, GUID_NULL, hr, nullptr);
            auto now = std::chrono::steady_clock::now();
            next = std::max(next + std::chrono::nanoseconds(33333333), now);
        }
        guard.unlock();
        CoUninitialize();
    }
public:
    ~Stream() { ShutdownStream(); }
    HRESULT Init(IMFMediaSource* parent, const std::wstring& framePath) {
        source = parent; path = framePath;
        HR(events.Init());
        HR(MFCreateAttributes(&attributes, 5));
        HR(attributes->SetGUID(MF_DEVICESTREAM_STREAM_CATEGORY, PINNAME_VIDEO_CAPTURE));
        HR(attributes->SetUINT32(MF_DEVICESTREAM_STREAM_ID, 0));
        HR(attributes->SetUINT32(MF_DEVICESTREAM_FRAMESERVER_SHARED, TRUE));
        HR(attributes->SetUINT32(MF_DEVICESTREAM_ATTRIBUTE_FRAMESOURCE_TYPES, MFFrameSourceTypes_Color));
        ComPtr<IMFMediaType> type;
        HR(MFCreateMediaType(&type));
        HR(type->SetGUID(MF_MT_MAJOR_TYPE, MFMediaType_Video));
        HR(type->SetGUID(MF_MT_SUBTYPE, MFVideoFormat_NV12));
        HR(MFSetAttributeSize(type.Get(), MF_MT_FRAME_SIZE, wt::Width, wt::Height));
        HR(MFSetAttributeRatio(type.Get(), MF_MT_FRAME_RATE, 30, 1));
        HR(MFSetAttributeRatio(type.Get(), MF_MT_PIXEL_ASPECT_RATIO, 1, 1));
        HR(type->SetUINT32(MF_MT_INTERLACE_MODE, MFVideoInterlace_Progressive));
        HR(type->SetUINT32(MF_MT_ALL_SAMPLES_INDEPENDENT, TRUE));
        HR(type->SetUINT32(MF_MT_FIXED_SIZE_SAMPLES, TRUE));
        HR(type->SetUINT32(MF_MT_SAMPLE_SIZE, wt::FrameBytes));
        HR(type->SetUINT32(MF_MT_DEFAULT_STRIDE, wt::Width));
        HR(type->SetUINT32(MF_MT_VIDEO_NOMINAL_RANGE, MFNominalRange_16_235));
        HR(type->SetUINT32(MF_MT_YUV_MATRIX, MFVideoTransferMatrix_BT601));
        IMFMediaType* types[] = { type.Get() };
        HR(MFCreateStreamDescriptor(0, 1, types, &descriptor));
        ComPtr<IMFMediaTypeHandler> handler;
        HR(descriptor->GetMediaTypeHandler(&handler));
        HR(handler->SetCurrentMediaType(type.Get()));
        worker = std::thread([this] { Run(); });
        return S_OK;
    }
    void ShutdownStream() {
        { std::lock_guard guard(mutex); shutdown = true; state = MF_STREAM_STATE_STOPPED; requests.clear(); source.Reset(); }
        wake.notify_all();
        if (worker.joinable()) worker.join();
        events.Shutdown(); bridge.Close();
    }
    HRESULT StartStream() {
        std::lock_guard guard(mutex);
        if (shutdown) return MF_E_SHUTDOWN;
        state = MF_STREAM_STATE_RUNNING; requests.clear();
        lastTimestamp = 0; sampleIndex = 0; next = std::chrono::steady_clock::now();
        PROPVARIANT pv{}; pv.vt = VT_I8; pv.hVal.QuadPart = MFGetSystemTime();
        HR(events.Send(MEStreamStarted, &pv));
        wake.notify_all(); return S_OK;
    }
    HRESULT StopStream() {
        std::lock_guard guard(mutex);
        if (shutdown) return MF_E_SHUTDOWN;
        state = MF_STREAM_STATE_STOPPED; requests.clear(); wake.notify_all();
        return events.Send(MEStreamStopped);
    }
    HRESULT Attributes(IMFAttributes** value) { return attributes.CopyTo(value); }
    EVENT_METHODS
    HRESULT STDMETHODCALLTYPE GetMediaSource(IMFMediaSource** value) override {
        if (!value) return E_POINTER; *value = nullptr;
        std::lock_guard guard(mutex); return shutdown ? MF_E_SHUTDOWN : source.CopyTo(value);
    }
    HRESULT STDMETHODCALLTYPE GetStreamDescriptor(IMFStreamDescriptor** value) override {
        std::lock_guard guard(mutex); return shutdown ? MF_E_SHUTDOWN : descriptor.CopyTo(value);
    }
    HRESULT STDMETHODCALLTYPE RequestSample(IUnknown* token) override {
        std::lock_guard guard(mutex);
        if (shutdown) return MF_E_SHUTDOWN;
        if (state != MF_STREAM_STATE_RUNNING) return MF_E_INVALIDREQUEST;
        if (requests.size() >= 5) return MF_E_NOTACCEPTING;
        requests.emplace_back(token); wake.notify_all(); return S_OK;
    }
    HRESULT STDMETHODCALLTYPE SetStreamState(MF_STREAM_STATE value) override {
        if (value != MF_STREAM_STATE_RUNNING && value != MF_STREAM_STATE_STOPPED && value != MF_STREAM_STATE_PAUSED) return E_INVALIDARG;
        std::lock_guard guard(mutex);
        if (shutdown) return MF_E_SHUTDOWN;
        state = value; if (value != MF_STREAM_STATE_RUNNING) requests.clear(); wake.notify_all(); return S_OK;
    }
    HRESULT STDMETHODCALLTYPE GetStreamState(MF_STREAM_STATE* value) override {
        if (!value) return E_POINTER;
        std::lock_guard guard(mutex); if (shutdown) return MF_E_SHUTDOWN; *value = state; return S_OK;
    }
};

class Source final : public RuntimeClass<RuntimeClassFlags<ClassicCom>, ChainInterfaces<IMFMediaSourceEx, IMFMediaSource, IMFMediaEventGenerator>, IMFGetService, IKsControl,
    IMFSampleAllocatorControl>, public Counted {
    std::recursive_mutex mutex;
    Events events;
    ComPtr<Stream> stream;
    ComPtr<IMFPresentationDescriptor> presentation;
    ComPtr<IMFAttributes> attributes;
    bool shutdown = false, announced = false;
public:
    ~Source() { Shutdown(); }
    HRESULT Init(const std::wstring& path, IMFAttributes* configuration = nullptr) {
        HR(events.Init());
        HR(MFCreateAttributes(&attributes, 4));
        if (configuration) HR(configuration->CopyAllItems(attributes.Get()));
        HR(attributes->SetGUID(MF_DEVSOURCE_ATTRIBUTE_SOURCE_TYPE, MF_DEVSOURCE_ATTRIBUTE_SOURCE_TYPE_VIDCAP_GUID));
        UINT32 nameLength = 0;
        if (FAILED(attributes->GetStringLength(MF_DEVSOURCE_ATTRIBUTE_FRIENDLY_NAME, &nameLength)))
            HR(attributes->SetString(MF_DEVSOURCE_ATTRIBUTE_FRIENDLY_NAME, L"WT MultiCam"));
        stream = Make<Stream>();
        if (!stream) return E_OUTOFMEMORY;
        HRESULT hr = stream->Init(this, path);
        if (FAILED(hr)) { stream->ShutdownStream(); return hr; }
        ComPtr<IMFStreamDescriptor> descriptor;
        hr = stream->GetStreamDescriptor(&descriptor);
        if (SUCCEEDED(hr)) { IMFStreamDescriptor* streams[] = { descriptor.Get() }; hr = MFCreatePresentationDescriptor(1, streams, &presentation); }
        if (SUCCEEDED(hr)) hr = presentation->SelectStream(0);
        if (FAILED(hr)) stream->ShutdownStream();
        return hr;
    }
    EVENT_METHODS
    HRESULT STDMETHODCALLTYPE GetCharacteristics(DWORD* value) override {
        if (!value) return E_POINTER; std::lock_guard guard(mutex);
        if (shutdown) return MF_E_SHUTDOWN; *value = MFMEDIASOURCE_IS_LIVE; return S_OK;
    }
    HRESULT STDMETHODCALLTYPE CreatePresentationDescriptor(IMFPresentationDescriptor** value) override {
        if (!value) return E_POINTER; *value = nullptr; std::lock_guard guard(mutex);
        return shutdown ? MF_E_SHUTDOWN : presentation->Clone(value);
    }
    HRESULT STDMETHODCALLTYPE Start(IMFPresentationDescriptor* pd, const GUID* format, const PROPVARIANT* position) override {
        if (!pd || !position) return E_POINTER;
        if (format && *format != GUID_NULL) return MF_E_UNSUPPORTED_TIME_FORMAT;
        if (position->vt != VT_EMPTY && (position->vt != VT_I8 || position->hVal.QuadPart != 0)) return MF_E_INVALIDREQUEST;
        std::lock_guard guard(mutex);
        if (shutdown) return MF_E_SHUTDOWN;
        DWORD count = 0; HR(pd->GetStreamDescriptorCount(&count)); if (count != 1) return E_INVALIDARG;
        BOOL selected = FALSE; ComPtr<IMFStreamDescriptor> descriptor;
        HR(pd->GetStreamDescriptorByIndex(0, &selected, &descriptor)); if (!selected) return MF_E_INVALIDREQUEST;
        DWORD id = 1; HR(descriptor->GetStreamIdentifier(&id)); if (id != 0) return E_INVALIDARG;
        ComPtr<IMFMediaTypeHandler> handler; ComPtr<IMFMediaType> proposed, expected;
        HR(descriptor->GetMediaTypeHandler(&handler)); HR(handler->GetCurrentMediaType(&proposed));
        ComPtr<IMFStreamDescriptor> ours; HR(stream->GetStreamDescriptor(&ours)); HR(ours->GetMediaTypeHandler(&handler));
        HR(handler->GetCurrentMediaType(&expected)); DWORD equal = 0; if (proposed->IsEqual(expected.Get(), &equal) != S_OK) return MF_E_INVALIDMEDIATYPE;
        auto q = events.Get(); if (!q) return MF_E_SHUTDOWN;
        HR(q->QueueEventParamUnk(announced ? MEUpdatedStream : MENewStream, GUID_NULL, S_OK, stream.Get()));
        announced = true;
        HR(stream->StartStream());
        PROPVARIANT pv{}; pv.vt = VT_I8; pv.hVal.QuadPart = MFGetSystemTime();
        return events.Send(MESourceStarted, &pv);
    }
    HRESULT STDMETHODCALLTYPE Stop() override {
        std::lock_guard guard(mutex); if (shutdown) return MF_E_SHUTDOWN;
        HR(stream->StopStream()); return events.Send(MESourceStopped);
    }
    HRESULT STDMETHODCALLTYPE Pause() override { return MF_E_INVALID_STATE_TRANSITION; }
    HRESULT STDMETHODCALLTYPE Shutdown() override {
        ComPtr<Stream> old;
        { std::lock_guard guard(mutex); if (shutdown) return S_OK; shutdown = true; old = stream; stream.Reset(); presentation.Reset(); }
        if (old) old->ShutdownStream(); events.Shutdown(); return S_OK;
    }
    HRESULT STDMETHODCALLTYPE GetSourceAttributes(IMFAttributes** value) override {
        std::lock_guard guard(mutex); return shutdown ? MF_E_SHUTDOWN : attributes.CopyTo(value);
    }
    HRESULT STDMETHODCALLTYPE GetStreamAttributes(DWORD id, IMFAttributes** value) override {
        std::lock_guard guard(mutex); if (shutdown) return MF_E_SHUTDOWN; return id ? MF_E_INVALIDSTREAMNUMBER : stream->Attributes(value);
    }
    HRESULT STDMETHODCALLTYPE SetD3DManager(IUnknown*) override { return E_NOTIMPL; }
    HRESULT STDMETHODCALLTYPE GetService(REFGUID, REFIID, void** value) override {
        if (!value) return E_POINTER; *value = nullptr; return MF_E_UNSUPPORTED_SERVICE;
    }
    HRESULT STDMETHODCALLTYPE KsProperty(PKSPROPERTY, ULONG, LPVOID, ULONG, ULONG* returned) override {
        if (returned) *returned = 0; return HRESULT_FROM_WIN32(ERROR_SET_NOT_FOUND);
    }
    HRESULT STDMETHODCALLTYPE KsMethod(PKSMETHOD, ULONG, LPVOID, ULONG, ULONG* returned) override {
        if (returned) *returned = 0; return HRESULT_FROM_WIN32(ERROR_SET_NOT_FOUND);
    }
    HRESULT STDMETHODCALLTYPE KsEvent(PKSEVENT, ULONG, LPVOID, ULONG, ULONG* returned) override {
        if (returned) *returned = 0; return HRESULT_FROM_WIN32(ERROR_SET_NOT_FOUND);
    }
    HRESULT STDMETHODCALLTYPE SetDefaultAllocator(DWORD id, IUnknown*) override { return id ? MF_E_INVALIDSTREAMNUMBER : S_OK; }
    HRESULT STDMETHODCALLTYPE GetAllocatorUsage(DWORD id, DWORD* input, MFSampleAllocatorUsage* usage) override {
        if (!input || !usage) return E_POINTER; if (id) return MF_E_INVALIDSTREAMNUMBER;
        *input = 0; *usage = MFSampleAllocatorUsage_UsesCustomAllocator; return S_OK;
    }
};

static HRESULT CreateSource(const std::wstring& path, REFIID iid, void** value, IMFAttributes* configuration = nullptr) {
    if (!value) return E_POINTER; *value = nullptr;
    auto source = Make<Source>(); if (!source) return E_OUTOFMEMORY;
    HRESULT hr = source->Init(path, configuration);
    if (SUCCEEDED(hr)) hr = source->QueryInterface(iid, value);
    if (FAILED(hr)) source->Shutdown();
    return hr;
}

// Frame Server CoCreates IMFActivate, copies its configuration attributes, then activates the source.
class Activation final : public RuntimeClass<RuntimeClassFlags<ClassicCom>, ChainInterfaces<IMFActivate, IMFAttributes>>, public Counted {
    ComPtr<IMFAttributes> attributes;
    ComPtr<IMFMediaSource> source;
    std::wstring path;
    std::mutex mutex;
public:
    // Releasing IMFActivate releases only this object's reference. The caller owns the
    // returned source and explicitly calls ShutdownObject or IMFMediaSource::Shutdown.
    // Frame Server may release its activation object while retaining the live source.
    ~Activation() = default;
    HRESULT Init(const std::wstring& value) { path = value; return MFCreateAttributes(&attributes, 8); }
    HRESULT STDMETHODCALLTYPE ActivateObject(REFIID iid, void** result) override {
        if (!result) return E_POINTER; *result = nullptr;
        std::lock_guard guard(mutex);
        try {
            if (!source) HR(CreateSource(path, __uuidof(IMFMediaSource), reinterpret_cast<void**>(source.GetAddressOf()), attributes.Get()));
            return source->QueryInterface(iid, result);
        } catch (...) { return E_OUTOFMEMORY; }
    }
    HRESULT STDMETHODCALLTYPE ShutdownObject() override {
        std::lock_guard guard(mutex); if (source) { source->Shutdown(); source.Reset(); } return S_OK;
    }
    HRESULT STDMETHODCALLTYPE DetachObject() override { std::lock_guard guard(mutex); source.Reset(); return S_OK; }
    HRESULT STDMETHODCALLTYPE GetItem(REFGUID key, PROPVARIANT* item) override { return attributes->GetItem(key, item); }
    HRESULT STDMETHODCALLTYPE GetItemType(REFGUID key, MF_ATTRIBUTE_TYPE* type) override { return attributes->GetItemType(key, type); }
    HRESULT STDMETHODCALLTYPE CompareItem(REFGUID key, REFPROPVARIANT item, BOOL* result) override { return attributes->CompareItem(key, item, result); }
    HRESULT STDMETHODCALLTYPE Compare(IMFAttributes* other, MF_ATTRIBUTES_MATCH_TYPE match, BOOL* result) override { return attributes->Compare(other, match, result); }
    HRESULT STDMETHODCALLTYPE GetUINT32(REFGUID key, UINT32* result) override { return attributes->GetUINT32(key, result); }
    HRESULT STDMETHODCALLTYPE GetUINT64(REFGUID key, UINT64* result) override { return attributes->GetUINT64(key, result); }
    HRESULT STDMETHODCALLTYPE GetDouble(REFGUID key, double* result) override { return attributes->GetDouble(key, result); }
    HRESULT STDMETHODCALLTYPE GetGUID(REFGUID key, GUID* result) override { return attributes->GetGUID(key, result); }
    HRESULT STDMETHODCALLTYPE GetStringLength(REFGUID key, UINT32* length) override { return attributes->GetStringLength(key, length); }
    HRESULT STDMETHODCALLTYPE GetString(REFGUID key, LPWSTR text, UINT32 size, UINT32* length) override { return attributes->GetString(key, text, size, length); }
    HRESULT STDMETHODCALLTYPE GetAllocatedString(REFGUID key, LPWSTR* text, UINT32* length) override { return attributes->GetAllocatedString(key, text, length); }
    HRESULT STDMETHODCALLTYPE GetBlobSize(REFGUID key, UINT32* size) override { return attributes->GetBlobSize(key, size); }
    HRESULT STDMETHODCALLTYPE GetBlob(REFGUID key, UINT8* blob, UINT32 size, UINT32* length) override { return attributes->GetBlob(key, blob, size, length); }
    HRESULT STDMETHODCALLTYPE GetAllocatedBlob(REFGUID key, UINT8** blob, UINT32* length) override { return attributes->GetAllocatedBlob(key, blob, length); }
    HRESULT STDMETHODCALLTYPE GetUnknown(REFGUID key, REFIID iid, LPVOID* result) override { return attributes->GetUnknown(key, iid, result); }
    HRESULT STDMETHODCALLTYPE SetItem(REFGUID key, REFPROPVARIANT item) override { return attributes->SetItem(key, item); }
    HRESULT STDMETHODCALLTYPE DeleteItem(REFGUID key) override { return attributes->DeleteItem(key); }
    HRESULT STDMETHODCALLTYPE DeleteAllItems() override { return attributes->DeleteAllItems(); }
    HRESULT STDMETHODCALLTYPE SetUINT32(REFGUID key, UINT32 item) override { return attributes->SetUINT32(key, item); }
    HRESULT STDMETHODCALLTYPE SetUINT64(REFGUID key, UINT64 item) override { return attributes->SetUINT64(key, item); }
    HRESULT STDMETHODCALLTYPE SetDouble(REFGUID key, double item) override { return attributes->SetDouble(key, item); }
    HRESULT STDMETHODCALLTYPE SetGUID(REFGUID key, REFGUID item) override { return attributes->SetGUID(key, item); }
    HRESULT STDMETHODCALLTYPE SetString(REFGUID key, LPCWSTR item) override { return attributes->SetString(key, item); }
    HRESULT STDMETHODCALLTYPE SetBlob(REFGUID key, const UINT8* blob, UINT32 length) override { return attributes->SetBlob(key, blob, length); }
    HRESULT STDMETHODCALLTYPE SetUnknown(REFGUID key, IUnknown* item) override { return attributes->SetUnknown(key, item); }
    HRESULT STDMETHODCALLTYPE LockStore() override { return attributes->LockStore(); }
    HRESULT STDMETHODCALLTYPE UnlockStore() override { return attributes->UnlockStore(); }
    HRESULT STDMETHODCALLTYPE GetCount(UINT32* count) override { return attributes->GetCount(count); }
    HRESULT STDMETHODCALLTYPE GetItemByIndex(UINT32 index, GUID* key, PROPVARIANT* item) override { return attributes->GetItemByIndex(index, key, item); }
    HRESULT STDMETHODCALLTYPE CopyAllItems(IMFAttributes* other) override { return attributes->CopyAllItems(other); }
};
static HRESULT CreateActivation(const std::wstring& path, REFIID iid, void** result) {
    if (!result) return E_POINTER; *result = nullptr;
    auto activation = Make<Activation>(); if (!activation) return E_OUTOFMEMORY;
    HR(activation->Init(path)); return activation->QueryInterface(iid, result);
}
class Factory final : public RuntimeClass<RuntimeClassFlags<ClassicCom>, IClassFactory>, public Counted {
    std::wstring path;
public:
    void Init(std::wstring value) { path = std::move(value); }
    HRESULT STDMETHODCALLTYPE CreateInstance(IUnknown* outer, REFIID iid, void** value) override {
        if (outer) return CLASS_E_NOAGGREGATION;
        try { return CreateActivation(path, iid, value); } catch (...) { return E_OUTOFMEMORY; }
    }
    HRESULT STDMETHODCALLTYPE LockServer(BOOL value) override { value ? ++locks : --locks; return S_OK; }
};
STDAPI DllGetClassObject(REFCLSID clsid, REFIID iid, void** value) {
    if (!value) return E_POINTER; *value = nullptr;
    wchar_t guid[40]{}; StringFromGUID2(clsid, guid, 40);
    std::wstring key = std::wstring(L"SOFTWARE\\Classes\\CLSID\\") + guid;
    wchar_t framePath[32768]{}; DWORD size = sizeof(framePath), marker = 0, markerSize = sizeof(marker);
    if (RegGetValueW(HKEY_LOCAL_MACHINE, key.c_str(), L"WTMultiCamSource", RRF_RT_REG_DWORD, nullptr, &marker, &markerSize) != ERROR_SUCCESS || marker != 1)
        return CLASS_E_CLASSNOTAVAILABLE;
    if (RegGetValueW(HKEY_LOCAL_MACHINE, key.c_str(), L"FramePath", RRF_RT_REG_SZ, nullptr, framePath, &size) != ERROR_SUCCESS)
        return CLASS_E_CLASSNOTAVAILABLE;
    try { auto factory = Make<Factory>(); if (!factory) return E_OUTOFMEMORY; factory->Init(framePath); return factory->QueryInterface(iid, value); }
    catch (...) { return E_OUTOFMEMORY; }
}
STDAPI DllCanUnloadNow() { return objects == 0 && locks == 0 ? S_OK : S_FALSE; }

// Narrow C ABI for the WPF receiver. No COM object is marshalled through .NET.
struct CameraHandle { ComPtr<IMFVirtualCamera> camera; };
struct ComScope { HRESULT hr = CoInitializeEx(nullptr, COINIT_MULTITHREADED); ~ComScope() { if (SUCCEEDED(hr)) CoUninitialize(); } };
extern "C" __declspec(dllexport) HRESULT __stdcall WT_StartCameraEx(LPCWSTR clsid, LPCWSTR name, void** value, DWORD* stage) {
    if (!stage) return E_POINTER; *stage = 0;
    if (!clsid || !name || !value) return E_POINTER; *value = nullptr;
    *stage = 1; ComScope com; HR(com.hr);
    *stage = 2; HR(MFStartup(MF_VERSION));
    auto handle = new (std::nothrow) CameraHandle(); if (!handle) { MFShutdown(); return E_OUTOFMEMORY; }
    *stage = 3;
    HRESULT hr = MFCreateVirtualCamera(MFVirtualCameraType_SoftwareCameraSource, MFVirtualCameraLifetime_System,
        MFVirtualCameraAccess_CurrentUser, name, clsid, nullptr, 0, &handle->camera);
    if (SUCCEEDED(hr)) { *stage = 4; hr = handle->camera->Start(nullptr); }
    if (FAILED(hr)) { if (handle->camera) handle->camera->Shutdown(); delete handle; MFShutdown(); return hr; }
    *value = handle; *stage = 0; return S_OK;
}
extern "C" __declspec(dllexport) HRESULT __stdcall WT_StartCamera(LPCWSTR clsid, LPCWSTR name, void** value) {
    DWORD stage = 0; return WT_StartCameraEx(clsid, name, value, &stage);
}
extern "C" __declspec(dllexport) HRESULT __stdcall WT_StopCamera(void* value, BOOL remove) {
    if (!value) return E_POINTER; ComScope com; HR(com.hr);
    auto handle = static_cast<CameraHandle*>(value);
    HRESULT hr = remove ? handle->camera->Remove() : S_OK;
    handle->camera->Shutdown(); delete handle; MFShutdown(); return hr;
}
extern "C" __declspec(dllexport) HRESULT __stdcall WT_RemoveCamera(LPCWSTR clsid, LPCWSTR name) {
    if (!clsid || !name) return E_POINTER;
    ComScope com; HR(com.hr); HR(MFStartup(MF_VERSION));
    ComPtr<IMFVirtualCamera> camera;
    HRESULT hr = MFCreateVirtualCamera(MFVirtualCameraType_SoftwareCameraSource, MFVirtualCameraLifetime_System,
        MFVirtualCameraAccess_CurrentUser, name, clsid, nullptr, 0, &camera);
    if (SUCCEEDED(hr)) { hr = camera->Remove(); camera->Shutdown(); }
    camera.Reset(); MFShutdown(); return hr;
}
extern "C" __declspec(dllexport) HRESULT __stdcall WT_OpenPublisher(LPCWSTR path, void** value) {
    if (!path || !value) return E_POINTER; *value = nullptr;
    auto bridge = new (std::nothrow) wt::Bridge(); if (!bridge) return E_OUTOFMEMORY;
    HRESULT hr = bridge->Open(path, true); if (FAILED(hr)) { delete bridge; return hr; } *value = bridge; return S_OK;
}
extern "C" __declspec(dllexport) HRESULT __stdcall WT_Publish(void* value, const BYTE* bytes, DWORD w, DWORD h, DWORD stride) {
    if (!value) return E_POINTER;
    try { return static_cast<wt::Bridge*>(value)->Publish(bytes, w, h, stride); } catch (...) { return E_OUTOFMEMORY; }
}
extern "C" __declspec(dllexport) void __stdcall WT_Clear(void* value) { if (value) static_cast<wt::Bridge*>(value)->Clear(); }
extern "C" __declspec(dllexport) void __stdcall WT_ClosePublisher(void* value) { if (value) { auto b = static_cast<wt::Bridge*>(value); b->Clear(); delete b; } }
extern "C" __declspec(dllexport) HRESULT __stdcall WT_TestSource(LPCWSTR path, IMFMediaSource** value) {
    try { return path ? CreateSource(path, __uuidof(IMFMediaSource), reinterpret_cast<void**>(value)) : E_POINTER; }
    catch (...) { return E_OUTOFMEMORY; }
}
extern "C" __declspec(dllexport) HRESULT __stdcall WT_TestActivation(LPCWSTR path, IMFActivate** value) {
    try { return path ? CreateActivation(path, __uuidof(IMFActivate), reinterpret_cast<void**>(value)) : E_POINTER; }
    catch (...) { return E_OUTOFMEMORY; }
}

#pragma once
#include <windows.h>
#include <cstdint>
#include <string>
#include <vector>

namespace wt {
constexpr DWORD Width = 1920, Height = 1080, FrameBytes = Width * Height * 3 / 2;
constexpr DWORD Magic = 0x57435631;
struct Header {
    DWORD magic, version, width, height, bytes, active;
    ULONGLONG tick, sequence;
};
static_assert(sizeof(Header) == 40);
constexpr DWORD FileBytes = sizeof(Header) + FrameBytes;

class Bridge {
    HANDLE file = INVALID_HANDLE_VALUE, mapping = nullptr;
    BYTE* view = nullptr;
    bool writer = false;
    std::vector<BYTE> converted;
public:
    ~Bridge() { Close(); }
    void Close();
    HRESULT Open(const std::wstring& path, bool write);
    HRESULT Publish(const BYTE* bgr, DWORD width, DWORD height, DWORD stride);
    void Clear();
    bool Read(BYTE* nv12);
};
}

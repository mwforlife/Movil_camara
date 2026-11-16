#include "FrameBridge.h"
#include <algorithm>
#include <cstring>

namespace wt {
static BYTE Byte(int value) { return static_cast<BYTE>(std::clamp(value, 0, 255)); }
static void Black(BYTE* data) {
    memset(data, 16, Width * Height);
    memset(data + Width * Height, 128, FrameBytes - Width * Height);
}
void Bridge::Close() {
    if (view) UnmapViewOfFile(view);
    if (mapping) CloseHandle(mapping);
    if (file != INVALID_HANDLE_VALUE) CloseHandle(file);
    view = nullptr; mapping = nullptr; file = INVALID_HANDLE_VALUE;
}
HRESULT Bridge::Open(const std::wstring& path, bool write) {
    Close(); writer = write;
    file = CreateFileW(path.c_str(), GENERIC_READ | (write ? GENERIC_WRITE : 0),
        FILE_SHARE_READ | FILE_SHARE_WRITE, nullptr, write ? OPEN_ALWAYS : OPEN_EXISTING, FILE_ATTRIBUTE_NORMAL, nullptr);
    if (file == INVALID_HANDLE_VALUE) return HRESULT_FROM_WIN32(GetLastError());
    LARGE_INTEGER size{};
    if (!GetFileSizeEx(file, &size)) return HRESULT_FROM_WIN32(GetLastError());
    if (write && size.QuadPart != FileBytes) {
        size.QuadPart = FileBytes;
        if (!SetFilePointerEx(file, size, nullptr, FILE_BEGIN) || !SetEndOfFile(file)) return HRESULT_FROM_WIN32(GetLastError());
    } else if (size.QuadPart != FileBytes) return HRESULT_FROM_WIN32(ERROR_BAD_LENGTH);
    mapping = CreateFileMappingW(file, nullptr, write ? PAGE_READWRITE : PAGE_READONLY, 0, FileBytes, nullptr);
    if (!mapping) return HRESULT_FROM_WIN32(GetLastError());
    view = static_cast<BYTE*>(MapViewOfFile(mapping, write ? FILE_MAP_WRITE : FILE_MAP_READ, 0, 0, FileBytes));
    if (!view) return HRESULT_FROM_WIN32(GetLastError());
    if (write) Clear();
    return S_OK;
}
void Bridge::Clear() {
    if (!writer || !view) return;
    OVERLAPPED ov{};
    if (!LockFileEx(file, LOCKFILE_EXCLUSIVE_LOCK, 0, FileBytes, 0, &ov)) return;
    auto* h = reinterpret_cast<Header*>(view);
    *h = { Magic, 1, Width, Height, FrameBytes, 0, GetTickCount64(), h->sequence + 1 };
    Black(view + sizeof(Header));
    UnlockFileEx(file, 0, FileBytes, 0, &ov);
}
HRESULT Bridge::Publish(const BYTE* bgr, DWORD width, DWORD height, DWORD stride) {
    if (!writer || !view) return E_UNEXPECTED;
    if (!bgr || !width || !height || width > 8192 || height > 8192 || stride < width * 4) return E_INVALIDARG;
    // Always produce a stable 1080p NV12 media type. Fit vertical video without stretching.
    DWORD outW = Width, outH = static_cast<DWORD>(static_cast<uint64_t>(height) * Width / width) & ~1u;
    if (outH > Height) { outH = Height; outW = static_cast<DWORD>(static_cast<uint64_t>(width) * Height / height) & ~1u; }
    if (!outW || !outH) return E_INVALIDARG;
    DWORD left = ((Width - outW) / 2) & ~1u, top = ((Height - outH) / 2) & ~1u;
    if (converted.size() != FrameBytes) converted.resize(FrameBytes);
    Black(converted.data());
    for (DWORD y = 0; y < outH; y += 2) {
        for (DWORD x = 0; x < outW; x += 2) {
            int red = 0, green = 0, blue = 0;
            for (DWORD dy = 0; dy < 2; ++dy) for (DWORD dx = 0; dx < 2; ++dx) {
                DWORD sx = static_cast<DWORD>(static_cast<uint64_t>(x + dx) * width / outW);
                DWORD sy = static_cast<DWORD>(static_cast<uint64_t>(y + dy) * height / outH);
                const BYTE* p = bgr + static_cast<size_t>(sy) * stride + sx * 4;
                int b = p[0], g = p[1], r = p[2]; red += r; green += g; blue += b;
                converted[(top + y + dy) * Width + left + x + dx] = Byte(((66*r + 129*g + 25*b + 128) >> 8) + 16);
            }
            red /= 4; green /= 4; blue /= 4;
            DWORD uv = Width * Height + ((top + y) / 2) * Width + left + x;
            converted[uv] = Byte(((-38*red - 74*green + 112*blue + 128) >> 8) + 128);
            converted[uv + 1] = Byte(((112*red - 94*green - 18*blue + 128) >> 8) + 128);
        }
    }
    OVERLAPPED ov{};
    if (!LockFileEx(file, LOCKFILE_EXCLUSIVE_LOCK | LOCKFILE_FAIL_IMMEDIATELY, 0, FileBytes, 0, &ov))
        return HRESULT_FROM_WIN32(GetLastError());
    auto* h = reinterpret_cast<Header*>(view);
    memcpy(view + sizeof(Header), converted.data(), FrameBytes);
    *h = { Magic, 1, Width, Height, FrameBytes, 1, GetTickCount64(), h->sequence + 1 };
    UnlockFileEx(file, 0, FileBytes, 0, &ov);
    return S_OK;
}
bool Bridge::Read(BYTE* nv12) {
    if (!view) return false;
    OVERLAPPED ov{};
    if (!LockFileEx(file, LOCKFILE_FAIL_IMMEDIATELY, 0, FileBytes, 0, &ov)) return false;
    const auto* h = reinterpret_cast<const Header*>(view);
    bool valid = h->magic == Magic && h->version == 1 && h->width == Width && h->height == Height &&
        h->bytes == FrameBytes && h->active == 1 && GetTickCount64() - h->tick < 2000;
    if (valid) memcpy(nv12, view + sizeof(Header), FrameBytes);
    UnlockFileEx(file, 0, FileBytes, 0, &ov);
    return valid;
}
}

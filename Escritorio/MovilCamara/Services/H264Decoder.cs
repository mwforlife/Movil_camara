using System.IO;
using System.Runtime.InteropServices;

namespace MovilCamara.Services;

/// <summary>Windows Media Foundation H.264 MFT plus native NV12 -> RGB32 conversion.
/// Native COM pointers stay inside this adapter; no external codec binaries are required.</summary>
public sealed class H264Decoder : IDisposable
{
    private static readonly Lazy<bool> Startup = new(() => { Check(MFStartup(0x00020070, 0)); return true; });
    private static readonly Guid TransformId = new("bf94c121-5b05-4e6f-8000-ba598961414d");
    private static readonly Guid Major = new("48eba18e-f8c9-4687-bf11-0a74c9f96a8f"), Video = new("73646976-0000-0010-8000-00aa00389b71");
    private static readonly Guid Subtype = new("f7e34c9a-42e8-4714-b74b-cb29d72c35e5");
    private static readonly Guid Avc = new("34363248-0000-0010-8000-00aa00389b71"), Nv12 = new("3231564e-0000-0010-8000-00aa00389b71");
    private static readonly Guid Rgb32 = new("00000016-0000-0010-8000-00aa00389b71");
    private static readonly Guid FrameSize = new("1652c33d-d6b2-4012-b834-72030849a37d"), FrameRate = new("c459a2e8-3d2c-4e44-b132-fee5156c7bb0");
    private static readonly Guid Aspect = new("c6376a1e-8d0a-4027-be45-6d9a0ad39bb6"), Interlace = new("e2724bb8-e676-4806-b4b2-a8d6efb44ccd");
    private static readonly Guid Stride = new("644b4e48-1e02-4516-b0eb-c01ca9d49ac6"), LowLatency = new("9c27891a-ed7a-40e1-88e8-b22727a024ee");
    private const int NeedInput = unchecked((int)0xc00d6d72), StreamChange = unchecked((int)0xc00d6d61), NotAccepting = unchecked((int)0xc00d36b0);
    private IntPtr decoder, converter;
    private bool outputReady;
    private int width, height;
    private int codedWidth, codedHeight;
    private int cropX, cropY;
    private readonly int fps;
    public H264Decoder(int width, int height, int fps)
    {
        if (width <= 0 || height <= 0 || width > 1920 || height > 1080 || fps is < 1 or > 60) throw new InvalidDataException("Perfil H.264 fuera de límites.");
        this.width = width; this.height = height; this.fps = fps;
        codedWidth = width; codedHeight = height;
        using var apartment = new Apartment();
        _ = Startup.Value;
        try
        {
            decoder = CreateTransform(new Guid("62ce7e72-4c71-4d20-b15d-452831a87d9d"));
            var type = MediaType(Avc, width, height, fps);
            try { Check(Call<SetType>(decoder, 15)(decoder, 0, type, 0)); }
            finally { Release(type); }
            if (Call<GetPointer>(decoder, 8)(decoder, out var attrs) >= 0)
            {
                try { Call<SetU32>(attrs, 21)(attrs, LowLatency, 1); } finally { Release(attrs); }
            }
            ConfigureOutput();
            Begin(decoder);
        }
        catch { Dispose(); throw; }
    }

    public byte[]? Decode(byte[] bytes, long ptsUs, bool renderOutput = true)
    {
        using var apartment = new Apartment();
        var sample = Sample(bytes.Length, bytes);
        try
        {
            Check(Call<SetI64>(sample, 36)(sample, ptsUs * 10));
            Check(Call<SetI64>(sample, 38)(sample, 10_000_000L / fps));
            var result = Call<Input>(decoder, 24)(decoder, 0, sample, 0);
            byte[]? latest = null;
            if (result == NotAccepting) { latest = Drain(renderOutput); result = Call<Input>(decoder, 24)(decoder, 0, sample, 0); }
            Check(result);
            return Drain(renderOutput) ?? latest;
        }
        finally { Release(sample); }
    }

    private byte[]? Drain(bool renderOutput)
    {
        byte[]? latest = null;
        for (var attempt = 0; attempt < 16; attempt++)
        {
            if (!outputReady && !ConfigureOutput()) return latest;
            var result = GetSample(decoder, codedWidth * codedHeight * 2, out var sample);
            if (result == NeedInput) return latest;
            if (result == StreamChange) { outputReady = false; continue; }
            Check(result);
            try
            {
                // Preserve AVC reference decoding, but avoid RGB conversion/allocation for obsolete output.
                if (!renderOutput) continue;
                Check(Call<Input>(converter, 24)(converter, 0, sample, 0));
                var converted = GetSample(converter, codedWidth * codedHeight * 4, out var rgb);
                if (converted == NeedInput) continue;
                Check(converted);
                try
                {
                    Check(Call<GetPointer>(rgb, 41)(rgb, out var buffer));
                    try
                    {
                        Check(Call<LockBuffer>(buffer, 3)(buffer, out var data, out _, out var length));
                        try
                        {
                            if (length < codedWidth * codedHeight * 4) throw new InvalidDataException("Frame RGB incompleto.");
                            latest = new byte[width * height * 4];
                            for (var row = 0; row < height; row++)
                                Marshal.Copy(IntPtr.Add(data, ((row + cropY) * codedWidth + cropX) * 4), latest, row * width * 4, width * 4);
                        }
                        finally { Check(Call<NoArgs>(buffer, 4)(buffer)); }
                    }
                    finally { Release(buffer); }
                }
                finally { Release(rgb); }
            }
            finally { Release(sample); }
        }
        return latest;
    }

    private bool ConfigureOutput()
    {
        for (uint index = 0; index < 64; index++)
        {
            var hr = Call<GetAvailable>(decoder, 14)(decoder, 0, index, out var type);
            if (hr < 0) return false; // Decoder may need SPS/PPS before exposing its output.
            try
            {
                Check(Call<GetGuid>(type, 10)(type, Subtype, out var subtype));
                if (subtype != Nv12) continue;
                Check(Call<SetType>(decoder, 16)(decoder, 0, type, 0));
                if (Call<GetU64>(type, 8)(type, FrameSize, out var size) >= 0)
                {
                    var actualWidth = (int)(size >> 32); var actualHeight = (int)(size & 0xffffffff);
                    if (actualWidth < width || actualHeight < height || actualWidth > (width + 15) / 16 * 16 || actualHeight > (height + 15) / 16 * 16)
                        throw new InvalidDataException("El bitstream cambió de resolución. Reinicia con el perfil correcto.");
                    codedWidth = actualWidth; codedHeight = actualHeight;
                    ReadAperture(type);
                }
                Release(converter); converter = CreateTransform(new Guid("98230571-0087-4204-b020-3282538e57d3"));
                Check(Call<SetType>(converter, 15)(converter, 0, type, 0));
                var rgbType = MediaType(Rgb32, codedWidth, codedHeight, fps);
                try
                {
                    Check(Call<SetU32>(rgbType, 21)(rgbType, Stride, (uint)(codedWidth * 4)));
                    Check(Call<SetType>(converter, 16)(converter, 0, rgbType, 0));
                }
                finally { Release(rgbType); }
                Begin(converter);
                outputReady = true;
                return true;
            }
            finally { Release(type); }
        }
        throw new InvalidDataException("El decodificador no ofrece salida NV12.");
    }
    private void ReadAperture(IntPtr type)
    {
        cropX = cropY = 0;
        foreach (var key in new[] { new Guid("d7388766-18fe-48c6-a177-ee894867c8c4"), new Guid("66758743-7e5f-400d-980a-aa8596c85696") })
        {
            var memory = Marshal.AllocHGlobal(16);
            try
            {
                if (Call<GetBlob>(type, 15)(type, key, memory, 16, out var length) < 0 || length != 16) continue;
                var x = Marshal.ReadInt16(memory, 2); var y = Marshal.ReadInt16(memory, 6);
                var w = Marshal.ReadInt32(memory, 8); var h = Marshal.ReadInt32(memory, 12);
                if (x < 0 || y < 0 || x + width > codedWidth || y + height > codedHeight || w != width || h != height)
                    throw new InvalidDataException("Área visible del bitstream incompatible con el perfil seleccionado.");
                cropX = x; cropY = y;
                return;
            }
            finally { Marshal.FreeHGlobal(memory); }
        }
    }
    private static IntPtr MediaType(Guid subtype, int width, int height, int fps)
    {
        Check(MFCreateMediaType(out var type));
        try
        {
            Check(Call<SetGuid>(type, 24)(type, Major, Video));
            Check(Call<SetGuid>(type, 24)(type, Subtype, subtype));
            Check(Call<SetU64>(type, 22)(type, FrameSize, ((ulong)width << 32) | (uint)height));
            Check(Call<SetU64>(type, 22)(type, FrameRate, ((ulong)fps << 32) | 1));
            Check(Call<SetU64>(type, 22)(type, Aspect, (1UL << 32) | 1));
            Check(Call<SetU32>(type, 21)(type, Interlace, 2));
            return type;
        }
        catch { Release(type); throw; }
    }
    private static IntPtr CreateTransform(Guid clsid)
    {
        Check(CoCreateInstance(clsid, IntPtr.Zero, 1, TransformId, out var value));
        return value;
    }
    private static void Begin(IntPtr transform)
    {
        Check(Call<Message>(transform, 23)(transform, 0x10000000, IntPtr.Zero));
        Check(Call<Message>(transform, 23)(transform, 0x10000003, IntPtr.Zero));
    }
    private static IntPtr Sample(int capacity, byte[]? bytes = null)
    {
        Check(MFCreateSample(out var sample));
        try
        {
            Check(MFCreateMemoryBuffer((uint)capacity, out var buffer));
            try
            {
                if (bytes != null)
                {
                    Check(Call<LockBuffer>(buffer, 3)(buffer, out var data, out _, out _));
                    try { Marshal.Copy(bytes, 0, data, bytes.Length); }
                    finally { Call<NoArgs>(buffer, 4)(buffer); }
                    Check(Call<SetLength>(buffer, 6)(buffer, (uint)bytes.Length));
                }
                Check(Call<AddBuffer>(sample, 42)(sample, buffer));
            }
            finally { Release(buffer); }
            return sample;
        }
        catch { Release(sample); throw; }
    }
    private static int GetSample(IntPtr transform, int fallbackSize, out IntPtr sample)
    {
        Check(Call<GetOutputInfo>(transform, 7)(transform, 0, out var info));
        var allocated = (info.Flags & 0x100) == 0 ? Sample((int)Math.Max(info.Size, (uint)fallbackSize)) : IntPtr.Zero;
        var output = new OutputBuffer { Sample = allocated };
        var hr = Call<Output>(transform, 25)(transform, 0, 1, ref output, out _);
        Release(output.Events);
        if (hr < 0) { Release(allocated); if (output.Sample != allocated) Release(output.Sample); sample = IntPtr.Zero; return hr; }
        if (allocated != IntPtr.Zero && output.Sample != allocated) Release(allocated);
        sample = output.Sample;
        if (sample == IntPtr.Zero) throw new InvalidDataException("Decodificador sin muestra de salida.");
        return hr;
    }
    public void Dispose()
    {
        using var apartment = new Apartment();
        Release(converter); converter = IntPtr.Zero;
        Release(decoder); decoder = IntPtr.Zero;
    }
    private static T Call<T>(IntPtr value, int slot) where T : Delegate =>
        Marshal.GetDelegateForFunctionPointer<T>(Marshal.ReadIntPtr(Marshal.ReadIntPtr(value), slot * IntPtr.Size));
    private static void Release(IntPtr p) { if (p != IntPtr.Zero) Marshal.Release(p); }
    private static void Check(int hr) { if (hr < 0) Marshal.ThrowExceptionForHR(hr); }
    private sealed class Apartment : IDisposable
    {
        private readonly int result = CoInitializeEx(IntPtr.Zero, 0);
        public void Dispose() { if (result >= 0) CoUninitialize(); }
    }
    [StructLayout(LayoutKind.Sequential)] private struct OutputInfo { public uint Flags, Size, Alignment; }
    [StructLayout(LayoutKind.Sequential)] private struct OutputBuffer { public uint StreamId; public IntPtr Sample; public uint Status; public IntPtr Events; }
    [UnmanagedFunctionPointer(CallingConvention.StdCall)] private delegate int SetGuid(IntPtr p, [In] in Guid key, [In] in Guid value);
    [UnmanagedFunctionPointer(CallingConvention.StdCall)] private delegate int GetGuid(IntPtr p, [In] in Guid key, out Guid value);
    [UnmanagedFunctionPointer(CallingConvention.StdCall)] private delegate int SetU32(IntPtr p, [In] in Guid key, uint value);
    [UnmanagedFunctionPointer(CallingConvention.StdCall)] private delegate int SetU64(IntPtr p, [In] in Guid key, ulong value);
    [UnmanagedFunctionPointer(CallingConvention.StdCall)] private delegate int GetU64(IntPtr p, [In] in Guid key, out ulong value);
    [UnmanagedFunctionPointer(CallingConvention.StdCall)] private delegate int SetType(IntPtr p, uint stream, IntPtr type, uint flags);
    [UnmanagedFunctionPointer(CallingConvention.StdCall)] private delegate int GetPointer(IntPtr p, out IntPtr value);
    [UnmanagedFunctionPointer(CallingConvention.StdCall)] private delegate int GetAvailable(IntPtr p, uint stream, uint index, out IntPtr type);
    [UnmanagedFunctionPointer(CallingConvention.StdCall)] private delegate int GetOutputInfo(IntPtr p, uint stream, out OutputInfo info);
    [UnmanagedFunctionPointer(CallingConvention.StdCall)] private delegate int Input(IntPtr p, uint stream, IntPtr sample, uint flags);
    [UnmanagedFunctionPointer(CallingConvention.StdCall)] private delegate int Output(IntPtr p, uint flags, uint count, ref OutputBuffer data, out uint status);
    [UnmanagedFunctionPointer(CallingConvention.StdCall)] private delegate int Message(IntPtr p, uint message, IntPtr parameter);
    [UnmanagedFunctionPointer(CallingConvention.StdCall)] private delegate int SetI64(IntPtr p, long value);
    [UnmanagedFunctionPointer(CallingConvention.StdCall)] private delegate int LockBuffer(IntPtr p, out IntPtr data, out uint max, out uint length);
    [UnmanagedFunctionPointer(CallingConvention.StdCall)] private delegate int NoArgs(IntPtr p);
    [UnmanagedFunctionPointer(CallingConvention.StdCall)] private delegate int SetLength(IntPtr p, uint length);
    [UnmanagedFunctionPointer(CallingConvention.StdCall)] private delegate int AddBuffer(IntPtr p, IntPtr buffer);
    [UnmanagedFunctionPointer(CallingConvention.StdCall)] private delegate int GetBlob(IntPtr p, [In] in Guid key, IntPtr buffer, uint capacity, out uint length);
    [DllImport("mfplat.dll", ExactSpelling = true)] private static extern int MFStartup(uint version, uint flags);
    [DllImport("mfplat.dll", ExactSpelling = true)] private static extern int MFCreateMediaType(out IntPtr type);
    [DllImport("mfplat.dll", ExactSpelling = true)] private static extern int MFCreateSample(out IntPtr sample);
    [DllImport("mfplat.dll", ExactSpelling = true)] private static extern int MFCreateMemoryBuffer(uint size, out IntPtr buffer);
    [DllImport("ole32.dll", ExactSpelling = true)] private static extern int CoCreateInstance([In] in Guid clsid, IntPtr outer, uint context, [In] in Guid iid, out IntPtr value);
    [DllImport("ole32.dll", ExactSpelling = true)] private static extern int CoInitializeEx(IntPtr reserved, uint model);
    [DllImport("ole32.dll", ExactSpelling = true)] private static extern void CoUninitialize();
}

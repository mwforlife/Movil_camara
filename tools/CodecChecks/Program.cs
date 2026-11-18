using System.Reflection;
using System.Runtime.InteropServices;
using MovilCamara.Services;

var width = args.Contains("--1080p60") || args.Contains("--1080p30") ? 1920 : 640;
var height = args.Contains("--1080p60") || args.Contains("--1080p30") ? 1080 : 360;
var fps = args.Contains("--1080p60") ? 60 : 30;
var frameDuration = 10_000_000L / fps;
var type = typeof(H264Decoder);
const BindingFlags privateStatic = BindingFlags.NonPublic | BindingFlags.Static;
object? Private(string name, params object?[] args) => type.GetMethod(name, privateStatic)!.Invoke(null, args);
void Release(IntPtr value) { if (value != IntPtr.Zero) Marshal.Release(value); }
object?[] Call(string name, IntPtr value, int slot, params object?[] rest)
{
    var function = (Delegate)type.GetMethod("Call", privateStatic)!.MakeGenericMethod(type.GetNestedType(name, BindingFlags.NonPublic)!)
        .Invoke(null, new object[] { value, slot })!;
    var args = new object?[rest.Length + 1]; args[0] = value; Array.Copy(rest, 0, args, 1, rest.Length);
    var hr = (int)function.DynamicInvoke(args)!;
    if (hr < 0) Marshal.ThrowExceptionForHR(hr);
    return args;
}
using var apartment = (IDisposable)Activator.CreateInstance(type.GetNestedType("Apartment", BindingFlags.NonPublic)!, true)!;
using var decoder = new H264Decoder(width, height, fps);
var encoder = (IntPtr)Private("CreateTransform", new Guid("6ca50344-051a-4ded-9779-a43305165e35"))!;
var output = (IntPtr)Private("MediaType", new Guid("34363248-0000-0010-8000-00aa00389b71"), width, height, fps)!;
var input = (IntPtr)Private("MediaType", new Guid("3231564e-0000-0010-8000-00aa00389b71"), width, height, fps)!;
try
{
    Call("SetU32", output, 21, new Guid("20332624-fb0d-4d9e-bd0d-cbf6786c102e"), (uint)1_000_000);
    Call("SetU32", output, 21, new Guid("ad76a80b-2d5c-4e0b-b375-64e520137036"), (uint)66);
    Call("SetType", encoder, 16, (uint)0, output, (uint)0);
    Call("SetType", encoder, 15, (uint)0, input, (uint)0);
    Private("Begin", encoder);
    var raw = new byte[width * height * 3 / 2];
    Array.Fill(raw, (byte)100, 0, width * height);
    Array.Fill(raw, (byte)128, width * height, width * height / 2);
    byte[]? pixels = null;
    var encodedCount = 0;
    var renderedAfterSkip = false;
    for (int i = 0; i < 40; i++)
    {
        var sample = (IntPtr)Private("Sample", raw.Length, raw)!;
        try
        {
            Call("SetI64", sample, 36, (long)i * frameDuration);
            Call("SetI64", sample, 38, frameDuration);
            Call("Input", encoder, 24, (uint)0, sample, (uint)0);
        }
        finally { Release(sample); }
        for (int j = 0; j < 8; j++)
        {
            object?[] arguments = { encoder, raw.Length * 4, IntPtr.Zero };
            var hr = (int)type.GetMethod("GetSample", privateStatic)!.Invoke(null, arguments)!;
            if (hr == unchecked((int)0xc00d6d72)) break;
            if (hr < 0) Marshal.ThrowExceptionForHR(hr);
            var compressed = (IntPtr)arguments[2]!;
            try
            {
                var buffer = (IntPtr)Call("GetPointer", compressed, 41, IntPtr.Zero)[1]!;
                try
                {
                    var locked = Call("LockBuffer", buffer, 3, IntPtr.Zero, (uint)0, (uint)0);
                    byte[] bytes;
                    try
                    {
                        bytes = new byte[(uint)locked[3]!];
                        Marshal.Copy((IntPtr)locked[1]!, bytes, 0, bytes.Length);
                    }
                    finally { Call("NoArgs", buffer, 4); }
                    if (encodedCount == 0) Console.WriteLine("First H.264 access unit: " + bytes.Length + " bytes");
                    var render = !args.Contains("--catch-up") || encodedCount % 3 == 2;
                    var decoded = decoder.Decode(bytes, (long)encodedCount * 1_000_000 / fps, render);
                    if (!render && decoded != null) throw new Exception("Catch-up allocated visible RGB output");
                    if (render && encodedCount >= 2 && decoded != null) renderedAfterSkip = true;
                    pixels = decoded ?? pixels;
                    encodedCount++;
                }
                finally { Release(buffer); }
            }
            finally { Release(compressed); }
        }
    }
    if (pixels == null) throw new Exception("Decoder produced no pixels after " + encodedCount + " encoded frames.");
    if (args.Contains("--catch-up") && !renderedAfterSkip) throw new Exception("No visible output after reference-only decoding");
    if (pixels.Length != width * height * 4 || Math.Abs(pixels[0] - pixels[1]) > 5 || Math.Abs(pixels[1] - pixels[2]) > 5 || pixels[0] < 40)
        throw new Exception("Decoded RGB output is not the expected gray test frame.");
    Console.WriteLine("PASS: real H.264 encode -> decode -> RGB32, " + pixels.Length + " bytes, gray pixel=" + pixels[0]);
}
finally { Release(encoder); Release(input); Release(output); }

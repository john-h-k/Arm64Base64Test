using System;
using System.Runtime.InteropServices;
using System.Diagnostics;
using BenchmarkDotNet.Attributes;

namespace Arm64Base64Test
{
    class Program
    {
        static unsafe void Main(string[] args)
        {
            Console.WriteLine(BitConverter.IsLittleEndian);
            var bytes = new byte[] { 83, 65, 66, 108, 65, 71, 119, 65, 98, 65, 66, 118, 65, 67, 65, 65, 86, 119, 66, 118, 65, 72, 73, 65, 98, 65, 66, 107, 65, 67, 69, 65, 73, 65, 66, 73, 65, 71, 85, 65, 98, 65, 66, 115, 65, 71, 56, 65, 73, 65, 66, 88, 65, 71, 56, 65, 99, 103, 66, 115, 65, 71, 81, 65, 73, 81, 65, 103, 65, 69, 103, 65, 90, 81, 66, 115, 65, 71, 119, 65, 98, 119, 65, 103, 65, 70, 99, 65, 98, 119, 66, 121, 65, 71, 119, 65, 90, 65, 65, 104, 65, 67, 65, 65, 83, 65, 66, 108, 65, 71, 119, 65, 98, 65, 66, 118, 65, 67, 65, 65, 86, 119, 66, 118, 65, 72, 73, 65, 98, 65, 66, 107, 65, 67, 69, 65, 73, 65, 66, 73, 65, 71, 85, 65, 98, 65, 66, 115, 65, 71, 56, 65, 73, 65, 66, 88, 65, 71, 56, 65, 99, 103, 66, 115, 65, 71, 81, 65, 73, 81, 65, 103, 65, 69, 103, 65, 90, 81, 66, 115, 65, 71, 119, 65, 98, 119, 65, 103, 65, 70, 99, 65, 98, 119, 66, 121, 65, 71, 119, 65, 90, 65, 65, 104, 65, 67, 65, 65, 83, 65, 66, 108, 65, 71, 119, 65, 98, 65, 66, 118, 65, 67, 65, 65, 86, 119, 66, 118, 65, 72, 73, 65, 98, 65, 66, 107, 65, 67, 69, };
            var result = new byte[1024];

            var op = Base64.DecodeFromUtf8_VectorLookup(bytes, result, out _, out var written);
            Console.WriteLine(op);

            Console.WriteLine(MemoryMarshal.Cast<byte, char>(result.AsSpan(0, written)).ToString());

            // fixed (byte* pSrc = bytes)
            // fixed (byte* pEnd = result)
            // {
            //     var src = pSrc;
            //     var end = pEnd;
            //     Base64.AvdSimd64Decode_VectorLookup(ref src, ref end, pSrc + 64, 128, 128, pSrc, pEnd);

            //     Console.WriteLine(MemoryMarshal.Cast<byte, char>(result.AsSpan(0, written)).ToString());
            // }
        }
    }

}

public class Base64Benchmark
{
    private byte[] _in512;
    private byte[] _out512;

    [GlobalSetup]
    public void Setup()
    {
        var rand = new Random();
        rand.NextBytes(_in512);
    }

    [Benchmark]
    public void Normal_512Byte()
    {
        Base64.DecodeFromUtf8(_in512, _out512, out _, out _);
    }

    [Benchmark]
    public void VectorLookup_512Byte()
    {
        Base64.DecodeFromUtf8_VectorLookup(_in512, _out512, out _, out _);
    }
}

public enum ExceptionArgument { length }
public class ThrowHelper { public static void ThrowArgumentOutOfRangeException(ExceptionArgument arg) => throw new ArgumentOutOfRangeException(); }

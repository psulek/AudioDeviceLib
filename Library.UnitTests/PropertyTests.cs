/*
  Copyright (c) 2026 Peter Šulek
  MIT License
*/

using System;
using System.Runtime.InteropServices;
using AudioDeviceLib.CoreAudioApi;
using AudioDeviceLib.CoreAudioApi.Interfaces;
using NUnit.Framework;

namespace AudioDeviceLib.UnitTests;

[TestFixture]
public class PropertyTests
{
    // Construct the native layout without exposing production-only mutation helpers.
    private static PropVariant CreateVariant(VarEnum type, Action<IntPtr>? writePayload = null)
    {
        int size = Marshal.SizeOf<PropVariant>();
        IntPtr buffer = Marshal.AllocCoTaskMem(size);
        try
        {
            Marshal.Copy(new byte[size], 0, buffer, size);
            Marshal.WriteInt16(buffer, (short)type);
            writePayload?.Invoke(IntPtr.Add(buffer, 8));
            return Marshal.PtrToStructure<PropVariant>(buffer);
        }
        finally
        {
            Marshal.FreeCoTaskMem(buffer);
        }
    }

    [TestCase(false)]
    [TestCase(true)]
    public void Conversion_ClearsNativePayload_AndPreservesSnapshot(bool blob)
    {
        byte[] bytes = { 0, 17, 128, 255 };
        PropVariant variant = CreateVariant(blob ? VarEnum.VT_BLOB : VarEnum.VT_LPWSTR, payload =>
        {
            if (blob)
            {
                IntPtr data = Marshal.AllocCoTaskMem(bytes.Length);
                Marshal.Copy(bytes, 0, data, bytes.Length);
                Marshal.WriteInt32(payload, bytes.Length);
                Marshal.WriteIntPtr(payload, IntPtr.Size == 8 ? 8 : 4, data);
            }
            else
            {
                Marshal.WriteIntPtr(payload, Marshal.StringToCoTaskMemUni("Snapshot"));
            }
        });
        try
        {
            PropertyValue snapshot = PropVariant.ToPropertyValue(ref variant);
            Assert.That(variant.VarType, Is.EqualTo(VarEnum.VT_EMPTY));
            Assert.That(variant.Value, Is.Null);
            Assert.That(snapshot.IsEmpty, Is.False);
            Assert.That(snapshot.VarType, Is.EqualTo(blob ? VarEnum.VT_BLOB : VarEnum.VT_LPWSTR));
            Assert.That(snapshot.Value, Is.EqualTo(blob ? (object)bytes : "Snapshot"));
        }
        finally
        {
            // Also frees the payload when testing a mutation that omits production cleanup.
            variant.Clear();
        }
    }

    [Test]
    public void Conversion_ClearsVariant_WhenConversionThrows()
    {
        PropVariant variant = CreateVariant(VarEnum.VT_DATE,
            payload => Marshal.WriteInt64(payload, BitConverter.DoubleToInt64Bits(double.MaxValue)));
        try
        {
            Assert.Throws<ArgumentException>(() => PropVariant.ToPropertyValue(ref variant));
            Assert.That(variant.VarType, Is.EqualTo(VarEnum.VT_EMPTY));
        }
        finally
        {
            variant.Clear();
        }
    }

    [TestCase(VarEnum.VT_EMPTY)]
    [TestCase(VarEnum.VT_NULL)]
    public void PresentEmptyKey_IsContained_AndReturnsEntry(VarEnum type)
    {
        var native = new StubStore { Type = type };
        var store = new PropertyStore(native);
        Assert.That(store.Contains(native.Key), Is.True);
        Assert.That(native.Reads, Is.Zero, "Contains must only enumerate keys");
        PropertyStoreProperty? entry = store[native.Key];
        Assert.That(entry, Is.Not.Null);
        entry = AudioFixture.RequireValue(entry);
        Assert.That(entry.Key, Is.EqualTo(native.Key));
        Assert.That(entry.Value.VarType, Is.EqualTo(type));
        Assert.That(entry.Value.IsEmpty, Is.True);
        Assert.That(store.TryGetValue(native.Key, out var value), Is.False);
        Assert.That(value.VarType, Is.EqualTo(type));
    }

    [Test]
    public void MissingKey_ReturnsNull_WithOneDirectValueRead()
    {
        var native = new StubStore();
        var store = new PropertyStore(native);
        var missing = new PropertyKey { FormatId = native.Key.FormatId, PropertyId = native.Key.PropertyId + 1 };
        Assert.That(store.Contains(missing), Is.False);
        Assert.That(store[missing], Is.Null);
        Assert.That(native.Reads, Is.EqualTo(1));
    }

    [Test]
    public void FailedRead_IndexerThrows_ButTryGetValueReturnsEmpty()
    {
        var native = new StubStore { ReadResult = unchecked((int)0x80004005) };
        var store = new PropertyStore(native);
        Assert.That(store.Contains(native.Key), Is.True);
        var error = Assert.Throws<COMException>(() => { var entry = store[native.Key]; });
        error = AudioFixture.RequireValue(error);
        Assert.That(error.HResult, Is.EqualTo(native.ReadResult));
        Assert.That(store.TryGetValue(native.Key, out var value), Is.False);
        Assert.That(value, Is.Not.Null);
        Assert.That(value.IsEmpty, Is.True);
        Assert.That(value.Value, Is.Null);
    }

    // A property that is present but carries a variant type this library does not convert has a
    // null Value for a reason other than emptiness. Reporting success there would hand the caller
    // nothing while claiming the read worked.
    [TestCase(VarEnum.VT_VECTOR | VarEnum.VT_LPWSTR)]
    [TestCase(VarEnum.VT_ARRAY | VarEnum.VT_I4)]
    [TestCase(VarEnum.VT_STREAM)]
    [TestCase(VarEnum.VT_UNKNOWN)]
    public void UnsupportedType_IsReportedAsUnsupported_NotAsEmpty(VarEnum type)
    {
        var native = new StubStore { Type = type };
        var store = new PropertyStore(native);

        Assert.That(store.TryGetValue(native.Key, out var value), Is.False,
            "an unconvertible variant type must not be reported as a successful read");
        Assert.That(value.IsSupported, Is.False);
        Assert.That(value.IsEmpty, Is.False, "the property is present; it is the type that is unhandled");
        Assert.That(value.VarType, Is.EqualTo(type), "the caller still needs to know what was found");
        Assert.That(value.Value, Is.Null);
    }

    [TestCase(VarEnum.VT_LPWSTR)]
    [TestCase(VarEnum.VT_CLSID)]
    [TestCase(VarEnum.VT_I4)]
    public void SupportedType_IsReportedAsSupported(VarEnum type)
    {
        var native = new StubStore { Type = type };
        var store = new PropertyStore(native);

        store.TryGetValue(native.Key, out var value);

        Assert.That(value.IsSupported, Is.True);
    }

    // The payload is a pointer to the GUID, not the GUID itself, so this reads through it rather
    // than off the variant's inline union.
    [Test]
    public void ClsidVariant_ReadsTheGuidBehindThePointer()
    {
        var expected = Guid.NewGuid();
        IntPtr payload = Marshal.AllocCoTaskMem(16);
        try
        {
            Marshal.Copy(expected.ToByteArray(), 0, payload, 16);
            PropVariant variant = CreateVariant(VarEnum.VT_CLSID,
                target => Marshal.WriteIntPtr(target, payload));

            Assert.That(variant.Value, Is.EqualTo(expected));
        }
        finally
        {
            Marshal.FreeCoTaskMem(payload);
        }
    }

    private sealed class StubStore : IPropertyStoreCOM
    {
        internal readonly PropertyKey Key = new PropertyKey { FormatId = Guid.NewGuid(), PropertyId = 1 };
        internal VarEnum Type;
        internal int ReadResult;
        internal int Reads;

        public int GetCount(out int count) { count = 1; return 0; }
        public int GetAt(int index, out PropertyKey key) { key = Key; return 0; }
        public int GetValue(ref PropertyKey key, out PropVariant value)
        {
            Reads++;
            value = CreateVariant(Type);
            return ReadResult;
        }
        public int SetValue(ref PropertyKey key, ref PropVariant value) => throw new NotSupportedException();
        public int Commit() => throw new NotSupportedException();
    }
}

/*
  LICENSE
  -------
  Copyright (C) 2007-2010 Ray Molenkamp

  This source code is provided 'as-is', without any express or implied
  warranty.  In no event will the authors be held liable for any damages
  arising from the use of this source code or the software it produces.

  Permission is granted to anyone to use this source code for any purpose,
  including commercial applications, and to alter it and redistribute it
  freely, subject to the following restrictions:

  1. The origin of this source code must not be misrepresented; you must not
     claim that you wrote the original source code.  If you use this source code
     in a product, an acknowledgment in the product documentation would be
     appreciated but is not required.
  2. Altered source versions must be plainly marked as such, and must not be
     misrepresented as being the original source code.
  3. This notice may not be removed or altered from any source distribution.
*/

/*
  MODIFICATIONS
  -------------
  This file is an ALTERED version of the original source by Ray Molenkamp and must not be
  misrepresented as being the original source code. Altered by Peter Šulek for AudioDeviceLib
  (https://github.com/psulek/AudioDeviceLib), starting from the copy bundled in
  AudioDeviceCmdlets (https://github.com/frgnca/AudioDeviceCmdlets, MIT).

  Changes from the original:
  - Namespace changed to `AudioDeviceLib.CoreAudioApi` (file-scoped); unused `using`
    directives removed.
  - Reformatted to the project's C# style (full braces, modern C# syntax) and annotated with XML
    documentation comments.
  - Added the `VarType` and `IsEmpty` accessors, exposing the previously private `vt` tag.
  - `Value` now returns `null` for `VT_EMPTY`/`VT_NULL` instead of falling through to the
    "FIXME Type = ..." diagnostic string.
  - `boolVal` and `date` retyped from `bool`/`DateTime` to `short`/`double`, the real widths of
    `VARIANT_BOOL` and `DATE`. That also makes every field blittable, which is what lets the struct
    reach `PropVariantClear` without the marshaller reinterpreting the union.
  - Added `Clear()`. The original never released the memory a returned PROPVARIANT owns, so every
    `VT_LPWSTR` or `VT_BLOB` read leaked its payload.
  - `Value` now handles `VT_BOOL` and `VT_DATE`, which previously fell through to the
    "FIXME Type = ..." diagnostic string despite the union carrying both.
  - `VT_I1` read the unsigned `bVal` instead of the signed `cVal`, and `VT_INT` read the 2-byte
    `iVal` instead of the 4-byte `lVal`, truncating every value above 16 bits. Both corrected.
  - An unconverted variant type now yields `null` rather than the "FIXME Type = ..." string, which
    a caller could not distinguish from a real string value.
  - `Value` now reads every member of the union that carries a value: `VT_UI1`, `VT_UI2`, `VT_UI8`,
    `VT_UINT`, `VT_R4`, `VT_R8`, `VT_ERROR` and `VT_FILETIME` were declared but never reachable.
    The three `wReserved` fields remain unread: they are PROPVARIANT padding, not values.
  - Cases reordered by width and kind so a missing one is visible at a glance.
*/

using System;
using System.Runtime.InteropServices;
using AudioDeviceLib.CoreAudioApi.Interfaces;

namespace AudioDeviceLib.CoreAudioApi;

/// <summary>Managed layout of the native <c>PROPVARIANT</c> used to read values from a property store.</summary>
[StructLayout(LayoutKind.Explicit)]
public struct PropVariant
{
    [FieldOffset(0)] short vt;
    [FieldOffset(2)] short wReserved1;
    [FieldOffset(4)] short wReserved2;
    [FieldOffset(6)] short wReserved3;
    [FieldOffset(8)] sbyte cVal;
    [FieldOffset(8)] byte bVal;
    [FieldOffset(8)] short iVal;
    [FieldOffset(8)] ushort uiVal;
    [FieldOffset(8)] int lVal;
    [FieldOffset(8)] uint ulVal;
    [FieldOffset(8)] long hVal;
    [FieldOffset(8)] ulong uhVal;
    [FieldOffset(8)] float fltVal;
    [FieldOffset(8)] double dblVal;
    [FieldOffset(8)] Blob blobVal;
    [FieldOffset(8)] double date;
    [FieldOffset(8)] short boolVal;
    [FieldOffset(8)] int scode;
    [FieldOffset(8)] System.Runtime.InteropServices.ComTypes.FILETIME filetime;
    [FieldOffset(8)] IntPtr everything_else;

    //I'm sure there is a more efficient way to do this but this works ..for now..
    internal byte[] GetBlob()
    {
        byte[] Result = new byte[blobVal.Length];
        for (int i = 0; i < blobVal.Length; i++)
        {
            Result[i] = Marshal.ReadByte((IntPtr)((long)(blobVal.Data) + i));
        }

        return Result;
    }

    // A PROPVARIANT FILETIME is UTC. Returning DateTime.FromFileTime instead would shift the value
    // into the machine's local zone and hand back a Kind of Local, which is a different instant on
    // any machine that is not on UTC. The high word is cast through uint first so a set top bit
    // does not sign-extend into the upper 32 bits of the tick count.
    private static DateTime FileTimeToUtc(System.Runtime.InteropServices.ComTypes.FILETIME ft)
    {
        long ticks = ((long)(uint)ft.dwHighDateTime << 32) | (uint)ft.dwLowDateTime;
        return DateTime.FromFileTimeUtc(ticks);
    }

    // A PROPVARIANT handed back by IPropertyStore::GetValue belongs to the caller: for VT_LPWSTR and
    // VT_BLOB the payload is allocated by the store and is leaked unless it is released. This is
    // declared `ref` rather than as a raw pointer, which only works because every field above is
    // blittable - a `bool` or `DateTime` field would make the marshaller try to convert the union
    // and throw before the call is made.
    [DllImport("ole32.dll")]
    private static extern int PropVariantClear(ref PropVariant pvar);

    /// <summary>Releases the native memory this value owns and resets it to an empty variant.</summary>
    public void Clear()
    {
        PropVariantClear(ref this);
    }

    /// <summary>Gets the variant type tag of this value.</summary>
    public VarEnum VarType
    {
        get { return (VarEnum)vt; }
    }

    /// <summary>Gets whether this variant carries no value.</summary>
    public bool IsEmpty
    {
        get { return vt == (short)VarEnum.VT_EMPTY || vt == (short)VarEnum.VT_NULL; }
    }

    /// <summary>Gets the variant value converted to a managed object based on its variant type.</summary>
    /// <returns>
    /// The value as a managed type for supported variant types (integers, floating point, boolean,
    /// date, file time, error code, string, blob), or <c>null</c> when the variant is empty or
    /// carries a type this class does not convert. <see cref="VarType"/> distinguishes those two
    /// cases. File times are returned as UTC; OLE dates carry no zone and are returned unspecified.
    /// </returns>
    public object Value
    {
        get
        {
            VarEnum ve = (VarEnum)vt;
            switch (ve)
            {
                // An absent property comes back from IPropertyStore::GetValue as S_OK + VT_EMPTY,
                // so an empty variant is an ordinary result, not a failure. Listed explicitly even
                // though the fallback below also returns null, so the intent survives a later edit.
                case VarEnum.VT_EMPTY:
                case VarEnum.VT_NULL:
                    return null;
                case VarEnum.VT_I1:
                    return cVal;
                case VarEnum.VT_I2:
                    return iVal;
                case VarEnum.VT_I4:
                case VarEnum.VT_INT:
                    return lVal;
                case VarEnum.VT_I8:
                    return hVal;

                case VarEnum.VT_UI1:
                    return bVal;
                case VarEnum.VT_UI2:
                    return uiVal;
                case VarEnum.VT_UI4:
                case VarEnum.VT_UINT:
                    return ulVal;
                case VarEnum.VT_UI8:
                    return uhVal;

                case VarEnum.VT_R4:
                    return fltVal;
                case VarEnum.VT_R8:
                    return dblVal;

                // VARIANT_BOOL is a 2-byte tri-state where true is -1 (0xFFFF) and false is 0, so
                // this is a comparison against zero rather than a cast.
                case VarEnum.VT_BOOL:
                    return boolVal != 0;
                // DATE is an OLE Automation date: a double counting days since 1899-12-30.
                case VarEnum.VT_DATE:
                    return DateTime.FromOADate(date);
                case VarEnum.VT_FILETIME:
                    return FileTimeToUtc(filetime);
                // An SCODE is an HRESULT, so it is handed back as the raw 32-bit status rather than
                // being turned into an exception - this is a value being read, not a call failing.
                case VarEnum.VT_ERROR:
                    return scode;

                case VarEnum.VT_LPWSTR:
                    return Marshal.PtrToStringUni(everything_else);
                case VarEnum.VT_BLOB:
                    return GetBlob();
            }

            // Not a diagnostic string: being a string, it would survive a (string) cast or an
            // `as string` and reach the caller looking like a real value. VarType reports the type
            // that was not converted.
            return null;
        }
    }
}
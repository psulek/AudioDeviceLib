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

  The changes are summarised in MODIFICATIONS.md at the repository root; the Git history of
  this file is the authoritative record.
*/

using System;
using System.Runtime.InteropServices;
using AudioDeviceLib.CoreAudioApi.Interfaces;
using JetBrains.Annotations;
using static AudioDeviceLib.CoreAudioApi.InteropUtils;

namespace AudioDeviceLib.CoreAudioApi;

/// <summary>
/// Property Store class, only supports reading properties at the moment.
/// </summary>
[PublicAPI]
public class PropertyStore
{
    private IPropertyStore _store;

    /// <summary>Gets the number of properties in the store.</summary>
    /// <exception cref="System.Runtime.InteropServices.COMException">Thrown when the underlying Core Audio call fails.</exception>
    public int Count
    {
        get
        {
            Marshal.ThrowExceptionForHR(_store.GetCount(out var result));
            return result;
        }
    }

    /// <summary>Gets the property at the specified zero-based index.</summary>
    /// <param name="index">The zero-based property index (0 to <see cref="Count"/> - 1).</param>
    /// <returns>The <see cref="PropertyStoreProperty"/> at the requested position.</returns>
    /// <exception cref="System.Runtime.InteropServices.COMException">Thrown when the underlying Core Audio call fails.</exception>
    public PropertyStoreProperty this[int index]
    {
        get
        {
            PropertyKey key = Get(index);
            Marshal.ThrowExceptionForHR(_store.GetValue(ref key, out var result));
            return new PropertyStoreProperty(key, result.ToPropertyValue());
        }
    }

    /// <summary>Determines whether the store contains any property from the given property set.</summary>
    /// <param name="guid">The format identifier (GUID) of the property set to look for.</param>
    /// <returns><c>true</c> if a property with the matching set GUID exists; otherwise <c>false</c>.</returns>
    public bool Contains(Guid guid)
    {
        int count = Count;
        for (int i = 0; i < count; i++)
        {
            PropertyKey key = Get(i);
            if (key.fmtid == guid)
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>Gets the first property whose property set matches the given GUID.</summary>
    /// <param name="guid">The format identifier (GUID) of the property set to look up.</param>
    /// <returns>The matching <see cref="PropertyStoreProperty"/>, or <c>null</c> if none is found.</returns>
    /// <exception cref="System.Runtime.InteropServices.COMException">Thrown when the underlying Core Audio call fails.</exception>
    public PropertyStoreProperty this[Guid guid]
    {
        get
        {
            int count = Count;
            for (int i = 0; i < count; i++)
            {
                PropertyKey key = Get(i);
                if (key.fmtid == guid)
                {
                    Marshal.ThrowExceptionForHR(_store.GetValue(ref key, out var result));
                    return new PropertyStoreProperty(key, result.ToPropertyValue());
                }
            }

            return null;
        }
    }

    /// <summary>Gets the property key at the specified zero-based index.</summary>
    /// <param name="index">The zero-based property index (0 to <see cref="Count"/> - 1).</param>
    /// <returns>The <see cref="PropertyKey"/> at the requested position.</returns>
    /// <exception cref="System.Runtime.InteropServices.COMException">Thrown when the underlying Core Audio call fails.</exception>
    public PropertyKey Get(int index)
    {
        Marshal.ThrowExceptionForHR(_store.GetAt(index, out PropertyKey key));
        return key;
    }

    /// <summary>Gets the managed property value at the specified zero-based index.</summary>
    /// <param name="index">The zero-based property index (0 to <see cref="Count"/> - 1).</param>
    /// <returns>The <see cref="PropertyValue"/> snapshot at the requested position.</returns>
    /// <remarks>The returned snapshot holds no unmanaged resources and requires no cleanup.</remarks>
    /// <exception cref="System.Runtime.InteropServices.COMException">Thrown when the underlying Core Audio call fails.</exception>
    public PropertyValue GetValue(int index)
    {
        PropertyKey key = Get(index);
        Marshal.ThrowExceptionForHR(_store.GetValue(ref key, out var result));
        return result.ToPropertyValue();
    }

    /// <summary>Determines whether the store contains a property matching the given key exactly.</summary>
    /// <param name="compareKey">The property key (set GUID and property id) to look for.</param>
    /// <returns><c>true</c> if a matching property exists; otherwise <c>false</c>.</returns>
    public bool Contains(PropertyKey compareKey)
    {
        int count = Count;
        for (int i = 0; i < count; i++)
        {
            PropertyKey key = Get(i);
            if (key.fmtid == compareKey.fmtid && key.pid == compareKey.pid)
            {
                return true;
            }
        }

        return false;
    }
    
    /// <summary>Gets the property that matches the given key exactly.</summary>
    /// <param name="queryKey">The property key (set GUID and property id) to look up.</param>
    /// <returns>The matching <see cref="PropertyStoreProperty"/>, or <c>null</c> if none is found.</returns>
    /// <exception cref="System.Runtime.InteropServices.COMException">Thrown when the underlying Core Audio call fails.</exception>
    public PropertyStoreProperty this[PropertyKey queryKey]
    {
        get
        {
            int count = Count;
            for (int i = 0; i < count; i++)
            {
                PropertyKey key = Get(i);
                if (key.fmtid == queryKey.fmtid && key.pid == queryKey.pid)
                {
                    Marshal.ThrowExceptionForHR(_store.GetValue(ref key, out var result));
                    return new PropertyStoreProperty(key, result.ToPropertyValue());
                }
            }

            return null;
        }
    }

    /// <summary>Reads the value of a single property by key, without scanning the store.</summary>
    /// <param name="key">The property key (set GUID and property id) to read.</param>
    /// <param name="value">The managed snapshot, or an empty snapshot when the key is absent or the COM read fails.</param>
    /// <returns><c>true</c> if the read succeeds and the variant is neither empty nor null; otherwise <c>false</c>.</returns>
    /// <remarks>The returned snapshot holds no unmanaged resources and requires no cleanup.</remarks>
    public bool TryGetValue(PropertyKey key, out PropertyValue value)
    {
        // IPropertyStore::GetValue takes the key directly, so this is one COM call where the
        // indexers walk the whole store. It also does NOT fail for a missing key: it returns S_OK
        // with a VT_EMPTY variant, which is why the emptiness check below is the real "found" test.
        //
        // The failure test must be HrFailed rather than a comparison against S_OK: the docs require
        // callers to use SUCCEEDED here, because a value converted to a more canonical form comes
        // back as INPLACE_S_TRUNCATED - a success code that still carries a usable variant.
        int hr = _store.GetValue(ref key, out var propValue);
        if (HrFailed(hr))
        {
            value = new PropertyValue(VarEnum.VT_EMPTY, null);
            return false;
        }

        value = propValue.ToPropertyValue();

        // A variant type this library does not convert leaves Value null, so reporting success
        // would hand the caller nothing while claiming the property was read. The snapshot is still
        // returned: VarType identifies what was found for anyone who wants to handle it.
        return !value.IsEmpty && value.IsSupported;
    }
    internal PropertyStore(IPropertyStore store)
    {
        _store = store;
    }
}

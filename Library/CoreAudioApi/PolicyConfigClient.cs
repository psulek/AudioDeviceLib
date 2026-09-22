/*
  MODIFICATIONS
  -------------
  This file is an ALTERED version of the corresponding source in AudioDeviceCmdlets
  (https://github.com/frgnca/AudioDeviceCmdlets, MIT) and must not be misrepresented as being
  the original source code. Altered by Peter Šulek for AudioDeviceLib
  (https://github.com/psulek/AudioDeviceLib).

  The changes are summarized in MODIFICATIONS.md at the repository root; the Git history of
  this file is the authoritative record.
*/

using System;
using System.Runtime.InteropServices;
using AudioDeviceLib.CoreAudioApi.Interfaces;

namespace AudioDeviceLib.CoreAudioApi;

[ComImport, Guid("870af99c-171d-4f9e-af0d-e63df40c2bc9")]
internal class PolicyConfigClientCOM;

/// <summary>
/// Managed wrapper over the undocumented Core Audio <c>IPolicyConfig</c> family of interfaces.
/// Used to change the default audio endpoint. Automatically selects the interface variant
/// supported by the running Windows version.
/// </summary>
/// <remarks>
/// Owns the activated COM wrapper and releases it on <see cref="Dispose"/>. There is no finalizer:
/// the wrapper is a managed RCW, so an instance that is dropped without being disposed still has its
/// underlying reference released when the GC collects it - disposal only makes that deterministic.
/// </remarks>
internal sealed class PolicyConfigClient : IDisposable
{
    // The activated COM wrapper. The three interface fields below are casts of THIS wrapper rather
    // than separate wrappers, so releasing this one reference releases all of them - see
    // ContractTests.PolicyConfigClient_InterfaceCastsAliasTheSameWrapper, which pins that assumption.
    private readonly object _client;

    private readonly IPolicyConfigCOM? _policyConfig;
    private readonly IPolicyConfigVistaCOM? _policyConfigVista;
    private readonly IPolicyConfig10COM? _policyConfig10;

    // volatile: there is no lock in this type, so the volatile read is the only thing ordering a
    // disposal on one thread against a guard check on another.
    private volatile bool _disposed;

    private const string UnsupportedMessage =
        "Changing the default audio endpoint is not supported on this system: none of the known " +
        "IPolicyConfig interface variants (IPolicyConfig, IPolicyConfigVista, IPolicyConfig10) " +
        "could be obtained from the PolicyConfigClient COM class (CLSID " +
        "870af99c-171d-4f9e-af0d-e63df40c2bc9). This API is undocumented and Windows may change or " +
        "remove it in any build.";

    /// <summary>Creates a client bound to the <c>IPolicyConfig</c> variant supported by the current OS.</summary>
    /// <exception cref="System.NotSupportedException">
    /// Thrown when none of the known <c>IPolicyConfig</c> variants can be obtained, so the default
    /// endpoint cannot be changed on this system.
    /// </exception>
    internal PolicyConfigClient()
    {
        // One activation, three QIs: `as` on the resulting wrapper queries the same COM object, so
        // there is no need to re-create it per variant.
        object client;
        try
        {
            client = new PolicyConfigClientCOM();
        }
        catch (Exception ex)
        {
            // The CLSID is undocumented and unregistered activation surfaces as a COM failure.
            // Reported as "not supported" because that is what it means to a caller, with the
            // original failure preserved for diagnosis.
            throw new NotSupportedException(UnsupportedMessage, ex);
        }

        _client = client;

        _policyConfig = client as IPolicyConfigCOM;
        if (_policyConfig != null)
        {
            return;
        }

        _policyConfigVista = client as IPolicyConfigVistaCOM;
        if (_policyConfigVista != null)
        {
            return;
        }

        _policyConfig10 = client as IPolicyConfig10COM;
        if (_policyConfig10 == null)
        {
            // Released before throwing: activation succeeded, so without this the wrapper would be
            // orphaned - the caller never receives an instance and so can never dispose it.
            Release(client);

            // Failing loudly rather than open: the alternative is SetDefaultEndpoint quietly doing
            // nothing and reporting success, leaving the caller's default device unchanged with no
            // indication that the request was ignored.
            throw new NotSupportedException(UnsupportedMessage);
        }
    }

    /// <summary>Sets the endpoint with the given ID as the default device for the specified role.</summary>
    /// <param name="devId">The Core Audio endpoint ID to make default.</param>
    /// <param name="eRole">The role (console, multimedia or communications) to assign.</param>
    /// <exception cref="System.Runtime.InteropServices.COMException">Thrown when the underlying policy-config call fails.</exception>
    /// <exception cref="System.ObjectDisposedException">Thrown when this client has been disposed.</exception>
    public void SetDefaultEndpoint(string devId, Role eRole)
    {
        InteropUtils.RequireNotDisposed(_disposed, this);

        if (_policyConfig != null)
        {
            InteropUtils.ThrowIfFailed(_policyConfig.SetDefaultEndpoint(devId, eRole));
            return;
        }
        
        if (_policyConfigVista != null)
        {
            InteropUtils.ThrowIfFailed(_policyConfigVista.SetDefaultEndpoint(devId, eRole));
            return;
        }
        
        if (_policyConfig10 != null)
        {
            InteropUtils.ThrowIfFailed(_policyConfig10.SetDefaultEndpoint(devId, eRole));
            return;
        }
        
        // if we reach here, none of the known variants were available!
        throw new NotSupportedException(UnsupportedMessage);
    }

    /// <summary>Releases the underlying COM wrapper. Safe to call more than once.</summary>
    /// <remarks>
    /// Not synchronized, because the only owner is <c>AudioController</c>, which disposes this from
    /// its own <c>Dispose</c> under its registration lock and only once in-flight calls have drained.
    /// </remarks>
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        Release(_client);
    }

    // Safe to release deterministically: this wrapper never leaves the library, so nothing else can
    // be holding it. Best-effort - a failure here must not mask the caller's own work.
    private static void Release(object comObject)
    {
        try
        {
            if (Marshal.IsComObject(comObject))
            {
                Marshal.ReleaseComObject(comObject);
            }
        }
        catch (Exception cleanupException)
        {
            InteropUtils.ReportFailure(cleanupException);
            // best-effort cleanup
        }
    }
}
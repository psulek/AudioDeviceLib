using System.Runtime.InteropServices;
using AudioDeviceLib.CoreAudioApi.Interfaces;

namespace AudioDeviceLib.CoreAudioApi;

[ComImport, Guid("870af99c-171d-4f9e-af0d-e63df40c2bc9")]
internal class _PolicyConfigClient
{
}

/// <summary>
/// Managed wrapper over the undocumented Core Audio <c>IPolicyConfig</c> family of interfaces.
/// Used to change the default audio endpoint. Automatically selects the interface variant
/// supported by the running Windows version.
/// </summary>
public class PolicyConfigClient
{
    private readonly IPolicyConfig _PolicyConfig;
    private readonly IPolicyConfigVista _PolicyConfigVista;
    private readonly IPolicyConfig10 _PolicyConfig10;

    /// <summary>Creates a client bound to the <c>IPolicyConfig</c> variant supported by the current OS.</summary>
    public PolicyConfigClient()
    {
        _PolicyConfig = new _PolicyConfigClient() as IPolicyConfig;
        if (_PolicyConfig != null)
        {
            return;
        }

        _PolicyConfigVista = new _PolicyConfigClient() as IPolicyConfigVista;
        if (_PolicyConfigVista != null)
        {
            return;
        }

        _PolicyConfig10 = new _PolicyConfigClient() as IPolicyConfig10;
    }

    /// <summary>Sets the endpoint with the given ID as the default device for the specified role.</summary>
    /// <param name="devID">The Core Audio endpoint ID to make default.</param>
    /// <param name="eRole">The role (console, multimedia or communications) to assign.</param>
    /// <exception cref="System.Runtime.InteropServices.COMException">Thrown when the underlying policy-config call fails.</exception>
    public void SetDefaultEndpoint(string devID, Role eRole)
    {
        if (_PolicyConfig != null)
        {
            Marshal.ThrowExceptionForHR(_PolicyConfig.SetDefaultEndpoint(devID, eRole));
            return;
        }

        if (_PolicyConfigVista != null)
        {
            Marshal.ThrowExceptionForHR(_PolicyConfigVista.SetDefaultEndpoint(devID, eRole));
            return;
        }

        if (_PolicyConfig10 != null)
        {
            Marshal.ThrowExceptionForHR(_PolicyConfig10.SetDefaultEndpoint(devID, eRole));
        }
    }
}
# Lifetime and threading

## Lifetime and threading

- Dispose controllers and every live device you obtain, including enumerated devices.
  Use `using` or `try`/`finally` to make cleanup reliable.
- A device owns its child wrappers. Disposing it also cleans up its volume, session,
  metering, and property resources.
- Dispose subscription tokens to stop listening. Disposing the owning controller,
  device, or session also removes its registrations.
- Repeated disposal is harmless. Live operations on disposed wrappers throw;
  detached snapshots require no disposal. Cached device metadata remains readable.
- Property values are managed results; native property memory is cleaned up internally.

Callbacks can arrive concurrently on non-UI threads. Keep handlers fast and
thread-safe, and marshal UI work to the UI thread. Do not register, unregister, or
dispose audio objects from inside a native event callback; schedule that work elsewhere.


## Compatibility and diagnostics

Device enumeration and audio controls use Windows Core Audio. Changing the default
device depends on the undocumented policy-configuration interface and may be
unsupported on some Windows configurations. COM must be available on the calling thread.

Callback and cleanup failures are reported through `System.Diagnostics.Trace`.
Configure a trace listener when you need diagnostic output.


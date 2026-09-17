# Modifications to Third-Party Source

AudioDeviceLib incorporates source code from two upstream projects — the CoreAudioApi
WASAPI interop layer by Ray Molenkamp, and AudioDeviceCmdlets by Francois Gendron. Their
licences and the provenance of each are reproduced in
[THIRD-PARTY-NOTICES.md](THIRD-PARTY-NOTICES.md).

This file exists to satisfy condition 2 of the Ray Molenkamp licence — *"Altered source
versions must be plainly marked as such, and must not be misrepresented as being the
original source code"* — by recording, in one place, the nature and extent of those
alterations. Every file carrying an upstream licence header is marked individually as
altered and points here.

## Extent

**The alterations are substantial.** This is not an upstream copy with cosmetic changes:
the interop layer has been reworked to the point where most files differ from their
originals in signature, behaviour, or both, and several upstream files were merged,
split, replaced or dropped entirely. No file should be read as representative of the
original work, and no behaviour observed here should be attributed to the original
authors.

Treat the upstream projects as the starting point for this code, not as a description of
it.

## Nature of the changes

Rather than enumerate per file, the alterations fall into these categories:

- **Namespacing and style.** Namespaces changed to the `AudioDeviceLib.*` tree, unused
  directives removed, code reformatted to this project's conventions, and public surface
  annotated with XML documentation the originals did not have.

- **Renamed types and members.** Several types and enum members were renamed away from
  their Win32-transliterated upstream names (for example `EDataFlow` → `DataFlow`).

- **Rewritten interop signatures.** COM declarations were revised for correctness against
  the native contracts: raw `HRESULT` returns with explicit `[PreserveSig]`, blittable
  parameter types in place of types whose default marshalling did not match the native
  ABI, pointer-versus-reference chosen per each parameter's documented nullability, and
  explicit marshalling attributes where an implicit default would have been wrong.

- **Resource and lifetime management.** Disposal, reference release, unmanaged buffer
  ownership and notification registration were reworked. Several upstream paths leaked or
  released incorrectly and no longer do.

- **Behavioural corrections.** Return values that were silently discarded are now
  honoured, reads that truncated or misinterpreted data were fixed, and error conditions
  that were indistinguishable from success are now distinguished.

- **Reshaped public API.** Consumers of this library do not implement COM interfaces.
  Attribute-free C# contracts, immutable snapshot types and extension methods were added,
  and the raw COM sinks were made internal behind adapters.

- **Removals and restructuring.** Upstream types that this library does not use were
  dropped; others were merged into different files or replaced outright. Notably the
  upstream `MMDevice` and `MMDeviceEnumerator` were folded into `Lib/AudioDevice.cs` and
  `Lib/AudioController.cs`, and the PowerShell cmdlet layer of AudioDeviceCmdlets was not
  carried over at all.

## Where to find the detail

This file deliberately does not list individual changes. The authoritative record of what
changed, when and why is the Git history of this repository:

```bash
git log --follow -- <path/to/file>
```

Individual source files carry short comments explaining decisions that are not obvious
from the code — in particular the interop declarations, where the reason for a given
signature is usually a sentence in the Windows Core Audio documentation rather than
anything visible in C#.

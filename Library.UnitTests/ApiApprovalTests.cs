/* Copyright (c) 2026 Peter Šulek. MIT License. */
using System.Threading.Tasks;
using NUnit.Framework;
using PublicApiGenerator;
using VerifyNUnit;

namespace AudioDeviceLib.UnitTests;

[TestFixture]
public sealed class ApiApprovalTests
{
    [Test]
    public Task ApproveApi()
    {
        var api = typeof(AudioController).Assembly.GeneratePublicApi(new ApiGeneratorOptions
        {
            IncludeAssemblyAttributes = false
        });

        // This fixture compiles only for net7.0-windows, which loads the netstandard2.0 asset.
        return Verifier.Verify(api)
            .UseDirectory("Snapshots")
            .UniqueForTargetFrameworkAndVersion();
    }
}

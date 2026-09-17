/*
  Copyright (c) 2026 Peter Šulek
  MIT License

  LoadedAssetTests.cs
  Proves each test leg is running against the library asset it is supposed to be running
  against, rather than the same one three times.
*/

using System.Reflection;
using System.Runtime.Versioning;
using NUnit.Framework;

namespace AudioDeviceLib.UnitTests;

[TestFixture]
public class LoadedAssetTests
{
    // The library ships net48, netstandard2.0 and net8.0-windows. Which one a given test leg picks
    // up is decided by NuGet asset selection, not by anything visible in the source - so it is
    // asserted here. If this fails, the suite has quietly collapsed onto one asset and the
    // multi-targeting is buying nothing.
    [Test]
    public void LoadedLibraryAsset_MatchesExpectedTargetFramework()
    {
        string? actual = typeof(AudioController).Assembly
            .GetCustomAttribute<TargetFrameworkAttribute>()
            ?.FrameworkName;

        Assert.That(actual, Is.Not.Null, "library assembly carries no TargetFrameworkAttribute");
        TestContext.Out.WriteLine($"Library asset under test: {actual}");

        // Note the symbols: a TFM like net7.0-windows defines NET7_0 plus a separate WINDOWS
        // symbol - there is no NET7_0_WINDOWS. NET7_0 and NET8_0 are exact-version symbols, so
        // they do not overlap the way NET7_0_OR_GREATER would.
#if NET48
        Assert.That(actual, Is.EqualTo(".NETFramework,Version=v4.8"));
#elif NET7_0
        Assert.That(actual, Is.EqualTo(".NETStandard,Version=v2.0"),
            "the net7.0-windows leg exists solely to exercise the netstandard2.0 asset");
#elif NET8_0
        Assert.That(actual, Does.StartWith(".NETCoreApp,Version=v8.0"));
#else
        Assert.Fail("Unexpected test target framework - add a case here.");
#endif
    }

    [Test]
    public void LibraryAssembly_IsTheOneUnderDevelopment()
    {
        Assembly library = typeof(AudioController).Assembly;
        Assert.That(library.GetName().Name, Is.EqualTo("AudioDeviceLib"));
    }
}

# Contributing and documentation


Run from the repository root on Windows:

```sh
dotnet build AudioDeviceLib.slnx -c Release
dotnet test Library.UnitTests/Library.UnitTests.csproj -c Release -m:1
```

Tests cover the three shipped assets through .NET Framework 4.8, .NET 7, and .NET 8
hosts. The .NET 7 host exercises the .NET Standard asset. Run targets serially because
hardware tests share the machine's audio devices.

Some integration tests change volume or mute and restore the previous settings.
Session metadata tests create their own silent session. Tests that require an audio
endpoint skip when none is available. Explicit race tests run separately:

```sh
dotnet test Library.UnitTests/Library.UnitTests.csproj -c Release -m:1 --filter FullyQualifiedName~ControllerDisposeRaceTests
```

Enable the repository's pre-commit hook in each clone:

```sh
git config core.hooksPath .githooks
```

The hook runs the Slopwatch code-quality check before committing.

Optional code-quality and coverage checks:

```sh
dotnet tool restore
dotnet slopwatch analyze --fail-on warning
dotnet test Library.UnitTests/Library.UnitTests.csproj -c Release -m:1 --settings coverage.runsettings --collect:"XPlat Code Coverage" --results-directory TestResults
dotnet reportgenerator -reports:"TestResults/**/coverage.opencover.xml" -targetdir:coverage -reporttypes:"Html;TextSummary"
```


## Build the documentation

Install the .NET SDK selected by `global.json`. Run from the repository root on Windows:

```sh
dotnet tool restore
dotnet docfx docfx/docfx.json --warningsAsErrors
dotnet docfx serve _site
```

Open <http://localhost:8080>. Re-run the build after editing Markdown or XML comments.
DocFX is pinned in `dotnet-tools.json`. Generated API YAML and `_site` are ignored;
edit guides under `docfx/articles` and API comments in `Library`.
Keep usage examples in the guides and README consistent when changing behavior.

## Publish to GitHub Pages

In the repository's **Settings > Pages**, select **GitHub Actions** as the build source.
The `Docs` workflow builds pull requests without deploying them. Pushes to `main`
and manual runs on `main` deploy the generated site to
<https://psulek.github.io/AudioDeviceLib/> through the `github-pages` environment.
No personal access token or generated-content branch is needed. If environment
protection rules require approval, approve the deployment in GitHub Actions.

The API reference reflects the source on `main`, which may include changes newer
than the published NuGet package.

## Publish new NuGet version

Push a git tag named with the package version in `v{version}` format, for example
`v1.0.0-rc.2`.

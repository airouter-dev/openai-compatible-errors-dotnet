# Release process

Releases use NuGet Trusted Publishing from GitHub Actions. Maintainers do not store a long-lived NuGet API key in repository secrets.

## One-time NuGet.org setup

Create a Trusted Publishing policy for:

- package owner: the NuGet.org account that owns `AiRouter.OpenAICompatibleErrors`;
- repository owner: `airouter-dev`;
- repository: `openai-compatible-errors-dotnet`;
- workflow: `publish.yml`;
- environment: `nuget`.

Configure the GitHub `nuget` environment with required reviewers. The workflow filename and environment must match the NuGet.org policy exactly.

Set the environment variable `NUGET_USER` to the personal NuGet.org profile name that created the policy. It is not an email address or an organization name, even when the selected package owner is an organization.

NuGet's temporary credential is currently owner-wide rather than restricted to one package. Keep the environment approval, signed-tag check, protected workflow, and build/publish job separation in place. The OIDC job downloads an already-built artifact and verifies its checksums; it does not restore or execute package build dependencies.

## Release checklist

1. Confirm the package ID is still available for the first release.
2. Update `Version`, `PackageReleaseNotes`, `CHANGELOG.md`, and package documentation.
3. Restore with `--locked-mode`, build, test all targets, run the sample, and pack.
4. Inspect the `.nupkg` and `.snupkg`; verify the README, license expression, repository metadata, DLL/XML/PDB assets, Source Link, and absence of runtime dependencies or unexpected files.
5. Install the package into fresh .NET 8 and .NET 10 consumers from a temporary local feed and run both.
6. Merge through a protected pull request with all required checks.
7. Create a signed annotated tag whose version exactly matches the project version: `vX.Y.Z`.
8. Let `publish.yml` build from the tag, attest the package artifacts, obtain a temporary NuGet key through OIDC, and push exactly once.
9. Verify the package page, registration metadata, install command, repository link, README rendering, and fresh restore from NuGet.org.
10. Publish release notes with checksums and provenance links.

The workflow refuses a tag/project version mismatch and does not use `--skip-duplicate`, so a repeated or conflicting publish fails visibly.

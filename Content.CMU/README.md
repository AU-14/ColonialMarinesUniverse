# CMU Content

This directory is the authoritative home for CMU-only gameplay code and resources.

## Layout

```text
Content.CMU/
  Shared/     Shared components, events, systems, and prediction-safe logic
  Server/     Authoritative, database-facing, and server-only behavior
  Client/     UI, overlays, input, audio, and visual presentation
  Resources/  Prototypes, textures, audio, maps, localization, and other assets
```

The three code directories compile into the existing `Content.Shared`, `Content.Server`, and
`Content.Client` assemblies. They are not separate assemblies. This preserves Robust source
generators, XAML compilation, partial types, internal access, networking, and runtime discovery.

Use namespaces that match the destination assembly, such as `Content.Shared.CMU`,
`Content.Server.CMU`, and `Content.Client.CMU`. Ported code may retain a compatible existing
namespace when changing it would add unnecessary migration churn.

## Resources

`Content.CMU/Resources` is mounted at the VFS root in development and tests and is merged into
client, server, and ACZ production packages. Mirror normal resource paths directly:

```text
Content.CMU/Resources/Prototypes/...
Content.CMU/Resources/Textures/...
Content.CMU/Resources/Audio/...
Content.CMU/Resources/Locale/...
Content.CMU/Resources/Maps/...
```

For example, `Content.CMU/Resources/Textures/Medical/scanner.rsi` is addressed in content as
`/Textures/Medical/scanner.rsi`.

Every relative resource path must be unique across `Resources/` and `Content.CMU/Resources/`.
The standard resource root is mounted first during development, so ambiguous overrides are
rejected to keep development and packaged behavior identical. Global singleton files such as
`manifest.yml` remain in the canonical `Resources/` root.

Tests remain in `Content.Tests` or `Content.IntegrationTests` so the existing test runners can
discover them.

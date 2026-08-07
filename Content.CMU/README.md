# CMU Content

This folder is the home for code and assets made specifically for Colonial Marines Universe (CMU).
New CMU features should usually be placed here.
 
## Where things go

```text
Content.CMU/
  Shared/     Gameplay used by both the client and server
  Server/     Server-only gameplay and data handling
  Client/     Interfaces, visuals, audio, and player input
  Resources/  Prototypes, textures, sounds, maps, and translations
```

The code folders are part of the existing Shared, Server, and Client projects. You do not need to
create a separate project for CMU code.

Match namespaces to the folder where the code lives:

```text
Shared/  -> Content.Shared.CMU
Server/  -> Content.Server.CMU
Client/  -> Content.Client.CMU
```

Existing code can keep a compatible namespace when renaming it would create unnecessary work.

## Resources

Put CMU assets in `Content.CMU/Resources` and use the same folder structure as the main
`Resources` directory:

```text
Content.CMU/Resources/Prototypes/...
Content.CMU/Resources/Textures/...
Content.CMU/Resources/Audio/...
Content.CMU/Resources/Locale/...
Content.CMU/Resources/Maps/...
```

When referencing an asset in code or YAML, leave `Content.CMU/Resources` out of its path. For
example:

```text
Content.CMU/Resources/Textures/Medical/scanner.rsi
```

is referenced as:

```text
/Textures/Medical/scanner.rsi
```

Do not create the same resource path in both `Resources/` and `Content.CMU/Resources/`. Keep
project-wide files, such as `manifest.yml`, in the main `Resources/` directory.

## Tests

Put unit tests in `Content.Tests` and integration tests in `Content.IntegrationTests` so the existing
test tools can find and run them.

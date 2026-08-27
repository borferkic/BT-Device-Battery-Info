# Contributing to BT Device Battery Info

## Basic workflow

1. Create a descriptive branch from `master`.
2. Keep changes focused and document every observable behavior change.
3. Restore and build before opening a pull request:

   ```powershell
   dotnet restore ".\BT Device Battery Info.slnx"
   dotnet build ".\BT Device Battery Info.slnx" --configuration Release
   ```

4. In the pull request, describe the device, Windows version, and validation steps used for Bluetooth or battery changes.

## UI and diagnostic changes

Windows Bluetooth APIs can behave differently depending on the hardware. Document known limitations and avoid exposing sensitive device identifiers in shared screenshots or logs.

Local builds and the `bin`, `obj`, `publish`, and `Builds-Test` folders are excluded from the repository. The current public baseline is version `0.10 — BUILD INICIAL`.

# Contribuir a BT Device Battery Info

## Flujo básico

1. Crea una rama descriptiva a partir de `main`.
2. Mantén los cambios enfocados y documenta todo comportamiento observable que cambie.
3. Actualiza `BTDeviceBatteryInfo/docs/pending.md` si queda trabajo futuro y registra los cambios visibles en `CHANGELOG.md` bajo `Unreleased`.
4. Ejecuta las validaciones antes de abrir una solicitud de cambios:

   ```powershell
   dotnet restore ".\BT Device Battery Info.slnx"
   dotnet format ".\BT Device Battery Info.slnx" --verify-no-changes --no-restore
   dotnet build ".\BT Device Battery Info.slnx" --configuration Release --no-restore
   dotnet test ".\BT Device Battery Info.slnx" --configuration Release --no-restore
   git diff --check
   ```

5. En la solicitud de cambios describe el dispositivo, la versión de Windows y los pasos de validación usados para cambios Bluetooth o de batería.

## Cambios de UI y diagnóstico

Las API Bluetooth de Windows pueden comportarse de forma distinta según el hardware. Documenta las limitaciones conocidas y evita exponer identificadores Bluetooth sensibles en capturas o registros compartidos.

Separa siempre la validación automatizada de la validación manual dependiente de hardware. Si `dotnet test` no descubre pruebas, indícalo explícitamente.

## Documentación y formato

- Usa `BTDeviceBatteryInfo/docs/architecture.md` para el comportamiento real y las responsabilidades de los módulos.
- Usa `BTDeviceBatteryInfo/docs/testing.md` para procedimientos reproducibles y evidencias de QA.
- Usa `BTDeviceBatteryInfo/docs/pending.md` como única lista de pendientes del proyecto.
- Usa `BTDeviceBatteryInfo/docs/changelog/unreleased.md` para el detalle de cambios que todavía no tienen versión.
- Mantén los archivos Markdown en LF y el código C#/XAML en CRLF, según `.editorconfig` y `.gitattributes`.
- Conserva la separación `Models`, `Services`, `ViewModels` y `MainWindow.xaml`.

Las compilaciones locales y las carpetas `bin`, `obj`, `publish`, `artifacts` y `Builds-Test` están excluidas del repositorio. La línea base pública actual es la versión `0.12 — Confiabilidad y experiencia`.

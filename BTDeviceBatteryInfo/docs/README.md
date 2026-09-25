# Documentación del proyecto

Esta carpeta contiene la documentación de mantenimiento, arquitectura, validación y evolución de BT Device Battery Info.

## Orden de lectura

1. [Arquitectura](architecture.md): flujo de arranque, responsabilidades y límites técnicos.
2. [Pruebas y validación](testing.md): comandos automatizados, matriz manual y formato de evidencias.
3. [Estándar de documentación](documentation-standard.md): formato, jerarquía y reglas de mantenimiento.
4. [Pendientes](pending.md): backlog canónico priorizado por impacto.
5. [Changelog](../../CHANGELOG.md): historial de versiones y cambios no publicados (`Unreleased`).
6. [Lineamientos de traspaso](lineamientos-traspaso.md): cómo retomar el proyecto en otra PC.
7. [Investigación de batería 8BitDo](research-8bitdo-battery.md): pruebas y decisiones sobre mandos.

## Convenciones

- Los documentos nuevos se escriben en español y conservan literalmente nombres técnicos, comandos, rutas y nombres oficiales.
- Cada documento tiene un único título H1 y usa títulos H2 para sus secciones principales.
- Los enlaces entre documentos usan rutas relativas.
- `docs/pending.md` es la única fuente de verdad para trabajo futuro.
- `CHANGELOG.md` (raíz) es el único changelog: los cambios no publicados van en `Unreleased` y cada versión tiene su entrada.
- La evidencia de QA de cada versión se registra en `pending.md` al cerrar la tarea correspondiente.
- `docs/documentation-standard.md` define el formato y el orden que deben seguir los documentos nuevos.

## Estado documental

La última versión pública es `0.21 — Language selection and Bluetooth status`. Los cambios posteriores se registran bajo `Unreleased` en `CHANGELOG.md`.

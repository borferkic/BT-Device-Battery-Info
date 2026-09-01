# Documentación del proyecto

Esta carpeta contiene la documentación de mantenimiento, arquitectura, validación y evolución de BT Device Battery Info.

## Orden de lectura

1. [Arquitectura](architecture.md): flujo de arranque, responsabilidades y límites técnicos.
2. [Pruebas y validación](testing.md): comandos automatizados, matriz manual y formato de evidencias.
3. [Estado del QA](qa-status.md): última revisión, evidencia y bloqueadores conocidos.
4. [Estándar de documentación](documentation-standard.md): formato, jerarquía y reglas de mantenimiento.
5. [Pendientes](pending.md): backlog canónico priorizado por impacto.
6. [Cambios no publicados](changelog/unreleased.md): registro detallado para la próxima versión.
7. [Versión 0.12](changelog/0.12.md): notas detalladas de la versión pública actual.
8. [Roadmap](roadmap.md): dirección general y decisiones de producto.
9. [Historial de changelog](changelog/): notas de versiones publicadas.

## Convenciones

- Los documentos nuevos se escriben en español y conservan literalmente nombres técnicos, comandos, rutas y nombres oficiales.
- Cada documento tiene un único título H1 y usa títulos H2 para sus secciones principales.
- Los enlaces entre documentos usan rutas relativas.
- `docs/pending.md` es la única fuente de verdad para trabajo futuro.
- `docs/qa-status.md` conserva el resultado de la última revisión, no reemplaza los procedimientos de `testing.md`.
- `docs/changelog/unreleased.md` reúne los cambios ya realizados que todavía no pertenecen a una versión publicada.
- `docs/documentation-standard.md` define el formato y el orden que deben seguir los documentos nuevos.
- `BTDeviceBatteryInfo/skills/Changelog.md` se conserva como material de apoyo legado; no es la skill operativa del proyecto.

## Estado documental

La línea base pública es la versión `0.12 — Confiabilidad y experiencia`. Los cambios posteriores se mantienen bajo `Unreleased` hasta que se defina la próxima versión.

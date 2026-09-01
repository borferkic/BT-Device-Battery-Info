# Estándar de documentación

Este documento define cómo se organiza y mantiene la documentación de BT Device Battery Info.

## Jerarquía de documentos

Mantén una sola fuente de verdad para cada propósito:

| Documento | Propósito | Debe contener |
| --- | --- | --- |
| `README.md` | Entrada pública | Qué hace el producto, requisitos, uso y límites principales. |
| `docs/architecture.md` | Diseño vigente | Flujo, módulos, ciclo de vida, datos y límites técnicos reales. |
| `docs/testing.md` | Validación | Comandos, matriz manual, evidencias y limitaciones de cobertura. |
| `docs/pending.md` | Trabajo futuro | Tareas priorizadas, criterios de aceptación y estado. |
| `docs/changelog/unreleased.md` | Próxima versión | Cambios realizados, validación y pendientes de publicación. |
| `CHANGELOG.md` | Historial público | Resumen user-facing por versión. |
| `docs/changelog/<versión>.md` | Detalle histórico | Alcance y validación de una versión publicada. |
| `CONTRIBUTING.md` | Colaboración | Flujo, formato, validaciones y evidencia requerida. |

No dupliques una tarea entre `roadmap.md`, `pending.md` y un plan local. `roadmap.md` solo expresa dirección; `pending.md` contiene el trabajo accionable.

## Formato Markdown

- Usa un único título H1 por archivo.
- Usa títulos H2 para secciones principales y H3 para tareas o subtemas.
- Deja una línea en blanco antes y después de cada lista, tabla o bloque de código.
- Usa listas con casillas para tareas: `- [ ]` pendiente y `- [x]` completada.
- Usa nombres de archivo, clases, propiedades, APIs, comandos y rutas entre backticks.
- Usa enlaces relativos dentro del repositorio y comprueba que el destino exista.
- Mantén Markdown en LF, con codificación UTF-8 y salto de línea final.
- Escribe documentos nuevos en español. Conserva en inglés las cadenas de UI, comandos y nombres oficiales que formen parte del producto.
- Los documentos históricos de versiones publicadas pueden conservar su idioma original; los cambios nuevos deben seguir el idioma del proyecto.

## Formato de una tarea pendiente

Cada tarea debe incluir:

1. identificador estable (`P-001`, `P-002`, etc.);
2. prioridad Alta, Media o Baja;
3. problema u objetivo observable;
4. acciones concretas;
5. criterios de aceptación verificables;
6. relación con código o documentación cuando sea conocida.

No marques una tarea como completada solo porque el código compile. La aceptación debe incluir pruebas adecuadas al riesgo.

## Changelog

Registra primero los cambios visibles bajo `Unreleased` y usa categorías consistentes: `Añadido`, `Cambiado`, `Corregido`, `Eliminado`, `Rendimiento` o `Seguridad`.

Para cada cambio relevante indica en `docs/changelog/unreleased.md`:

- qué cambió y por qué;
- impacto visible;
- validación ejecutada;
- limitaciones o pruebas pendientes.

No presentes una tarea del backlog como resuelta hasta que el changelog y la validación estén actualizados.

## QA y evidencia

Los resultados de QA deben distinguir:

- validaciones automatizadas que realmente se ejecutaron;
- validaciones manuales que requieren hardware o interacción visual;
- pruebas no ejecutables en el entorno;
- defectos observados, con prioridad y ubicación.

Nunca describas `dotnet test` como cobertura real si no se descubrieron pruebas. Redacta identificadores Bluetooth y evita incluirlos en documentos compartidos.

## Formato del código

- C# y XAML usan cuatro espacios, finales de línea CRLF y namespaces file-scoped cuando corresponda.
- Markdown, YAML y archivos de configuración textual usan LF.
- Conserva la separación por `Models`, `Services`, `ViewModels`, `Resources` y la ventana WPF.
- Las refactorizaciones de formato deben separarse de cambios de comportamiento cuando sea posible.
- Toda modificación observable debe actualizar arquitectura, pruebas y changelog en la misma tarea.

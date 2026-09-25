# Lineamientos de trabajo del proyecto

- Guarda las decisiones e instrucciones de este proyecto aquí, dentro de `BTDeviceBatteryInfo`; no las registres en memorias compartidas con otros proyectos.
- Nombra las builds de revisión en `Builds` con versión y número consecutivo, sin palabras descriptivas: `0.18-dev1`, `0.18-dev2`, etc. La última versión publicada es `0.18`.
- Al limpiar builds antiguas, elimina únicamente las carpetas cuyo nombre corresponda a una versión `dev`; elimina todo su contenido, incluidas capturas e imágenes. Conserva todas las carpetas de versiones publicadas.
- Antes de generar cualquier build, presenta al usuario la lista concreta de tareas y espera su confirmación explícita.
- Espera la prueba manual del usuario antes de actualizar QA, changelog o pendientes con resultados. Mantén cada pendiente en un máximo de dos líneas.
- No subas la carpeta `.claude/` (skills y configuración local de Claude Code) a Git; está excluida en `.gitignore`.
- Tras subir una versión a `main`, crea y sube su tag anotado `v<versión>` (mensaje `BT Device Battery Info <versión>`) sin pedir confirmación. La release de GitHub sigue requiriendo autorización.

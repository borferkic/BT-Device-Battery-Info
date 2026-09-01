---
name: changelog-generator
description: Crea automáticamente changelogs orientados a los usuarios a partir de commits de Git, analizando el historial de commits, categorizando los cambios, detectando el idioma requerido y transformando commits técnicos en notas de versión claras y fáciles de entender. El changelog se genera automáticamente en el idioma indicado por el usuario o detectado a partir del contexto existente.
---

> Estado: material de apoyo legado. La skill operativa del proyecto es `bt-device-battery-info-project`; las reglas documentales versionadas están en `docs/documentation-standard.md`.

# Generador de Changelog

Esta skill transforma commits técnicos de Git en changelogs claros, profesionales y fáciles de entender para clientes y usuarios.

Analiza el historial de Git, identifica los cambios relevantes y convierte mensajes técnicos de commits en notas de versión orientadas al usuario final.

Además, detecta automáticamente el idioma que debe utilizar el changelog y mantiene ese idioma de forma consistente en todo el documento.

## Cuándo usar esta skill

- Preparar notas de versión para una nueva versión
- Crear resúmenes semanales o mensuales de actualizaciones del producto
- Documentar cambios para los clientes
- Redactar entradas de changelog para publicaciones en tiendas de aplicaciones
- Generar notificaciones de actualización
- Crear documentación interna de versiones
- Mantener una página pública de changelog o actualizaciones del producto
- Crear notas de versión para GitHub
- Generar changelogs automáticamente a partir del historial de Git

## Qué hace esta skill

1. **Analiza el historial de Git**
   Examina los commits realizados durante un período específico, desde una versión determinada o entre dos versiones.

2. **Categoriza los cambios**
   Agrupa automáticamente los commits en categorías lógicas, como:
   - Nuevas funciones
   - Mejoras
   - Correcciones de errores
   - Cambios incompatibles (Breaking Changes)
   - Seguridad
   - Rendimiento
   - Otros cambios relevantes

3. **Traduce contenido técnico → lenguaje para usuarios**
   Convierte mensajes técnicos escritos por desarrolladores en descripciones claras que puedan entender clientes y usuarios finales.

4. **Aplica un formato profesional**
   Genera changelogs limpios, estructurados y fáciles de leer.

5. **Filtra contenido innecesario**
   Excluye automáticamente cambios internos que normalmente no deberían aparecer en un changelog público, como:
   - Refactorizaciones internas
   - Cambios de formato
   - Tests
   - Cambios menores de CI/CD
   - Commits de mantenimiento sin impacto para el usuario
   - Merge commits sin cambios relevantes
   - Cambios internos de dependencias sin impacto visible

6. **Sigue las directrices del proyecto**
   Si existe un archivo como `CHANGELOG_STYLE.md`, utiliza sus instrucciones para determinar el formato, estructura y tono del changelog.

7. **Detecta automáticamente el idioma**
   Identifica el idioma que debe utilizarse para generar el changelog y mantiene ese idioma de forma consistente en todo el documento.

## Reglas de idioma

La detección y selección del idioma debe seguir este orden de prioridad:

1. **Idioma especificado explícitamente por el usuario**

   Si el usuario solicita un idioma concreto, siempre debe utilizarse ese idioma.

   Ejemplo:

   `Genera el changelog en inglés`

   Resultado: todo el changelog debe generarse en inglés.

2. **Idioma del changelog existente**

   Si existe un archivo `CHANGELOG.md`, analiza su contenido y utiliza el idioma predominante como idioma de salida.

3. **Idioma definido en CHANGELOG_STYLE.md**

   Si existe un archivo `CHANGELOG_STYLE.md` y contiene instrucciones relacionadas con el idioma, estas deben respetarse.

4. **Idioma utilizado por el usuario**

   Si no existe ninguna instrucción explícita ni archivos que permitan determinar el idioma, utiliza el idioma principal utilizado por el usuario en su solicitud.

5. **Idioma de los commits**

   El idioma de los commits puede utilizarse como referencia secundaria, pero no debe tener prioridad sobre las reglas anteriores.

## Consistencia del idioma

Una vez determinado el idioma:

- Todo el changelog debe escribirse en ese idioma.
- Los títulos deben utilizar ese idioma.
- Las categorías deben utilizar ese idioma.
- Las descripciones deben utilizar ese idioma.
- Las explicaciones deben utilizar ese idioma.
- Las notas adicionales deben utilizar ese idioma.
- No mezclar idiomas innecesariamente.

No traducir elementos técnicos que deban conservarse, como:

- Nombres de funciones
- Nombres de clases
- Variables
- APIs
- Endpoints
- Comandos
- Rutas
- Versiones
- Nombres de archivos
- Nombres propios
- Nombres oficiales de productos
- Identificadores técnicos

Por ejemplo:

`Fixed authentication issue in /api/v2/login`

Puede convertirse en:

`Se corrigió un problema de autenticación en /api/v2/login.`

La ruta `/api/v2/login` debe mantenerse sin modificaciones.

## Cómo usarla

### Uso básico

Desde el repositorio del proyecto:

`Crea un changelog con los commits realizados desde la última versión`

También:

`Genera un changelog con todos los commits de la última semana`

O:

`Crea las notas de versión para la versión 2.5.0`

### Especificando un idioma

`Genera el changelog de la versión 2.5.0 en español`

`Generate the changelog for version 2.5.0 in English`

`Crie o changelog da versão 2.5.0 em português`

El idioma especificado por el usuario siempre tiene prioridad.

### Con un rango de fechas específico

`Crea un changelog con todos los commits realizados entre el 1 y el 15 de marzo`

### Entre versiones

`Genera un changelog con todos los cambios entre v2.4.0 y v2.5.0`

### Desde una versión

`Genera un changelog con todos los commits realizados desde v2.4.0`

### Con directrices personalizadas

`Crea un changelog con los commits realizados desde v2.4.0 utilizando las directrices de CHANGELOG_STYLE.md`

## Flujo de trabajo

Cuando se solicite generar un changelog:

1. Determinar el rango de commits solicitado.
2. Revisar si existe `CHANGELOG_STYLE.md`.
3. Revisar si existe `CHANGELOG.md`.
4. Determinar el idioma del changelog.
5. Obtener los commits correspondientes.
6. Analizar cada commit.
7. Eliminar commits irrelevantes para el usuario final.
8. Identificar cambios duplicados o relacionados.
9. Agrupar commits que representen una misma funcionalidad.
10. Categorizar los cambios.
11. Transformar las descripciones técnicas en lenguaje orientado al usuario.
12. Ordenar los cambios por importancia.
13. Generar el changelog.
14. Revisar que todo el contenido mantenga el mismo idioma.
15. Presentar el resultado para revisión antes de publicarlo o guardarlo.

## Reglas para interpretar commits

No copiar simplemente el mensaje del commit.

El objetivo es comprender qué cambió y explicar su impacto para el usuario.

Por ejemplo:

Commit:

`fix auth token refresh race condition`

Evitar:

`Fixed auth token refresh race condition`

Preferir:

`Se mejoró la estabilidad de las sesiones corrigiendo un problema que podía provocar errores durante la renovación automática de la autenticación.`

Otro ejemplo:

Commit:

`feat: add bulk device deletion`

Evitar:

`Added bulk device deletion`

Preferir:

`Ahora es posible eliminar múltiples dispositivos simultáneamente, reduciendo el tiempo necesario para administrar grandes cantidades de dispositivos.`

## Agrupación de commits

Varios commits pueden formar parte de una misma funcionalidad.

Por ejemplo:

`feat: add device bulk selector`

`feat: add delete selected devices`

`fix: bulk delete confirmation modal`

`fix: refresh device list after bulk delete`

En lugar de generar cuatro entradas independientes, pueden combinarse en:

`**Eliminación masiva de dispositivos**: Ahora puedes seleccionar y eliminar varios dispositivos al mismo tiempo. También se mejoró el proceso de confirmación y la actualización automática de la lista después de eliminar dispositivos.`

## Categorías recomendadas

Las categorías pueden adaptarse al idioma detectado.

Ejemplo en español:

### ✨ Nuevas funciones

### 🔧 Mejoras

### ⚡ Rendimiento

### 🐛 Correcciones

### 🔒 Seguridad

### ⚠️ Cambios importantes

Ejemplo en inglés:

### ✨ New Features

### 🔧 Improvements

### ⚡ Performance

### 🐛 Bug Fixes

### 🔒 Security

### ⚠️ Breaking Changes

No es obligatorio mostrar categorías vacías.

## Ejemplo de salida

Si el usuario solicita:

`Crea un changelog con los commits de los últimos 7 días`

Y el idioma detectado es español:

### Ejemplo de changelog — Semana del 10 de marzo de 2024

## ✨ Nuevas funciones

- **Espacios de trabajo para equipos**: Ahora puedes crear espacios de trabajo separados para diferentes proyectos, invitar miembros de tu equipo y mantener todo organizado.

- **Atajos de teclado**: Se añadieron nuevos atajos de teclado para acceder más rápidamente a las funciones principales.

## 🔧 Mejoras

- **Sincronización más rápida**: Se mejoró el sistema de sincronización para reducir significativamente el tiempo necesario para actualizar archivos entre dispositivos.

- **Mejor búsqueda**: La búsqueda ahora puede encontrar coincidencias dentro del contenido de los archivos y no solamente en sus títulos.

## 🐛 Correcciones

- Se corrigió un problema que impedía subir imágenes grandes.
- Se resolvieron inconsistencias relacionadas con las zonas horarias en publicaciones programadas.
- Se corrigió el contador de notificaciones.

## Tono del changelog

El contenido debe ser:

- Claro
- Conciso
- Profesional
- Fácil de entender
- Orientado al usuario
- Informativo
- Natural

Evitar lenguaje excesivamente técnico cuando no sea necesario.

Evitar mencionar detalles internos de implementación que no aporten valor al usuario.

## Buenas prácticas

- Ejecutar la skill desde la raíz del repositorio Git.
- Especificar rangos de fechas o versiones cuando sea posible.
- Utilizar `CHANGELOG_STYLE.md` para mantener un formato consistente.
- Revisar changelogs anteriores para mantener coherencia.
- Combinar commits relacionados cuando representen una misma funcionalidad.
- Priorizar el impacto para el usuario sobre los detalles técnicos.
- Mantener un único idioma en todo el changelog.
- Revisar el changelog generado antes de publicarlo.
- Guardar el resultado en `CHANGELOG.md` cuando corresponda.

## Casos de uso relacionados

- Crear GitHub Release Notes
- Crear notas de actualización para aplicaciones
- Preparar descripciones para App Store o Google Play
- Generar correos de actualización para usuarios
- Crear publicaciones para redes sociales
- Generar documentación interna de releases
- Crear resúmenes semanales de desarrollo
- Mantener páginas públicas de actualizaciones del producto

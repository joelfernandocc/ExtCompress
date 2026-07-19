# Manual de Usuario de ExtCompress

Bienvenido al Manual de Usuario Avanzado de CLI para ExtCompress. ExtCompress es una utilidad de compresión de alto rendimiento que utiliza el algoritmo Zstandard (zstd).

## Comandos

### 1. Comprimir (Compress)
Comprime uno o más archivos en un archivo `.extc`.

**Sintaxis:**
`ExtCompress.exe /compress "archivo1" "archivo2" ... /out "salida.extc" [/level 1-22]`

**Parámetros:**
- `"archivo1" "archivo2" ...`: Los archivos a comprimir.
- `/out "salida.extc"`: La ruta del archivo resultante.
- `/level 1-22` *(Opcional)*: Nivel de compresión Zstandard.
  - **1-9**: Compresión rápida.
  - **10-19**: Alta compresión.
  - **20-22**: Ultra alta compresión.

### 2. Modo Relámpago (Lightning)
Modo rápido, equivalente al nivel 1.

**Sintaxis:**
`ExtCompress.exe /lightning "archivo1" "archivo2" ... /out "salida.extc"`

### 3. Descomprimir (Decompress)
Extrae los contenidos de un archivo `.extc`.

**Sintaxis:**
`ExtCompress.exe /decompress "entrada.extc" /out "carpeta"`

## Acerca de la compresión Zstandard
ExtCompress utiliza Zstandard, ofreciendo niveles del 1 al 22 para equilibrar velocidad y tamaño.

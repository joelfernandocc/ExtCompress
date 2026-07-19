# ExtCompress 📦⚡

**ExtCompress** es un motor de compresión de ultra velocidad impulsado por el algoritmo avanzado Zstandard (Zstd). Desarrollado por **Soluciones Digitales Camargo**, está diseñado para exprimir el máximo rendimiento del hardware moderno, ofreciendo tasas de compresión superiores y velocidades de descompresión relámpago a través de procesamiento multi-hilo nativo.

---

## 🚀 Características Principales

*   **⚡ Ultra Velocidad Zstandard:** Utiliza el potente algoritmo de Meta (Zstd) para un equilibrio inigualable entre velocidad y ratio de compresión.
*   **🧠 Procesamiento Multi-hilo (Multi-threading):** Exprime todos los núcleos de tu procesador para comprimir gigabytes de datos en segundos.
*   **🔒 Cifrado AES-256 (Próximamente):** Arquitectura base lista para implementar cifrado de grado militar en tus archivos `.extc`.
*   **🖥️ Interfaz Híbrida (CLI / GUI):**
    *   **Línea de Comandos (CLI):** Perfecto para automatización, servidores y usuarios avanzados con decenas de parámetros personalizables.
    *   **Interfaz Gráfica (GUI):** Asistente visual e intuitivo con barra de progreso detallada e indicadores de rendimiento en tiempo real (MB/s).
*   **🌐 Soporte Multilingüe Automático:** Interfaz y terminal responsivas al idioma nativo de tu sistema operativo (Inglés / Español).
*   **🛡️ Firma Digital:** Binarios nativos pre-firmados para garantizar la integridad y seguridad en entornos corporativos.

---

## 🛠️ Instalación

La manera más fácil de instalar ExtCompress es utilizando nuestro instalador oficial. El instalador configura automáticamente las variables de entorno, los menús contextuales y los íconos de los archivos `.extc`.

### Instalación Manual
Descarga el ejecutable `ExtCompress_Installer.exe` desde la sección de **Releases** en este repositorio y haz doble clic sobre él para iniciar el asistente visual de instalación.

### Instalación vía Winget
También puedes instalar la aplicación directamente desde el gestor oficial de paquetes de Windows usando tu terminal:
```cmd
winget install extcompress
```

---

## 💻 Uso Básico (Línea de Comandos)

ExtCompress está diseñado para ser directo y poderoso. Abre tu terminal (CMD o PowerShell) y prueba estos comandos:

### Comprimir archivos o carpetas
```cmd
extcompress compress "C:\Ruta\A\Tu\Carpeta"
```
*Esto generará un archivo ultra comprimido con extensión `.extc` en el mismo directorio.*

### Descomprimir
```cmd
extcompress extract "C:\Ruta\A\Tu\Archivo.extc"
```
*Esto extraerá el contenido de forma ultra rápida.*

### Ver todos los comandos y parámetros
Para ver el listado completo de opciones, niveles de compresión, hilos y filtros disponibles:
```cmd
extcompress help
```

---

## ⚙️ Opciones Avanzadas de Compresión

ExtCompress te da control total sobre cómo se procesan tus datos. Puedes combinar estas banderas al comprimir:

| Bandera | Descripción | Ejemplo |
| :--- | :--- | :--- |
| `--level <N>` | Nivel de compresión (1=Rápido, 22=Máximo). Por defecto: 3 | `--level 19` |
| `--threads <N>` | Número de hilos de CPU a utilizar. Por defecto: Automático | `--threads 8` |
| `--silent` | Ejecuta el proceso en la terminal sin abrir la Interfaz Gráfica | `--silent` |
| `--benchmark` | Mide y muestra el tiempo exacto que tardó el proceso | `--benchmark` |
| `--format json` | Devuelve la salida en formato JSON (ideal para scripts) | `--format json` |

**Ejemplo Avanzado:**
```cmd
extcompress compress "C:\Data" --level 15 --threads 16 --silent --benchmark
```

---

## 👨‍💻 Desarrollo y Compilación

ExtCompress está construido en C# bajo el framework **.NET 8.0**. 

Para compilar el proyecto desde el código fuente y empaquetarlo en un único archivo binario:

```cmd
dotnet publish ExtCompress.csproj -c Release -r win-x64 --self-contained false -p:PublishSingleFile=true
```

El proyecto se divide en dos partes:
1.  **Motor Core (`ExtCompress.csproj`):** El corazón de la aplicación, encargado del procesamiento de archivos, compresión Zstd y dibujado de UI.
2.  **Instalador (`ExtCompress.Installer.csproj`):** Aplicación WinForms encargada de desempaquetar el motor y configurarlo globalmente en el sistema de destino.

---

## 📜 Licencia

Este proyecto está bajo la Licencia **MIT**. Consulta el archivo `LICENSE` para más detalles.

Desarrollado con ❤️ por **Soluciones Digitales Camargo**.

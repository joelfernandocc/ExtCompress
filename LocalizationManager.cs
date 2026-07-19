using System;
using System.Collections.Generic;
using System.Globalization;

namespace ExtCompress;

public static class LocalizationManager
{
    public static bool IsSpanish { get; private set; }

    static LocalizationManager()
    {
        var culture = CultureInfo.CurrentUICulture.TwoLetterISOLanguageName;
        IsSpanish = culture.Equals("es", StringComparison.OrdinalIgnoreCase);
    }

    public static string Get(string key, params object[] args)
    {
        string text = (IsSpanish && StringsES.TryGetValue(key, out string valES)) ? valES : 
                      (StringsEN.TryGetValue(key, out string valEN) ? valEN : key);
        
        if (args != null && args.Length > 0)
        {
            try { return string.Format(text, args); } catch { return text; }
        }
        return text;
    }

    private static readonly Dictionary<string, string> StringsEN = new Dictionary<string, string>
    {
        // CLI / Program.cs
        { "CLI_HelpTitle", "ExtCompress By Soluciones Digitales Camargo" },
        { "CLI_NoArgs", "Error: No arguments provided. Use 'extcompress help' for usage instructions." },
        { "CLI_InvalidAction", "Error: Invalid action '{0}'. Use 'compress' or 'extract'." },
        { "CLI_FileNotFound", "Error: Input path '{0}' does not exist." },
        { "CLI_Compressing", "Compressing {0}..." },
        { "CLI_Extracting", "Extracting {0}..." },
        { "CLI_TaskCompleted", "Task completed successfully. Press any key to exit." },
        { "CLI_UnknownParam", "Unknown parameter: {0}" },
        { "CLI_MissingValue", "Missing value for parameter: {0}" },

        // ProgressForm.cs
        { "UI_Title", "ExtCompress" },
        { "UI_Completed", "{0}% completed" },
        { "UI_Calculating", "Calculating..." },
        { "UI_Speed", "Speed: {0}" },
        { "UI_TimeRemaining", "Time remaining: {0}" },
        { "UI_ItemsRemaining", "Items remaining: {0} to process" },
        { "UI_Cancel", "Cancel" },
        { "UI_Canceling", "Canceling..." },
        { "UI_Close", "Close" },
        { "UI_Error", "Error" },
        { "UI_CompressingTo", "Compressing to: {0}" },
        { "UI_ExtractingTo", "Extracting to: {0}" },
        { "UI_Compression", "Compression" },
        { "UI_Extraction", "Extraction" },
        { "UI_TimeFormat_HM", "{0} hours, {1} minutes" },
        { "UI_TimeFormat_MS", "{0} minutes, {1} seconds" },
        { "UI_TimeFormat_S", "{0} seconds" },

        // Engine.cs
        { "Engine_NotExtc", "The file is not a .extc archive or is corrupted." },
        { "Engine_UnsupportedVersion", "Unsupported .extc file version." },
        { "Engine_InvalidSignature", "Invalid author signature. This package does not belong to Soluciones Digitales Camargo." },
        { "Engine_MissingSignature", "Missing author signature. File corrupted or unauthorized." },

        // Installer
        { "Install_Starting", "Starting ExtCompress Installation...\n" },
        { "Install_UpdateDetected", "Previous installation detected. Starting smart update of ExtCompress...\n" },
        { "Install_UpdatePurged", "\nPrevious version purged successfully. Installing new files...\n" },
        { "Install_CreatingDir", "Creating installation directory" },
        { "Install_CopyingExe", "Copying ExtCompress.exe" },
        { "Install_AddingContext", "Adding Windows Context Menus" },
        { "Install_ZombieKeys", "Cleaning old zombie keys" },
        { "Install_AddRemove", "Creating Add/Remove Programs entry" },
        { "Install_CertPrompt", "=================================================\nDo you want to install the Digital Certificate for 'Soluciones Digitales Camargo'?\nThis will prevent Windows from showing ExtCompress as 'Unknown Publisher' in the future.\nPress 'S' to install the certificate, or any other key to skip this step." },
        { "Install_CertSuccess", "[+] Certificate installed successfully on the local machine!\n" },
        { "Install_CertError", "[-] Error installing certificate: {0}\n" },
        { "Install_CertSkipped", "[!] Certificate installation skipped.\n" },
        { "Install_Completed", "\nInstallation complete! Thank you for choosing ExtCompress." },
        
        // Uninstaller
        { "Uninstall_Starting", "Starting ExtCompress Uninstallation...\n" },
        { "Uninstall_RemovingContext", "Removing Context Menus" },
        { "Uninstall_RemovingEngine", "Removing Engine Files" },
        { "Uninstall_RemovingAddRemove", "Removing Add/Remove Programs entry" },
        { "Uninstall_UpdatingPath", "Updating PATH environment variable" },
        { "Uninstall_Removed", "\nThe program has been removed from the system." },
        { "Uninstall_Note", "Note: You can manually delete the ExtCompress folder in Program Files if it is empty." },
        { "Uninstall_Completed", "\nUninstallation complete! Thank you for having used ExtCompress." }
    };

    private static readonly Dictionary<string, string> StringsES = new Dictionary<string, string>
    {
        // CLI / Program.cs
        { "CLI_HelpTitle", "ExtCompress de Soluciones Digitales Camargo" },
        { "CLI_NoArgs", "Error: No se proporcionaron argumentos. Usa 'extcompress help' para instrucciones." },
        { "CLI_InvalidAction", "Error: Acción inválida '{0}'. Usa 'compress' o 'extract'." },
        { "CLI_FileNotFound", "Error: La ruta de entrada '{0}' no existe." },
        { "CLI_Compressing", "Comprimiendo {0}..." },
        { "CLI_Extracting", "Extrayendo {0}..." },
        { "CLI_TaskCompleted", "Tarea completada con éxito. Presiona cualquier tecla para salir." },
        { "CLI_UnknownParam", "Parámetro desconocido: {0}" },
        { "CLI_MissingValue", "Falta el valor para el parámetro: {0}" },

        // ProgressForm.cs
        { "UI_Title", "ExtCompress" },
        { "UI_Completed", "{0}% completado" },
        { "UI_Calculating", "Calculando..." },
        { "UI_Speed", "Velocidad: {0}" },
        { "UI_TimeRemaining", "Tiempo restante: {0}" },
        { "UI_ItemsRemaining", "Elementos restantes: {0} por procesar" },
        { "UI_Cancel", "Cancelar" },
        { "UI_Canceling", "Cancelando..." },
        { "UI_Close", "Cerrar" },
        { "UI_Error", "Error" },
        { "UI_CompressingTo", "Comprimiendo hacia: {0}" },
        { "UI_ExtractingTo", "Extrayendo hacia: {0}" },
        { "UI_Compression", "Compresión" },
        { "UI_Extraction", "Extracción" },
        { "UI_TimeFormat_HM", "{0} horas, {1} minutos" },
        { "UI_TimeFormat_MS", "{0} minutos, {1} segundos" },
        { "UI_TimeFormat_S", "{0} segundos" },

        // Engine.cs
        { "Engine_NotExtc", "El archivo no es de tipo .extc o está corrupto." },
        { "Engine_UnsupportedVersion", "Versión del archivo .extc no soportada." },
        { "Engine_InvalidSignature", "Firma de autor inválida. Este paquete no pertenece a Soluciones Digitales Camargo." },
        { "Engine_MissingSignature", "Firma de autor ausente. Archivo corrupto o no autorizado." },

        // Installer
        { "Install_Starting", "Iniciando instalación de ExtCompress...\n" },
        { "Install_UpdateDetected", "Instalación previa detectada. Iniciando actualización inteligente de ExtCompress...\n" },
        { "Install_UpdatePurged", "\nVersión anterior purgada con éxito. Instalando archivos nuevos...\n" },
        { "Install_CreatingDir", "Creando directorio de instalación" },
        { "Install_CopyingExe", "Copiando ExtCompress.exe" },
        { "Install_AddingContext", "Añadiendo Menús Contextuales" },
        { "Install_ZombieKeys", "Limpiando claves zombies" },
        { "Install_AddRemove", "Creando entrada en Programas y Características" },
        { "Install_CertPrompt", "=================================================\n¿Deseas instalar el Certificado Digital de 'Soluciones Digitales Camargo'?\nEsto evitará que Windows muestre a ExtCompress como 'Editor Desconocido' en el futuro.\nPresiona 'S' para instalar el certificado, o cualquier otra tecla para saltar este paso." },
        { "Install_CertSuccess", "[+] ¡Certificado instalado correctamente en el equipo local!\n" },
        { "Install_CertError", "[-] Error instalando certificado: {0}\n" },
        { "Install_CertSkipped", "[!] Instalación de certificado omitida.\n" },
        { "Install_Completed", "\n¡Instalación completada! Gracias por elegir ExtCompress." },
        
        // Uninstaller
        { "Uninstall_Starting", "Iniciando desinstalación de ExtCompress...\n" },
        { "Uninstall_RemovingContext", "Eliminando Menús Contextuales" },
        { "Uninstall_RemovingEngine", "Eliminando archivos del motor" },
        { "Uninstall_RemovingAddRemove", "Eliminando entrada de Programas y Características" },
        { "Uninstall_UpdatingPath", "Actualizando variable de entorno PATH" },
        { "Uninstall_Removed", "\nEl programa ha sido removido del sistema." },
        { "Uninstall_Note", "Nota: Puedes eliminar manualmente la carpeta ExtCompress en Archivos de Programa si quedó vacía." },
        { "Uninstall_Completed", "\n¡Desinstalación completada! Gracias por haber usado ExtCompress." }
    };
}

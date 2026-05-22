using System;
using System.Collections.Generic;
using System.IO;

namespace CIDE.Services;

public class EditorConfigSettings
{
    public string IndentStyle { get; set; } = "space";
    public int IndentSize { get; set; } = 4;
}

public static class EditorConfigService
{
    public static EditorConfigSettings Parse(string workspaceRoot, string filePath)
    {
        var settings = new EditorConfigSettings();
        var editorConfigPath = Path.Combine(workspaceRoot, ".editorconfig");

        if (!File.Exists(editorConfigPath))
        {
            return settings;
        }

        try
        {
            var lines = File.ReadAllLines(editorConfigPath);
            var inAllSection = false;
            foreach (var line in lines)
            {
                var trimmed = line.Trim();
                if (string.IsNullOrEmpty(trimmed) || trimmed.StartsWith('#') || trimmed.StartsWith(';'))
                {
                    continue;
                }

                if (trimmed.StartsWith('['))
                {
                    inAllSection = trimmed == "[*]" || trimmed.EndsWith(Path.GetExtension(filePath) + "]", StringComparison.Ordinal);
                    continue;
                }

                if (inAllSection)
                {
                    var parts = trimmed.Split('=', 2, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
                    if (parts.Length == 2)
                    {
                        var key = parts[0].ToLowerInvariant();
                        var value = parts[1].ToLowerInvariant();

                        if (key == "indent_style")
                        {
                            settings.IndentStyle = value;
                        }
                        else if (key == "indent_size" && int.TryParse(value, out var size))
                        {
                            settings.IndentSize = size;
                        }
                    }
                }
            }
        }
        catch
        {
        }

        return settings;
    }
}

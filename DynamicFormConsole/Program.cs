using DynamicFormLibrary;
using DynamicFormLibrary.Models;
using Newtonsoft.Json;

namespace DynamicFormConsole
{
    /// <summary>
    /// Console test application for the DynamicForm library.
    /// Loads the sample form.io JSON files from the 'samples' folder and
    /// displays each one as a dynamic Windows Form dialog.
    /// </summary>
    internal static class Program
    {
        [STAThread]
        private static void Main(string[] args)
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);

            // Resolve the samples directory relative to the executable
            string samplesDir = ResolveSamplesDirectory();

            Console.WriteLine("=== DynamicForm Console Test Application ===");
            Console.WriteLine($"Samples directory: {samplesDir}");
            Console.WriteLine();

            if (!Directory.Exists(samplesDir))
            {
                Console.WriteLine($"ERROR: Samples directory not found at '{samplesDir}'.");
                Console.WriteLine("Please ensure the samples folder exists next to the executable.");
                Console.WriteLine("Press any key to exit...");
                Console.ReadKey();
                return;
            }

            var jsonFiles = Directory.GetFiles(samplesDir, "*.json", SearchOption.TopDirectoryOnly);
            if (jsonFiles.Length == 0)
            {
                Console.WriteLine("No JSON sample files found in the samples directory.");
                Console.WriteLine("Press any key to exit...");
                Console.ReadKey();
                return;
            }

            // If a specific file name was passed as a command-line argument, use that
            if (args.Length > 0)
            {
                string requested = args[0];
                var match = jsonFiles.FirstOrDefault(f =>
                    Path.GetFileNameWithoutExtension(f).Equals(requested, StringComparison.OrdinalIgnoreCase)
                    || Path.GetFileName(f).Equals(requested, StringComparison.OrdinalIgnoreCase));

                if (match == null)
                {
                    Console.WriteLine($"No sample file found for '{requested}'.");
                }
                else
                {
                    RunSample(match);
                }
                return;
            }

            // Interactive menu
            while (true)
            {
                Console.WriteLine("Available sample forms:");
                for (int i = 0; i < jsonFiles.Length; i++)
                {
                    Console.WriteLine($"  [{i + 1}] {Path.GetFileNameWithoutExtension(jsonFiles[i])}");
                }
                Console.WriteLine("  [0] Exit");
                Console.Write("Select a form to open: ");

                string? input = Console.ReadLine()?.Trim();
                if (string.IsNullOrEmpty(input) || input == "0")
                    break;

                if (!int.TryParse(input, out int choice) || choice < 1 || choice > jsonFiles.Length)
                {
                    Console.WriteLine("Invalid selection. Please try again.\n");
                    continue;
                }

                RunSample(jsonFiles[choice - 1]);
                Console.WriteLine();
            }

            Console.WriteLine("Goodbye!");
        }

        // ── Sample runner ─────────────────────────────────────────────────

        private static void RunSample(string filePath)
        {
            string name = Path.GetFileNameWithoutExtension(filePath);
            Console.WriteLine($"\nOpening form: {name}");

            string json;
            try
            {
                json = File.ReadAllText(filePath);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"ERROR reading file: {ex.Message}");
                return;
            }

            // Optionally ask for initial data JSON
            Console.Write("Enter initial data JSON (leave blank to skip): ");
            string? initialData = Console.ReadLine()?.Trim();
            if (string.IsNullOrEmpty(initialData))
                initialData = null;

            try
            {
                string? result = DynamicFormBuilder.ShowForm(json, initialData);

                if (result == null)
                {
                    Console.WriteLine("Form cancelled by user.");
                }
                else
                {
                    Console.WriteLine("Form submitted successfully. Result JSON:");
                    Console.WriteLine(result);

                    // Offer to save result
                    Console.Write("Save result JSON to file? (y/N): ");
                    string? save = Console.ReadLine()?.Trim().ToLowerInvariant();
                    if (save == "y")
                    {
                        string outPath = Path.Combine(
                            Path.GetDirectoryName(filePath) ?? ".",
                            $"{name}_result_{DateTime.Now:yyyyMMddHHmmss}.json");
                        File.WriteAllText(outPath, result);
                        Console.WriteLine($"Result saved to: {outPath}");
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"ERROR displaying form: {ex.Message}");
            }
        }

        // ── Helpers ───────────────────────────────────────────────────────

        private static string ResolveSamplesDirectory()
        {
            // Walk up from the executable location until we find the 'samples' folder
            string? dir = AppContext.BaseDirectory;
            for (int i = 0; i < 8; i++)
            {
                if (dir == null) break;
                string candidate = Path.Combine(dir, "samples");
                if (Directory.Exists(candidate))
                    return candidate;
                dir = Path.GetDirectoryName(dir);
            }

            // Fallback: next to the executable
            return Path.Combine(AppContext.BaseDirectory, "samples");
        }
    }
}

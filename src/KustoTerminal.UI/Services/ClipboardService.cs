using System.Runtime.InteropServices;

namespace KustoTerminal.UI.Services
{
    public static class ClipboardService
    {
        public static void SetClipboardWithHtml(string htmlContent)
        {
            try
            {
                if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                {
                    SetClipboardWithHtmlWindows(htmlContent);
                }
                else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
                {
                    SetClipboardWithHtmlMacOS(htmlContent);
                }
                else
                {
                    // For Linux or other platforms, fall back to plain text only
                    //return CopyTextToClipboard(plainTextContent);
                }
            }
            catch
            { }
        }

        /// <summary>
        /// Sets clipboard on Windows with both HTML and plain text formats
        /// </summary>
        private static bool SetClipboardWithHtmlWindows(string htmlClipboardFormat)
        {
#if WINDOWS
            try
            {
                // Convert HTML to Windows Clipboard HTML Format (CF_HTML)
                
                // Use System.Windows.Forms.Clipboard for Windows
                var dataObject = new System.Windows.Forms.DataObject();
                dataObject.SetData(System.Windows.Forms.DataFormats.Html, htmlClipboardFormat);
                
                var thread = new Thread(() =>
                {
                    System.Windows.Forms.Clipboard.SetDataObject(dataObject, true);
                });
                thread.SetApartmentState(ApartmentState.STA);
                thread.Start();
                thread.Join();
                return true;
            }
            catch (Exception e)
            {
                // Fallback: just return false as Windows Forms clipboard failed
                return false;
            }
#else
            // System.Windows.Forms not available on this platform
            return false;
#endif
        }

        /// <summary>
        /// Sets clipboard on macOS with both HTML and plain text formats
        /// </summary>
        private static bool SetClipboardWithHtmlMacOS(string htmlContent)
        {
            try
            {
                // Create temporary HTML file
                var htmlTempFile = System.IO.Path.GetTempFileName() + ".html";
                System.IO.File.WriteAllText(htmlTempFile, htmlContent);

                try
                {
                    // Use textutil to convert HTML to RTF and copy to pasteboard
                    // This allows pasting into rich text applications with formatting
                    var processInfo = new System.Diagnostics.ProcessStartInfo
                    {
                        FileName = "/bin/sh",
                        Arguments = $"-c \"cat '{htmlTempFile}' | /usr/bin/textutil -stdin -format html -convert rtf -stdout | /usr/bin/pbcopy -Prefer rtf\"",
                        UseShellExecute = false,
                        RedirectStandardOutput = true,
                        RedirectStandardError = true,
                        CreateNoWindow = true
                    };

                    using var process = System.Diagnostics.Process.Start(processInfo);
                    process?.WaitForExit();

                    if (process?.ExitCode == 0)
                    {
                        return true;
                    }
                }
                finally
                {
                    // Clean up temp file
                    try { System.IO.File.Delete(htmlTempFile); } catch { }
                }
            }
            catch
            {
                // Ignore errors and try fallback
            }

            return false;
        }

        /// <summary>
        /// Fallback to plain text using pbcopy
        /// </summary>
        private static bool TryPbcopyPlainText(string plainTextContent)
        {
            try
            {
                var processInfo = new System.Diagnostics.ProcessStartInfo
                {
                    FileName = "/usr/bin/pbcopy",
                    UseShellExecute = false,
                    RedirectStandardInput = true,
                    CreateNoWindow = true
                };

                using var process = System.Diagnostics.Process.Start(processInfo);
                if (process != null)
                {
                    process.StandardInput.Write(plainTextContent);
                    process.StandardInput.Close();
                    process.WaitForExit();
                    return process.ExitCode == 0;
                }
            }
            catch
            {
                return false;
            }

            return false;
        }
    }
}

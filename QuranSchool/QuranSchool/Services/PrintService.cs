using System.Drawing.Printing;

namespace QuranSchool.Services;

public class PrintService
{
    public IEnumerable<string> GetPrinters()
    {
        return PrinterSettings.InstalledPrinters.Cast<string>();
    }

    public string GetDefaultPrinter()
    {
        var defaultPrinter = new PrinterSettings().PrinterName;

        return defaultPrinter;
    }
}
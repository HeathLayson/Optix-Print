#region Using directives
using System;
using System.IO;
using System.Net.Sockets;
using UAManagedCore;
using FTOptix.NetLogic;
using FTOptix.Report;
using FTOptix.Core;
using FTOptix.HMIProject;
#endregion

/// <summary>
/// Prints a generated report over raw JetDirect (TCP port 9100).
///
/// Requires these variables on this NetLogic object, set up in Studio:
///   - ReportObject  (NodePointer) -> point this at the Report node that
///                     generates the PDF (e.g. Reports/Sine).
///   - ReportPdfPath (ResourceUri) -> set this to the PDF's location,
///                     e.g. %PROJECTDIR%/Reports/Sine.pdf.
///   - PrinterIp     (String)      -> the printer's IP address.
///   - PrinterPort   (Int32)       -> the printer's raw/JetDirect port
///                     (9100 is the common default).
///
/// Report generation itself is expected to be triggered elsewhere (a
/// button calling Report.GeneratePdf, a schedule, etc.) - this NetLogic
/// only reacts once that report finishes and sends the resulting PDF to
/// the printer. Works the same on the Optix Edge Linux runtime as on
/// Windows since it's plain managed socket code, no OS print spooler
/// involved. The target printer must be able to interpret raw PDF on
/// port 9100 - confirm this against the printer's spec sheet ("direct
/// PDF printing" / "raw socket printing"); many printers on that port
/// expect PCL or PostScript instead.
/// </summary>
public class Print_Report : BaseNetLogic
{
    public override void Start()
    {
        var printerIpVariable = LogicObject.GetVariable("PrinterIp");
        var printerPortVariable = LogicObject.GetVariable("PrinterPort");
        if (printerIpVariable == null || string.IsNullOrEmpty(printerIpVariable.Value))
        {
            Log.Error("Print_Report", "PrinterIp variable is missing or empty.");
            return;
        }
        if (printerPortVariable == null)
        {
            Log.Error("Print_Report", "PrinterPort variable is missing.");
            return;
        }
        printerIp = printerIpVariable.Value;
        printerPort = printerPortVariable.Value;

        var reportPointer = LogicObject.GetVariable("ReportObject");
        if (reportPointer == null || reportPointer.Value == null)
        {
            Log.Error("Print_Report", "ReportObject variable is missing or not linked to a Report node.");
            return;
        }

        report = InformationModel.Get<Report>(reportPointer.Value);
        if (report == null)
        {
            Log.Error("Print_Report", "ReportObject does not point to a valid Report node.");
            return;
        }

        report.OnGeneratePdfCompleted += Report_OnGeneratePdfCompleted;
    }

    public override void Stop()
    {
        if (report != null)
            report.OnGeneratePdfCompleted -= Report_OnGeneratePdfCompleted;
    }

    private void Report_OnGeneratePdfCompleted(object sender, GeneratePdfCompletedEvent e)
    {
        if (e.Result != GeneratePdfCompletedResult.PdfSuccessfullyGenerated)
        {
            Log.Error("Print_Report", $"Report generation failed: {e.Result}");
            return;
        }

        var pdfPathVariable = LogicObject.GetVariable("ReportPdfPath");
        if (pdfPathVariable == null || pdfPathVariable.Value == null)
        {
            Log.Error("Print_Report", "ReportPdfPath variable is missing or empty.");
            return;
        }

        var pdfPath = new ResourceUri(pdfPathVariable.Value).Uri;
        Log.Info("Print_Report", $"Report generated at {pdfPath}, sending to printer...");
        PrintPdfRaw(pdfPath);
    }

    private void PrintPdfRaw(string filePath)
    {
        try
        {
            var bytes = File.ReadAllBytes(filePath);
            using var client = new TcpClient();
            var connectTask = client.ConnectAsync(printerIp, printerPort);
            if (!connectTask.Wait(ConnectTimeoutMs))
            {
                Log.Error("Print_Report", $"Timed out connecting to printer {printerIp}:{printerPort}");
                return;
            }

            using var stream = client.GetStream();
            stream.Write(bytes, 0, bytes.Length);
            stream.Flush();
            Log.Info("Print_Report", $"Sent {filePath} ({bytes.Length} bytes) to {printerIp}:{printerPort}");
        }
        catch (Exception ex)
        {
            Log.Error("Print_Report", $"Failed to print {filePath}: {ex.Message}");
        }
    }

    private const int ConnectTimeoutMs = 5000;

    private Report report;
    private string printerIp;
    private int printerPort;
}

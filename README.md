FactoryTalk Optix NetLogic script that automatically sends a generated PDF report to a network printer over raw JetDirect / TCP port 9100 (no OS print spooler, so it works identically on the Optix Edge Linux runtime and on Windows).

How it works:

Start()	Reads four configuration variables off the NetLogic object — PrinterIp (String), PrinterPort (Int32), ReportObject (NodePointer to a Report node), ReportPdfPath (ResourceUri). Validates each, resolves the Report node, and subscribes to its OnGeneratePdfCompleted event. Any missing/invalid config logs an error and aborts.
Report_OnGeneratePdfCompleted	Fires when a report PDF finishes generating (generation is triggered elsewhere — a button, schedule, etc.). If the result isn't PdfSuccessfullyGenerated, it logs and stops. Otherwise it resolves the PDF's filesystem path from ReportPdfPath and calls PrintPdfRaw.
PrintPdfRaw	Reads the PDF into a byte array, opens a TcpClient to printerIp:printerPort with a 5-second connect timeout, writes the raw bytes to the socket, flushes, and logs the byte count. All wrapped in try/catch that logs failures.
Stop()	Unsubscribes from the event.
Notable points / caveats
Raw PDF over port 9100 only works if the printer natively interprets PDF ("direct PDF printing" / "raw socket printing"). Many port-9100 printers expect PCL or PostScript instead — the header comment flags this.
connectTask.Wait(...) blocks the calling thread during connect; the subsequent stream.Write has no send timeout, so a stalled printer could hang that thread.
No retry logic — a failed print is logged and dropped.

Is it linked particulary to the specific sine report to monitor the report creation. For example If I reused this code elsewhere Would the script need to be modified?
No — it's not bound to the Sine report in code. Every "Sine" mention is just an example inside the doc comment. The actual behavior is fully driven by the four NetLogic variables:

ReportObject → which Report node to watch
ReportPdfPath → where that report's PDF lands
PrinterIp / PrinterPort → destination
Reusing it elsewhere
No script modification needed. Drop the same NetLogic onto another object (or another project), set those four variables in Studio, and it works. Start() resolves the report node dynamically via InformationModel.Get<Report>(reportPointer.Value) and subscribes to that node's OnGeneratePdfCompleted.

Things to watch when reusing
One instance = one report. The script holds a single ReportObject and subscribes to one report's completion event. If you want to print several different reports, you need a separate instance of this NetLogic per report, each with its own variable set.

ReportPdfPath must be kept in sync manually. The completion event (GeneratePdfCompletedEvent e) only carries a success/failure Result — it does not hand you the output path. The script re-reads ReportPdfPath independently, so if that variable doesn't match the path the report is actually configured to generate to, it'll read the wrong file (or fail).

It reacts to every generation of that report. Any trigger that runs GeneratePdf on the linked report — button, schedule, script — will cause a print. There's no filter or enable flag.

So: parameterized and portable; you'd only touch the .cs if you wanted different logic (multiple reports in one instance, an enable/disable toggle, print-once semantics, etc.).

<img width="1235" height="307" alt="image" src="https://github.com/user-attachments/assets/ef2d30c8-10e0-4b59-92ab-7bce6f9fa5d1" />



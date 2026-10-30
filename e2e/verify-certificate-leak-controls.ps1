param(
    [string]$Evidence = (Join-Path $PSScriptRoot 'full-certificate-cbee4b1')
)
$ErrorActionPreference = 'Stop'
$bin = Join-Path $PSScriptRoot 'EncryptionInfoE2E\bin\Release\net10.0'
[Reflection.Assembly]::LoadFrom((Join-Path $bin 'Microsoft.Data.SqlClient.dll')) | Out-Null
$assembly = [Reflection.Assembly]::LoadFrom((Join-Path $bin 'EncryptionInfoE2E.dll'))
$program = $assembly.GetType('EncryptionInfoE2E.Program', $true)
$flags = [Reflection.BindingFlags]'Static,NonPublic'
function Field([string]$name) { $program.GetField($name, $flags) }
$traceType = $assembly.GetType('EncryptionInfoE2E.TraceListener', $true)
$listener = [Activator]::CreateInstance($traceType, $true)
$events = $traceType.GetField('Messages').GetValue($listener)
(Field 's_trace').SetValue($null, $listener)
(Field 's_tfm').SetValue($null, 'negative-control')
(Field 's_sni').SetValue($null, 'managed')
$raw = [IO.File]::ReadAllBytes((Join-Path $Evidence 'certs\SQL2025.cer'))
$base64 = [Convert]::ToBase64String($raw)
$hex = [Convert]::ToHexString($raw)
$certificate = [Security.Cryptography.X509Certificates.X509CertificateLoader]::LoadCertificate($raw)
$thumbprint = $certificate.Thumbprint
$certificate.Dispose()
(Field 's_allCertificates').GetValue($null).Add($base64)
$messages = (Field 's_messagesAndToString').GetValue($null)
$writer = [IO.StreamWriter]::new((Join-Path $Evidence 'negative-leak-controls.tsv'), $false)
$writer.AutoFlush = $true
(Field 's_out').SetValue($null, $writer)
Add-Type @'
using System.Diagnostics.Tracing;
[EventSource(Name = "Microsoft.Data.SqlClient.EventSource")]
public sealed class CertificateLeakControlSource : EventSource
{
    [Event(1, Level = EventLevel.Informational)]
    public void Text(string value) { WriteEvent(1, value); }
    [Event(2, Level = EventLevel.Informational)]
    public void Bytes(byte[] value) { WriteEvent(2, value); }
}
'@
$source = [CertificateLeakControlSource]::new()
$checks = 0
try {
    foreach ($sink in 'exception','trace') {
        foreach ($kind in 'base64','hex','thumbprint','binary') {
            $events.Clear()
            $messages.Clear()
            (Field 's_fail').SetValue($null, 0)
            $events.Enqueue('Harmless control event')
            $value = switch ($kind) {
                'base64' { $base64 }
                'hex' { $hex }
                'thumbprint' { $thumbprint }
                'binary' { [BitConverter]::ToString($raw) }
            }
            if ($sink -eq 'exception') { $messages.Add($value) }
            elseif ($kind -eq 'binary') { $source.Bytes($raw) }
            else { $source.Text($value) }
            $program.GetMethod('CheckNoLeaks', $flags).Invoke($null, @("$sink/$kind")) | Out-Null
            if ((Field 's_fail').GetValue($null) -ne 1) { throw "Leak detector missed $sink/$kind." }
            $checks++
        }
    }
}
finally {
    $writer.Dispose()
    $source.Dispose()
    $listener.Dispose()
}
"PASS: $checks deliberate leaks rejected; negative controls excluded from product totals."

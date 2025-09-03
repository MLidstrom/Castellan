Param(
  [string]$Base = 'C:\Users\matsl\Castellan\scripts'
)

$ErrorActionPreference = 'Stop'
$results = @()

$files = Get-ChildItem -Path $Base -Filter '*.ps1' -Recurse -File -ErrorAction SilentlyContinue

foreach ($f in $files) {
  $tokens = $null
  $errors = $null
  [void][System.Management.Automation.Language.Parser]::ParseFile($f.FullName, [ref]$tokens, [ref]$errors)
  foreach ($e in ($errors | Where-Object { $_ })) {
    $results += [PSCustomObject]@{
      Type    = 'ParseError'
      File    = $f.FullName
      Line    = $e.Extent.StartLineNumber
      Column  = $e.Extent.StartColumnNumber
      Message = $e.Message
    }
  }
}

$pssa = Get-Module -ListAvailable PSScriptAnalyzer | Select-Object -First 1
if ($pssa) {
  $an = Invoke-ScriptAnalyzer -Path $Base -Recurse -Severity Error,Warning -ErrorAction SilentlyContinue
  foreach ($a in $an) {
    $results += [PSCustomObject]@{
      Type     = 'Analyzer'
      RuleName = $a.RuleName
      Severity = $a.Severity
      File     = $a.ScriptPath
      Line     = $a.Line
      Column   = $a.Column
      Message  = $a.Message
    }
  }
}

if ($results.Count -gt 0) {
  $results | ConvertTo-Json -Depth 5
  exit 1
} else {
  'OK'
}

